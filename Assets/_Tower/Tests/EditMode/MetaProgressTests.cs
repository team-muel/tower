using System.IO;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class MetaProgressTests
    {
        [Test]
        public void Conquest_GrantsPlatinumAndRecordsTheShortcut()
        {
            var meta = MetaProgress.Restore(null).Value;

            Result<int> paid = meta.RecordConquest(0);

            Assert.That(paid.IsSuccess, Is.True);
            Assert.That(meta.Platinum, Is.EqualTo(MetaProgress.PlatinumPerConquest));
            Assert.That(meta.ConquestCount, Is.EqualTo(1));
            Assert.That(meta.HasShortcut(0), Is.True);
            Assert.That(meta.RecordConquest(-1).IsFailure, Is.True);
        }

        [Test]
        public void SlotUnlock_RequiresPlatinumAndCapsAtMaxSlots()
        {
            var meta = MetaProgress.Restore(null).Value;
            Assert.That(meta.PurchaseSlotUnlock(1).IsFailure, Is.True, "no platinum yet");

            for (int conquest = 0; conquest < MetaProgress.SlotUnlockCost; conquest++)
            {
                meta.RecordConquest(conquest);
            }

            Result<int> bought = meta.PurchaseSlotUnlock(1);
            Assert.That(bought.IsSuccess, Is.True);
            Assert.That(meta.Platinum, Is.Zero);
            Assert.That(meta.SlotUnlocksFor(1), Is.EqualTo(1));

            for (int conquest = 0; conquest < MetaProgress.SlotUnlockCost * 3; conquest++)
            {
                meta.RecordConquest(100 + conquest);
            }

            Assert.That(meta.PurchaseSlotUnlock(1).IsSuccess, Is.True);
            Assert.That(meta.SlotUnlocksFor(1), Is.EqualTo(MetaProgress.MaxSlotUnlocksPerFaction));
            Assert.That(meta.PurchaseSlotUnlock(1).IsFailure, Is.True, "capped at max slots");
        }

        [Test]
        public void SlotCountFor_AppliesFactionUnlocksWithinLoadoutBounds()
        {
            var meta = MetaProgress.Restore(null).Value;
            AbilityDef ability = AbilityDef.CreateRuntime(
                "meta-hit", AbilityTag.Apply, 3, 2, AbilityTargetType.Enemy);
            CharacterDef fighter = CharacterDef.CreateRuntime(
                "meta-fighter", "Fighter", 20, 4, 2, 10,
                DispositionType.Aggressive, new[] { ability, ability }, factionId: 2);
            try
            {
                Assert.That(meta.SlotCountFor(fighter), Is.EqualTo(2));

                for (int conquest = 0; conquest < MetaProgress.SlotUnlockCost; conquest++)
                {
                    meta.RecordConquest(conquest);
                }

                meta.PurchaseSlotUnlock(2);
                Assert.That(meta.SlotCountFor(fighter), Is.EqualTo(3));
                Assert.That(meta.SlotCountFor(null), Is.EqualTo(AbilityLoadout.DefaultSlots));
            }
            finally
            {
                Object.DestroyImmediate(ability);
                Object.DestroyImmediate(fighter);
            }
        }

        [Test]
        public void CaptureRestore_RoundTripsThroughTheRepository()
        {
            string path = Path.Combine(Path.GetTempPath(), "tower-t61-meta-test.json");
            MetaProgressRepository repository = MetaProgressRepository.Create(path).Value;
            try
            {
                var meta = MetaProgress.Restore(null).Value;
                for (int conquest = 0; conquest < MetaProgress.SlotUnlockCost + 1; conquest++)
                {
                    meta.RecordConquest(conquest);
                }

                meta.PurchaseSlotUnlock(3);

                Assert.That(repository.Save(meta.Capture()).IsSuccess, Is.True);
                Result<MetaProgressSnapshot> loaded = repository.Load();
                Assert.That(loaded.IsSuccess, Is.True);
                Result<MetaProgress> restored = MetaProgress.Restore(loaded.Value);
                Assert.That(restored.IsSuccess, Is.True);
                Assert.That(restored.Value.Platinum, Is.EqualTo(1));
                Assert.That(restored.Value.ConquestCount, Is.EqualTo(MetaProgress.SlotUnlockCost + 1));
                Assert.That(restored.Value.SlotUnlocksFor(3), Is.EqualTo(1));
                Assert.That(restored.Value.HasShortcut(0), Is.True);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void Restore_RejectsCorruptSnapshots()
        {
            Assert.That(MetaProgress.Restore(new MetaProgressSnapshot { platinum = -1 }).IsFailure, Is.True);
            Assert.That(MetaProgress.Restore(new MetaProgressSnapshot
            {
                factionUnlocks = new[]
                {
                    new MetaFactionUnlockSnapshot { factionId = 1, slotUnlocks = 99 }
                }
            }).IsFailure, Is.True);
        }
    }
}
