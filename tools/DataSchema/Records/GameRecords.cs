// Tower static-data SCHEMA (single source of truth).
// Consumed by the Sdp CLIs (StaticDataHeaderGenerator + ExcelColumnExtractor)
// at BUILD TIME to (a) generate/sync Excel headers and (b) extract + validate
// Excel -> CSV. This project targets modern .NET and is NOT compiled into Unity.
// Unity reads the resulting validated CSV via a thin loader (see docs/tasks/T-Data.md).
//
// One record == one Excel sheet row. Parameter name == column header.
// Enums match by string name. [Range]/[RegularExpression] validate at extract + load.
// Column change = edit here; the extractor then fails loudly if Excel drifts.
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
        string Id,
        string DisplayName,
        [Range(1, 99)] int DurationTurns,
        bool Stackable);

    [StaticDataRecord("Tower_GameData", "Passives")]
    public sealed record PassiveRecord(
        string Id,
        string DisplayName,
        string EffectHookKey);

    [StaticDataRecord("Tower_GameData", "Abilities")]
    public sealed record AbilityRecord(
        string Id,
        string DisplayName,
        AbilityTag Tag,
        [NullString("")] string? TargetMark,                 // ref Marks.Id; empty for None/Amplify
        [Range(1, 99)] int Range,
        [Range(0, 99)] int Cost,
        [Range(0, 9999)] int BasePower,
        float AmplificationMultiplier,
        AbilityTargetType TargetType,
        [Range(0, 99)] int CooldownRounds);

    [StaticDataRecord("Tower_GameData", "Characters")]
    public sealed record CharacterRecord(
        string Id,
        string DisplayName,
        [Range(1, 99999)] int MaxHp,
        [Range(0, 9999)] int Attack,
        [Range(0, 9999)] int Defense,
        [Range(0, 999)] int Speed,
        DispositionType Disposition,
        [NullString("")] string? Passive,                    // ref Passives.Id
        string DefaultAbilities,            // ";"-joined ref Abilities.Id (slot order)
        bool IsReturner,
        bool ChainLocked,
        bool IsPreset,
        int FactionId);

    // v0 placeholder — Item code model not yet implemented (align on impl).
    [StaticDataRecord("Tower_GameData", "Items")]
    public sealed record ItemRecord(
        string Id,
        string DisplayName,
        ResourceScope ResourceScope,
        int Power,
        int StackMax,
        [NullString("")] string? Description);

    // v0 placeholder — weighted probability table (Hades economy / encounter rolls).
    [StaticDataRecord("Tower_GameData", "DropTables")]
    public sealed record DropTableRecord(
        string TableId,
        string EntryId,
        [Range(1, 100000)] int Weight,
        RewardType RewardType,
        [NullString("")] string? RefId,
        int MinDepth,
        [NullString("")] int? MaxDepth);   // blank = open-ended (loader maps to int.MaxValue)
}
