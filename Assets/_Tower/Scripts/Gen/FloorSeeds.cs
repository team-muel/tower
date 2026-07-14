namespace Tower.Gen
{
    // T59: every floor of a stair-step derives its own deterministic terrain
    // seed and layout stretch from the authored base seed, so the ten floors
    // stop being clones of one map while staying fully reproducible.
    public static class FloorSeeds
    {
        // splitmix-style avalanche; stable across platforms.
        public static int TerrainSeed(int baseSeed, int floorNumber)
        {
            unchecked
            {
                uint value = (uint)baseSeed + ((uint)floorNumber * 0x9E3779B9u);
                value ^= value >> 16;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
                value *= 0xC2B2AE35u;
                value ^= value >> 16;
                return value == 0u ? floorNumber + 1 : (int)value;
            }
        }

        // Per-floor travel-length stretch in [0.85, 1.15]; keeps the floor
        // footprint varied without touching authored layout inputs.
        public static float TravelStretch(int baseSeed, int floorNumber)
        {
            unchecked
            {
                uint value = (uint)TerrainSeed(baseSeed, floorNumber);
                return 0.85f + ((value % 1000u) / 999f * 0.3f);
            }
        }
    }
}
