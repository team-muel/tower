using System.Collections.Generic;
using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tower.Tests.EditMode
{
    public sealed class CompanionEntityTests
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
        public void Profile_RejectsReturnerAndMissingBody()
        {
            var body = Track(new GameObject("Body"));
            var returner = CreateCharacter("returner", isReturner: true);
            var returnerProfile = Track(CompanionVisualProfile.CreateRuntime(
                returner,
                body,
                null,
                Color.white,
                Vector3.back));
            var missingBodyProfile = Track(CompanionVisualProfile.CreateRuntime(
                CreateCharacter("companion"),
                null,
                null,
                Color.white,
                Vector3.back));

            Assert.That(returnerProfile.Validate().IsFailure, Is.True);
            Assert.That(missingBodyProfile.Validate().IsFailure, Is.True);
        }

        [Test]
        public void Configure_BindsRosterIdentityAndKinematicBody()
        {
            var leader = Track(new GameObject("Leader"));
            var bodyPrefab = Track(new GameObject("BodyPrefab"));
            var character = CreateCharacter("ember", DispositionType.Aggressive);
            var profile = CreateProfile(character, bodyPrefab, new Vector3(-1f, 0f, -2f));
            var entityObject = Track(new GameObject("Entity"));
            var entity = entityObject.AddComponent<CompanionEntity>();

            var result = entity.Configure(profile, leader.transform, new Transform[0]);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(entity.CharacterDefinition, Is.SameAs(character));
            Assert.That(entity.UnitId, Is.EqualTo("ember"));
            Assert.That(entity.Disposition, Is.EqualTo(DispositionType.Aggressive));
            Assert.That(entity.VisualRoot, Is.Not.Null);
            Assert.That(entityObject.GetComponent<Rigidbody>().isKinematic, Is.True);
        }

        [Test]
        public void FormationTarget_UsesLeaderLocalSpace()
        {
            var leader = Track(new GameObject("Leader"));
            leader.transform.SetPositionAndRotation(
                new Vector3(10f, 0f, 4f),
                Quaternion.Euler(0f, 90f, 0f));
            var entity = CreateEntity(
                leader.transform,
                CreateProfile(CreateCharacter("ward"), Track(new GameObject("Body")), Vector3.back * 2f));

            Assert.That(entity.FormationTarget().x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(entity.FormationTarget().z, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void Tick_FollowsFormationAndFacesClosestEnemy()
        {
            var leader = Track(new GameObject("Leader"));
            var closeEnemy = Track(new GameObject("CloseEnemy"));
            var farEnemy = Track(new GameObject("FarEnemy"));
            closeEnemy.transform.position = new Vector3(2f, 0f, 0f);
            farEnemy.transform.position = new Vector3(-8f, 0f, 0f);
            var profile = Track(CompanionVisualProfile.CreateRuntime(
                CreateCharacter("glass"),
                Track(new GameObject("Body")),
                null,
                Color.white,
                new Vector3(0f, 0f, -2f),
                arriveDistance: 0.1f,
                moveSpeed: 2f,
                turnSpeed: 720f));
            var entity = CreateEntity(
                leader.transform,
                profile,
                new[] { farEnemy.transform, closeEnemy.transform });
            entity.transform.position = Vector3.zero;

            entity.Tick(0.5f);

            Assert.That(entity.transform.position.z, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(entity.IsMoving, Is.True);
            Assert.That(Vector3.Dot(entity.transform.forward, closeEnemy.transform.position - entity.transform.position), Is.GreaterThan(0f));
        }

        [Test]
        public void PartySpawner_CreatesOneDistinctEntityPerRosterMember()
        {
            var root = Track(new GameObject("PartyRoot"));
            var leader = Track(new GameObject("Leader"));
            var body = Track(new GameObject("Body"));
            var ember = CreateProfile(CreateCharacter("ember"), body, new Vector3(-1f, 0f, -1f));
            var ward = CreateProfile(CreateCharacter("ward"), body, new Vector3(1f, 0f, -1f));
            var glass = CreateProfile(CreateCharacter("glass"), body, new Vector3(0f, 0f, -2f));
            var spawner = root.AddComponent<CompanionPartySpawner>();
            spawner.Configure(leader.transform, new[] { ember, ward, glass }, new Transform[0]);

            var result = spawner.SpawnNow();

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.Count, Is.EqualTo(3));
            Assert.That(
                new[] { result.Value[0].UnitId, result.Value[1].UnitId, result.Value[2].UnitId },
                Is.EquivalentTo(new[] { "ember", "ward", "glass" }));
            Assert.That(result.Value[0].VisualRoot, Is.Not.SameAs(result.Value[1].VisualRoot));
        }

        [Test]
        public void PartySpawner_RejectsDuplicateRosterIdentity()
        {
            var root = Track(new GameObject("PartyRoot"));
            var leader = Track(new GameObject("Leader"));
            var body = Track(new GameObject("Body"));
            var profile = CreateProfile(CreateCharacter("same"), body, Vector3.back);
            var spawner = root.AddComponent<CompanionPartySpawner>();
            spawner.Configure(leader.transform, new[] { profile, profile }, new Transform[0]);

            var result = spawner.SpawnNow();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("duplicate unit id"));
        }

        private CompanionEntity CreateEntity(
            Transform leader,
            CompanionVisualProfile profile,
            Transform[] enemies = null)
        {
            var entityObject = Track(new GameObject("Entity"));
            var entity = entityObject.AddComponent<CompanionEntity>();
            var result = entity.Configure(profile, leader, enemies ?? new Transform[0]);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return entity;
        }

        private CompanionVisualProfile CreateProfile(
            CharacterDef character,
            GameObject body,
            Vector3 formationOffset)
        {
            return Track(CompanionVisualProfile.CreateRuntime(
                character,
                body,
                null,
                Color.white,
                formationOffset));
        }

        private CharacterDef CreateCharacter(
            string id,
            DispositionType disposition = DispositionType.Protective,
            bool isReturner = false)
        {
            var ability = Track(AbilityDef.CreateRuntime(
                id + "-strike",
                AbilityTag.Apply,
                3,
                1,
                AbilityTargetType.Enemy));
            return Track(CharacterDef.CreateRuntime(
                id,
                id,
                10,
                2,
                1,
                5,
                disposition,
                new[] { ability },
                isReturner: isReturner));
        }

        private T Track<T>(T createdObject) where T : Object
        {
            createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
