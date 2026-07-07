using System;

namespace Tower.Gen
{
    // 노드에서 나가는 갈림길 옵션 하나. 방향성(from→to): 노드의 outgoing routes = 그 갈림길의 선택지.
    public sealed class RouteEdge
    {
        public RouteEdge(int id, int fromNodeId, int toNodeId, RouteType routeType)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (fromNodeId < 0) throw new ArgumentOutOfRangeException(nameof(fromNodeId));
            if (toNodeId < 0) throw new ArgumentOutOfRangeException(nameof(toNodeId));
            if (fromNodeId == toNodeId)
                throw new ArgumentException("A route must connect two different nodes.", nameof(toNodeId));

            Id = id; FromNodeId = fromNodeId; ToNodeId = toNodeId; RouteType = routeType;
        }

        public int Id { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public RouteType RouteType { get; }  // 안전/전투/위험/특수
    }
}
