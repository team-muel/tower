using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public enum RunEventKind
    {
        Encounter,
        Boss
    }

    public sealed class RunEventSlot
    {
        internal RunEventSlot(string eventId, int floorNumber, RunEventKind kind)
        {
            EventId = eventId;
            FloorNumber = floorNumber;
            Kind = kind;
        }

        public string EventId { get; }
        public int FloorNumber { get; }
        public RunEventKind Kind { get; }
    }

    // One stair-step is ten floors. A fresh entry seed deterministically places
    // six or seven ordinary encounters before the mandatory floor-ten boss.
    public sealed class RunEventPlan
    {
        public const int FloorCount = 10;
        public const int MinimumEventCount = 7;
        public const int MaximumEventCount = 8;

        private readonly List<RunEventSlot> slots;

        private RunEventPlan(int seed, List<RunEventSlot> slots)
        {
            Seed = seed;
            this.slots = slots;
        }

        public int Seed { get; }
        public IReadOnlyList<RunEventSlot> Slots => slots;

        public static RunEventPlan Create(int seed)
        {
            uint random = Mix(unchecked((uint)seed));
            int eventCount = MinimumEventCount + (int)(Next(ref random) & 1u);

            var candidateFloors = Enumerable.Range(1, FloorCount - 1).ToArray();
            for (int index = candidateFloors.Length - 1; index > 0; index--)
            {
                int swapIndex = (int)(Next(ref random) % (uint)(index + 1));
                int value = candidateFloors[index];
                candidateFloors[index] = candidateFloors[swapIndex];
                candidateFloors[swapIndex] = value;
            }

            int ordinaryCount = eventCount - 1;
            var selectedFloors = candidateFloors.Take(ordinaryCount).OrderBy(floor => floor).ToArray();
            var generated = new List<RunEventSlot>(eventCount);
            for (int index = 0; index < selectedFloors.Length; index++)
            {
                generated.Add(new RunEventSlot(EventId(index), selectedFloors[index], RunEventKind.Encounter));
            }

            generated.Add(new RunEventSlot(EventId(generated.Count), FloorCount, RunEventKind.Boss));
            return new RunEventPlan(seed, generated);
        }

        internal static Result<RunEventPlan> Restore(int seed, IEnumerable<RunEventSlotSnapshot> savedSlots)
        {
            if (savedSlots == null)
            {
                return Result<RunEventPlan>.Failure("Run event slots are required.");
            }

            var restored = new List<RunEventSlot>();
            foreach (RunEventSlotSnapshot saved in savedSlots)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.eventId))
                {
                    return Result<RunEventPlan>.Failure("Run event slots require non-blank ids.");
                }

                restored.Add(new RunEventSlot(saved.eventId, saved.floorNumber, saved.kind));
            }

            if (restored.Count < MinimumEventCount || restored.Count > MaximumEventCount)
            {
                return Result<RunEventPlan>.Failure("A run requires seven or eight events.");
            }

            if (restored.Select(slot => slot.EventId).Distinct(StringComparer.Ordinal).Count() != restored.Count)
            {
                return Result<RunEventPlan>.Failure("Run event ids must be unique.");
            }

            if (restored.Select(slot => slot.FloorNumber).Distinct().Count() != restored.Count
                || restored.Any(slot => slot.FloorNumber < 1 || slot.FloorNumber > FloorCount))
            {
                return Result<RunEventPlan>.Failure("Run event floors must be unique and within the stair-step.");
            }

            for (int index = 1; index < restored.Count; index++)
            {
                if (restored[index - 1].FloorNumber >= restored[index].FloorNumber)
                {
                    return Result<RunEventPlan>.Failure("Run event slots must be ordered by floor.");
                }
            }

            RunEventSlot final = restored[restored.Count - 1];
            if (final.FloorNumber != FloorCount || final.Kind != RunEventKind.Boss
                || restored.Take(restored.Count - 1).Any(slot => slot.Kind != RunEventKind.Encounter))
            {
                return Result<RunEventPlan>.Failure("The floor-ten boss must be the final run event.");
            }

            return Result<RunEventPlan>.Success(new RunEventPlan(seed, restored));
        }

        private static string EventId(int zeroBasedIndex)
        {
            return $"event-{zeroBasedIndex + 1:00}";
        }

        private static uint Mix(uint value)
        {
            value += 0x9E3779B9u;
            value ^= value >> 16;
            value *= 0x85EBCA6Bu;
            value ^= value >> 13;
            value *= 0xC2B2AE35u;
            value ^= value >> 16;
            return value == 0u ? 0xA341316Cu : value;
        }

        private static uint Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
