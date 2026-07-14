using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using Tower.Floor;
using Tower.Gen;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class ForestFloorRunEncounterTests
    {
        [Test]
        public void Rebuild_DefaultPreviewSelectsFirstRunEventAndMatchingNode()
        {
            GameObject host = new GameObject("forest-run-event");
            try
            {
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                renderer.Rebuild();

                Assert.That(renderer.ScheduledRunEvent, Is.Not.Null);
                Assert.That(renderer.ScheduledRunEvent.Kind, Is.EqualTo(RunEventKind.Encounter));
                Assert.That(renderer.EncounterNodeId, Is.GreaterThanOrEqualTo(0));
                Assert.That(renderer.Graph.NodeById(renderer.EncounterNodeId).IsEntrance, Is.False);
                Assert.That(host.GetComponentInChildren<ForestPlayerController>(), Is.Not.Null);
                Assert.That(renderer.CameraTransform, Is.Not.Null);
                Assert.That(host.GetComponentsInChildren<MeshCollider>()
                    .Any(collider => collider.name.StartsWith("ForkTrail_")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Rebuild_UnscheduledFloorDoesNotActivateComposerEncounter()
        {
            GameObject host = new GameObject("forest-no-run-event");
            try
            {
                RunEventPlan plan = RunEventPlan.Create(777);
                int unscheduledFloor = Enumerable.Range(1, 9)
                    .First(floor => plan.Slots.All(slot => slot.FloorNumber != floor));
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                SetPrivateField(renderer, "runFloorNumber", unscheduledFloor);

                renderer.Rebuild();

                Assert.That(renderer.ScheduledRunEvent, Is.Null);
                Assert.That(renderer.EncounterNodeId, Is.EqualTo(-1));
                Assert.That(renderer.IsEncounterBlocking, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Rebuild_FloorTenSelectsGeneratedBossNode()
        {
            GameObject host = new GameObject("forest-boss-run-event");
            try
            {
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                SetPrivateField(renderer, "runFloorNumber", RunEventPlan.FloorCount);

                renderer.Rebuild();

                Assert.That(renderer.ScheduledRunEvent.Kind, Is.EqualTo(RunEventKind.Boss));
                FloorNode node = renderer.Graph.NodeById(renderer.EncounterNodeId);
                Assert.That(node.IsBossRoom, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExitProximity_StartsVisibleForkTraversalWithoutPhysicsCallback()
        {
            GameObject host = new GameObject("forest-fork-proximity");
            try
            {
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                renderer.Rebuild();
                var layout = new LinearStubLayout(renderer.Graph);
                renderer.PlayerTransform.position = layout.GetField(renderer.CurrentNodeId).ExitPoint;

                Assert.That(renderer.TryEnterNearestForkAtExit(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EncounterOutcome_CompletesProgressGrantsOnceAndPresentsResult()
        {
            GameObject host = new GameObject("forest-encounter-outcome");
            EncounterRewardProfile rewards = EncounterRewardProfile.CreateRuntime(
                RewardType.Resource,
                1,
                "Run resource",
                RewardType.Ability,
                1,
                "Ability draft");
            try
            {
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                SetPrivateField(renderer, "encounterRewardProfile", rewards);
                renderer.Rebuild();
                var combatResult = new GeneratedEncounterResult(
                    renderer.ScheduledRunEvent.EventId,
                    CombatTeam.Player,
                    4,
                    2.8f);

                InvokeOutcome(renderer, combatResult);
                InvokeOutcome(renderer, combatResult);

                Assert.That(renderer.RunEventProgress.CompletedCount, Is.EqualTo(1));
                Assert.That(renderer.RewardInventory.ClaimCount, Is.EqualTo(1));
                Assert.That(renderer.RewardInventory.AmountOf(RewardType.Resource), Is.EqualTo(1));
                Assert.That(renderer.ResultPresenter, Is.Not.Null);
                Assert.That(renderer.ResultPresenter.IsVisible, Is.True);
                Assert.That(renderer.ResultPresenter.Headline, Does.Contain("1/"));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(rewards);
            }
        }

        [Test]
        public void RunLifecycle_AdvancesThroughEveryEventFloorToConquest()
        {
            GameObject host = new GameObject("forest-run-conquest");
            EncounterRewardProfile rewards = EncounterRewardProfile.CreateRuntime(
                RewardType.Resource,
                1,
                "Run resource",
                RewardType.Ability,
                1,
                "Ability draft");
            try
            {
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                SetPrivateField(renderer, "encounterRewardProfile", rewards);
                renderer.Rebuild();

                int totalEvents = renderer.RunLifecycle.Progress.Plan.Slots.Count;
                for (int index = 0; index < totalEvents; index++)
                {
                    RunEventSlot scheduled = renderer.ScheduledRunEvent;
                    Assert.That(scheduled, Is.Not.Null, $"missing scheduled event at index {index}");
                    Assert.That(renderer.RunLifecycle.FloorNumber, Is.EqualTo(scheduled.FloorNumber));
                    InvokeOutcome(renderer, new GeneratedEncounterResult(
                        scheduled.EventId, CombatTeam.Player, 3, 2.0f));

                    Result<RunOutcome> advanced = renderer.AdvanceRunFloor();
                    Assert.That(advanced.IsSuccess, Is.True,
                        advanced.IsFailure ? advanced.Error : string.Empty);
                    Assert.That(advanced.Value, Is.EqualTo(index == totalEvents - 1
                        ? RunOutcome.Conquered
                        : RunOutcome.FloorAdvanced));
                }

                Assert.That(renderer.RunLifecycle.IsConquered, Is.True);
                Assert.That(renderer.RewardInventory.ClaimCount, Is.EqualTo(totalEvents));
                Assert.That(renderer.RewardInventory.AmountOf(RewardType.Ability), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(rewards);
            }
        }

        [Test]
        public void RunLifecycle_DefeatRetreatsAndThirdDefeatRegresses()
        {
            GameObject host = new GameObject("forest-run-defeat");
            EncounterRewardProfile rewards = EncounterRewardProfile.CreateRuntime(
                RewardType.Resource,
                1,
                "Run resource",
                RewardType.Ability,
                1,
                "Ability draft");
            try
            {
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                SetPrivateField(renderer, "encounterRewardProfile", rewards);
                renderer.Rebuild();

                RunEventSlot first = renderer.ScheduledRunEvent;
                InvokeOutcome(renderer, new GeneratedEncounterResult(
                    first.EventId, CombatTeam.Player, 3, 2.0f));
                Assert.That(renderer.RunEventProgress.CompletedCount, Is.EqualTo(1));

                InvokeDefeat(renderer, first.EventId);
                Assert.That(renderer.RunLifecycle.RetreatCount, Is.EqualTo(1));
                Assert.That(renderer.RunEventProgress.CompletedCount, Is.Zero);
                Assert.That(renderer.RewardInventory.ClaimCount, Is.Zero);
                Assert.That(renderer.ScheduledRunEvent, Is.Not.Null);
                Assert.That(renderer.ScheduledRunEvent.EventId, Is.EqualTo(first.EventId));
                Assert.That(renderer.RunLifecycle.FloorNumber, Is.EqualTo(first.FloorNumber));

                InvokeDefeat(renderer, first.EventId);
                Assert.That(renderer.RunLifecycle.RetreatCount, Is.EqualTo(2));

                InvokeDefeat(renderer, first.EventId);
                Assert.That(renderer.RunLifecycle.RetreatCount, Is.Zero,
                    "third retreat is the great regression and resets the count");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(rewards);
            }
        }

        private static void InvokeDefeat(ForestFloorRenderer renderer, string eventId)
        {
            MethodInfo method = typeof(ForestFloorRenderer).GetMethod(
                "OnEncounterDefeated",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(renderer, new object[] { eventId });
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokeOutcome(
            ForestFloorRenderer renderer,
            GeneratedEncounterResult result)
        {
            MethodInfo method = typeof(ForestFloorRenderer).GetMethod(
                "OnEncounterResolved",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(renderer, new object[] { result });
        }
    }
}
