namespace Tower.Core
{
    // T19: camera offset from the orbit focus point, in world axes. Adding
    // this to the focus position places the camera; pairing it with an
    // Euler(pitch, yaw, 0) rotation makes the camera look at the focus.
    public readonly struct OrbitOffset
    {
        public OrbitOffset(float x, float y, float z)
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
