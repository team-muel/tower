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
            Assert.AreEqual(RoomKind.Entrance, entrance.Kind);
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
            Assert.AreEqual(RoomKind.Boss, exit.Kind);
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

        [Test]
        public void IncludeCamp_MarksRoomBeforeExitAndClearsEncounter()
        {
            FloorGenParams parameters = new FloorGenParams(
                451,
                new IntRange(5, 5),
                true,
                new IntRange(8, 14),
                new[] { "melee", "ranged" },
                "boss",
                includeCamp: true);

            FloorLayout layout = FloorGenerator.Generate(parameters);
            FloorRoom camp = FindCamp(layout);
            FloorRoom exit = layout.Rooms[layout.ExitRoomId];

            Assert.AreEqual(RoomKind.Camp, camp.Kind);
            Assert.IsFalse(camp.IsEntrance);
            Assert.IsFalse(camp.IsExit);
            Assert.IsFalse(camp.IsBossRoom);
            Assert.IsFalse(camp.Encounter.HasEncounter);
            Assert.AreEqual(0, camp.Encounter.EnemyCount);
            Assert.IsTrue(AreConnected(layout, camp.Id, exit.Id));
            Assert.AreEqual(RoomKind.Boss, exit.Kind);
            Assert.IsTrue(exit.Encounter.IsBoss);
        }

        [Test]
        public void IncludeCamp_DoesNotPerturbSeededGeometry()
        {
            FloorGenParams withoutCamp = new FloorGenParams(
                8821,
                new IntRange(5, 5),
                false,
                new IntRange(8, 14),
                new[] { "melee", "ranged" },
                "boss");
            FloorGenParams withCamp = new FloorGenParams(
                8821,
                new IntRange(5, 5),
                false,
                new IntRange(8, 14),
                new[] { "melee", "ranged" },
                "boss",
                includeCamp: true);

            FloorLayout first = FloorGenerator.Generate(withoutCamp);
            FloorLayout second = FloorGenerator.Generate(withCamp);

            Assert.AreEqual(GeometrySignature(first), GeometrySignature(second));
        }

        [Test]
        public void IncludeCamp_RemainsDeterministicForSameSeed()
        {
            FloorGenParams parameters = new FloorGenParams(
                912,
                new IntRange(4, 5),
                false,
                new IntRange(8, 14),
                new[] { "melee", "ranged", "elite" },
                "boss",
                includeCamp: true);

            FloorLayout first = FloorGenerator.Generate(parameters);
            FloorLayout second = FloorGenerator.Generate(parameters);

            Assert.AreEqual(first.ToStableString(), second.ToStableString());
            Assert.AreEqual(FindCamp(first).Id, FindCamp(second).Id);
        }

        [Test]
        public void BiomeIdFlowsToLayoutTheme()
        {
            FloorGenParams parameters = new FloorGenParams(
                77,
                new IntRange(3, 3),
                false,
                new IntRange(8, 8),
                new[] { "melee" },
                "boss",
                biomeId: BiomeId.CrystalMine);

            FloorLayout layout = FloorGenerator.Generate(parameters);

            Assert.AreEqual(BiomeId.CrystalMine, layout.BiomeTheme.Id);
            Assert.AreEqual(BiomeTheme.For(BiomeId.CrystalMine), layout.BiomeTheme);
            Assert.Greater(layout.BiomeTheme.FogDensity, 0f);
            Assert.Greater(layout.BiomeTheme.DirectionalLightIntensity, 0f);
        }

        [Test]
        public void BiomeThemePresetsExistForCanonicalBiomes()
        {
            BiomeId[] ids =
            {
                BiomeId.Forest,
                BiomeId.Desert,
                BiomeId.GhostManor,
                BiomeId.CrystalMine
            };

            for (int i = 0; i < ids.Length; i++)
            {
                BiomeTheme theme = BiomeTheme.For(ids[i]);

                Assert.AreEqual(ids[i], theme.Id);
                Assert.Greater(theme.FogDensity, 0f);
                Assert.Greater(theme.DirectionalLightIntensity, 0f);
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

        private static FloorRoom FindCamp(FloorLayout layout)
        {
            FloorRoom camp = null;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (layout.Rooms[i].Kind == RoomKind.Camp)
                {
                    Assert.IsNull(camp, "Only one camp room is expected.");
                    camp = layout.Rooms[i];
                }
            }

            Assert.IsNotNull(camp, "Expected a camp room.");
            return camp;
        }

        private static bool AreConnected(FloorLayout layout, int roomAId, int roomBId)
        {
            for (int i = 0; i < layout.Edges.Count; i++)
            {
                FloorEdge edge = layout.Edges[i];
                if ((edge.RoomAId == roomAId && edge.RoomBId == roomBId) ||
                    (edge.RoomAId == roomBId && edge.RoomBId == roomAId))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GeometrySignature(FloorLayout layout)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                FloorRoom room = layout.Rooms[i];
                builder.Append("room:")
                    .Append(room.Id).Append(',')
                    .Append(room.Depth).Append(',')
                    .Append(room.Map.Width).Append('x').Append(room.Map.Height);

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

            for (int i = 0; i < layout.Edges.Count; i++)
            {
                FloorEdge edge = layout.Edges[i];
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
