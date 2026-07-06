using System.Globalization;

namespace Tower.Core
{
    // Immutable camera tuning value set; ToJson feeds the -devcam P dump.
    public readonly struct CameraTuningState
    {
        public CameraTuningState(float pitch, float distance, float fov, float followDamping)
        {
            Pitch = pitch;
            Distance = distance;
            Fov = fov;
            FollowDamping = followDamping;
        }

        public float Pitch { get; }
        public float Distance { get; }
        public float Fov { get; }
        public float FollowDamping { get; }

        public static CameraTuningState Default => new CameraTuningState(
            CameraTuning.DefaultPitch,
            CameraTuning.DefaultDistance,
            CameraTuning.DefaultFov,
            CameraTuning.DefaultFollowDamping);

        public string ToJson()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"pitch\":{0},\"distance\":{1},\"fov\":{2},\"followDamping\":{3}}}",
                Format(Pitch),
                Format(Distance),
                Format(Fov),
                Format(FollowDamping));
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
