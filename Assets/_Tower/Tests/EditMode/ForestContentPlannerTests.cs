using NUnit.Framework;
using Tower.Floor;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // T40b: deterministic forest content generation (pure, engine-agnostic logic).
    public sealed class ForestContentPlannerTests
    {
        private static FloorFieldRect Field(int id)
        {
            return new FloorFieldRect(id, new Vector3(0f, 0f, id * 40f), 26f, 15f);
        }

        [Test]
        public void SameSeedAndNodeProducesIdenticalPlan()
        {
            FloorFieldRect field = Field(2);
            ForestContentPlan a = ForestContentPlanner.Build(999, 2, field);
            ForestContentPlan b = ForestContentPlanner.Build(999, 2, field);

            Assert.AreEqual(a.Trees.Count, b.Trees.Count);
            Assert.AreEqual(a.Rocks.Count, b.Rocks.Count);
            Assert.AreEqual(a.PathWaypoints.Count, b.PathWaypoints.Count);
            for (int i = 0; i < a.Trees.Count; i++)
            {
                Assert.AreEqual(a.Trees[i].Position, b.Trees[i].Position);
                Assert.AreEqual(a.Trees[i].Height, b.Trees[i].Height, 0f);
                Assert.AreEqual(a.Trees[i].CanopyCount, b.Trees[i].CanopyCount);
            }

            Assert.AreEqual(a.Clearing.Center, b.Clearing.Center);
            Assert.AreEqual(a.Clearing.Radius, b.Clearing.Radius, 0f);
        }

        [Test]
        public void DifferentNodeIdsDiverge()
        {
            // Same field so any divergence comes purely from the node-id salt.
            FloorFieldRect field = Field(0);
            ForestContentPlan a = ForestContentPlanner.Build(999, 1, field);
            ForestContentPlan b = ForestContentPlanner.Build(999, 2, field);
            Assert.AreNotEqual(a.Clearing.Center, b.Clearing.Center);
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            ForestContentPlan a = ForestContentPlanner.Build(1, 3, Field(3));
            ForestContentPlan b = ForestContentPlanner.Build(2, 3, Field(3));
            Assert.AreNotEqual(a.Clearing.Center, b.Clearing.Center);
        }

        [Test]
        public void AllPropsLieInsideFieldBounds()
        {
            FloorFieldRect field = Field(4);
            ForestContentPlan plan = ForestContentPlanner.Build(555, 4, field);
            Assert.IsTrue(plan.Trees.Count > 0, "Expected trees to fill the field.");

            foreach (ForestProp t in plan.Trees)
            {
                Assert.IsTrue(field.ContainsXZ(t.Position.x, t.Position.z), "Tree escaped the field rect.");
            }

            foreach (ForestProp r in plan.Rocks)
            {
                Assert.IsTrue(field.ContainsXZ(r.Position.x, r.Position.z), "Rock escaped the field rect.");
            }
        }

        [Test]
        public void TreesAvoidTheClearing()
        {
            FloorFieldRect field = Field(6);
            ForestContentPlan plan = ForestContentPlanner.Build(321, 6, field);
            foreach (ForestProp t in plan.Trees)
            {
                Assert.IsFalse(plan.Clearing.Contains(t.Position.x, t.Position.z),
                    "A tree was placed inside the clearing.");
            }
        }

        [Test]
        public void PathRunsFromEntryToExitEdge()
        {
            FloorFieldRect field = Field(0);
            ForestContentPlan plan = ForestContentPlanner.Build(42, 0, field);
            Assert.GreaterOrEqual(plan.PathWaypoints.Count, 2);
            Vector3 first = plan.PathWaypoints[0];
            Vector3 last = plan.PathWaypoints[plan.PathWaypoints.Count - 1];
            Assert.Less(first.z, last.z, "Path should progress along the travel axis.");
        }

        [Test]
        public void ElongatedFieldIsLongerAlongTravelAxis()
        {
            FloorFieldRect field = Field(0);
            Assert.Less(field.Elongation, 1f, "Cross axis must be shorter than the travel axis.");
        }
    }
}
