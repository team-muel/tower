using System;
using System.Linq;

namespace Tower.Core
{
    [Serializable]
    public sealed class RunEventSlotSnapshot
    {
        public string eventId;
        public int floorNumber;
        public RunEventKind kind;
    }

    [Serializable]
    public sealed class RunEventProgressSnapshot
    {
        public int seed;
        public RunEventSlotSnapshot[] slots = new RunEventSlotSnapshot[0];
        public string[] completedEventIds = new string[0];
    }

    // Completion is a strict prefix of the plan. Repeating the most recently
    // completed event is harmless, while skipping an unresolved event fails.
    public sealed class RunEventProgress
    {
        private RunEventProgress(RunEventPlan plan, int completedCount)
        {
            Plan = plan;
            CompletedCount = completedCount;
        }

        public RunEventPlan Plan { get; }
        public int CompletedCount { get; }
        public bool IsComplete => CompletedCount == Plan.Slots.Count;
        public RunEventSlot NextPending => IsComplete ? null : Plan.Slots[CompletedCount];

        public static RunEventProgress Create(RunEventPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            return new RunEventProgress(plan, 0);
        }

        public Result<RunEventProgress> CompleteNext(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return Result<RunEventProgress>.Failure("Event id is required.");
            }

            int eventIndex = -1;
            for (int index = 0; index < Plan.Slots.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(Plan.Slots[index].EventId, eventId))
                {
                    eventIndex = index;
                    break;
                }
            }

            if (eventIndex < 0)
            {
                return Result<RunEventProgress>.Failure($"Unknown run event '{eventId}'.");
            }

            if (eventIndex < CompletedCount)
            {
                return Result<RunEventProgress>.Success(this);
            }

            if (eventIndex > CompletedCount)
            {
                return Result<RunEventProgress>.Failure("Run events cannot be completed out of order.");
            }

            return Result<RunEventProgress>.Success(new RunEventProgress(Plan, CompletedCount + 1));
        }

        public RunEventProgressSnapshot Capture()
        {
            return new RunEventProgressSnapshot
            {
                seed = Plan.Seed,
                slots = Plan.Slots.Select(slot => new RunEventSlotSnapshot
                {
                    eventId = slot.EventId,
                    floorNumber = slot.FloorNumber,
                    kind = slot.Kind
                }).ToArray(),
                completedEventIds = Plan.Slots.Take(CompletedCount).Select(slot => slot.EventId).ToArray()
            };
        }

        public static Result<RunEventProgress> Restore(RunEventProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return Result<RunEventProgress>.Failure("Run event progress is required.");
            }

            Result<RunEventPlan> plan = RunEventPlan.Restore(snapshot.seed, snapshot.slots);
            if (plan.IsFailure)
            {
                return Result<RunEventProgress>.Failure(plan.Error);
            }

            string[] completed = snapshot.completedEventIds ?? new string[0];
            if (completed.Length > plan.Value.Slots.Count)
            {
                return Result<RunEventProgress>.Failure("Completed event count exceeds the run plan.");
            }

            for (int index = 0; index < completed.Length; index++)
            {
                if (!StringComparer.Ordinal.Equals(completed[index], plan.Value.Slots[index].EventId))
                {
                    return Result<RunEventProgress>.Failure("Completed run events must match the plan prefix.");
                }
            }

            return Result<RunEventProgress>.Success(new RunEventProgress(plan.Value, completed.Length));
        }
    }
}
