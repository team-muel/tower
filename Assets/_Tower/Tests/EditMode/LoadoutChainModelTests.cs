using System.Collections.Generic;
using NUnit.Framework;
using Tower.UI;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // Covers the drag-and-drop reorder / chain->initiative derivation logic that
    // was factored out of LoadoutMenuController so it is testable without any
    // uGUI objects. The drag interaction itself is runtime uGUI and not
    // unit-tested here; the model that the drag feeds is.
    [TestFixture]
    public sealed class LoadoutChainModelTests
    {
        [Test]
        public void DeriveInitiative_MapsChainPositionsToAllyValues()
        {
            Assert.That(LoadoutChainModel.DeriveInitiative(0), Is.EqualTo(100));
            Assert.That(LoadoutChainModel.DeriveInitiative(1), Is.EqualTo(90));
            Assert.That(LoadoutChainModel.DeriveInitiative(2), Is.EqualTo(80));
            Assert.That(LoadoutChainModel.DeriveInitiative(3), Is.EqualTo(70));
        }

        [Test]
        public void DeriveInitiative_PastTable_UsesTail_AndNegativeIsZero()
        {
            Assert.That(LoadoutChainModel.DeriveInitiative(4), Is.EqualTo(LoadoutChainModel.TailInitiative));
            Assert.That(LoadoutChainModel.DeriveInitiative(9), Is.EqualTo(LoadoutChainModel.TailInitiative));
            Assert.That(LoadoutChainModel.DeriveInitiative(-1), Is.EqualTo(0));
        }

        [Test]
        public void Reorder_MovesMemberDown_WithoutMutatingInput()
        {
            var order = new List<string> { "a", "b", "c", "d" };
            var result = LoadoutChainModel.Reorder(order, 0, 2);

            Assert.That(result, Is.EqualTo(new[] { "b", "c", "a", "d" }));
            // Input list is untouched.
            Assert.That(order, Is.EqualTo(new[] { "a", "b", "c", "d" }));
        }

        [Test]
        public void Reorder_MovesMemberUp()
        {
            var order = new List<string> { "a", "b", "c", "d" };
            var result = LoadoutChainModel.Reorder(order, 3, 0);
            Assert.That(result, Is.EqualTo(new[] { "d", "a", "b", "c" }));
        }

        [Test]
        public void Reorder_ClampsTargetPastEnd_AndIgnoresBadSource()
        {
            var order = new List<string> { "a", "b", "c" };

            var clamped = LoadoutChainModel.Reorder(order, 0, 99);
            Assert.That(clamped, Is.EqualTo(new[] { "b", "c", "a" }));

            var badSource = LoadoutChainModel.Reorder(order, 7, 0);
            Assert.That(badSource, Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void BuildAssignments_AllUnlocked_NumbersEveryMember()
        {
            var order = new List<string> { "regressor", "ember", "ward", "glass" };
            var assignments = LoadoutChainModel.BuildAssignments(order, _ => false);

            Assert.That(assignments.Count, Is.EqualTo(4));
            Assert.That(assignments[0].ChainPosition, Is.EqualTo(0));
            Assert.That(assignments[0].Initiative, Is.EqualTo(100));
            Assert.That(assignments[3].ChainPosition, Is.EqualTo(3));
            Assert.That(assignments[3].Initiative, Is.EqualTo(70));
            Assert.That(assignments[2].ChainLocked, Is.False);
        }

        [Test]
        public void BuildAssignments_LockedMemberExcluded_DoesNotConsumeSlot()
        {
            var order = new List<string> { "a", "locked", "b", "c" };
            var assignments = LoadoutChainModel.BuildAssignments(order, id => id == "locked");

            Assert.That(assignments.Count, Is.EqualTo(4));

            // "a" is first unlocked -> slot 0.
            Assert.That(assignments[0].Id, Is.EqualTo("a"));
            Assert.That(assignments[0].ChainPosition, Is.EqualTo(0));
            Assert.That(assignments[0].Initiative, Is.EqualTo(100));

            // locked member: excluded, no slot, no initiative.
            Assert.That(assignments[1].Id, Is.EqualTo("locked"));
            Assert.That(assignments[1].ChainLocked, Is.True);
            Assert.That(assignments[1].ChainPosition, Is.EqualTo(-1));
            Assert.That(assignments[1].Initiative, Is.EqualTo(0));

            // "b" follows "a" without the locked member consuming a slot -> slot 1.
            Assert.That(assignments[2].Id, Is.EqualTo("b"));
            Assert.That(assignments[2].ChainPosition, Is.EqualTo(1));
            Assert.That(assignments[2].Initiative, Is.EqualTo(90));

            Assert.That(assignments[3].Id, Is.EqualTo("c"));
            Assert.That(assignments[3].ChainPosition, Is.EqualTo(2));
            Assert.That(assignments[3].Initiative, Is.EqualTo(80));
        }

        [Test]
        public void ChainOrder_ExcludesLockedMembers_PreservesOrder()
        {
            var order = new List<string> { "a", "locked", "b" };
            var chain = LoadoutChainModel.ChainOrder(order, id => id == "locked");
            Assert.That(chain, Is.EqualTo(new[] { "a", "b" }));
        }
    }

    // The save round-trip of a drag reorder goes through TowerSliceContent's
    // PlayerPrefs-backed chain, matching the interaction: reorder in the model,
    // persist, reload.
    [TestFixture]
    public sealed class LoadoutChainReorderSaveTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [Test]
        public void ReorderThenSave_RoundTripsThroughLoadoutChain()
        {
            var start = TowerSliceContent.GetLoadoutChain();
            Assert.That(start, Is.EqualTo(new[] { "regressor", "ember", "ward", "glass" }));

            // Drag "glass" (index 3) to the front (index 0).
            var reordered = LoadoutChainModel.Reorder(start, 3, 0);
            Assert.That(reordered, Is.EqualTo(new[] { "glass", "regressor", "ember", "ward" }));

            TowerSliceContent.SetLoadoutChain(reordered);

            var loaded = TowerSliceContent.GetLoadoutChain();
            Assert.That(loaded, Is.EqualTo(reordered));

            // Front of the reordered chain now derives the top ally initiative.
            var assignments = LoadoutChainModel.BuildAssignments(loaded, _ => false);
            Assert.That(assignments[0].Id, Is.EqualTo("glass"));
            Assert.That(assignments[0].Initiative, Is.EqualTo(100));
        }
    }
}
