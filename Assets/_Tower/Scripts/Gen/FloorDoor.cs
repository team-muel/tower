using System;
using Tower.Core;

namespace Tower.Gen
{
    public sealed class FloorDoor
    {
        internal FloorDoor(int roomId, int connectedRoomId, GridPos position, FloorDoorSide side)
        {
            if (roomId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roomId), "Room id cannot be negative.");
            }

            if (connectedRoomId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(connectedRoomId), "Connected room id cannot be negative.");
            }

            RoomId = roomId;
            ConnectedRoomId = connectedRoomId;
            Position = position;
            Side = side;
        }

        public int RoomId { get; }

        public int ConnectedRoomId { get; }

        public GridPos Position { get; }

        public FloorDoorSide Side { get; }
    }
}
