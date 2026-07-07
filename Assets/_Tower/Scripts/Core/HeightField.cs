using System;

namespace Tower.Core
{
    // Deterministic procedural height field (75 §4). Pure C#: no UnityEngine.Mathf.
    // Integer-hash value noise + smoothstep interpolation. The single source for
    // (a) terrain mesh, (b) derived lighting normals, (c) module placement.
    //
    // Determinism contract: identical (seed, params, coordinate) always yields an
    // identical height, so two instances built from the same seed agree everywhere.
    public sealed class HeightField
    {
        // Offset so the second octave samples a different region of noise space
        // (avoids the two octaves lining up their lattice minima/maxima).
        private const float Octave2OffsetX = 137.13f;
        private const float Octave2OffsetZ = 71.57f;

        public HeightField(int seed, HeightFieldParams parameters)
        {
            Seed = seed;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        public int Seed { get; }

        public HeightFieldParams Parameters { get; }

        // World-space height at (x, z). Deterministic for a given seed + params.
        public float Sample(float x, float z)
        {
            HeightFieldParams p = Parameters;

            // Base terrain: tilted plane + up to two value-noise octaves.
            float terrain = p.BaseSlopeX * x + p.BaseSlopeZ * z;

            if (p.NoiseAmplitude > 0f && p.NoiseFrequency > 0f)
            {
                terrain += p.NoiseAmplitude * Noise(x * p.NoiseFrequency, z * p.NoiseFrequency, 0);
            }

            if (p.Octave2Amplitude > 0f && p.Octave2Frequency > 0f)
            {
                terrain += p.Octave2Amplitude * Noise(
                    x * p.Octave2Frequency + Octave2OffsetX,
                    z * p.Octave2Frequency + Octave2OffsetZ,
                    1);
            }

            // Road corridor grading: within |x| < half width the ground flattens
            // across x and drops by roadGradeDepth, blending back to terrain at the
            // corridor edge so the transition is smooth (sunken road look).
            if (p.RoadCorridorHalfWidth > 0f)
            {
                float ax = x < 0f ? -x : x;
                if (ax < p.RoadCorridorHalfWidth)
                {
                    float roadHeight = p.BaseSlopeZ * z - p.RoadGradeDepth;
                    float t = ax / p.RoadCorridorHalfWidth; // 0 at center, 1 at edge
                    float s = Smoothstep(t);
                    return Lerp(roadHeight, terrain, s);
                }
            }

            return terrain;
        }

        // Grid of samples over a rectangular region. Row index maps to z, column to x.
        // resolution is the number of samples per axis (>= 2). Result is [resolution, resolution].
        public float[,] Generate(int resolution, float sizeX, float sizeZ, float originX, float originZ)
        {
            if (resolution < 2)
                throw new ArgumentOutOfRangeException(nameof(resolution), "Resolution must be at least 2.");
            if (sizeX <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sizeX), "Size X must be positive.");
            if (sizeZ <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sizeZ), "Size Z must be positive.");

            float[,] heights = new float[resolution, resolution];
            float step = 1f / (resolution - 1);

            for (int row = 0; row < resolution; row++)
            {
                float z = originZ + (row * step) * sizeZ;
                for (int col = 0; col < resolution; col++)
                {
                    float x = originX + (col * step) * sizeX;
                    heights[row, col] = Sample(x, z);
                }
            }

            return heights;
        }

        // --- Pure value noise -------------------------------------------------

        // Value noise in [-1, 1] with smoothstep-interpolated lattice values.
        // channel salts the hash so different octaves use independent lattices.
        private float Noise(float x, float z, int channel)
        {
            int x0 = FloorToInt(x);
            int z0 = FloorToInt(z);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            float tx = x - x0;
            float tz = z - z0;
            float sx = Smoothstep(tx);
            float sz = Smoothstep(tz);

            float n00 = Lattice(x0, z0, channel);
            float n10 = Lattice(x1, z0, channel);
            float n01 = Lattice(x0, z1, channel);
            float n11 = Lattice(x1, z1, channel);

            float nx0 = Lerp(n00, n10, sx);
            float nx1 = Lerp(n01, n11, sx);
            return Lerp(nx0, nx1, sz);
        }

        // Deterministic lattice value in [-1, 1] from integer coordinates + seed.
        private float Lattice(int x, int z, int channel)
        {
            uint h = Hash((uint)x, (uint)z, (uint)Seed, (uint)channel);
            // Map the low 24 bits to [0, 1), then to [-1, 1].
            float unit = (h & 0xFFFFFFu) / 16777216f;
            return unit * 2f - 1f;
        }

        // 32-bit integer avalanche hash (FNV-1a mixing + final scramble). Deterministic.
        private static uint Hash(uint x, uint z, uint seed, uint channel)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ x) * 16777619u;
                h = (h ^ z) * 16777619u;
                h = (h ^ seed) * 16777619u;
                h = (h ^ channel) * 16777619u;
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                h *= 0x297a2d39u;
                h ^= h >> 15;
                return h;
            }
        }

        private static int FloorToInt(float value)
        {
            int i = (int)value;
            // Round toward negative infinity for negative fractionals.
            return (value < i) ? i - 1 : i;
        }

        private static float Smoothstep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
