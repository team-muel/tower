using System;
using System.Collections.Generic;

namespace Tower.Gen
{
    public sealed class BiomeTheme
    {
        private static readonly Dictionary<BiomeId, BiomeTheme> Presets = new Dictionary<BiomeId, BiomeTheme>
        {
            {
                BiomeId.Forest,
                new BiomeTheme(
                    BiomeId.Forest,
                    new BiomeColor(0.16f, 0.24f, 0.18f),
                    new BiomeColor(0.34f, 0.45f, 0.36f),
                    0.025f,
                    new BiomeColor(0.95f, 0.88f, 0.72f),
                    1.1f,
                    new BiomeColor(0.20f, 0.36f, 0.22f),
                    new BiomeColor(0.32f, 0.28f, 0.18f))
            },
            {
                BiomeId.Desert,
                new BiomeTheme(
                    BiomeId.Desert,
                    new BiomeColor(0.36f, 0.28f, 0.18f),
                    new BiomeColor(0.72f, 0.58f, 0.36f),
                    0.018f,
                    new BiomeColor(1.00f, 0.84f, 0.52f),
                    1.35f,
                    new BiomeColor(0.68f, 0.51f, 0.28f),
                    new BiomeColor(0.46f, 0.34f, 0.20f))
            },
            {
                BiomeId.GhostManor,
                new BiomeTheme(
                    BiomeId.GhostManor,
                    new BiomeColor(0.12f, 0.13f, 0.18f),
                    new BiomeColor(0.30f, 0.33f, 0.42f),
                    0.045f,
                    new BiomeColor(0.62f, 0.70f, 0.95f),
                    0.75f,
                    new BiomeColor(0.20f, 0.20f, 0.25f),
                    new BiomeColor(0.34f, 0.32f, 0.39f))
            },
            {
                BiomeId.CrystalMine,
                new BiomeTheme(
                    BiomeId.CrystalMine,
                    new BiomeColor(0.12f, 0.19f, 0.24f),
                    new BiomeColor(0.24f, 0.45f, 0.52f),
                    0.032f,
                    new BiomeColor(0.70f, 0.94f, 1.00f),
                    1.2f,
                    new BiomeColor(0.20f, 0.38f, 0.42f),
                    new BiomeColor(0.48f, 0.70f, 0.78f))
            }
        };

        public BiomeTheme(
            BiomeId id,
            BiomeColor ambientColor,
            BiomeColor fogColor,
            float fogDensity,
            BiomeColor directionalLightColor,
            float directionalLightIntensity,
            BiomeColor tileTintA,
            BiomeColor tileTintB)
        {
            if (fogDensity < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fogDensity), "Fog density cannot be negative.");
            }

            if (directionalLightIntensity < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(directionalLightIntensity), "Light intensity cannot be negative.");
            }

            Id = id;
            AmbientColor = ambientColor;
            FogColor = fogColor;
            FogDensity = fogDensity;
            DirectionalLightColor = directionalLightColor;
            DirectionalLightIntensity = directionalLightIntensity;
            TileTintA = tileTintA;
            TileTintB = tileTintB;
        }

        public BiomeId Id { get; }

        public BiomeColor AmbientColor { get; }

        public BiomeColor FogColor { get; }

        public float FogDensity { get; }

        public BiomeColor DirectionalLightColor { get; }

        public float DirectionalLightIntensity { get; }

        public BiomeColor TileTintA { get; }

        public BiomeColor TileTintB { get; }

        public static BiomeTheme For(BiomeId id)
        {
            if (!Presets.TryGetValue(id, out BiomeTheme theme))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unsupported biome id.");
            }

            return theme;
        }
    }
}
