using System;
using System.Collections.Generic;

namespace Tower.Core
{
    public sealed class EncounterReward
    {
        private EncounterReward(string eventId, RewardType type, int amount, string displayName)
        {
            EventId = eventId;
            Type = type;
            Amount = amount;
            DisplayName = displayName;
        }

        public string EventId { get; }
        public RewardType Type { get; }
        public int Amount { get; }
        public string DisplayName { get; }

        public static Result<EncounterReward> Create(
            string eventId,
            RewardType type,
            int amount,
            string displayName)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return Result<EncounterReward>.Failure("Encounter reward requires an event id.");
            }

            if (type == RewardType.None)
            {
                return Result<EncounterReward>.Failure("Encounter reward type cannot be None.");
            }

            if (amount <= 0)
            {
                return Result<EncounterReward>.Failure("Encounter reward amount must be positive.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return Result<EncounterReward>.Failure("Encounter reward requires a display name.");
            }

            return Result<EncounterReward>.Success(
                new EncounterReward(eventId, type, amount, displayName.Trim()));
        }

        internal bool HasSamePayload(EncounterReward other)
        {
            return other != null
                && Type == other.Type
                && Amount == other.Amount
                && StringComparer.Ordinal.Equals(DisplayName, other.DisplayName);
        }
    }

    // Run-scoped inventory seam. T58 owns serialization and lifecycle clearing;
    // this ledger only guarantees one deterministic grant per completed event.
    public sealed class RunRewardInventory
    {
        private readonly Dictionary<string, EncounterReward> claims =
            new Dictionary<string, EncounterReward>(StringComparer.Ordinal);
        private readonly Dictionary<RewardType, int> totals =
            new Dictionary<RewardType, int>();

        public int ClaimCount => claims.Count;
        public IReadOnlyDictionary<string, EncounterReward> Claims => claims;

        public int AmountOf(RewardType type)
        {
            return totals.TryGetValue(type, out int amount) ? amount : 0;
        }

        // True means newly granted. False means an identical retry was safely ignored.
        public Result<bool> Grant(EncounterReward reward)
        {
            if (reward == null)
            {
                return Result<bool>.Failure("Encounter reward is required.");
            }

            if (claims.TryGetValue(reward.EventId, out EncounterReward existing))
            {
                return existing.HasSamePayload(reward)
                    ? Result<bool>.Success(false)
                    : Result<bool>.Failure(
                        $"Encounter '{reward.EventId}' cannot grant conflicting rewards.");
            }

            claims.Add(reward.EventId, reward);
            totals[reward.Type] = AmountOf(reward.Type) + reward.Amount;
            return Result<bool>.Success(true);
        }
    }
}
