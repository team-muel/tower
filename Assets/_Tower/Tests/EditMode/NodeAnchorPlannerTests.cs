using System.Text;
using NUnit.Framework;
using Tower.Core;
using Tower.Floor;
using Tower.Gen;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // T41 (M4): deterministic interaction-anchor placement (pure logic).
    public sealed class NodeAnchorPlannerTests
    {
        private static FloorFieldRect Field(int id)
        {
            return new FloorFieldRect(id, new Vector3(0f, 0f, id * 40f), 26f, 15f);
        }

        private static ForestClearing Clearing(int id)
        {
            return new ForestClearing(new Vector3(0f, 0f, id * 40f), 4f);
        }

        private static FloorNode Node(int id, RoomKind kind, bool entrance = false, bool exit = false, bool boss = false)
        {
            return new FloorNode(id, id, kind, "tmpl", entrance, exit, boss);
        }

        private static bool Contains(NodeAnchorPlan plan, InteractableKind kind)
        {
            foreach (PlacedAnchor a in plan.Anchors)
            {
                if (a.Def.Kind == kind) return true;
            }

            return false;
        }

        private static string Signature(NodeAnchorPlan plan)
        {
            StringBuilder sb = new StringBuilder();
            foreach (PlacedAnchor a in plan.Anchors)
            {
                sb.Append(a.Def.Kind).Append('|');
            }

            return sb.ToString();
        }

        [Test]
        public void SameSeedAndNodeProducesIdenticalPlan()
        {
            FloorNode node = Node(3, RoomKind.Normal);
            NodeAnchorPlan a = NodeAnchorPlanner.Build(999, node, Field(3), Clearing(3));
            NodeAnchorPlan b = NodeAnchorPlanner.Build(999, node, Field(3), Clearing(3));

            Assert.AreEqual(a.Anchors.Count, b.Anchors.Count);
            for (int i = 0; i < a.Anchors.Count; i++)
            {
                Assert.AreEqual(a.Anchors[i].Def.Id, b.Anchors[i].Def.Id);
                Assert.AreEqual(a.Anchors[i].Def.Kind, b.Anchors[i].Def.Kind);
                Assert.AreEqual(a.Anchors[i].Position, b.Anchors[i].Position);
            }
        }

        [Test]
        public void RoleMappingContracts()
        {
            Assert.IsTrue(Contains(NodeAnchorPlanner.Build(1, Node(0, RoomKind.Entrance, entrance: true), Field(0), Clearing(0)),
                InteractableKind.Inspect), "Entrance must offer an Inspect anchor.");

            NodeAnchorPlan camp = NodeAnchorPlanner.Build(1, Node(2, RoomKind.Camp), Field(2), Clearing(2));
            Assert.IsTrue(Contains(camp, InteractableKind.Shrine), "Camp must offer an 오브 Shrine.");
            Assert.IsTrue(Contains(camp, InteractableKind.Grave), "Camp must offer a 묘비 Grave.");

            Assert.IsTrue(Contains(NodeAnchorPlanner.Build(1, Node(5, RoomKind.Boss, boss: true), Field(5), Clearing(5)),
                InteractableKind.Chest), "Boss must offer a reward Chest.");

            Assert.IsTrue(Contains(NodeAnchorPlanner.Build(1, Node(4, RoomKind.Exit, exit: true), Field(4), Clearing(4)),
                InteractableKind.Resource), "Exit must offer a Resource.");
        }

        [Test]
        public void EveryNormalNodeHasAtLeastOneAnchor()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                NodeAnchorPlan plan = NodeAnchorPlanner.Build(seed, Node(3, RoomKind.Normal), Field(3), Clearing(3));
                Assert.GreaterOrEqual(plan.Anchors.Count, 1, $"Seed {seed} produced no anchor.");
            }
        }

        [Test]
        public void AllAnchorsLieInsideFieldBounds()
        {
            FloorFieldRect field = Field(7);
            NodeAnchorPlan plan = NodeAnchorPlanner.Build(555, Node(7, RoomKind.Camp), field, Clearing(7));
            foreach (PlacedAnchor a in plan.Anchors)
            {
                Assert.IsTrue(field.ContainsXZ(a.Position.x, a.Position.z), $"Anchor {a.Def.Id} escaped the field.");
            }
        }

        [Test]
        public void AnchorIdsAreUniqueAndRegistrable()
        {
            NodeAnchorPlan plan = NodeAnchorPlanner.Build(321, Node(3, RoomKind.Normal), Field(3), Clearing(3));
            InteractableRegistry registry = new InteractableRegistry();
            foreach (PlacedAnchor a in plan.Anchors)
            {
                Result r = registry.Add(a.Def);
                Assert.IsTrue(r.IsSuccess, r.Error);
            }

            Assert.AreEqual(plan.Anchors.Count, registry.Entries.Count);
        }

        [Test]
        public void PlannerIsSeedSensitive()
        {
            FloorNode node = Node(3, RoomKind.Normal);
            string baseline = Signature(NodeAnchorPlanner.Build(1, node, Field(3), Clearing(3)));
            bool diverged = false;
            for (int seed = 2; seed <= 12 && !diverged; seed++)
            {
                if (Signature(NodeAnchorPlanner.Build(seed, node, Field(3), Clearing(3))) != baseline)
                {
                    diverged = true;
                }
            }

            Assert.IsTrue(diverged, "Anchor plan should vary with the seed.");
        }
    }
}
