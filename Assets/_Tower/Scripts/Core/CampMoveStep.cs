namespace Tower.Core
{
    // Result of one CampMover integration step on the camp ground plane.
    public readonly struct CampMoveStep
    {
        public CampMoveStep(float x, float z, bool arrived)
        {
            X = x;
            Z = z;
            Arrived = arrived;
        }

        public float X { get; }

        public float Z { get; }

        public bool Arrived { get; }
    }
}
