using System;
using Tower.Core;

namespace Tower.Gen
{
    // T25: a door's determined next-room + reward preview. Promoted from the
    // T17 UI-only nextRoomPreview into engine data. Assigned when a room's
    // doors open (combat cleared / room entry) and never re-rolled on click:
    // the player sees the trade-off BEFORE choosing (Hades economy).
    //
    // Lives in Tower.Gen so it can reference RoomKind + BiomeId directly while
    // the reward/risk/lock vocabulary stays in the pure Tower.Core assembly.
    public sealed class PortalDef
    {
        public PortalDef(
            int fromRoomId,
            int doorIndex,
            FloorDoorSide entranceDir,
            FloorDoorSide exitDir,
            int toRoomId,
            RoomKind toRoomKind,
            BiomeId toBiome,
            int toDepth,
            RewardType rewardType,
            int rewardMagnitude,
            PortalRisk riskTags,
            PortalLockReason lockReason,
            bool rerollAllowed)
        {
            if (fromRoomId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromRoomId), "From-room id cannot be negative.");
            }

            if (doorIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(doorIndex), "Door index cannot be negative.");
            }

            if (toDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(toDepth), "Depth cannot be negative.");
            }

            if (rewardMagnitude < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rewardMagnitude), "Reward magnitude cannot be negative.");
            }

            FromRoomId = fromRoomId;
            DoorIndex = doorIndex;
            EntranceDir = entranceDir;
            ExitDir = exitDir;
            ToRoomId = toRoomId;
            ToRoomKind = toRoomKind;
            ToBiome = toBiome;
            ToDepth = toDepth;
            RewardType = rewardType;
            RewardMagnitude = rewardMagnitude;
            RiskTags = riskTags;
            LockReason = lockReason;
            RerollAllowed = rerollAllowed;
        }

        // Room whose doors are opening (owner of this portal).
        public int FromRoomId { get; }

        // Which door on the from-room this portal is bound to (0-based).
        public int DoorIndex { get; }

        // The side the player enters the next room through.
        public FloorDoorSide EntranceDir { get; }

        // The side of the from-room this door sits on.
        public FloorDoorSide ExitDir { get; }

        // --- toRoomCandidate ---

        // Candidate destination room id, or -1 when the destination is the
        // floor exit / not a concrete generated room.
        public int ToRoomId { get; }

        public RoomKind ToRoomKind { get; }

        public BiomeId ToBiome { get; }

        public int ToDepth { get; }

        // --- rewardCandidate (type + magnitude) ---

        public RewardType RewardType { get; }

        public int RewardMagnitude { get; }

        // --- preview metadata ---

        public PortalRisk RiskTags { get; }

        public PortalLockReason LockReason { get; }

        // Locked doors carry a lock reason other than None.
        public bool IsLocked => LockReason != PortalLockReason.None;

        // Whether the reward may be re-rolled (v0: only unlocked, non-boss
        // resource/heal offers).
        public bool RerollAllowed { get; }

        public bool HasRisk(PortalRisk risk)
        {
            return (RiskTags & risk) == risk && risk != PortalRisk.None;
        }
    }
}
