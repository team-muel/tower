using System;
using System.Collections.Generic;
using System.Text;

namespace Tower.Gen
{
    public sealed class FloorLayout
    {
        internal FloorLayout(
            int seed,
            bool isBossFloor,
            IReadOnlyList<FloorRoom> rooms,
            IReadOnlyList<FloorEdge> edges,
            int entranceRoomId,
            int exitRoomId)
        {
            if (rooms == null)
            {
                throw new ArgumentNullException(nameof(rooms));
            }

            if (edges == null)
            {
                throw new ArgumentNullException(nameof(edges));
            }

            if (rooms.Count == 0)
            {
                throw new ArgumentException("Floor layout requires at least one room.", nameof(rooms));
            }

            Seed = seed;
            IsBossFloor = isBossFloor;
            Rooms = new List<FloorRoom>(rooms);
            Edges = new List<FloorEdge>(edges);
            EntranceRoomId = entranceRoomId;
            ExitRoomId = exitRoomId;
        }

        public int Seed { get; }

        public bool IsBossFloor { get; }

        public IReadOnlyList<FloorRoom> Rooms { get; }

        public IReadOnlyList<FloorEdge> Edges { get; }

        public int EntranceRoomId { get; }

        public int ExitRoomId { get; }

        public string ToStableString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("seed=").Append(Seed)
                .Append(";bossFloor=").Append(IsBossFloor)
                .Append(";entrance=").Append(EntranceRoomId)
                .Append(";exit=").Append(ExitRoomId);

            for (int i = 0; i < Rooms.Count; i++)
            {
                FloorRoom room = Rooms[i];
                builder.Append("|room:")
                    .Append(room.Id).Append(',')
                    .Append(room.Depth).Append(',')
                    .Append(room.Map.Width).Append('x').Append(room.Map.Height).Append(',')
                    .Append(room.IsEntrance).Append(',')
                    .Append(room.IsExit).Append(',')
                    .Append(room.IsBossRoom).Append(',')
                    .Append(room.Encounter.IsBoss).Append(',')
                    .Append(room.Encounter.EnemyCount);

                for (int slotIndex = 0; slotIndex < room.Encounter.EnemySlots.Count; slotIndex++)
                {
                    FloorEnemySlot slot = room.Encounter.EnemySlots[slotIndex];
                    builder.Append(",slot:")
                        .Append(slot.Index).Append(':')
                        .Append(slot.KindSlot);
                }

                for (int doorIndex = 0; doorIndex < room.Doors.Count; doorIndex++)
                {
                    FloorDoor door = room.Doors[doorIndex];
                    builder.Append(",door:")
                        .Append(door.ConnectedRoomId).Append(':')
                        .Append(door.Position.X).Append(':')
                        .Append(door.Position.Y).Append(':')
                        .Append(door.Side);
                }
            }

            for (int i = 0; i < Edges.Count; i++)
            {
                FloorEdge edge = Edges[i];
                builder.Append("|edge:")
                    .Append(edge.RoomAId).Append('-')
                    .Append(edge.RoomBId).Append(',')
                    .Append(edge.DoorA.Position.X).Append(':')
                    .Append(edge.DoorA.Position.Y).Append(':')
                    .Append(edge.DoorA.Side).Append(',')
                    .Append(edge.DoorB.Position.X).Append(':')
                    .Append(edge.DoorB.Position.Y).Append(':')
                    .Append(edge.DoorB.Side);
            }

            return builder.ToString();
        }
    }
}
