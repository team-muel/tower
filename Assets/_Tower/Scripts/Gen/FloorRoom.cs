using System;
using System.Collections.Generic;
using Tower.Core;

namespace Tower.Gen
{
    public sealed class FloorRoom
    {
        internal FloorRoom(
            int id,
            int depth,
            GridMap map,
            IReadOnlyList<FloorDoor> doors,
            FloorEncounter encounter,
            bool isEntrance,
            bool isExit,
            bool isBossRoom)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Room id cannot be negative.");
            }

            if (depth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), "Depth cannot be negative.");
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (doors == null)
            {
                throw new ArgumentNullException(nameof(doors));
            }

            if (encounter == null)
            {
                throw new ArgumentNullException(nameof(encounter));
            }

            Id = id;
            Depth = depth;
            Map = map;
            Doors = new List<FloorDoor>(doors);
            Encounter = encounter;
            IsEntrance = isEntrance;
            IsExit = isExit;
            IsBossRoom = isBossRoom;
        }

        public int Id { get; }

        public int Depth { get; }

        public GridMap Map { get; }

        public IReadOnlyList<FloorDoor> Doors { get; }

        public FloorEncounter Encounter { get; }

        public bool IsEntrance { get; }

        public bool IsExit { get; }

        public bool IsBossRoom { get; }
    }
}
