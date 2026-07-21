using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using Tower.Floor;
using Tower.Gen;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class PartyTraversalAndFloorSeedTests
    {
        [Test]
        public void BreadcrumbTrail_RecordsOnlyAtSpacingSteps()
        {
            var trail = new BreadcrumbTrail(pointSpacing: 0.5f);

            Assert.That(trail.Record(Vector3.zero), Is.True);
            Assert.That(trail.Record(new Vector3(0.2f, 0f, 0f)), Is.False, "below spacing");
            Assert.That(trail.Record(new Vector3(0.6f, 0f, 0f)), Is.True);
            Assert.That(trail.Count, Is.EqualTo(2));
        }

        [Test]
        public void BreadcrumbTrail_SamplesDistanceBehindTheHead()
        {
            var trail = new BreadcrumbTrail(pointSpacing: 0.25f);
            for (int step = 0; step <= 40; step++)
            {
                trail.Record(new Vector3(step * 0.25f, 0f, 0f));
            }

            Vector3 behind = trail.Sample(2.0f, 0f, Vector3.one * 99f);

            Assert.That(behind.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(behind.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BreadcrumbTrail_AppliesPathNormalLateralOffset()
        {
            var trail = new BreadcrumbTrail(pointSpacing: 0.25f);
            for (int step = 0; step <= 40; step++)
            {
                trail.Record(new Vector3(step * 0.25f, 0f, 0f));
            }

            // Path runs along +X; its up-cross normal points along -Z.
            Vector3 offsetSample = trail.Sample(2.0f, 0.5f, Vector3.zero);

            Assert.That(offsetSample.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(offsetSample.z, Is.EqualTo(-0.5f).Within(0.001f));
        }

        [Test]
        public void BreadcrumbTrail_ClampsToTailSoFollowersNeverOvertake()
        {
            var trail = new BreadcrumbTrail(pointSpacing: 0.25f);
            trail.Record(Vector3.zero);
            trail.Record(new Vector3(0.3f, 0f, 0f));

            Vector3 clamped = trail.Sample(50f, 0f, Vector3.one * 99f);

            Assert.That(clamped.x, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BreadcrumbTrail_FallsBackWhenEmpty()
        {
            var trail = new BreadcrumbTrail();
            Vector3 fallback = new Vector3(3f, 2f, 1f);

            Assert.That(trail.Sample(1f, 0.2f, fallback), Is.EqualTo(fallback));
        }

        [Test]
        public void FloorSeeds_AreDeterministicAndDistinctAcrossTheStairStep()
        {
            for (int floor = 1; floor <= RunEventPlan.FloorCount; floor++)
            {
                Assert.That(
                    FloorSeeds.TerrainSeed(777, floor),
                    Is.EqualTo(FloorSeeds.TerrainSeed(777, floor)),
                    "terrain seed must be deterministic");
                float stretch = FloorSeeds.TravelStretch(777, floor);
                Assert.That(stretch, Is.InRange(0.85f, 1.15f));
                for (int other = floor + 1; other <= RunEventPlan.FloorCount; other++)
                {
                    Assert.That(
                        FloorSeeds.TerrainSeed(777, floor),
                        Is.Not.EqualTo(FloorSeeds.TerrainSeed(777, other)),
                        $"floors {floor} and {other} must not clone terrain");
                }
            }
        }

        [Test]
        public void Renderer_GeneratesDifferentTerrainPerFloor()
        {
            GameObject hostA = new GameObject("floor-seed-a");
            GameObject hostB = new GameObject("floor-seed-b");
            try
            {
                ForestFloorRenderer first = hostA.AddComponent<ForestFloorRenderer>();
                SetPrivateField(first, "runFloorNumber", 1);
                first.Rebuild();

                ForestFloorRenderer second = hostB.AddComponent<ForestFloorRenderer>();
                SetPrivateField(second, "runFloorNumber", 2);
                second.Rebuild();

                Assert.That(first.Graph.Seed, Is.Not.EqualTo(second.Graph.Seed));
            }
            finally
            {
                Object.DestroyImmediate(hostA);
                Object.DestroyImmediate(hostB);
            }
        }

        [Test]
        public void Renderer_FallbackCameraUsesTheOwnerTunedOrbit()
        {
            // Other fixtures can leave an active MainCamera behind, which
            // bypasses the fallback path; park them for this test.
            Camera[] parked = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var reactivate = new System.Collections.Generic.List<GameObject>();
            foreach (Camera parkedCamera in parked)
            {
                if (parkedCamera != null && parkedCamera.gameObject.activeSelf)
                {
                    parkedCamera.gameObject.SetActive(false);
                    reactivate.Add(parkedCamera.gameObject);
                }
            }

            GameObject host = new GameObject("camera-canon");
            try
            {
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                renderer.Rebuild();

                IsoCameraFollow orbit = renderer.CameraTransform.GetComponent<IsoCameraFollow>();
                Assert.That(orbit, Is.Not.Null, "T63: canon orbit camera must drive the run loop");
                Assert.That(orbit.basePitch, Is.EqualTo(25f), "owner-tuned 2026-07-12 value");
                Assert.That(orbit.yawSensitivity, Is.EqualTo(4f));
                Assert.That(orbit.distance, Is.EqualTo(14f));
                Assert.That(renderer.CameraTransform.GetComponent<ForestFloorCamera>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (GameObject parkedObject in reactivate)
                {
                    if (parkedObject != null)
                    {
                        parkedObject.SetActive(true);
                    }
                }
            }
        }

        [Test]
        public void PillbugBody_BuildsSegmentedBodyWithSingleRootCollider()
        {
            GameObject body = PillbugBodyBuilder.Build("pillbug-test", new Color(0.4f, 0.15f, 0.1f));
            try
            {
                int expectedParts = 1 // abdomen
                    + PillbugBodyBuilder.ShellSegments
                    + 1 // head
                    + 2 // antennae
                    + (PillbugBodyBuilder.LegPairs * 2);
                Assert.That(body.transform.childCount, Is.EqualTo(expectedParts));
                Assert.That(body.GetComponent<SphereCollider>(), Is.Not.Null);
                Assert.That(body.GetComponentsInChildren<Collider>(), Has.Length.EqualTo(1),
                    "child primitives must not keep their own colliders");
                Assert.That(body.GetComponentsInChildren<Renderer>(), Has.Length.EqualTo(expectedParts));
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void EnemyProfiles_AuthorThePillbugBodyStyle()
        {
            EnemyCombatProfile profile = EnemyCombatProfile.CreateRuntime(
                "melee",
                null,
                PrimitiveType.Sphere,
                Color.red,
                Vector3.one,
                bodyStyle: EnemyBodyStyle.Pillbug);

            Assert.That(profile.BodyStyle, Is.EqualTo(EnemyBodyStyle.Pillbug));
            Object.DestroyImmediate(profile);
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
