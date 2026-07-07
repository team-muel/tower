using System;
using System.Collections.Generic;

namespace Tower.Gen
{
    // DD2 biome_data_export 대응. 이 층계에서 legal한 route 종류·가중치와 정찰 확률.
    // 구체 route/roadEvent id는 나중(정적 데이터=Sdp)에.
    public sealed class BiomeDef
    {
        public BiomeDef(BiomeId id, IReadOnlyDictionary<RouteType, int> routeWeights,
                        int scoutNodeChance, int scoutRouteChance)
        {
            if (routeWeights == null) throw new ArgumentNullException(nameof(routeWeights));
            Id = id; RouteWeights = new Dictionary<RouteType, int>(routeWeights);
            ScoutNodeChance = scoutNodeChance; ScoutRouteChance = scoutRouteChance;
        }

        public BiomeId Id { get; }
        public IReadOnlyDictionary<RouteType, int> RouteWeights { get; } // route_data_export m_Chance
        public int ScoutNodeChance { get; }
        public int ScoutRouteChance { get; }

        // 층계 생성이 legal route 가중을 뽑을 수 있도록 하는 프리셋(BiomeTheme.For 대응).
        private static readonly Dictionary<BiomeId, BiomeDef> Presets = new Dictionary<BiomeId, BiomeDef>
        {
            {
                BiomeId.Forest,
                new BiomeDef(
                    BiomeId.Forest,
                    new Dictionary<RouteType, int>
                    {
                        { RouteType.Safe, 40 },
                        { RouteType.Combat, 40 },
                        { RouteType.Hazard, 15 },
                        { RouteType.Special, 5 }
                    },
                    60,
                    50)
            },
            {
                BiomeId.Desert,
                new BiomeDef(
                    BiomeId.Desert,
                    new Dictionary<RouteType, int>
                    {
                        { RouteType.Safe, 25 },
                        { RouteType.Combat, 45 },
                        { RouteType.Hazard, 25 },
                        { RouteType.Special, 5 }
                    },
                    50,
                    40)
            },
            {
                BiomeId.GhostManor,
                new BiomeDef(
                    BiomeId.GhostManor,
                    new Dictionary<RouteType, int>
                    {
                        { RouteType.Safe, 20 },
                        { RouteType.Combat, 40 },
                        { RouteType.Hazard, 30 },
                        { RouteType.Special, 10 }
                    },
                    40,
                    30)
            },
            {
                BiomeId.CrystalMine,
                new BiomeDef(
                    BiomeId.CrystalMine,
                    new Dictionary<RouteType, int>
                    {
                        { RouteType.Safe, 30 },
                        { RouteType.Combat, 35 },
                        { RouteType.Hazard, 20 },
                        { RouteType.Special, 15 }
                    },
                    55,
                    45)
            }
        };

        public static BiomeDef For(BiomeId id)
        {
            if (!Presets.TryGetValue(id, out BiomeDef def))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unsupported biome id.");
            }

            return def;
        }
    }
}
