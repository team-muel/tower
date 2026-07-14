using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using Tower.Floor;
using Tower.Gen;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class GeneratedFloorEncounterHostTests
    {
        [Test]
        public void ConfigureActivateResolve_SpawnsCompositionAndInvokesUnlock()
        {
            GameObject root = new GameObject("GeneratedEncounterTest");
            GameObject player = new GameObject("Player");
            try
            {
                ForestPlayerController movement = player.AddComponent<ForestPlayerController>();
                FloorEncounter encounter = FloorEncounterComposer.Compose(
                    EncounterBudget.Default,
                    RoomKind.Normal,
                    77,
                    2,
                    2,
                    BiomeId.Forest,
                    new[] { "melee", "ranged" },
                    "boss");
                RunEventSlot runEvent = RunEventPlan.Create(77).Slots[0];
                bool unlocked = false;
                var host = root.AddComponent<GeneratedFloorEncounterHost>();

                Result configured = host.Configure(
                    player.transform,
                    movement,
                    encounter,
                    runEvent,
                    Vector3.forward,
                    _ => unlocked = true,
                    7f,
                    0.45f);

                Assert.That(configured.IsSuccess, Is.True, configured.Error);
                Assert.That(host.EnemyCount, Is.EqualTo(encounter.EnemyCount));
                Assert.That(host.IsCombatActive, Is.False);

                host.Tick(0f);
                Assert.That(movement.enabled, Is.False);
                host.Tick(0.45f);
                Assert.That(host.IsCombatActive, Is.True);
                Assert.That(movement.enabled, Is.True);

                Result resolved = host.ResolveEncounter();
                Assert.That(resolved.IsSuccess, Is.True, resolved.Error);
                Assert.That(host.IsResolved, Is.True);
                Assert.That(unlocked, Is.True);
                Assert.That(host.EnemyCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Configure_RejectsBossKindMismatch()
        {
            GameObject root = new GameObject("GeneratedEncounterMismatchTest");
            GameObject player = new GameObject("Player");
            try
            {
                ForestPlayerController movement = player.AddComponent<ForestPlayerController>();
                FloorEncounter encounter = FloorEncounterComposer.Compose(
                    EncounterBudget.Default,
                    RoomKind.Boss,
                    2,
                    3,
                    3,
                    BiomeId.Forest,
                    new[] { "melee" },
                    "boss");
                RunEventSlot ordinaryEvent = RunEventPlan.Create(2).Slots[0];
                var host = root.AddComponent<GeneratedFloorEncounterHost>();

                Assert.That(host.Configure(
                    player.transform,
                    movement,
                    encounter,
                    ordinaryEvent,
                    Vector3.zero,
                    _ => { }).IsFailure, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
            }
        }
    }
}
