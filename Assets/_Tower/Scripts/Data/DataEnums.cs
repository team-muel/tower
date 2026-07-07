namespace Tower.Data
{
    // Mirrors Tower.DataSchema.ResourceScope (tools/DataSchema/Records/GameRecords.cs).
    // Not present in Tower.Core, so defined here for the data layer.
    public enum ResourceScope
    {
        Permanent = 0,
        Temporary = 1
    }

    // Mirrors Tower.DataSchema.RewardType (tools/DataSchema/Records/GameRecords.cs).
    public enum RewardType
    {
        Heal = 0,
        Resource = 1,
        Ability = 2,
        Shortcut = 3
    }
}
