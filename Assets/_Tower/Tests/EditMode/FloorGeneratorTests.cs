using System.Collections.Generic;
using NUnit.Framework;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    public sealed class FloorGeneratorTests
    {
        [Test]
        public void SameSeedAndParamsProduceStableLayout()
        {
            FloorGenParams parameters = new FloorGenParams(12345);

            FloorLayout first = FloorGenerator.Generate(parameters);
            FloorLayout second = FloorGenerator.Generate(parameters);

            Assert.AreEqual(first.ToStableString(), second.ToStableString());
        }

        [Test]
        public void RoomCountStaysWithinConfiguredRange()
        {
            FloorGenParams parameters = new FloorGenParams(
                13,
                new IntRange(4, 5),
                false,
                new IntRange(8, 14),
                new[] { "melee", "ranged" },
                "boss");

            FloorLayout layout = FloorGenerator.Generate(parameters);

            Assert.That(layout.Rooms.Count, Is.InRange(4, 5));
        }

        [Test]
        public void GraphConnectsEveryRoom()
        {
            FloorLayout layout = FloorGenerator.Generate(new FloorGenParams(777));

            HashSet<int> reached = Traverse(layout);

            Assert.AreEqual(layout.Rooms.Count, reached.Count);
        }

        [Test]
        public void EntranceRoomHasNoEncounter()
        {
            FloorLayout layout = FloorGenerator.Generate(new FloorGenParams(29));

            FloorRoom entrance = layout.Rooms[layout.EntranceRoomId];

            Assert.IsTrue(entrance.IsEntrance);
            Assert.IsFalse(entrance.Encounter.HasEncounter);
            Assert.AreEqual(0, entrance.Encounter.EnemyCount);
        }

        [Test]
        public void BossFloorMakesExitRoomSingleBossEncounter()
        {
            FloorLayout layout = FloorGenerator.Generate(new FloorGenParams(64, true));

            FloorRoom exit = layout.Rooms[layout.ExitRoomId];

            Assert.IsTrue(layout.IsBossFloor);
            Assert.IsTrue(exit.IsExit);
            Assert.IsTrue(exit.IsBossRoom);
            Assert.IsTrue(exit.Encounter.HasEncounter);
            Assert.IsTrue(exit.Encounter.IsBoss);
            Assert.AreEqual(1, exit.Encounter.EnemyCount);
            Assert.AreEqual("boss", exit.Encounter.EnemySlots[0].KindSlot);
        }

        [Test]
        public void NonEntranceEncountersFollowDepthAndSizeClamp()
        {
            FloorLayout layout = FloorGenerator.Generate(new FloorGenParams(90210));

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                FloorRoom room = layout.Rooms[i];
                if (room.IsEntrance)
                {
                    continue;
                }

                int sizeBonus = System.Math.Max(0, ((room.Map.Width * room.Map.Height) - 64) / 64);
                int expected = Clamp(1 + room.Depth + sizeBonus, 1, 5);
                Assert.AreEqual(expected, room.Encounter.EnemyCount);
                Assert.AreEqual(room.Encounter.EnemyCount, room.Encounter.EnemySlots.Count);
            }
        }

        private static HashSet<int> Traverse(FloorLayout layout)
        {
            Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                adjacency[layout.Rooms[i].Id] = new List<int>();
            }

            for (int i = 0; i < layout.Edges.Count; i++)
            {
                FloorEdge edge = layout.Edges[i];
                adjacency[edge.RoomAId].Add(edge.RoomBId);
                adjacency[edge.RoomBId].Add(edge.RoomAId);
            }

            HashSet<int> reached = new HashSet<int>();
            Queue<int> open = new Queue<int>();
            open.Enqueue(layout.EntranceRoomId);
            reached.Add(layout.EntranceRoomId);

            while (open.Count > 0)
            {
                int current = open.Dequeue();
                List<int> neighbors = adjacency[current];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    if (reached.Add(neighbors[i]))
                    {
                        open.Enqueue(neighbors[i]);
                    }
                }
            }

            return reached;
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
