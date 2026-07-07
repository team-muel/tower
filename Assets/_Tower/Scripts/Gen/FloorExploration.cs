using System.Collections.Generic;

namespace Tower.Gen
{
    // 런타임 탐험 상태. M키 맵은 여기 있는 것만 렌더(방문 노드 + 정찰 route).
    // 전체 조망(모든 노드)은 베타/디버그 빌드에서만 이 게이트를 우회.
    public sealed class FloorExploration
    {
        private readonly HashSet<int> _visitedNodes = new HashSet<int>();
        private readonly HashSet<int> _scoutedRoutes = new HashSet<int>();

        public void MarkVisited(int nodeId) => _visitedNodes.Add(nodeId);
        public void MarkScouted(int routeId) => _scoutedRoutes.Add(routeId);
        public bool IsVisited(int nodeId) => _visitedNodes.Contains(nodeId);
        public bool IsScouted(int routeId) => _scoutedRoutes.Contains(routeId);
        public IReadOnlyCollection<int> VisitedNodes => _visitedNodes;
        public IReadOnlyCollection<int> ScoutedRoutes => _scoutedRoutes;
    }
}
