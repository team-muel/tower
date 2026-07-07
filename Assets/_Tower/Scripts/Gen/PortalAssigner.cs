using System;
using System.Collections.Generic;
using Tower.Core;

namespace Tower.Gen
{
    // T25→T30: 결정적 node -> RouteOffer 배정(구 door -> PortalDef). 노드의 갈림길이
    // 열릴 때 호출. 같은 (graph seed, node)는 항상 같은 offer → RNG 미노출, 재클릭
    // 리롤 없음. 각 outgoing route(RouteEdge)마다 스카우트 프리뷰(RouteOption)를 만든다.
    //
    // 우선순위(Hades 축약): Boss/Exit 목적 → Shortcut 보상. Camp → Heal.
    //   그 외 → eligible 보상 풀에서 결정적 선택(연속 중복 억제).
    public static class PortalAssigner
    {
        // 현재 노드보다 몇 depth band 더 깊이 닿을 수 있는지(초과 시 depth-gated).
        private const int MaxDepthReach = 1;

        // eligible 보상 풀(억제가 안정적 링을 걷도록 순서 고정).
        private static readonly RewardType[] EligiblePool =
        {
            RewardType.Heal,
            RewardType.Resource,
            RewardType.Ability,
            RewardType.Shortcut
        };

        public static RouteOffer AssignForNode(FloorGraph graph, FloorNode node)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            List<RouteEdge> outgoing = new List<RouteEdge>(graph.RoutesFrom(node.Id));
            if (outgoing.Count == 0)
            {
                return RouteOffer.Empty(node.Id);
            }

            BiomeDef biomeDef = BiomeDef.For(graph.BiomeTheme.Id);
            List<RouteOption> options = new List<RouteOption>(outgoing.Count);
            RewardType previousReward = RewardType.None;
            for (int index = 0; index < outgoing.Count; index++)
            {
                options.Add(AssignForRoute(graph, node, outgoing[index], index, biomeDef, ref previousReward));
            }

            return new RouteOffer(node.Id, options);
        }

        public static RouteOption AssignForRoute(FloorGraph graph, FloorNode node, RouteEdge route, int index)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            RewardType ignored = RewardType.None;
            return AssignForRoute(graph, node, route, index, BiomeDef.For(graph.BiomeTheme.Id), ref ignored);
        }

        private static RouteOption AssignForRoute(
            FloorGraph graph,
            FloorNode node,
            RouteEdge route,
            int index,
            BiomeDef biomeDef,
            ref RewardType previousReward)
        {
            BiomeId biome = graph.BiomeTheme.Id;
            uint hash = Hash(graph.Seed, node.Id, node.Depth, index, (int)biome, route.Id);

            FloorNode toNode = graph.NodeById(route.ToNodeId);
            RoomKind toKind = toNode != null ? toNode.Kind : RoomKind.Normal;
            int toDepth = toNode != null ? toNode.Depth : node.Depth + 1;

            RewardType reward = PickReward(hash, toKind, previousReward);
            int magnitude = RewardMagnitude(reward, toDepth, hash);
            previousReward = reward;

            PortalRisk risk = BuildRisk(graph, toKind, toDepth, reward, route.RouteType);
            PortalLockReason lockReason = BuildLockReason(node, toKind, toDepth, reward);
            bool rerollAllowed = RerollAllowed(lockReason, reward);
            bool scouted = (int)(hash % 100u) < biomeDef.ScoutRouteChance;

            return new RouteOption(
                route.Id,
                route.RouteType,
                route.ToNodeId,
                toKind,
                biome,
                toDepth,
                reward,
                magnitude,
                risk,
                lockReason,
                rerollAllowed,
                scouted);
        }

        private static RewardType PickReward(uint hash, RoomKind toKind, RewardType previousReward)
        {
            // Boss/Exit 목적은 항상 Shortcut 보상을 매단다.
            if (toKind == RoomKind.Boss || toKind == RoomKind.Exit)
            {
                return RewardType.Shortcut;
            }

            // Camp는 안전 회복 갈래.
            if (toKind == RoomKind.Camp)
            {
                return RewardType.Heal;
            }

            int start = (int)(hash % (uint)EligiblePool.Length);
            for (int step = 0; step < EligiblePool.Length; step++)
            {
                RewardType candidate = EligiblePool[(start + step) % EligiblePool.Length];

                // 연속 중복 억제: 대안이 있으면 직전과 같은 보상은 건너뛴다.
                if (candidate == previousReward)
                {
                    continue;
                }

                return candidate;
            }

            return EligiblePool[start];
        }

        private static int RewardMagnitude(RewardType reward, int toDepth, uint hash)
        {
            switch (reward)
            {
                case RewardType.None:
                    return 0;
                case RewardType.Heal:
                    return 10 + (toDepth * 5);
                case RewardType.Resource:
                    return 5 + (int)(hash % 6u) + toDepth;
                case RewardType.Ability:
                    return 1;
                case RewardType.Shortcut:
                    return 1;
                default:
                    return 0;
            }
        }

        private static PortalRisk BuildRisk(FloorGraph graph, RoomKind toKind, int toDepth, RewardType reward, RouteType routeType)
        {
            PortalRisk risk = PortalRisk.None;

            if (toKind == RoomKind.Boss)
            {
                risk |= PortalRisk.Boss;
            }

            // 깊은 일반 노드는 강적 위험을 신호한다(격자 조우가 노드에서 빠졌으므로 depth로 근사).
            if (toKind == RoomKind.Normal && toDepth >= 3)
            {
                risk |= PortalRisk.Elite;
            }

            // 위험 route 또는 보스층 출구 접근은 hazard flavour.
            if (routeType == RouteType.Hazard || (graph.IsBossFloor && toKind == RoomKind.Exit))
            {
                risk |= PortalRisk.Hazard;
            }

            // 위험한 방 뒤의 shortcut은 하이스테이크.
            if (reward == RewardType.Shortcut && (risk & (PortalRisk.Boss | PortalRisk.Elite)) != PortalRisk.None)
            {
                risk |= PortalRisk.HighStakes;
            }

            return risk;
        }

        private static PortalLockReason BuildLockReason(FloorNode node, RoomKind toKind, int toDepth, RewardType reward)
        {
            // Boss 보상은 boss-gated.
            if (toKind == RoomKind.Boss)
            {
                return PortalLockReason.BossGated;
            }

            // 한 depth band 넘게 앞서가면 depth-gated.
            if (toDepth - node.Depth > MaxDepthReach)
            {
                return PortalLockReason.DepthGated;
            }

            // Ability 보상은 v0에서 열쇠 필요.
            if (reward == RewardType.Ability)
            {
                return PortalLockReason.RequiresKey;
            }

            return PortalLockReason.None;
        }

        private static bool RerollAllowed(PortalLockReason lockReason, RewardType reward)
        {
            if (lockReason != PortalLockReason.None)
            {
                return false;
            }

            return reward == RewardType.Heal || reward == RewardType.Resource;
        }

        // 결정적 FNV-1a 스타일 믹스. RNG 미노출(T25/T30 하드 제약).
        private static uint Hash(int seed, int nodeId, int depth, int index, int biome, int routeId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)seed) * 16777619u;
                hash = (hash ^ (uint)nodeId) * 16777619u;
                hash = (hash ^ (uint)depth) * 16777619u;
                hash = (hash ^ (uint)index) * 16777619u;
                hash = (hash ^ (uint)biome) * 16777619u;
                hash = (hash ^ (uint)routeId) * 16777619u;
                return hash;
            }
        }
    }
}
