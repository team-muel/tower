using System;

namespace Tower.Core
{
    public static class GridDistance
    {
        public static int Manhattan(GridPos a, GridPos b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }
    }
}
