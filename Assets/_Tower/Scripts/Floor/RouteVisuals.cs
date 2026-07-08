using Tower.Gen;
using UnityEngine;

namespace Tower.Floor
{
    // v0 in-world hint palette for the diegetic fork choice. Colors are a deliberate,
    // legible mapping (green = safe, red = combat, violet = hazard, gold = special);
    // exact tuning is deferred, see the T40 PR notes.
    public static class RouteVisuals
    {
        public static Color Tint(RouteType type)
        {
            switch (type)
            {
                case RouteType.Safe: return new Color(0.55f, 0.80f, 0.45f);
                case RouteType.Combat: return new Color(0.85f, 0.35f, 0.30f);
                case RouteType.Hazard: return new Color(0.60f, 0.35f, 0.75f);
                case RouteType.Special: return new Color(0.92f, 0.78f, 0.35f);
                default: return Color.gray;
            }
        }

        public static Color ToColor(BiomeColor c)
        {
            return new Color(c.R, c.G, c.B);
        }
    }
}
