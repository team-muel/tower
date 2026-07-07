using System;
using System.Collections.Generic;
using Tower.Core;

namespace Tower.Gen
{
    // T25: deterministic door -> PortalDef assignment. Called when a room's
    // doors open. Same (layout seed, room) always yields the same offer, so no
    // RNG is exposed and clicking a door never re-rolls its preview.
    //
    // Priority (reduced Hades concept): ForceNext > Linked > EligiblePool.
    //   ForceNext    - the from-room is the last combat room; the door forces
    //                  the floor exit / boss.
    //   Linked       - a real FloorEdge connects this door to a generated room;
    //                  the candidate mirrors that room.
    //   EligiblePool - no concrete link; fabricate a deterministic candidate
    //                  from the eligible reward pool.
    //
    // Repetition suppression: within one room's offer, the same reward type is
    // not handed out on two consecutive doors when an alternative exists.
    public static class PortalAssigner
    {
        // How much deeper than the current room a candidate may reach before it
        // is depth-gated (v0 keeps travel to the next depth band).
        private const int MaxDepthReach = 1;

        // v0 eligible reward pool, ordered so suppression walks a stable ring.
        private static readonly RewardType[] EligiblePool =
        {
            RewardType.Heal,
            RewardType.Resource,
            RewardType.Ability,
            RewardType.Shortcut
        };

        public static PortalOffer AssignForRoom(FloorLayout layout, FloorRoom room)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (room.Doors.Count == 0)
            {
                return PortalOffer.Empty(room.Id);
            }

            BiomeId biome = layout.BiomeTheme.Id;
            List<PortalDef> portals = new List<PortalDef>(room.Doors.Count);
            RewardType previousReward = RewardType.None;

            for (int doorIndex = 0; doorIndex < room.Doors.Count; doorIndex++)
            {
                FloorDoor door = room.Doors[doorIndex];
                PortalDef portal = AssignForDoor(layout, room, door, doorIndex, biome, ref previousReward);
                portals.Add(portal);
            }

            return new PortalOffer(room.Id, portals);
        }

        public static PortalDef AssignForDoor(
            FloorLayout layout,
            FloorRoom room,
            FloorDoor door,
            int doorIndex)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (door == null)
            {
                throw new ArgumentNullException(nameof(door));
            }

