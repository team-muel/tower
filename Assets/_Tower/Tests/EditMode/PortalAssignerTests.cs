using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    public sealed class PortalAssignerTests
    {
        [Test]
        public void AssignForRoom_SameSeedProducesDeterministicOffer()
        {
            var firstLayout = FloorGenerator.Generate(FixedParams(250125));
            var secondLayout = FloorGenerator.Generate(FixedParams(250125));
            var firstRoom = FirstRoomWithDoors(firstLayout);
            var secondRoom = FindRoom(secondLayout, firstRoom.Id);

            var firstOffer = PortalAssigner.AssignForRoom(firstLayout, firstRoom);
            var secondOffer = PortalAssigner.AssignForRoom(secondLayout, secondRoom);

            Assert.That(PortalSignature(firstOffer), Is.EqualTo(PortalSignature(secondOffer)));
        }

        [Test]
        public void AssignForRoom_LockReasonRulesApply()
        {
            var bossLayout = FloorGenerator.Generate(FixedParams(640064, isBossFloor: true));
            var bossApproach = FindRoomConnectedToExit(bossLayout);
            var bossOffer = PortalAssigner.AssignForRoom(bossLayout, bossApproach);
            var bossPortal = FirstPortalWithLock(bossOffer, PortalLockReason.BossGated);

            Assert.That(bossPortal.ToRoomKind, Is.EqualTo(RoomKind.Boss));
            Assert.That(bossPortal.IsLocked, Is.True);

            var abilityLayout = FloorGenerator.Generate(FixedParams(12345));
            var abilityRoom = FindRoom(abilityLayout, 1);
            var abilityPortal = PortalAssigner.AssignForDoor(abilityLayout, abilityRoom, abilityRoom.Doors[0], 0);

            Assert.That(abilityPortal.RewardType, Is.EqualTo(RewardType.Ability));
            Assert.That(abilityPortal.LockReason, Is.EqualTo(PortalLockReason.RequiresKey));
            Assert.That(abilityPortal.IsLocked, Is.True);

            var depthLayout = DepthGatedLayout();
            var depthOffer = PortalAssigner.AssignForRoom(depthLayout, depthLayout.Rooms[0]);
            var depthPortal = depthOffer.Portals[0];

            Assert.That(depthPortal.ToDepth, Is.EqualTo(3));
            Assert.That(depthPortal.LockReason, Is.EqualTo(PortalLockReason.DepthGated));
            Assert.That(depthPortal.IsLocked, Is.True);
        }

        [Test]
        public void AssignForRoom_RerollAllowedOnlyForUnlockedHealOrResource()
        {
            var checkedPortals = 0;
            int[] seeds = { 20260706, 250125, 451451, 90210, 770077 };
            for (var seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
            {
                var layout = FloorGenerator.Generate(FixedParams(seeds[seedIndex], includeCamp: seedIndex % 2 == 0));
                for (var roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
                {
                    var offer = PortalAssigner.AssignForRoom(layout, layout.Rooms[roomIndex]);
                    for (var portalIndex = 0; portalIndex < offer.Portals.Count; portalIndex++)
                    {
                        var portal = offer.Portals[portalIndex];
                        var expected = !portal.IsLocked
                            && (portal.RewardType == RewardType.Heal || portal.RewardType == RewardType.Resource);

                        Assert.That(portal.RerollAllowed, Is.EqualTo(expected), PortalSignature(portal));
                        checkedPortals++;
                    }
                }
            }

            Assert.That(checkedPortals, Is.GreaterThan(0));
        }

        [Test]
        public void AssignForRoom_SuppressesConsecutiveRewardTypesWhenAlternativesExist()
        {
            var layout = FloorGenerator.Generate(FixedParams(12345));
            var room = FindRoom(layout, 1);
            var offer = PortalAssigner.AssignForRoom(layout, room);

            Assert.That(offer.Portals.Count, Is.GreaterThanOrEqualTo(2));
            for (var index = 1; index < offer.Portals.Count; index++)
            {
                Assert.That(
                    offer.Portals[index].RewardType,
                    Is.Not.EqualTo(offer.Portals[index - 1].RewardType),
                    PortalSignature(offer));
            }
        }

        [Test]
        public void AssignForRoom_OfferCoversEveryDoorOfTheRoom()
        {
            int[] seeds = { 250125, 252525, 640064, 770077 };
            for (var seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
            {
                var layout = FloorGenerator.Generate(FixedParams(seeds[seedIndex], isBossFloor: seedIndex == 2));
                for (var roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
                {
                    var room = layout.Rooms[roomIndex];
                    var offer = PortalAssigner.AssignForRoom(layout, room);

                    Assert.That(offer.RoomId, Is.EqualTo(room.Id));
                    Assert.That(offer.Count, Is.EqualTo(room.Doors.Count));
                    for (var doorIndex = 0; doorIndex < room.Doors.Count; doorIndex++)
                    {
                        var portal = offer.ForDoor(doorIndex);
                        Assert.That(portal, Is.Not.Null, "Missing portal for door " + doorIndex);
                        Assert.That(portal.FromRoomId, Is.EqualTo(room.Id));
                        Assert.That(portal.DoorIndex, Is.EqualTo(doorIndex));
                    }
                }
            }
        }

        private static FloorGenParams FixedParams(int seed, bool isBossFloor = false, bool includeCamp = false)
        {
            return new FloorGenParams(
                seed,
                new IntRange(5, 5),
                isBossFloor,
                new IntRange(8, 8),
                new[] { "melee", "ranged", "elite" },
                "boss",
                includeCamp);
        }

        private static FloorRoom FirstRoomWithDoors(FloorLayout layout)
        {
            for (var index = 0; index < layout.Rooms.Count; index++)
            {
                if (layout.Rooms[index].Doors.Count > 0)
                {
                    return layout.Rooms[index];
                }
            }

            Assert.Fail("Expected at least one room with doors.");
            return null;
        }

        private static FloorRoom FindRoom(FloorLayout layout, int roomId)
        {
            for (var index = 0; index < layout.Rooms.Count; index++)
            {
                if (layout.Rooms[index].Id == roomId)
                {
                    return layout.Rooms[index];
                }
            }

            Assert.Fail("Missing room " + roomId);
            return null;
        }

        private static FloorRoom FindRoomConnectedToExit(FloorLayout layout)
        {
            for (var roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
            {
                var room = layout.Rooms[roomIndex];
                if (room.Id == layout.ExitRoomId)
                {
                    continue;
                }

                for (var doorIndex = 0; doorIndex < room.Doors.Count; doorIndex++)
                {
                    if (room.Doors[doorIndex].ConnectedRoomId == layout.ExitRoomId)
                    {
                        return room;
                    }
                }
            }

            Assert.Fail("Missing room connected to exit.");
            return null;
        }

        private static PortalDef FirstPortalWithLock(PortalOffer offer, PortalLockReason reason)
        {
            for (var index = 0; index < offer.Portals.Count; index++)
            {
                if (offer.Portals[index].LockReason == reason)
                {
                    return offer.Portals[index];
                }
            }

            Assert.Fail("Missing portal with lock reason " + reason);
            return null;
        }

        private static string PortalSignature(PortalOffer offer)
        {
            var parts = new List<string>();
            for (var index = 0; index < offer.Portals.Count; index++)
            {
                parts.Add(PortalSignature(offer.Portals[index]));
            }

            return string.Join("|", parts);
        }

        private static string PortalSignature(PortalDef portal)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}:{8}:{9}:{10}:{11}:{12}",
                portal.FromRoomId,
                portal.DoorIndex,
                portal.EntranceDir,
                portal.ExitDir,
                portal.ToRoomId,
                portal.ToRoomKind,
                portal.ToBiome,
                portal.ToDepth,
                portal.RewardType,
                portal.RewardMagnitude,
                portal.RiskTags,
                portal.LockReason,
                portal.RerollAllowed);
        }

        private static FloorLayout DepthGatedLayout()
        {
            var doorToDeepRoom = Create<FloorDoor>(0, 1, new GridPos(7, 4), FloorDoorSide.East);
            var returnDoor = Create<FloorDoor>(1, 0, new GridPos(0, 4), FloorDoorSide.West);
            var fromRoom = CreateRoom(0, 0, RoomKind.Normal, new[] { doorToDeepRoom });
            var deepRoom = CreateRoom(1, 3, RoomKind.Normal, new[] { returnDoor });
            var edge = Create<FloorEdge>(0, 1, doorToDeepRoom, returnDoor);

            return Create<FloorLayout>(
                404040,
                false,
                new[] { fromRoom, deepRoom },
                new[] { edge },
                0,
                1,
                BiomeTheme.For(BiomeId.Forest));
        }

        private static FloorRoom CreateRoom(int id, int depth, RoomKind kind, IReadOnlyList<FloorDoor> doors)
        {
            return Create<FloorRoom>(
                id,
                depth,
                new GridMap(8, 8),
                doors,
                CreateEncounter(),
                id == 0,
                false,
                false,
                kind);
        }

        private static FloorEncounter CreateEncounter()
        {
            var slot = Create<FloorEnemySlot>(0, "melee");
            return Create<FloorEncounter>(false, 1, new[] { slot });
        }

        private static T Create<T>(params object[] args)
        {
            return (T)Activator.CreateInstance(
                typeof(T),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                args,
                CultureInfo.InvariantCulture);
        }
    }
}
