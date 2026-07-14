using System.Collections.Generic;
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
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null) Object.DestroyImmediate(createdObjects[index]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void ConfigureActivateCombatVictory_SpawnsHpViewsAndAutomaticallyUnlocks()
        {
            GameObject root = Track(new GameObject("GeneratedEncounterTest"));
            GameObject player = Track(new GameObject("Player"));
            ForestPlayerController movement = player.AddComponent<ForestPlayerController>();
            CharacterDef playerDefinition = Character(
                "player",
                100,
                10,
                2,
                20,
                Ability("player-strike", 30, 5f, 0f));
            CharacterDef enemyDefinition = Character(
                "pillbug",
                12,
                0,
                0,
                5,
                Ability("enemy-strike", 1, 2f, 1f));
            EnemyCombatProfile[] profiles =
            {
                Profile("melee", enemyDefinition),
                Profile("ranged", enemyDefinition)
            };
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
            GeneratedEncounterResult outcome = null;
            var host = root.AddComponent<GeneratedFloorEncounterHost>();

            Result configured = host.Configure(
                player.transform,
                movement,
                playerDefinition,
                new CompanionEntity[0],
                profiles,
                encounter,
                runEvent,
                Vector3.forward,
                result =>
                {
                    outcome = result;
                    unlocked = true;
                },
                7f,
                0.45f);

            Assert.That(configured.IsSuccess, Is.True, configured.Error);
            Assert.That(host.EnemyCount, Is.EqualTo(encounter.EnemyCount));
            Assert.That(host.Views.Count, Is.EqualTo(1 + encounter.EnemyCount));
            Assert.That(host.Views, Has.All.Matches<CombatantWorldView>(view => view.FillRatio == 1f));

            host.Tick(0f);
            Assert.That(movement.enabled, Is.False);
            host.Tick(0.45f);
            Assert.That(host.IsCombatActive, Is.True);
            Assert.That(movement.enabled, Is.True);

            for (int tick = 0; tick < 120 && !host.IsResolved; tick++)
            {
                host.Tick(0.1f);
            }

            Assert.That(host.IsResolved, Is.True);
            Assert.That(host.IsPlayerDefeated, Is.False);
            Assert.That(host.CombatState.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(host.Metrics.ActionCount, Is.GreaterThan(0));
            Assert.That(unlocked, Is.True);
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.EventId, Is.EqualTo(runEvent.EventId));
            Assert.That(outcome.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(outcome.ActionCount, Is.EqualTo(host.Metrics.ActionCount));
            Assert.That(outcome.DurationSeconds, Is.EqualTo(host.CombatState.ElapsedSeconds));
            Assert.That(host.EnemyCount, Is.Zero);
        }

        [Test]
        public void Configure_RejectsMissingEnemyKindProfile()
        {
            GameObject root = Track(new GameObject("GeneratedEncounterProfileTest"));
            GameObject player = Track(new GameObject("Player"));
            ForestPlayerController movement = player.AddComponent<ForestPlayerController>();
            CharacterDef playerDefinition = Character("player", 20, 2, 1, 10, Ability("strike", 5, 2f, 0f));
            CharacterDef enemyDefinition = Character("pillbug", 10, 1, 0, 5, Ability("claw", 2, 2f, 0f));
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
            var host = root.AddComponent<GeneratedFloorEncounterHost>();

            Result configured = host.Configure(
                player.transform,
                movement,
                playerDefinition,
                new CompanionEntity[0],
                new[] { Profile("melee", enemyDefinition) },
                encounter,
                runEvent,
                Vector3.zero,
                _ => { });

            Assert.That(configured.IsFailure, Is.True);
            Assert.That(configured.Error, Does.Contain("ranged"));
        }

        [Test]
        public void Configure_RejectsBossKindMismatch()
        {
            GameObject root = Track(new GameObject("GeneratedEncounterMismatchTest"));
            GameObject player = Track(new GameObject("Player"));
            ForestPlayerController movement = player.AddComponent<ForestPlayerController>();
            CharacterDef playerDefinition = Character("player", 20, 2, 1, 10, Ability("strike", 5, 2f, 0f));
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
                playerDefinition,
                new CompanionEntity[0],
                new EnemyCombatProfile[0],
                encounter,
                ordinaryEvent,
                Vector3.zero,
                _ => { }).IsFailure, Is.True);
        }

        private AbilityDef Ability(string id, int power, float range, float cooldown)
        {
            return Track(AbilityDef.CreateRuntime(
                id,
                AbilityTag.None,
                power,
                (int)range,
                AbilityTargetType.Enemy,
                cooldownSeconds: cooldown));
        }

        private CharacterDef Character(
            string id,
            int maxHp,
            int attack,
            int defense,
            int speed,
            AbilityDef ability)
        {
            return Track(CharacterDef.CreateRuntime(
                id,
                id,
                maxHp,
                attack,
                defense,
                speed,
                DispositionType.Aggressive,
                new[] { ability }));
        }

        private EnemyCombatProfile Profile(string kindSlot, CharacterDef definition)
        {
            return Track(EnemyCombatProfile.CreateRuntime(
                kindSlot,
                definition,
                PrimitiveType.Sphere,
                Color.red,
                Vector3.one));
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
