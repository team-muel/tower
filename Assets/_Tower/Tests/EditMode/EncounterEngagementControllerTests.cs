using System.Collections.Generic;
using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tower.Tests.EditMode
{
    public sealed class EncounterEngagementControllerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void Entry_HoldsPlayerThenEnablesEnemyBrainAndRestoresControl()
        {
            var player = Track(new GameObject("Player"));
            var enemy = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            enemy.transform.position = new Vector3(8f, 0f, 0f);
            var movement = player.AddComponent<TestMovementBehaviour>();
            var brain = enemy.AddComponent<PillbugBrain>();
            var host = Track(new GameObject("Encounter"));
            var controller = host.AddComponent<EncounterEngagementController>();

            var configured = controller.Configure(
                player.transform,
                enemy.transform,
                movement,
                brain,
                7f,
                0.4f);

            Assert.That(configured.IsSuccess, Is.True, configured.Error);
            Assert.That(brain.EngagementEnabled, Is.False);
            controller.Tick(0.1f);
            Assert.That(controller.Phase, Is.EqualTo(EncounterPhase.Exploring));
            Assert.That(movement.enabled, Is.True);

            enemy.transform.position = new Vector3(7f, 0f, 0f);
            controller.Tick(0f);
            Assert.That(controller.Phase, Is.EqualTo(EncounterPhase.IntroHold));
            Assert.That(movement.enabled, Is.False);
            Assert.That(brain.EngagementEnabled, Is.False);

            controller.Tick(0.4f);
            Assert.That(controller.Phase, Is.EqualTo(EncounterPhase.Active));
            Assert.That(movement.enabled, Is.True);
            Assert.That(brain.EngagementEnabled, Is.True);
        }

        [Test]
        public void Resolve_DisablesEnemyButNeverLeavesPlayerLocked()
        {
            var player = Track(new GameObject("Player"));
            var enemy = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            enemy.transform.position = Vector3.right;
            var movement = player.AddComponent<TestMovementBehaviour>();
            var brain = enemy.AddComponent<PillbugBrain>();
            var host = Track(new GameObject("Encounter"));
            var controller = host.AddComponent<EncounterEngagementController>();
            controller.Configure(player.transform, enemy.transform, movement, brain, 7f, 0.1f);
            controller.Tick(0f);
            controller.Tick(0.1f);

            var resolved = controller.ResolveEncounter();

            Assert.That(resolved.IsSuccess, Is.True, resolved.Error);
            Assert.That(controller.Phase, Is.EqualTo(EncounterPhase.Resolved));
            Assert.That(movement.enabled, Is.True);
            Assert.That(brain.EngagementEnabled, Is.False);
        }

        private T Track<T>(T createdObject) where T : Object
        {
            createdObjects.Add(createdObject);
            return createdObject;
        }

        private sealed class TestMovementBehaviour : MonoBehaviour
        {
        }
    }
}
