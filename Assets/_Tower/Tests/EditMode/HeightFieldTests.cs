using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // T31: deterministic procedural height field (75 §4). Pure C# noise, no Mathf.
    public sealed class HeightFieldTests
    {
        private static readonly HeightFieldParams RollingWithRoad = new HeightFieldParams(
            noiseAmplitude: 1.5f,
            noiseFrequency: 0.08f,
            octave2Amplitude: 0.5f,
            octave2Frequency: 0.22f,
            roadCorridorHalfWidth: 3f,
            roadGradeDepth: 0.6f);

        [Test]
        public void SameSeedProducesIdenticalSamples()
        {
            HeightField a = new HeightField(4242, RollingWithRoad);
            HeightField b = new HeightField(4242, RollingWithRoad);

            for (int i = 0; i < 50; i++)
            {
                float x = -25f + i * 1.31f;
                float z = 40f - i * 0.97f;
                Assert.AreEqual(a.Sample(x, z), b.Sample(x, z), 0f,
                    $"Same seed must be deterministic at ({x}, {z}).");
            }
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            HeightField a = new HeightField(1, RollingWithRoad);
            HeightField b = new HeightField(2, RollingWithRoad);

            int differences = 0;
            for (int i = 0; i < 50; i++)
            {
                // Stay outside the road corridor so the terrain noise (seed-driven) shows.
                float x = 10f + i * 0.7f;
                float z = -5f + i * 1.1f;
                if (a.Sample(x, z) != b.Sample(x, z))
                {
                    differences++;
                }
            }

            Assert.Greater(differences, 0, "Different seeds should produce different terrain.");
        }

        [Test]
        public void FlatParamsOutsideCorridorAreConstant()
        {
            HeightField hf = new HeightField(99, HeightFieldParams.Flat);
            float baseline = hf.Sample(20f, 20f);

            for (int i = 0; i < 40; i++)
            {
                float x = 5f + i * 2.3f;
                float z = -30f + i * 1.7f;
                Assert.AreEqual(baseline, hf.Sample(x, z), 0f,
                    "Flat params (amp 0, slope 0) must be constant everywhere.");
            }

            Assert.AreEqual(0f, baseline, "Flat ground with zero slope sits at height 0.");
        }

        [Test]
        public void RoadCorridorIsLowerThanFlanks()
        {
            // Flat terrain + a graded road corridor: the center must sit below the sides.
            HeightFieldParams roadOnly = new HeightFieldParams(
                roadCorridorHalfWidth: 3f,
                roadGradeDepth: 0.6f);
            HeightField hf = new HeightField(7, roadOnly);

            for (float z = -10f; z <= 10f; z += 2.5f)
            {
                float center = hf.Sample(0f, z);
                float leftFlank = hf.Sample(-6f, z);
                float rightFlank = hf.Sample(6f, z);

                Assert.Less(center, leftFlank, $"Road center must be below the left flank at z={z}.");
                Assert.Less(center, rightFlank, $"Road center must be below the right flank at z={z}.");
            }
        }

        [Test]
        public void SamplesAreFinite()
        {
            HeightField hf = new HeightField(-31337, RollingWithRoad);

            for (int i = 0; i < 200; i++)
            {
                float x = -100f + i * 1.37f;
                float z = 100f - i * 0.71f;
                float h = hf.Sample(x, z);
                Assert.IsFalse(float.IsNaN(h), $"Sample must not be NaN at ({x}, {z}).");
                Assert.IsFalse(float.IsInfinity(h), $"Sample must not be Infinity at ({x}, {z}).");
            }
        }

        [Test]
        public void GenerateIsDeterministicAndMatchesSample()
        {
            HeightField hf = new HeightField(555, RollingWithRoad);

            float[,] grid = hf.Generate(16, 30f, 30f, -15f, -15f);
            float[,] again = hf.Generate(16, 30f, 30f, -15f, -15f);

            Assert.AreEqual(16, grid.GetLength(0));
            Assert.AreEqual(16, grid.GetLength(1));

            float step = 1f / 15f;
            for (int row = 0; row < 16; row++)
            {
                for (int col = 0; col < 16; col++)
                {
                    Assert.AreEqual(grid[row, col], again[row, col], 0f, "Generate must be deterministic.");

                    float x = -15f + (col * step) * 30f;
                    float z = -15f + (row * step) * 30f;
                    Assert.AreEqual(hf.Sample(x, z), grid[row, col], 0f,
                        "Generate must match Sample at the same coordinate.");
                }
            }
        }
    }
}
