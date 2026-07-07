using System;
using Tower.Core;

namespace Tower.Gen
{
    // Binds a FloorGraph node (74) to a procedural height field (75 §4). Derives a
    // per-node seed from the graph seed and node id via FNV-1a so terrain is
    // deterministic and distinct per node without any shared mutable state.
    public static class HeightFieldFactory
    {
        public static HeightField ForNode(int graphSeed, FloorNode node, BiomeDef biome = null)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            int nodeSeed = DeriveNodeSeed(graphSeed, node.Id);
            HeightFieldParams parameters = ParamsFor(biome);
            return new HeightField(nodeSeed, parameters);
        }

        // v0 params: general open-segment terrain. When a biome is supplied its
        // identity nudges the amplitude/road profile; otherwise the sensible default.
        private static HeightFieldParams ParamsFor(BiomeDef biome)
        {
            if (biome == null)
            {
                return HeightFieldParams.Default;
            }

            switch (biome.Id)
            {
                case BiomeId.Desert:
                    // Dunes: taller primary swell, gentler detail, wider shallow road.
                    return new HeightFieldParams(
                        noiseAmplitude: 2.2f,
                        noiseFrequency: 0.06f,
                        octave2Amplitude: 0.4f,
                        octave2Frequency: 0.2f,
                        roadCorridorHalfWidth: 3.5f,
                        roadGradeDepth: 0.4f);
                case BiomeId.CrystalMine:
                    // Jagged: lower swell, sharp high-frequency detail, deep narrow road.
                    return new HeightFieldParams(
                        noiseAmplitude: 1.2f,
                        noiseFrequency: 0.12f,
                        octave2Amplitude: 0.8f,
                        octave2Frequency: 0.35f,
                        roadCorridorHalfWidth: 2.5f,
                        roadGradeDepth: 0.9f);
                case BiomeId.GhostManor:
                    // Subdued rolling ground with a pronounced sunken road.
                    return new HeightFieldParams(
                        noiseAmplitude: 1.0f,
                        noiseFrequency: 0.09f,
                        octave2Amplitude: 0.5f,
                        octave2Frequency: 0.24f,
                        roadCorridorHalfWidth: 3f,
                        roadGradeDepth: 0.8f);
                case BiomeId.Forest:
                default:
                    return HeightFieldParams.Default;
            }
        }

        // FNV-1a over the graph seed and node id bytes. Deterministic per (seed, id).
        private static int DeriveNodeSeed(int graphSeed, int nodeId)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = FnvBytes(h, (uint)graphSeed);
                h = FnvBytes(h, (uint)nodeId);
                return (int)h;
            }
        }

        private static uint FnvBytes(uint h, uint value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    byte b = (byte)((value >> (i * 8)) & 0xFF);
                    h = (h ^ b) * 16777619u;
                }

                return h;
            }
        }
    }
}
