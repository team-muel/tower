using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tower.Gen
{
    // 층계 골격 생성기(node+route 모델, 74 §6/§8). 격자 방·격자 문 생성은 폐기.
    // 노드 스켈레톤(id/depth/kind/roomTemplateId/flags) + 갈림길(route) 종류를
    // BiomeDef 가중으로 결정적으로 배정한다. 조우/전투 격자는 T49에서 제거됐다.
    public static class FloorGenerator
    {
        public static FloorGraph Generate(FloorGenParams parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            Random random = new Random(parameters.Seed);
            int nodeCount = NextInclusive(random, parameters.RoomCountRange.Min, parameters.RoomCountRange.Max);
            int exitNodeId = nodeCount - 1;
            int campNodeId = parameters.IncludeCamp ? exitNodeId - 1 : -1;
            BiomeId biome = parameters.BiomeId;

            List<FloorNode> nodes = new List<FloorNode>(nodeCount);
            for (int id = 0; id < nodeCount; id++)
            {
                bool isEntrance = id == 0;
                bool isExit = id == exitNodeId;
                bool isBossRoom = parameters.IsBossFloor && isExit;
                bool isCamp = id == campNodeId && !isEntrance && !isExit;
                RoomKind kind = GetRoomKind(isEntrance, isExit, isBossRoom, isCamp);
                int depth = id;
                string roomTemplateId = BuildTemplateId(biome, kind, id);
                nodes.Add(new FloorNode(id, depth, kind, roomTemplateId, isEntrance, isExit, isBossRoom));
            }

            BiomeDef biomeDef = BiomeDef.For(biome);
            List<RouteEdge> routes = new List<RouteEdge>();
            int routeId = 0;
            for (int id = 0; id < exitNodeId; id++)
            {
                // 갈림길: 같은 다음 노드로 향하는 두 갈래(안전 성향 / 위험 성향) — DD2 route 선택.
                RouteType primary = PickRouteType(random, biomeDef);
                routes.Add(new RouteEdge(routeId++, id, id + 1, primary));
                RouteType alternate = PickRouteType(random, biomeDef);
                routes.Add(new RouteEdge(routeId++, id, id + 1, alternate));
            }

            return new FloorGraph(
                parameters.Seed,
                parameters.IsBossFloor,
                nodes,
                routes,
                0,
                exitNodeId,
                BiomeTheme.For(biome));
        }

        private static RouteType PickRouteType(Random random, BiomeDef biomeDef)
        {
            int total = 0;
            foreach (KeyValuePair<RouteType, int> pair in biomeDef.RouteWeights)
            {
                if (pair.Value > 0)
                {
                    total += pair.Value;
                }
            }

            if (total <= 0)
            {
                return RouteType.Combat;
            }

            int roll = random.Next(0, total);
            // 결정적 순회를 위해 enum 순서로 걷는다.
            RouteType[] order = { RouteType.Safe, RouteType.Combat, RouteType.Hazard, RouteType.Special };
            for (int i = 0; i < order.Length; i++)
            {
                if (biomeDef.RouteWeights.TryGetValue(order[i], out int weight) && weight > 0)
                {
                    if (roll < weight)
                    {
                        return order[i];
                    }

                    roll -= weight;
                }
            }

            return RouteType.Combat;
        }

        private static string BuildTemplateId(BiomeId biome, RoomKind kind, int id)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/{2}",
                biome.ToString().ToLowerInvariant(),
                kind.ToString().ToLowerInvariant(),
                id);
        }

        private static RoomKind GetRoomKind(bool isEntrance, bool isExit, bool isBossRoom, bool isCamp)
        {
            if (isEntrance)
            {
                return RoomKind.Entrance;
            }

            if (isCamp)
            {
                return RoomKind.Camp;
            }

            if (isBossRoom)
            {
                return RoomKind.Boss;
            }

            if (isExit)
            {
                return RoomKind.Exit;
            }

            return RoomKind.Normal;
        }

        private static int NextInclusive(Random random, int min, int max)
        {
            return random.Next(min, max + 1);
        }
    }
}
