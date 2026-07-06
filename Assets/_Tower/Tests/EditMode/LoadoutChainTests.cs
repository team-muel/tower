using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using Tower.UI;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    [TestFixture]
    public sealed class LoadoutChainTests
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
        public void LoadoutChain_SaveAndLoad_RoundTrips()
        {
            // Default chain
            var defaultChain = TowerSliceContent.GetLoadoutChain();
            Assert.That(defaultChain, Is.EqualTo(new[] { "regressor", "ember", "ward", "glass" }));

            // Modify and save
            var newChain = new List<string> { "ember", "regressor", "glass", "ward" };
            TowerSliceContent.SetLoadoutChain(newChain);

            // Load and check
            var loadedChain = TowerSliceContent.GetLoadoutChain();
            Assert.That(loadedChain, Is.EqualTo(newChain));
        }

        [Test]
        public void CreateRosterFromLoadout_AppliesChainSorting()
        {
            var content = TowerSliceContent.Create();

            // Set custom order: ward -> glass -> regressor -> ember
            var newChain = new List<string> { "ward", "glass", "regressor", "ember" };
            TowerSliceContent.SetLoadoutChain(newChain);

            var roster = content.CreateRosterFromLoadout();
            Assert.That(roster.Count, Is.EqualTo(4));
            Assert.That(roster[0].UnitId, Is.EqualTo("ward"));
            Assert.That(roster[1].UnitId, Is.EqualTo("glass"));
            Assert.That(roster[2].UnitId, Is.EqualTo("regressor"));
            Assert.That(roster[3].UnitId, Is.EqualTo("ember"));
        }
    }
}
