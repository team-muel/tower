using System;

namespace Tower.Gen
{
    public sealed class FloorGenParams
    {
        public FloorGenParams(int seed)
            : this(seed, new IntRange(3, 5), false, new IntRange(8, 14))
        {
        }

        public FloorGenParams(int seed, bool isBossFloor)
            : this(seed, new IntRange(3, 5), isBossFloor, new IntRange(8, 14))
        {
        }

        public FloorGenParams(
            int seed,
            IntRange roomCountRange,
            bool isBossFloor,
            IntRange roomSizeRange,
            bool includeCamp = false,
            BiomeId biomeId = BiomeId.Forest)
        {
            if (roomCountRange.Min < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(roomCountRange), "Floor v0 requires at least 3 rooms.");
            }

            if (roomCountRange.Max > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(roomCountRange), "Floor v0 supports at most 5 rooms.");
            }

            if (roomSizeRange.Min < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(roomSizeRange), "Floor v0 rooms must be at least 8x8.");
            }

            if (roomSizeRange.Max > 14)
            {
                throw new ArgumentOutOfRangeException(nameof(roomSizeRange), "Floor v0 rooms must be at most 14x14.");
            }

            Seed = seed;
            RoomCountRange = roomCountRange;
            IsBossFloor = isBossFloor;
            RoomSizeRange = roomSizeRange;
            IncludeCamp = includeCamp;
            BiomeId = biomeId;
        }

        public int Seed { get; }

        public IntRange RoomCountRange { get; }

        public bool IsBossFloor { get; }

        public IntRange RoomSizeRange { get; }

        public bool IncludeCamp { get; }

        public BiomeId BiomeId { get; }
    }
}
