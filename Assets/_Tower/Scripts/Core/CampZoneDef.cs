namespace Tower.Core
{
    // Data-driven camp interaction zone: a labelled circle on the camp ground
    // plane. Pure C# so radius checks stay unit-testable.
    public sealed class CampZoneDef
    {
        private CampZoneDef(string id, string label, float x, float z, float radius)
        {
            Id = id;
            Label = label;
            X = x;
            Z = z;
            Radius = radius;
        }

        public string Id { get; }

        public string Label { get; }

        public float X { get; }

        public float Z { get; }

        public float Radius { get; }

        public static Result<CampZoneDef> Create(string id, string label, float x, float z, float radius)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<CampZoneDef>.Failure("Zone id is required.");
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                return Result<CampZoneDef>.Failure("Zone label is required.");
            }

            if (float.IsNaN(radius) || radius <= 0f)
            {
                return Result<CampZoneDef>.Failure("Zone radius must be greater than zero.");
            }

            if (float.IsNaN(x) || float.IsNaN(z))
            {
                return Result<CampZoneDef>.Failure("Zone position must be a number.");
            }

            return Result<CampZoneDef>.Success(new CampZoneDef(id, label, x, z, radius));
        }

        // Boundary-inclusive: standing exactly on the radius counts as inside.
        public bool Contains(float x, float z)
        {
            float dx = x - X;
            float dz = z - Z;
            return (dx * dx) + (dz * dz) <= Radius * Radius;
        }

        public float SquaredDistanceTo(float x, float z)
        {
            float dx = x - X;
            float dz = z - Z;
            return (dx * dx) + (dz * dz);
        }
    }
}
