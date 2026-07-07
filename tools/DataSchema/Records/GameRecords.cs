// Tower static-data SCHEMA (single source of truth).
// Consumed by the Sdp CLIs (StaticDataHeaderGenerator + ExcelColumnExtractor)
// at BUILD TIME to (a) generate/sync Excel headers and (b) extract + validate
// Excel -> CSV. This project targets modern .NET and is NOT compiled into Unity.
// Unity reads the resulting validated CSV via a thin loader (see docs/tasks/T-Data.md).
//
// One record == one Excel sheet row. [ColumnName] pins each column header to the
// camelCase contract the Unity loader (Tower.Data.DataCatalog/CsvTable) already
// reads, so Sdp is a byte-identical drop-in generator (no Unity change, no drift).
// Enums match by string name. [Range] validates at extract + load. [NullString("")]
// marks a nullable column whose empty cell means null. Column change = edit here;
// the extractor then fails loudly if Excel drifts.
//
// NOTE: enums are duplicated here for tool self-containment. They mirror
// Tower.Core (AbilityTag/DispositionType/AbilityTargetType) EXACTLY. If/when the
// records are proven netstandard2.1/C#9-safe with Sdp.Attributes available in
// Unity, share these files with the game to make tool-schema == game-types.

using Sdp.Attributes;

namespace Tower.DataSchema
{
    public enum AbilityTag { None, Apply, Consume, Amplify }
    public enum DispositionType { Aggressive, Protective }
    public enum AbilityTargetType { Enemy, Ally, Cell }
    public enum ResourceScope { Permanent, Temporary }
    public enum RewardType { Heal, Resource, Ability, Shortcut }

    [StaticDataRecord("Tower_GameData", "Marks")]
    public sealed record MarkRecord(
        [ColumnName("id")] string Id,
        [ColumnName("displayName")] string DisplayName,
        [ColumnName("durationTurns")][Range(1, 99)] int DurationTurns,
        [ColumnName("stackable")] bool Stackable);

    [StaticDataRecord("Tower_GameData", "Passives")]
    public sealed record PassiveRecord(
        [ColumnName("id")] string Id,
        [ColumnName("displayName")] string DisplayName,
        [ColumnName("effectHookKey")] string EffectHookKey);

    [StaticDataRecord("Tower_GameData", "Abilities")]
    public sealed record AbilityRecord(
        [ColumnName("id")] string Id,
        [ColumnName("displayName")] string DisplayName,
        [ColumnName("tag")] AbilityTag Tag,
        [ColumnName("targetMark")][NullString("")] string? TargetMark,   // ref Marks.Id; empty for None/Amplify
        [ColumnName("range")][Range(1, 99)] int Range,
        [ColumnName("cost")][Range(0, 99)] int Cost,
        [ColumnName("basePower")][Range(0, 9999)] int BasePower,
        [ColumnName("amplificationMultiplier")] float AmplificationMultiplier,
        [ColumnName("targetType")] AbilityTargetType TargetType,
        [ColumnName("cooldownRounds")][Range(0, 99)] int CooldownRounds);

    [StaticDataRecord("Tower_GameData", "Characters")]
    public sealed record CharacterRecord(
        [ColumnName("id")] string Id,
        [ColumnName("displayName")] string DisplayName,
        [ColumnName("maxHp")][Range(1, 99999)] int MaxHp,
        [ColumnName("attack")][Range(0, 9999)] int Attack,
        [ColumnName("defense")][Range(0, 9999)] int Defense,
        [ColumnName("speed")][Range(0, 999)] int Speed,
        [ColumnName("disposition")] DispositionType Disposition,
        [ColumnName("passive")][NullString("")] string? Passive,         // ref Passives.Id
        [ColumnName("defaultAbilities")] string DefaultAbilities,        // ";"-joined ref Abilities.Id (slot order)
        [ColumnName("isReturner")] bool IsReturner,
        [ColumnName("chainLocked")] bool ChainLocked,
        [ColumnName("isPreset")] bool IsPreset,
        [ColumnName("factionId")] int FactionId);

    // Item code model implemented in Tower.Data (ItemData + load-time validation).
    [StaticDataRecord("Tower_GameData", "Items")]
    public sealed record ItemRecord(
        [ColumnName("id")] string Id,
        [ColumnName("displayName")] string DisplayName,
        [ColumnName("resourceScope")] ResourceScope ResourceScope,
        [ColumnName("power")] int Power,
        [ColumnName("stackMax")] int StackMax,
        [ColumnName("description")][NullString("")] string? Description);

    // Weighted probability table (Hades economy / encounter rolls). refId FK
    // (Resource->Items, Ability->Abilities) validated at load in Tower.Data.
    [StaticDataRecord("Tower_GameData", "DropTables")]
    public sealed record DropTableRecord(
        [ColumnName("tableId")] string TableId,
        [ColumnName("entryId")] string EntryId,
        [ColumnName("weight")][Range(1, 100000)] int Weight,
        [ColumnName("rewardType")] RewardType RewardType,
        [ColumnName("refId")][NullString("")] string? RefId,
        [ColumnName("minDepth")] int MinDepth,
        [ColumnName("maxDepth")][NullString("")] int? MaxDepth);   // blank = open-ended (loader maps to int.MaxValue)
}
