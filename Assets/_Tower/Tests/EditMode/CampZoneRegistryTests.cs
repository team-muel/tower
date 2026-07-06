using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class CampZoneRegistryTests
    {
        private CampZoneRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new CampZoneRegistry();
        }

        [Test]
        public void Add_Zone_Succeeds()
        {
            var added = registry.Add(Zone("campfire", 0f, 0f, 2f));

            Assert.That(added.IsSuccess, Is.True);
            Assert.That(registry.Zones.Count, Is.EqualTo(1));
        }

        [Test]
        public void Add_Null_Fails()
        {
            Assert.That(registry.Add(null).IsFailure, Is.True);
        }

        [Test]
        public void Add_DuplicateId_Fails()
        {
            registry.Add(Zone("campfire", 0f, 0f, 2f));

            var duplicate = registry.Add(Zone("campfire", 5f, 5f, 1f));

            Assert.That(duplicate.IsFailure, Is.True);
            Assert.That(duplicate.Error, Does.Contain("campfire"));
            Assert.That(duplicate.Error, Does.Contain("already registered"));
        }

        [Test]
        public void FindAt_InsideZone_ReturnsZone()
        {
            registry.Add(Zone("campfire", -4f, 3f, 2.6f));
            registry.Add(Zone("depart-gate", 0f, 9f, 3f));

            var found = registry.FindAt(-3.5f, 3.5f);

            Assert.That(found, Is.Not.Null);
            Assert.That(found.Id, Is.EqualTo("campfire"));
        }

        [Test]
        public void FindAt_OutsideAllZones_ReturnsNull()
        {
            registry.Add(Zone("campfire", -4f, 3f, 2.6f));
            registry.Add(Zone("depart-gate", 0f, 9f, 3f));

            Assert.That(registry.FindAt(10f, -10f), Is.Null);
        }

        [Test]
        public void FindAt_OverlappingZones_ReturnsNearestCenter()
        {
            registry.Add(Zone("a", 0f, 0f, 4f));
            registry.Add(Zone("b", 3f, 0f, 4f));

            Assert.That(registry.FindAt(0.5f, 0f).Id, Is.EqualTo("a"));
            Assert.That(registry.FindAt(2.5f, 0f).Id, Is.EqualTo("b"));
        }

        [Test]
        public void FindAt_EmptyRegistry_ReturnsNull()
        {
            Assert.That(registry.FindAt(0f, 0f), Is.Null);
        }

        [Test]
        public void FindAt_MovingThroughZone_EntersAndExits()
        {
            registry.Add(Zone("depart-gate", 0f, 9f, 3f));

            Assert.That(registry.FindAt(0f, 4f), Is.Null);
            Assert.That(registry.FindAt(0f, 7f), Is.Not.Null);
            Assert.That(registry.FindAt(0f, 12f), Is.Not.Null);
            Assert.That(registry.FindAt(0f, 12.5f), Is.Null);
        }

        private static CampZoneDef Zone(string id, float x, float z, float radius)
        {
            return CampZoneDef.Create(id, id + " 라벨", x, z, radius).Value;
        }
    }
}
