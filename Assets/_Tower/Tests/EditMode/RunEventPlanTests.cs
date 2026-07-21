using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class RunEventPlanTests
    {
        [Test]
        public void Create_ProducesSevenOrEightOrderedEventsAndFinalBoss()
        {
            var observedCounts = new HashSet<int>();

            for (int seed = -32; seed <= 32; seed++)
            {
                RunEventPlan plan = RunEventPlan.Create(seed);
                observedCounts.Add(plan.Slots.Count);

                Assert.That(plan.Slots.Count, Is.InRange(7, 8));
                Assert.That(plan.Slots.Select(slot => slot.FloorNumber), Is.Ordered.Ascending);
                Assert.That(plan.Slots.Select(slot => slot.FloorNumber).Distinct().Count(),
                    Is.EqualTo(plan.Slots.Count));
                Assert.That(plan.Slots.Take(plan.Slots.Count - 1).All(slot => slot.Kind == RunEventKind.Encounter),
                    Is.True);
                Assert.That(plan.Slots.Last().FloorNumber, Is.EqualTo(10));
                Assert.That(plan.Slots.Last().Kind, Is.EqualTo(RunEventKind.Boss));
            }

            Assert.That(observedCounts, Is.EquivalentTo(new[] { 7, 8 }));
        }

        [Test]
        public void Create_SameSeedProducesSamePlan()
        {
            RunEventPlan first = RunEventPlan.Create(1847);
            RunEventPlan second = RunEventPlan.Create(1847);

            Assert.That(second.Slots.Select(Signature), Is.EqualTo(first.Slots.Select(Signature)));
        }

        [Test]
        public void Create_SeedSetProducesMoreThanOneLayout()
        {
            int distinctLayouts = Enumerable.Range(0, 32)
                .Select(seed => string.Join("|", RunEventPlan.Create(seed).Slots.Select(Signature)))
                .Distinct()
                .Count();

            Assert.That(distinctLayouts, Is.GreaterThan(1));
        }

        [Test]
        public void CompleteNext_IsOrderedAndIdempotent()
        {
            RunEventProgress progress = RunEventProgress.Create(RunEventPlan.Create(12));
            string firstId = progress.Plan.Slots[0].EventId;
            string secondId = progress.Plan.Slots[1].EventId;

            Assert.That(progress.CompleteNext(secondId).IsFailure, Is.True);

            Result<RunEventProgress> completed = progress.CompleteNext(firstId);
            Assert.That(completed.IsSuccess, Is.True, completed.Error);
            Assert.That(completed.Value.CompletedCount, Is.EqualTo(1));
            Assert.That(completed.Value.NextPending.EventId, Is.EqualTo(secondId));

            Result<RunEventProgress> repeated = completed.Value.CompleteNext(firstId);
            Assert.That(repeated.IsSuccess, Is.True, repeated.Error);
            Assert.That(repeated.Value, Is.SameAs(completed.Value));
            Assert.That(repeated.Value.CompletedCount, Is.EqualTo(1));
        }

        [Test]
        public void CaptureRestore_PreservesAuthoredLayoutAndProgress()
        {
            RunEventProgress progress = RunEventProgress.Create(RunEventPlan.Create(-912));
            progress = progress.CompleteNext(progress.NextPending.EventId).Value;
            progress = progress.CompleteNext(progress.NextPending.EventId).Value;

            RunEventProgressSnapshot snapshot = progress.Capture();
            Result<RunEventProgress> restored = RunEventProgress.Restore(snapshot);

            Assert.That(restored.IsSuccess, Is.True, restored.Error);
            Assert.That(restored.Value.Plan.Seed, Is.EqualTo(progress.Plan.Seed));
            Assert.That(restored.Value.Plan.Slots.Select(Signature),
                Is.EqualTo(progress.Plan.Slots.Select(Signature)));
            Assert.That(restored.Value.CompletedCount, Is.EqualTo(2));
            Assert.That(restored.Value.NextPending.EventId, Is.EqualTo(progress.NextPending.EventId));
        }

        [Test]
        public void Restore_RejectsNonPrefixCompletion()
        {
            RunEventProgressSnapshot snapshot = RunEventProgress.Create(RunEventPlan.Create(3)).Capture();
            snapshot.completedEventIds = new[] { snapshot.slots[1].eventId };

            Assert.That(RunEventProgress.Restore(snapshot).IsFailure, Is.True);
        }

        private static string Signature(RunEventSlot slot)
        {
            return $"{slot.EventId}:{slot.FloorNumber}:{slot.Kind}";
        }
    }
}
