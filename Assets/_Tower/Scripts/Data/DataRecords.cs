using System.Collections.Generic;
using Tower.Core;

namespace Tower.Data
{
    // Immutable POCO mirrors of the build-time schema
    // (tools/DataSchema/Records/GameRecords.cs). One instance == one CSV row.
    // Field names match the CSV headers.

    public sealed class MarkData
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int DurationTurns;
        public readonly bool Stackable;

        public MarkData(string id, string displayName, int durationTurns, bool stackable)
        {
            Id = id;
            DisplayName = displayName;
            DurationTurns = durationTurns;
            Stackable = stackable;
        }
    }

    public sealed class PassiveData
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string EffectHookKey;

        public PassiveData(string id, string displayName, string effectHookKey)
        {
            Id = id;
            DisplayName = displayName;
            EffectHookKey = effectHookKey;
        }
    }

    public sealed class AbilityData
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly AbilityTag Tag;
        public readonly string TargetMark;       // ref Marks.Id; empty for None/Amplify
        public readonly int Range;
        public readonly int Cost;
        public readonly int BasePower;
        public readonly float AmplificationMultiplier;
        public readonly AbilityTargetType TargetType;
        public readonly int CooldownRounds;

        public AbilityData(
            string id,
            string displayName,
            AbilityTag tag,
            string targetMark,
            int range,
            int cost,
            int basePower,
            float amplificationMultiplier,
            AbilityTargetType targetType,
            int cooldownRounds)
        {
            Id = id;
            DisplayName = displayName;
            Tag = tag;
            TargetMark = targetMark;
            Range = range;
            Cost = cost;
            BasePower = basePower;
            AmplificationMultiplier = amplificationMultiplier;
            TargetType = targetType;
            CooldownRounds = cooldownRounds;
        }
    }

    public sealed class CharacterData
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int MaxHp;
        public readonly int Attack;
        public readonly int Defense;
        public readonly int Speed;
        public readonly DispositionType Disposition;
        public readonly string Passive;                 // ref Passives.Id (may be empty)
        public readonly IReadOnlyList<string> DefaultAbilities; // ref Abilities.Id, slot order
        public readonly bool IsReturner;
        public readonly bool ChainLocked;
        public readonly bool IsPreset;
        public readonly int FactionId;

        public CharacterData(
            string id,
            string displayName,
            int maxHp,
            int attack,
            int defense,
            int speed,
            DispositionType disposition,
            string passive,
            IReadOnlyList<string> defaultAbilities,
            bool isReturner,
            bool chainLocked,
            bool isPreset,
            int factionId)
        {
            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            Disposition = disposition;
            Passive = passive;
            DefaultAbilities = defaultAbilities;
            IsReturner = isReturner;
            ChainLocked = chainLocked;
            IsPreset = isPreset;
            FactionId = factionId;
        }
    }

    public sealed class ItemData
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly ResourceScope ResourceScope;
        public readonly int Power;
        public readonly int StackMax;
        public readonly string Description;

        public ItemData(
            string id,
            string displayName,
            ResourceScope resourceScope,
            int power,
            int stackMax,
            string description)
        {
            Id = id;
            DisplayName = displayName;
            ResourceScope = resourceScope;
            Power = power;
            StackMax = stackMax;
            Description = description;
        }
    }

    public sealed class DropTableEntryData
    {
        public readonly string TableId;
        public readonly string EntryId;
        public readonly int Weight;
        public readonly RewardType RewardType;
        public readonly string RefId;
        public readonly int MinDepth;
        public readonly int MaxDepth;

        public DropTableEntryData(
            string tableId,
            string entryId,
            int weight,
            RewardType rewardType,
            string refId,
            int minDepth,
            int maxDepth)
        {
            TableId = tableId;
            EntryId = entryId;
            Weight = weight;
            RewardType = rewardType;
            RefId = refId;
            MinDepth = minDepth;
            MaxDepth = maxDepth;
        }
    }
}
