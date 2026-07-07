using System;
using Tower.Core;

namespace Tower.Gen
{
    // 갈림길 옵션 하나의 스카우트 프리뷰(구 PortalDef, route로 이관).
    // 정찰(scout)로 노출: 목적 노드 유형/깊이 + 보상 후보 + 위험 + 잠금 + 리롤.
    public sealed class RouteOption
    {
        public RouteOption(int routeId, RouteType routeType, int toNodeId, RoomKind toKind,
                           BiomeId toBiome, int toDepth, RewardType rewardType, int rewardMagnitude,
                           PortalRisk riskTags, PortalLockReason lockReason, bool rerollAllowed, bool scouted)
        {
            if (routeId < 0) throw new ArgumentOutOfRangeException(nameof(routeId));
            if (toDepth < 0) throw new ArgumentOutOfRangeException(nameof(toDepth));
            if (rewardMagnitude < 0) throw new ArgumentOutOfRangeException(nameof(rewardMagnitude));

            RouteId = routeId; RouteType = routeType; ToNodeId = toNodeId; ToKind = toKind;
            ToBiome = toBiome; ToDepth = toDepth; RewardType = rewardType; RewardMagnitude = rewardMagnitude;
            RiskTags = riskTags; LockReason = lockReason; RerollAllowed = rerollAllowed; Scouted = scouted;
        }

        public int RouteId { get; }
        public RouteType RouteType { get; }
        public int ToNodeId { get; }
        public RoomKind ToKind { get; }
        public BiomeId ToBiome { get; }
        public int ToDepth { get; }
        public RewardType RewardType { get; }
        public int RewardMagnitude { get; }
        public PortalRisk RiskTags { get; }        // 개명 권장: RouteRisk
        public PortalLockReason LockReason { get; } // 개명 권장: RouteLockReason
        public bool RerollAllowed { get; }
        public bool Scouted { get; }               // 정찰 전이면 유형만 흐리게 노출
        public bool IsLocked => LockReason != PortalLockReason.None;

        public bool HasRisk(PortalRisk risk)
        {
            return (RiskTags & risk) == risk && risk != PortalRisk.None;
        }
    }
}
