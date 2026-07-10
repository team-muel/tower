using System;
namespace Tower.Gen
{
    // 노드의 lazy 바인딩 런타임 내용(74 §2 "런타임 FloorNodeContent 별도").
    // 골격 노드(FloorNode)는 전투 격자/조우를 들지 않는다. 현재는 숲 렌더러와
    // 미리보기가 필요한 결정적 공간 치수만 바인딩한다.
    public sealed class FloorNodeContent
    {
        public FloorNodeContent(int nodeId, int width, int height)
        {
            if (nodeId < 0) throw new ArgumentOutOfRangeException(nameof(nodeId));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            NodeId = nodeId;
            Width = width;
            Height = height;
        }

        public int NodeId { get; }
        public int Width { get; }
        public int Height { get; }
    }
}
