using System;

namespace Tower.Core
{
    // Procedural terrain parameters (75 §4 heightfield single-source). Pure, immutable.
    // General terrain: flat / slope / hill via params. NOT a forced climb.
    // A flat field is amplitude 0 and slope 0. A road corridor (|x| < half width)
    // is graded flat and lowered by roadGradeDepth so paths read as sunken roads.
    public sealed class HeightFieldParams
    {
        public HeightFieldParams(
            float baseSlopeX = 0f,
            float baseSlopeZ = 0f,
            float noiseAmplitude = 0f,
            float noiseFrequency = 0f,
            float octave2Amplitude = 0f,
            float octave2Frequency = 0f,
            float roadCorridorHalfWidth = 0f,
            float roadGradeDepth = 0f)
        {
            if (noiseAmplitude < 0f)
                throw new ArgumentOutOfRangeException(nameof(noiseAmplitude), "Noise amplitude cannot be negative.");
            if (noiseFrequency < 0f)
                throw new ArgumentOutOfRangeException(nameof(noiseFrequency), "Noise frequency cannot be negative.");
            if (octave2Amplitude < 0f)
                throw new ArgumentOutOfRangeException(nameof(octave2Amplitude), "Octave 2 amplitude cannot be negative.");
            if (octave2Frequency < 0f)
                throw new ArgumentOutOfRangeException(nameof(octave2Frequency), "Octave 2 frequency cannot be negative.");
            if (roadCorridorHalfWidth < 0f)
                throw new ArgumentOutOfRangeException(nameof(roadCorridorHalfWidth), "Road corridor half width cannot be negative.");
            if (roadGradeDepth < 0f)
                throw new ArgumentOutOfRangeException(nameof(roadGradeDepth), "Road grade depth cannot be negative.");

            BaseSlopeX = baseSlopeX;
            BaseSlopeZ = baseSlopeZ;
            NoiseAmplitude = noiseAmplitude;
            NoiseFrequency = noiseFrequency;
            Octave2Amplitude = octave2Amplitude;
            Octave2Frequency = octave2Frequency;
            RoadCorridorHalfWidth = roadCorridorHalfWidth;
            RoadGradeDepth = roadGradeDepth;
        }

        // Linear tilt of the base plane (world units of height per world unit of x/z). 0 = level.
        public float BaseSlopeX { get; }
        public float BaseSlopeZ { get; }

        // Primary value-noise octave: bumps/hills.
        public float NoiseAmplitude { get; }
        public float NoiseFrequency { get; }

        // Optional second octave for finer detail. Amplitude 0 disables it.
        public float Octave2Amplitude { get; }
        public float Octave2Frequency { get; }

        // Road corridor grading: |x| < half width is flattened and dropped by depth.
        public float RoadCorridorHalfWidth { get; }
        public float RoadGradeDepth { get; }

        public bool HasRoadCorridor => RoadCorridorHalfWidth > 0f;

        // Perfectly level ground.
        public static readonly HeightFieldParams Flat = new HeightFieldParams();

        // Sensible general terrain for an open road segment (75 §7): gentle rolling
        // ground with a sunken road corridor down the middle.
        public static readonly HeightFieldParams Default = new HeightFieldParams(
            baseSlopeX: 0f,
            baseSlopeZ: 0f,
            noiseAmplitude: 1.5f,
            noiseFrequency: 0.08f,
            octave2Amplitude: 0.5f,
            octave2Frequency: 0.22f,
            roadCorridorHalfWidth: 3f,
            roadGradeDepth: 0.6f);
    }
}
