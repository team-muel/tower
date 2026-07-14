using System.IO;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class RunLifecycleTests
    {
        private const int Seed = 4242;

        private static EncounterReward RewardFor(RunEventSlot slot)
        {
            RewardType type = slot.Kind == RunEventKind.Boss ? RewardType.Ability : RewardType.Resource;
            return EncounterReward.Create(slot.EventId, type, 1, "Run reward").Value;
        }

        private static void AdvanceToFloor(RunLifecycle run, int floorNumber)
        {
            while (run.FloorNumber < floorNumber)
            {
                Result<RunOutcome> advanced = run.AdvanceFloor();
                Assert.That(advanced.IsSuccess, Is.True, advanced.IsFailure ? advanced.Error : string.Empty);
                Assert.That(advanced.Value, Is.EqualTo(RunOutcome.FloorAdvanced));
            }
        }

        [Test]
        public void CreateNew_StartsAtFloorOneWithFullPlan()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);

            Assert.That(run.FloorNumber, Is.EqualTo(1));
            Assert.That(run.RetreatCount, Is.Zero);
            Assert.That(run.IsConquered, Is.False);
            Assert.That(run.Progress.CompletedCount, Is.Zero);
            Assert.That(run.Progress.Plan.Slots.Count, Is.InRange(
                RunEventPlan.MinimumEventCount, RunEventPlan.MaximumEventCount));
            Assert.That(run.Rewards.ClaimCount, Is.Zero);
        }

        [Test]
        public void ResolveEvent_RejectsMismatchedReward()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);
            RunEventSlot first = run.NextPendingEvent;
            AdvanceToFloor(run, first.FloorNumber);
            EncounterReward wrongTarget = EncounterReward.Create(
                "some-other-event", RewardType.Resource, 1, "Run reward").Value;

            Result<bool> resolved = run.ResolveEvent(first.EventId, wrongTarget);

            Assert.That(resolved.IsFailure, Is.True);
            Assert.That(run.Progress.CompletedCount, Is.Zero);
        }

        [Test]
        public void ResolveEvent_IsIdempotentForRepeats()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);
            RunEventSlot first = run.NextPendingEvent;
            AdvanceToFloor(run, first.FloorNumber);

            Result<bool> initial = run.ResolveEvent(first.EventId, RewardFor(first));
            Result<bool> repeat = run.ResolveEvent(first.EventId, RewardFor(first));

            Assert.That(initial.IsSuccess, Is.True);
            Assert.That(initial.Value, Is.True);
            Assert.That(repeat.IsSuccess, Is.True);
            Assert.That(repeat.Value, Is.False);
            Assert.That(run.Progress.CompletedCount, Is.EqualTo(1));
            Assert.That(run.Rewards.ClaimCount, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceFloor_BlocksWhileCurrentFloorEventIsPending()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);
            RunEventSlot first = run.NextPendingEvent;
            AdvanceToFloor(run, first.FloorNumber);

            Result<RunOutcome> blocked = run.AdvanceFloor();

            Assert.That(blocked.IsFailure, Is.True);
            Assert.That(blocked.Error, Does.Contain(first.EventId));
        }

        [Test]
        public void FullRun_AdvancesThroughAllEventsAndConquers()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);
            int totalEvents = run.Progress.Plan.Slots.Count;
            for (int index = 0; index < totalEvents; index++)
            {
                RunEventSlot slot = run.NextPendingEvent;
                AdvanceToFloor(run, slot.FloorNumber);
                Result<bool> resolved = run.ResolveEvent(slot.EventId, RewardFor(slot));
                Assert.That(resolved.IsSuccess, Is.True, resolved.IsFailure ? resolved.Error : string.Empty);
            }

            Result<RunOutcome> conquered = run.AdvanceFloor();

            Assert.That(conquered.IsSuccess, Is.True);
            Assert.That(conquered.Value, Is.EqualTo(RunOutcome.Conquered));
            Assert.That(run.IsConquered, Is.True);
            Assert.That(run.Rewards.ClaimCount, Is.EqualTo(totalEvents));
            Assert.That(run.AdvanceFloor().IsFailure, Is.True);
            Assert.That(run.Retreat().IsFailure, Is.True);
        }

        [Test]
        public void Retreat_ResetsRunScopedProgressAndRewards()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);
            RunEventSlot first = run.NextPendingEvent;
            AdvanceToFloor(run, first.FloorNumber);
            run.ResolveEvent(first.EventId, RewardFor(first));

            Result<RunOutcome> retreat = run.Retreat();

            Assert.That(retreat.IsSuccess, Is.True);
            Assert.That(retreat.Value, Is.EqualTo(RunOutcome.Retreated));
            Assert.That(run.RetreatCount, Is.EqualTo(1));
            Assert.That(run.FloorNumber, Is.EqualTo(1));
            Assert.That(run.Progress.CompletedCount, Is.Zero);
            Assert.That(run.Rewards.ClaimCount, Is.Zero);
        }

        [Test]
        public void ThirdRetreat_IsTheGreatRegression()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);

            Assert.That(run.Retreat().Value, Is.EqualTo(RunOutcome.Retreated));
            Assert.That(run.Retreat().Value, Is.EqualTo(RunOutcome.Retreated));
            Result<RunOutcome> third = run.Retreat();

            Assert.That(third.IsSuccess, Is.True);
            Assert.That(third.Value, Is.EqualTo(RunOutcome.GreatRegression));
            Assert.That(run.RetreatCount, Is.Zero);
            Assert.That(run.FloorNumber, Is.EqualTo(1));
        }

        [Test]
        public void CaptureRestore_RoundTripsMidRunState()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);
            RunEventSlot first = run.NextPendingEvent;
            AdvanceToFloor(run, first.FloorNumber);
            run.ResolveEvent(first.EventId, RewardFor(first));
            run.AdvanceFloor();
            run.Retreat();
            RunEventSlot again = run.NextPendingEvent;
            AdvanceToFloor(run, again.FloorNumber);
            run.ResolveEvent(again.EventId, RewardFor(again));

            Result<RunLifecycle> restored = RunLifecycle.Restore(run.Capture());

            Assert.That(restored.IsSuccess, Is.True, restored.IsFailure ? restored.Error : string.Empty);
            Assert.That(restored.Value.Seed, Is.EqualTo(run.Seed));
            Assert.That(restored.Value.FloorNumber, Is.EqualTo(run.FloorNumber));
            Assert.That(restored.Value.RetreatCount, Is.EqualTo(run.RetreatCount));
            Assert.That(restored.Value.Progress.CompletedCount, Is.EqualTo(run.Progress.CompletedCount));
            Assert.That(restored.Value.Rewards.ClaimCount, Is.EqualTo(run.Rewards.ClaimCount));
            Assert.That(
                restored.Value.Rewards.AmountOf(RewardType.Resource),
                Is.EqualTo(run.Rewards.AmountOf(RewardType.Resource)));
        }

        [Test]
        public void Restore_RejectsRewardClaimWithoutCompletedEvent()
        {
            RunLifecycle run = RunLifecycle.CreateNew(Seed);
            RunLifecycleSnapshot snapshot = run.Capture();
            snapshot.rewards = new[]
            {
                new RunRewardClaimSnapshot
                {
                    eventId = "event-01",
                    type = RewardType.Resource,
                    amount = 1,
                    displayName = "Orphan claim"
                }
            };

            Result<RunLifecycle> restored = RunLifecycle.Restore(snapshot);

            Assert.That(restored.IsFailure, Is.True);
            Assert.That(restored.Error, Does.Contain("event-01"));
        }

        [Test]
        public void Restore_RejectsEmptySnapshot()
        {
            Assert.That(RunLifecycle.Restore(null).IsFailure, Is.True);
            Assert.That(RunLifecycle.Restore(new RunLifecycleSnapshot()).IsFailure, Is.True);
        }

        [Test]
        public void RewardInventory_CaptureRestoreRoundTrips()
        {
            var inventory = new RunRewardInventory();
            inventory.Grant(EncounterReward.Create("event-01", RewardType.Resource, 2, "Stock").Value);
            inventory.Grant(EncounterReward.Create("event-02", RewardType.Ability, 1, "Draft").Value);

            Result<RunRewardInventory> restored = RunRewardInventory.Restore(inventory.Capture());

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value.ClaimCount, Is.EqualTo(2));
            Assert.That(restored.Value.AmountOf(RewardType.Resource), Is.EqualTo(2));
            Assert.That(restored.Value.AmountOf(RewardType.Ability), Is.EqualTo(1));
        }

        [Test]
        public void SaveRepository_RoundTripsRunLifecycleThroughJson()
        {
            string path = Path.Combine(Path.GetTempPath(), "tower-t58-run-lifecycle-test.json");
            SaveRepository repository = SaveRepository.Create(path).Value;
            try
            {
                RunLifecycle run = RunLifecycle.CreateNew(Seed);
                RunEventSlot first = run.NextPendingEvent;
                AdvanceToFloor(run, first.FloorNumber);
                run.ResolveEvent(first.EventId, RewardFor(first));

                Result saved = repository.Save(new SaveGame { runLifecycle = run.Capture() });
                Assert.That(saved.IsSuccess, Is.True, saved.IsFailure ? saved.Error : string.Empty);

                Result<SaveGame> loaded = repository.Load();
                Assert.That(loaded.IsSuccess, Is.True);

                Result<RunLifecycle> restored = RunLifecycle.Restore(loaded.Value.runLifecycle);
                Assert.That(restored.IsSuccess, Is.True, restored.IsFailure ? restored.Error : string.Empty);
                Assert.That(restored.Value.FloorNumber, Is.EqualTo(run.FloorNumber));
                Assert.That(restored.Value.Progress.CompletedCount, Is.EqualTo(1));
                Assert.That(restored.Value.Rewards.ClaimCount, Is.EqualTo(1));
            }
            finally
            {
                repository.Delete();
            }
        }

        [Test]
        public void QaCommandLine_ParsesFreshRunFlag()
        {
            Assert.That(QaCommandLine.HasFreshRunFlag(new[] { "game.exe", "-qaFreshRun" }), Is.True);
            Assert.That(QaCommandLine.HasFreshRunFlag(new[] { "game.exe", "-qaAutoEncounter" }), Is.False);
            Assert.That(QaCommandLine.HasFreshRunFlag(null), Is.False);
        }

        [Test]
        public void QaCommandLine_ParsesAutoRunFlag()
        {
            Assert.That(QaCommandLine.HasAutoRunFlag(new[] { "game.exe", "-qaAutoRun" }), Is.True);
            Assert.That(QaCommandLine.HasAutoRunFlag(new[] { "game.exe", "-qaFreshRun" }), Is.False);
            Assert.That(QaCommandLine.HasAutoRunFlag(null), Is.False);
        }
    }
}
