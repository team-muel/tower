using System;
using Tower.Core;

namespace Tower.Gen
{
    // 노드의 lazy 바인딩 런타임 내용(74 §2 "런타임 FloorNodeContent 별도").
    // 골격 노드(FloorNode)는 조우를 들지 않는다. 조우 자리에 실제로 들어설 때
    // seed로부터 결정적으로 조우(Encounter)를 만든다. (전투 격자는 T49 철거로 제거.)
    public sealed class FloorNodeContent
    {
        public FloorNodeContent(int nodeId, FloorEncounter encounter)
        {
            if (nodeId < 0) throw new ArgumentOutOfRangeException(nameof(nodeId));
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));

            NodeId = nodeId;
            Encounter = encounter;
        }

        public int NodeId { get; }
        public FloorEncounter Encounter { get; }
    }
}
