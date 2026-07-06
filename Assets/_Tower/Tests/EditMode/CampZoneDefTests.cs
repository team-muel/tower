using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class CampZoneDefTests
    {
        [Test]
        public void Create_ValidZone_Succeeds()
        {
            var created = CampZoneDef.Create("depart-gate", "출발 게이트", 0f, 9f, 3f);

            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.Id, Is.EqualTo("depart-gate"));
            Assert.That(created.Value.Label, Is.EqualTo("출발 게이트"));
            Assert.That(created.Value.Radius, Is.EqualTo(3f));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Create_InvalidId_Fails(string id)
        {
            Assert.That(CampZoneDef.Create(id, "라벨", 0f, 0f, 1f).IsFailure, Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Create_InvalidLabel_Fails(string label)
        {
            Assert.That(CampZoneDef.Create("zone", label, 0f, 0f, 1f).IsFailure, Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Create_InvalidRadius_Fails(float radius)
        {
            Assert.That(CampZoneDef.Create("zone", "라벨", 0f, 0f, radius).IsFailure, Is.True);
        }

        [Test]
        public void Contains_InsideRadius_IsTrue()
        {
            var zone = CampZoneDef.Create("zone", "라벨", 2f, 3f, 2f).Value;

            Assert.That(zone.Contains(2.5f, 3.5f), Is.True);
        }

        [Test]
        public void Contains_ExactlyOnBoundary_IsTrue()
        {
            var zone = CampZoneDef.Create("zone", "라벨", 0f, 0f, 2f).Value;

            Assert.That(zone.Contains(2f, 0f), Is.True);
            Assert.That(zone.Contains(0f, -2f), Is.True);
        }

        [Test]
        public void Contains_OutsideRadius_IsFalse()
        {
            var zone = CampZoneDef.Create("zone", "라벨", 0f, 0f, 2f).Value;

            Assert.That(zone.Contains(2.01f, 0f), Is.False);
            Assert.That(zone.Contains(1.5f, 1.5f), Is.False);
        }

        [Test]
        public void Contains_EnterThenExit_TracksTransitions()
        {
            // Walk a straight line through the zone: out -> in -> out.
            var zone = CampZoneDef.Create("zone", "라벨", 0f, 5f, 1.5f).Value;

            Assert.That(zone.Contains(0f, 2f), Is.False);
            Assert.That(zone.Contains(0f, 4f), Is.True);
            Assert.That(zone.Contains(0f, 5f), Is.True);
            Assert.That(zone.Contains(0f, 7f), Is.False);
        }
    }
}
