using System;
using System.Collections.Generic;
using Tower.Core;

namespace Tower.Gen
{
    public static class FloorGenerator
    {
        public static FloorLayout Generate(FloorGenParams parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            Random random = new Random(parameters.Seed);
            int roomCount = NextInclusive(random, parameters.RoomCountRange.Min, parameters.RoomCountRange.Max);
            bool hasBranch = roomCount >= 4 && random.Next(0, 2) == 1;
            int exitRoomId = roomCount - 1;
            int branchRoomId = hasBranch ? roomCount - 2 : -1;

            GridMap[] maps = new GridMap[roomCount];
            int[] depths = new int[roomCount];
            List<FloorDoor>[] doors = new List<FloorDoor>[roomCount];

            for (int i = 0; i < roomCount; i++)
            {
                int width = NextInclusive(random, parameters.RoomSizeRange.Min, parameters.RoomSizeRange.Max);
                int height = NextInclusive(random, parameters.RoomSizeRange.Min, parameters.RoomSizeRange.Max);
                maps[i] = new GridMap(width, height);
                doors[i] = new List<FloorDoor>();
            }

            List<FloorEdge> edges = new List<FloorEdge>();
            if (hasBranch)
            {
                BuildBranchedGraph(random, maps, depths, doors, edges, branchRoomId, exitRoomId);
            }
            else
            {
                BuildLinearGraph(random, maps, depths, doors, edges, exitRoomId);
            }

            int campRoomId = parameters.IncludeCamp ? FindRoomBeforeExit(edges, exitRoomId) : -1;
            List<FloorRoom> rooms = new List<FloorRoom>();
            for (int id = 0; id < roomCount; id++)
            {
                bool isEntrance = id == 0;
                bool isExit = id == exitRoomId;
                bool isBossRoom = parameters.IsBossFloor && isExit;
                bool isCamp = id == campRoomId;
                RoomKind kind = GetRoomKind(isEntrance, isExit, isBossRoom, isCamp);
                FloorEncounter encounter = CreateEncounter(parameters, id, maps[id], depths[id], isEntrance, isBossRoom, isCamp);
                rooms.Add(new FloorRoom(id, depths[id], maps[id], doors[id], encounter, isEntrance, isExit, isBossRoom, kind));
            }

            return new FloorLayout(parameters.Seed, parameters.IsBossFloor, rooms, edges, 0, exitRoomId, BiomeTheme.For(parameters.BiomeId));
        }

        private static void BuildLinearGraph(
            Random random,
            IReadOnlyList<GridMap> maps,
            int[] depths,
            List<FloorDoor>[] doors,
            List<FloorEdge> edges,
            int exitRoomId)
        {
            for (int id = 0; id <= exitRoomId; id++)
            {
                depths[id] = id;
            }

            for (int id = 0; id < exitRoomId; id++)
            {
                AddEdge(random, maps, doors, edges, id, id + 1, FloorDoorSide.East, FloorDoorSide.West);
            }
        }

        private static void BuildBranchedGraph(
            Random random,
            IReadOnlyList<GridMap> maps,
            int[] depths,
            List<FloorDoor>[] doors,
            List<FloorEdge> edges,
            int branchRoomId,
            int exitRoomId)
        {
            int lastMainRoomBeforeExit = branchRoomId - 1;
            for (int id = 0; id <= lastMainRoomBeforeExit; id++)
            {
                depths[id] = id;
            }

            depths[exitRoomId] = lastMainRoomBeforeExit + 1;

            for (int id = 0; id < lastMainRoomBeforeExit; id++)
            {
                AddEdge(random, maps, doors, edges, id, id + 1, FloorDoorSide.East, FloorDoorSide.West);
            }

            AddEdge(random, maps, doors, edges, lastMainRoomBeforeExit, exitRoomId, FloorDoorSide.East, FloorDoorSide.West);

            int branchFromRoomId = NextInclusive(random, 0, lastMainRoomBeforeExit);
            depths[branchRoomId] = depths[branchFromRoomId] + 1;
            AddEdge(random, maps, doors, edges, branchFromRoomId, branchRoomId, FloorDoorSide.South, FloorDoorSide.North);
        }

        private static void AddEdge(
            Random random,
            IReadOnlyList<GridMap> maps,
            List<FloorDoor>[] doors,
            List<FloorEdge> edges,
            int roomAId,
            int roomBId,
            FloorDoorSide sideA,
            FloorDoorSide sideB)
        {
            FloorDoor doorA = new FloorDoor(roomAId, roomBId, CreateDoorPosition(random, maps[roomAId], sideA), sideA);
            FloorDoor doorB = new FloorDoor(roomBId, roomAId, CreateDoorPosition(random, maps[roomBId], sideB), sideB);
            doors[roomAId].Add(doorA);
            doors[roomBId].Add(doorB);
            edges.Add(new FloorEdge(roomAId, roomBId, doorA, doorB));
        }

        private static GridPos CreateDoorPosition(Random random, GridMap map, FloorDoorSide side)
        {
            switch (side)
            {
                case FloorDoorSide.North:
                    return new GridPos(NextInterior(random, map.Width), 0);
                case FloorDoorSide.East:
                    return new GridPos(map.Width - 1, NextInterior(random, map.Height));
                case FloorDoorSide.South:
                    return new GridPos(NextInterior(random, map.Width), map.Height - 1);
                case FloorDoorSide.West:
                    return new GridPos(0, NextInterior(random, map.Height));
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, "Unsupported door side.");
            }
        }

        private static FloorEncounter CreateEncounter(
            FloorGenParams parameters,
            int roomId,
            GridMap map,
            int depth,
            bool isEntrance,
            bool isBossRoom,
            bool isCamp)
        {
            if (isEntrance || isCamp)
            {
                return FloorEncounter.None();
            }

            if (isBossRoom)
            {
                return new FloorEncounter(true, 1, new[] { new FloorEnemySlot(0, parameters.BossKindSlot) });
            }

            int sizeBonus = Math.Max(0, ((map.Width * map.Height) - 64) / 64);
            int enemyCount = Clamp(1 + depth + sizeBonus, 1, 5);
            List<FloorEnemySlot> slots = new List<FloorEnemySlot>();
            for (int i = 0; i < enemyCount; i++)
            {
                int slotIndex = (roomId + depth + map.Width + map.Height + i) % parameters.EnemyKindSlots.Count;
                slots.Add(new FloorEnemySlot(i, parameters.EnemyKindSlots[slotIndex]));
            }

            return new FloorEncounter(false, enemyCount, slots);
        }

        private static int FindRoomBeforeExit(IReadOnlyList<FloorEdge> edges, int exitRoomId)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                FloorEdge edge = edges[i];
                if (edge.RoomAId == exitRoomId)
                {
                    return edge.RoomBId;
                }

                if (edge.RoomBId == exitRoomId)
                {
                    return edge.RoomAId;
                }
            }

            return -1;
        }

        private static RoomKind GetRoomKind(bool isEntrance, bool isExit, bool isBossRoom, bool isCamp)
        {
            if (isEntrance)
            {
                return RoomKind.Entrance;
            }

            if (isCamp)
            {
                return RoomKind.Camp;
            }

            if (isBossRoom)
            {
                return RoomKind.Boss;
            }

            if (isExit)
            {
                return RoomKind.Exit;
            }

            return RoomKind.Normal;
        }

        private static int NextInclusive(Random random, int min, int max)
        {
            return random.Next(min, max + 1);
        }

        private static int NextInterior(Random random, int size)
        {
            if (size <= 2)
            {
                return size / 2;
            }

            return random.Next(1, size - 1);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
