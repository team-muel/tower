using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    [Serializable]
    public sealed class MetaFactionUnlockSnapshot
    {
        public int factionId;
        public int slotUnlocks;
    }

    [Serializable]
    public sealed class MetaProgressSnapshot
    {
        public int platinum;
        public int conquestCount;
        public int[] shortcutStairways = new int[0];
        public MetaFactionUnlockSnapshot[] factionUnlocks = new MetaFactionUnlockSnapshot[0];
    }

    // T61 meta spine. Platinum pierces the great regression (2026-07-08
    // canon: 백금화 = 대회귀 관통 보존), so it persists in its own file apart
    // from the run save. The only bonus wired end-to-end in v0 is the slot
    // unlock — the one lever the owner already confirmed platinum buys
    // (2026-07-08; start 2 / min 1 / max 4 system canon). Stat/cooldown/HP
    // bonuses (13 §7) stay as named seams until their combat wiring lane.
    public sealed class MetaProgress
    {
        // Provisional pricing/grant data — economy detail remains owner-frozen;
        // these are deterministic placeholders, not canon numbers.
        public const int PlatinumPerConquest = 1;
        public const int SlotUnlockCost = 3;
        public const int MaxSlotUnlocksPerFaction = AbilityLoadout.MaxSlots - AbilityLoadout.DefaultSlots;

        private readonly Dictionary<int, int> factionSlotUnlocks = new Dictionary<int, int>();
        private readonly HashSet<int> shortcutStairways = new HashSet<int>();

        public int Platinum { get; private set; }
        public int ConquestCount { get; private set; }
        public IReadOnlyCollection<int> ShortcutStairways => shortcutStairways;

        public int SlotUnlocksFor(int factionId)
        {
            return factionSlotUnlocks.TryGetValue(factionId, out int unlocks) ? unlocks : 0;
        }

        // Slot count a character of this faction starts encounters with.
        public int SlotCountFor(CharacterDef definition)
        {
            if (definition == null)
            {
                return AbilityLoadout.DefaultSlots;
            }

            int baseline = Math.Max(
                AbilityLoadout.MinSlots,
                definition.DefaultAbilities?.Length ?? AbilityLoadout.DefaultSlots);
            return Math.Min(AbilityLoadout.MaxSlots, baseline + SlotUnlocksFor(definition.FactionId));
        }

        public bool HasShortcut(int stairwayIndex) => shortcutStairways.Contains(stairwayIndex);

        // Conquest: records the shortcut (재등반 압축 seam) and pays platinum.
        public Result<int> RecordConquest(int stairwayIndex)
        {
            if (stairwayIndex < 0)
            {
                return Result<int>.Failure("Stairway index cannot be negative.");
            }

            shortcutStairways.Add(stairwayIndex);
            ConquestCount++;
            Platinum += PlatinumPerConquest;
            return Result<int>.Success(Platinum);
        }

        public Result<int> PurchaseSlotUnlock(int factionId)
        {
            if (SlotUnlocksFor(factionId) >= MaxSlotUnlocksPerFaction)
            {
                return Result<int>.Failure("Faction already holds the maximum slot unlocks.");
            }

            if (Platinum < SlotUnlockCost)
            {
                return Result<int>.Failure(
                    $"Slot unlock costs {SlotUnlockCost} platinum; {Platinum} held.");
            }

            Platinum -= SlotUnlockCost;
            factionSlotUnlocks[factionId] = SlotUnlocksFor(factionId) + 1;
            return Result<int>.Success(Platinum);
        }

        public MetaProgressSnapshot Capture()
        {
            return new MetaProgressSnapshot
            {
                platinum = Platinum,
                conquestCount = ConquestCount,
                shortcutStairways = shortcutStairways.OrderBy(index => index).ToArray(),
                factionUnlocks = factionSlotUnlocks
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new MetaFactionUnlockSnapshot
                    {
                        factionId = pair.Key,
                        slotUnlocks = pair.Value
                    })
                    .ToArray()
            };
        }

        public static Result<MetaProgress> Restore(MetaProgressSnapshot snapshot)
        {
            var meta = new MetaProgress();
            if (snapshot == null)
            {
                return Result<MetaProgress>.Success(meta);
            }

            if (snapshot.platinum < 0 || snapshot.conquestCount < 0)
            {
                return Result<MetaProgress>.Failure("Meta progress counts cannot be negative.");
            }

            meta.Platinum = snapshot.platinum;
            meta.ConquestCount = snapshot.conquestCount;
            foreach (int stairway in snapshot.shortcutStairways ?? new int[0])
            {
                if (stairway < 0)
                {
                    return Result<MetaProgress>.Failure("Shortcut stairway indices cannot be negative.");
                }

                meta.shortcutStairways.Add(stairway);
            }

            foreach (MetaFactionUnlockSnapshot unlock in snapshot.factionUnlocks
                ?? new MetaFactionUnlockSnapshot[0])
            {
                if (unlock == null)
                {
                    continue;
                }

                if (unlock.slotUnlocks < 0 || unlock.slotUnlocks > MaxSlotUnlocksPerFaction)
                {
                    return Result<MetaProgress>.Failure("Faction slot unlocks are out of range.");
                }

                if (unlock.slotUnlocks > 0)
                {
                    meta.factionSlotUnlocks[unlock.factionId] = unlock.slotUnlocks;
                }
            }

            return Result<MetaProgress>.Success(meta);
        }
    }
}
