using System;

namespace Tower.Gen
{
    public sealed class FloorEdge
    {
        internal FloorEdge(int roomAId, int roomBId, FloorDoor doorA, FloorDoor doorB)
        {
            if (roomAId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roomAId), "Room id cannot be negative.");
            }

            if (roomBId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roomBId), "Room id cannot be negative.");
            }

            if (roomAId == roomBId)
            {
                throw new ArgumentException("An edge must connect two different rooms.", nameof(roomBId));
            }

            if (doorA == null)
            {
                throw new ArgumentNullException(nameof(doorA));
            }

            if (doorB == null)
            {
                throw new ArgumentNullException(nameof(doorB));
            }

            RoomAId = roomAId;
            RoomBId = roomBId;
            DoorA = doorA;
            DoorB = doorB;
        }

        public int RoomAId { get; }

        public int RoomBId { get; }

        public FloorDoor DoorA { get; }

        public FloorDoor DoorB { get; }
    }
}