            RewardType ignored = RewardType.None;
            return AssignForDoor(layout, room, door, doorIndex, layout.BiomeTheme.Id, ref ignored);
        }

        private static PortalDef AssignForDoor(
            FloorLayout layout,
            FloorRoom room,
            FloorDoor door,
            int doorIndex,
            BiomeId biome,
            ref RewardType previousReward)
        {
            uint hash = Hash(layout.Seed, room.Id, room.Depth, doorIndex, (int)biome);

            // --- toRoomCandidate via priority chain ---
            FloorRoom linked = FindRoom(layout, door.ConnectedRoomId);
            bool forceNext = IsForceNext(layout, room);

            int toRoomId;
            RoomKind toKind;
            int toDepth;
            if (forceNext)
            {
                FloorRoom exit = FindRoom(layout, layout.ExitRoomId);
                toRoomId = layout.ExitRoomId;
                toKind = exit != null ? exit.Kind : RoomKind.Exit;
                toDepth = exit != null ? exit.Depth : room.Depth + 1;
            }
            else if (linked != null)
            {
                toRoomId = linked.Id;
                toKind = linked.Kind;
                toDepth = linked.Depth;
            }
            else
            {
                // EligiblePool fallback: fabricate a deterministic candidate.
                toRoomId = -1;
                toKind = RoomKind.Normal;
                toDepth = room.Depth + 1;
            }

            // --- rewardCandidate (type + magnitude) ---
            RewardType reward = PickReward(hash, toKind, previousReward);
            int magnitude = RewardMagnitude(reward, toDepth, hash);
            previousReward = reward;

            // --- risk tags ---
            PortalRisk risk = BuildRisk(layout, linked, toKind, reward);

            // --- lock reason ---
            PortalLockReason lockReason = BuildLockReason(room, toKind, toDepth, reward);

            // --- reroll policy ---
            bool rerollAllowed = RerollAllowed(lockReason, reward);

            return new PortalDef(
                room.Id,
                doorIndex,
                door.Side,
                door.Side,
                toRoomId,
                toKind,
                biome,
                toDepth,
                reward,
                magnitude,
                risk,
                lockReason,
                rerollAllowed);
        }

        private static bool IsForceNext(FloorLayout layout, FloorRoom room)
        {
            // The room is force-linked to the exit when one of its doors leads
            // straight to the floor exit and the exit is a boss/exit payoff.
            if (room.Id == layout.ExitRoomId)
            {
                return false;
            }

            for (int i = 0; i < room.Doors.Count; i++)
            {
                if (room.Doors[i].ConnectedRoomId == layout.ExitRoomId)
                {
                    FloorRoom exit = FindRoom(layout, layout.ExitRoomId);
                    return exit != null && (exit.IsBossRoom || exit.Kind == RoomKind.Boss);
                }
            }

            return false;
        }

        private static RewardType PickReward(uint hash, RoomKind toKind, RewardType previousReward)
        {
            // Boss/exit destinations always dangle the Shortcut payoff.
            if (toKind == RoomKind.Boss || toKind == RoomKind.Exit)
            {
                return RewardType.Shortcut;
            }

            // Camps are the safe restorative branch.
            if (toKind == RoomKind.Camp)
            {
                return RewardType.Heal;
            }

            int start = (int)(hash % (uint)EligiblePool.Length);
            for (int step = 0; step < EligiblePool.Length; step++)
            {
                RewardType candidate = EligiblePool[(start + step) % EligiblePool.Length];

                // Repetition suppression: skip a reward that repeats the
                // previous door when another option exists.
                if (candidate == previousReward)
                {
                    continue;
                }

                return candidate;
            }

            // All options collapsed onto previousReward (single-item pool).
            return EligiblePool[start];
        }

        private static int RewardMagnitude(RewardType reward, int toDepth, uint hash)
        {
            switch (reward)
            {
                case RewardType.None:
                    return 0;
                case RewardType.Heal:
                    return 10 + (toDepth * 5);
                case RewardType.Resource:
                    return 5 + (int)(hash % 6u) + toDepth;
                case RewardType.Ability:
                    return 1;
                case RewardType.Shortcut:
                    return 1;
                default:
                    return 0;
            }
        }

        private static PortalRisk BuildRisk(FloorLayout layout, FloorRoom linked, RoomKind toKind, RewardType reward)
        {
            PortalRisk risk = PortalRisk.None;

            if (toKind == RoomKind.Boss)
            {
                risk |= PortalRisk.Boss;
            }

            if (linked != null && linked.Encounter != null)
            {
                if (linked.Encounter.IsBoss)
                {
                    risk |= PortalRisk.Boss;
                }
                else if (linked.Encounter.EnemyCount >= 3)
                {
                    risk |= PortalRisk.Elite;
                }
            }

            // Boss floors add a hazard flavour to their exit approach.
            if (layout.IsBossFloor && toKind == RoomKind.Exit)
            {
                risk |= PortalRisk.Hazard;
            }

            // A shortcut behind a dangerous room is high stakes.
            if (reward == RewardType.Shortcut && (risk & (PortalRisk.Boss | PortalRisk.Elite)) != PortalRisk.None)
            {
                risk |= PortalRisk.HighStakes;
            }

            return risk;
        }

        private static PortalLockReason BuildLockReason(FloorRoom room, RoomKind toKind, int toDepth, RewardType reward)
        {
            // Boss payoffs are boss-gated.
            if (toKind == RoomKind.Boss)
            {
                return PortalLockReason.BossGated;
            }

            // Travelling more than one depth band ahead is depth-gated.
            if (toDepth - room.Depth > MaxDepthReach)
            {
                return PortalLockReason.DepthGated;
            }

            // Ability rewards require a key to claim in v0.
            if (reward == RewardType.Ability)
            {
                return PortalLockReason.RequiresKey;
            }

            return PortalLockReason.None;
        }

        private static bool RerollAllowed(PortalLockReason lockReason, RewardType reward)
        {
            // Only unlocked, low-stakes restorative/resource offers may reroll.
            if (lockReason != PortalLockReason.None)
            {
                return false;
            }

            return reward == RewardType.Heal || reward == RewardType.Resource;
        }

        private static FloorRoom FindRoom(FloorLayout layout, int roomId)
        {
            if (roomId < 0)
            {
                return null;
            }

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (layout.Rooms[i].Id == roomId)
                {
                    return layout.Rooms[i];
                }
            }

            return null;
        }

        // Deterministic FNV-1a style mix. Kept private so no RNG surface leaks
        // from Tower.Gen (T25 hard constraint: seed-based, RNG not exposed).
        private static uint Hash(int seed, int roomId, int depth, int doorIndex, int biome)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)seed) * 16777619u;
                hash = (hash ^ (uint)roomId) * 16777619u;
                hash = (hash ^ (uint)depth) * 16777619u;
                hash = (hash ^ (uint)doorIndex) * 16777619u;
                hash = (hash ^ (uint)biome) * 16777619u;
                return hash;
            }
        }
    }
}
