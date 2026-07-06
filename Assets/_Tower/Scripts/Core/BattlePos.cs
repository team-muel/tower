using System;
using System.Globalization;

namespace Tower.Core
{
    // T20: continuous battlefield coordinate. One analog unit equals one
    // legacy grid cell (see BattleScale), so existing integer ranges and
    // movement budgets keep their numeric meaning unchanged.
    public readonly struct BattlePos : IEquatable<BattlePos>
    {
        public readonly float X;
        public readonly float Y;

        public BattlePos(float x, float y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(BattlePos other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is BattlePos other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", X, Y);
        }

        public static bool operator ==(BattlePos left, BattlePos right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BattlePos left, BattlePos right)
        {
            return !left.Equals(right);
        }
    }
}
