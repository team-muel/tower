namespace Tower.Core
{
    // Camera offset from the current focus point in world axes.
    public readonly struct CameraOffset
    {
        public CameraOffset(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }
}
