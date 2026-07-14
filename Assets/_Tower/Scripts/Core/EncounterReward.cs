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

    [Serializable]
    public sealed class RunRewardClaimSnapshot
    {
        public string eventId;
        public RewardType type;
        public int amount;
        public string displayName;
    }

    // Run-scoped inventory ledger. T58 owns serialization (Capture/Restore
    // below) and lifecycle clearing (RunLifecycle.Retreat allocates a fresh
    // ledger); this class guarantees one deterministic grant per event.
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

        public RunRewardClaimSnapshot[] Capture()
        {
            var ordered = new List<RunRewardClaimSnapshot>(claims.Count);
            foreach (KeyValuePair<string, EncounterReward> claim in claims)
            {
                ordered.Add(new RunRewardClaimSnapshot
                {
                    eventId = claim.Key,
                    type = claim.Value.Type,
                    amount = claim.Value.Amount,
                    displayName = claim.Value.DisplayName
                });
            }

            ordered.Sort((a, b) => StringComparer.Ordinal.Compare(a.eventId, b.eventId));
            return ordered.ToArray();
        }

        public static Result<RunRewardInventory> Restore(RunRewardClaimSnapshot[] snapshots)
        {
            var inventory = new RunRewardInventory();
            foreach (RunRewardClaimSnapshot snapshot in snapshots ?? new RunRewardClaimSnapshot[0])
            {
                if (snapshot == null)
                {
                    return Result<RunRewardInventory>.Failure("Reward claim entries cannot be null.");
                }

                Result<EncounterReward> reward = EncounterReward.Create(
                    snapshot.eventId,
                    snapshot.type,
                    snapshot.amount,
                    snapshot.displayName);
                if (reward.IsFailure)
                {
                    return Result<RunRewardInventory>.Failure(reward.Error);
                }

                Result<bool> granted = inventory.Grant(reward.Value);
                if (granted.IsFailure)
                {
                    return Result<RunRewardInventory>.Failure(granted.Error);
                }
            }

            return Result<RunRewardInventory>.Success(inventory);
        }
    }
}
