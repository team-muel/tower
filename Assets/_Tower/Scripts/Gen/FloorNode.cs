using System;

namespace Tower.Gen
{
    // 골격 노드. 스켈레톤(id/depth/kind/roomTemplateId/flags)은 층 진입 시 확정(DC-4=C).
    // 내용(조우·보상)은 lazy 바인딩 → 런타임 FloorNodeContent(별도).
    public sealed class FloorNode
    {
        public FloorNode(int id, int depth, RoomKind kind, string roomTemplateId,
                         bool isEntrance, bool isExit, bool isBossRoom)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (depth < 0) throw new ArgumentOutOfRangeException(nameof(depth));
            if (string.IsNullOrWhiteSpace(roomTemplateId))
                throw new ArgumentException("Room template id is required.", nameof(roomTemplateId));

            Id = id; Depth = depth; Kind = kind; RoomTemplateId = roomTemplateId;
            IsEntrance = isEntrance; IsExit = isExit; IsBossRoom = isBossRoom;
        }

        public int Id { get; }
        public int Depth { get; }
        public RoomKind Kind { get; }
        public string RoomTemplateId { get; }   // 손제작 템플릿 참조(절차가 선택). 내용은 lazy.
        public bool IsEntrance { get; }
        public bool IsExit { get; }
        public bool IsBossRoom { get; }
    }
}
