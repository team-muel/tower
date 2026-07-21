using System;
using System.Linq;

namespace Tower.Core
{
    public enum RunOutcome
    {
        // Moved to the next floor of the stair-step; the run stays active.
        FloorAdvanced,

        // The floor-ten boss event was completed and the stair-step is conquered.
        Conquered,

        // Rolled back to floor one; run-scoped progress, rewards and anchors reset.
        Retreated,

        // Third retreat: the great regression. Retreat count resets with the run.
        GreatRegression
    }

    [Serializable]
    public sealed class RunLifecycleSnapshot
    {
        public int seed;
        public int floorNumber;
        public int retreatCount;
        public bool isConquered;
        public RunEventProgressSnapshot progress;
        public RunRewardClaimSnapshot[] rewards = new RunRewardClaimSnapshot[0];
    }

    // T58 run macro-state for one stair-step (ten floors, seven or eight events).
    // Composes the T53 event plan/progress and the T57 run-scoped reward ledger.
    // Retreat semantics (count, great-regression threshold) reuse the T8
    // ExpeditionRules constants; roster death bookkeeping stays owned by
    // ExpeditionRules and is integrated in the meta lane (T61).
    public sealed class RunLifecycle
    {
        private RunLifecycle(
            int seed,
            int floorNumber,
            int retreatCount,
            bool isConquered,
            RunEventProgress progress,
            RunRewardInventory rewards)
        {
            Seed = seed;
            FloorNumber = floorNumber;
            RetreatCount = retreatCount;
            IsConquered = isConquered;
            Progress = progress;
            Rewards = rewards;
        }

        public int Seed { get; }
        public int FloorNumber { get; private set; }
        public int RetreatCount { get; private set; }
        public bool IsConquered { get; private set; }
        public RunEventProgress Progress { get; private set; }
        public RunRewardInventory Rewards { get; private set; }

        public RunEventSlot NextPendingEvent => Progress.NextPending;

        public bool CurrentFloorHasPendingEvent =>
            NextPendingEvent != null && NextPendingEvent.FloorNumber == FloorNumber;

        public static RunLifecycle CreateNew(int seed)
        {
            RunEventPlan plan = RunEventPlan.Create(seed);
            return new RunLifecycle(
                seed,
                floorNumber: 1,
                retreatCount: 0,
                isConquered: false,
                RunEventProgress.Create(plan),
                new RunRewardInventory());
        }

        // Completes the next pending event and grants its run-scoped reward.
        // Identical retries are idempotent (returns false), matching the
        // T53 prefix-progress and T57 single-grant contracts.
        public Result<bool> ResolveEvent(string eventId, EncounterReward reward)
        {
            if (IsConquered)
            {
                return Result<bool>.Failure("A conquered run has no events left to resolve.");
            }

            if (reward == null || !StringComparer.Ordinal.Equals(reward.EventId, eventId))
            {
                return Result<bool>.Failure("Run event reward must target the resolved event.");
            }

            Result<RunEventProgress> completed = Progress.CompleteNext(eventId);
            if (completed.IsFailure)
            {
                return Result<bool>.Failure(completed.Error);
            }

            Result<bool> granted = Rewards.Grant(reward);
            if (granted.IsFailure)
            {
                return Result<bool>.Failure(granted.Error);
            }

            Progress = completed.Value;
            return granted;
        }

        // Moves the party up one floor. The current floor's event must be
        // resolved first; advancing off floor ten requires the completed boss
        // and conquers the stair-step.
        public Result<RunOutcome> AdvanceFloor()
        {
            if (IsConquered)
            {
                return Result<RunOutcome>.Failure("A conquered run cannot advance further.");
            }

            if (CurrentFloorHasPendingEvent)
            {
                return Result<RunOutcome>.Failure(
                    $"Floor {FloorNumber} still has the unresolved event '{NextPendingEvent.EventId}'.");
            }

            if (FloorNumber >= RunEventPlan.FloorCount)
            {
                if (!Progress.IsComplete)
                {
                    return Result<RunOutcome>.Failure("The floor-ten boss must be resolved before conquest.");
                }

                IsConquered = true;
                return Result<RunOutcome>.Success(RunOutcome.Conquered);
            }

            FloorNumber++;
            return Result<RunOutcome>.Success(RunOutcome.FloorAdvanced);
        }

        // Party wipe or voluntary retreat: back to floor one with run-scoped
        // progress and rewards reset. The third retreat is the great
        // regression (ExpeditionRules threshold) and resets the retreat count.
        public Result<RunOutcome> Retreat()
        {
            if (IsConquered)
            {
                return Result<RunOutcome>.Failure("A conquered run cannot retreat.");
            }

            RetreatCount++;
            RunOutcome outcome = RunOutcome.Retreated;
            if (RetreatCount >= ExpeditionRules.GreatRegressionRetreatThreshold)
            {
                outcome = RunOutcome.GreatRegression;
                RetreatCount = 0;
            }

            FloorNumber = 1;
            Progress = RunEventProgress.Create(Progress.Plan);
            Rewards = new RunRewardInventory();
            return Result<RunOutcome>.Success(outcome);
        }

        public RunLifecycleSnapshot Capture()
        {
            return new RunLifecycleSnapshot
            {
                seed = Seed,
                floorNumber = FloorNumber,
                retreatCount = RetreatCount,
                isConquered = IsConquered,
                progress = Progress.Capture(),
                rewards = Rewards.Capture()
            };
        }

        public static Result<RunLifecycle> Restore(RunLifecycleSnapshot snapshot)
        {
            if (snapshot == null || snapshot.progress == null || snapshot.progress.slots == null
                || snapshot.progress.slots.Length == 0)
            {
                return Result<RunLifecycle>.Failure("Run lifecycle snapshot is required.");
            }

            if (snapshot.floorNumber < 1 || snapshot.floorNumber > RunEventPlan.FloorCount)
            {
                return Result<RunLifecycle>.Failure("Run floor must be within the stair-step.");
            }

            if (snapshot.retreatCount < 0
                || snapshot.retreatCount >= ExpeditionRules.GreatRegressionRetreatThreshold)
            {
                return Result<RunLifecycle>.Failure("Run retreat count is out of range.");
            }

            Result<RunEventProgress> progress = RunEventProgress.Restore(snapshot.progress);
            if (progress.IsFailure)
            {
                return Result<RunLifecycle>.Failure(progress.Error);
            }

            if (snapshot.isConquered && !progress.Value.IsComplete)
            {
                return Result<RunLifecycle>.Failure("A conquered run must have all events completed.");
            }

            Result<RunRewardInventory> rewards = RunRewardInventory.Restore(snapshot.rewards);
            if (rewards.IsFailure)
            {
                return Result<RunLifecycle>.Failure(rewards.Error);
            }

            string[] completedIds = snapshot.progress.completedEventIds ?? new string[0];
            foreach (string claimedEventId in rewards.Value.Claims.Keys)
            {
                if (!completedIds.Contains(claimedEventId, StringComparer.Ordinal))
                {
                    return Result<RunLifecycle>.Failure(
                        $"Reward claim '{claimedEventId}' has no completed run event.");
                }
            }

            return Result<RunLifecycle>.Success(new RunLifecycle(
                snapshot.seed,
                snapshot.floorNumber,
                snapshot.retreatCount,
                snapshot.isConquered,
                progress.Value,
                rewards.Value));
        }
    }
}
