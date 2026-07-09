namespace Tower.Floor
{
    // Pure, engine-agnostic PRNG (xorshift32) with an FNV-1a seed mixer. No
    // engine RNG, no shared mutable state: identical (seed, salt) always
    // produces the identical stream, so all derived forest content is deterministic
    // and reproducible across platforms and runs.
    public struct DeterministicRng
    {
        private uint _state;

        public DeterministicRng(uint seed)
        {
            _state = seed == 0u ? 2166136261u : seed;
        }

        // FNV-1a mix of a base seed with an integer salt (e.g. a node id) to spawn
        // an independent, still-deterministic stream per (seed, salt) pair.
        public static DeterministicRng ForSalt(int seed, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = Fnv(h, (uint)seed);
                h = Fnv(h, (uint)salt);
                return new DeterministicRng(h);
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
        }

        // Uniform float in [0, 1).
        public float NextFloat()
        {
            return (NextUInt() & 0xFFFFFFu) / 16777216f;
        }

        // Uniform float in [min, max).
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        // Uniform int in [minInclusive, maxExclusive).
        public int RangeInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        private static uint Fnv(uint h, uint value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    byte b = (byte)((value >> (i * 8)) & 0xFF);
                    h = (h ^ b) * 16777619u;
                }

                return h;
            }
        }
    }

    public static class PropPrefabSelector
    {
        public static int PickIndex(int seed, int nodeId, int slot, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (count == 1)
            {
                return 0;
            }

            unchecked
            {
                uint h = 2166136261u;
                h = Fnv(h, (uint)seed);
                h = Fnv(h, (uint)nodeId);
                h = Fnv(h, (uint)slot);
                return (int)(h % (uint)count);
            }
        }

        private static uint Fnv(uint h, uint value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    byte b = (byte)((value >> (i * 8)) & 0xFF);
                    h = (h ^ b) * 16777619u;
                }

                return h;
            }
        }
    }
}
