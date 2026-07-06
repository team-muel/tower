using System;

namespace Tower.Gen
{
    public struct BiomeColor
    {
        public BiomeColor(float r, float g, float b)
        {
            ValidateChannel(r, nameof(r));
            ValidateChannel(g, nameof(g));
            ValidateChannel(b, nameof(b));

            R = r;
            G = g;
            B = b;
        }

        public float R { get; }

        public float G { get; }

        public float B { get; }

        private static void ValidateChannel(float value, string name)
        {
            if (value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name, "Color channels must be between 0 and 1.");
            }
        }
    }
}
