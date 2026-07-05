using System;

namespace Tower.Gen
{
    public readonly struct IntRange : IEquatable<IntRange>
    {
        public readonly int Min;
        public readonly int Max;

        public IntRange(int min, int max)
        {
            if (min > max)
            {
                throw new ArgumentOutOfRangeException(nameof(min), "Range minimum cannot be greater than maximum.");
            }

            Min = min;
            Max = max;
        }

        public bool Contains(int value)
        {
            return value >= Min && value <= Max;
        }

        public bool Equals(IntRange other)
        {
            return Min == other.Min && Max == other.Max;
        }

        public override bool Equals(object obj)
        {
            return obj is IntRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Min * 397) ^ Max;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}..{1}", Min, Max);
        }
    }
}
