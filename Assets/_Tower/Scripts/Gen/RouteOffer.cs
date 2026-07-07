using System;
using System.Collections.Generic;

namespace Tower.Gen
{
    // 한 노드의 갈림길 선택지 집합(구 PortalOffer).
    public sealed class RouteOffer
    {
        private static readonly RouteOption[] EmptyOptions = new RouteOption[0];

        public RouteOffer(int nodeId, IReadOnlyList<RouteOption> options)
        {
            if (nodeId < 0) throw new ArgumentOutOfRangeException(nameof(nodeId));
            if (options == null) throw new ArgumentNullException(nameof(options));
            NodeId = nodeId; Options = new List<RouteOption>(options);
        }

        public int NodeId { get; }
        public IReadOnlyList<RouteOption> Options { get; }
        public int Count => Options.Count;

        public static RouteOffer Empty(int nodeId)
        {
            return new RouteOffer(nodeId, EmptyOptions);
        }

        // 옵션 인덱스로 조회(갈림길 UI에서 i번째 선택지).
        public RouteOption ForIndex(int index)
        {
            if (index < 0 || index >= Options.Count)
            {
                return null;
            }

            return Options[index];
        }

        // route id로 조회.
        public RouteOption ForRoute(int routeId)
        {
            for (int i = 0; i < Options.Count; i++)
            {
                if (Options[i].RouteId == routeId)
                {
                    return Options[i];
                }
            }

            return null;
        }
    }
}
