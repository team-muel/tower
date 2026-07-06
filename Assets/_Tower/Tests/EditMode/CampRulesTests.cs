using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class CampRulesTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void EnterCamp_RecoversThirtyPercentRounded()
        {
            ExpeditionMember member = CreateMember("wounded", 10, 15);

            IReadOnlyList<ExpeditionMember> rested = EnterCamp(member);

            Assert.AreEqual(15, rested[0].State.CurrentHp);
        }

        [Test]
        public void EnterCamp_DoesNotOverhealFullHealthMember()
        {
            ExpeditionMember member = CreateMember("full", 20, 20);

            IReadOnlyList<ExpeditionMember> rested = EnterCamp(member);

            Assert.AreEqual(20, rested[0].State.CurrentHp);
        }

        [Test]
        public void EnterCamp_DoesNotAffectDeadMember()
        {
            ExpeditionMember member = CreateMember("dead", 0, 15);

            IReadOnlyList<ExpeditionMember> rested = EnterCamp(member);

            Assert.AreEqual(0, rested[0].State.CurrentHp);
            Assert.IsTrue(rested[0].IsDead);
        }

        [Test]
        public void EnterCamp_InvokesEventHookPlaceholder()
        {
            ExpeditionMember member = CreateMember("speaker", 7, 10);
            RecordingCampEventHook hook = new RecordingCampEventHook();

            Result<IReadOnlyList<ExpeditionMember>> result = CampRules.EnterCamp(new[] { member }, hook);

            Assert.IsTrue(result.IsSuccess, result.Error);
            Assert.AreEqual(1, hook.CallCount);
            Assert.AreEqual("speaker", hook.LastParty[0].UnitId);
        }

        private static IReadOnlyList<ExpeditionMember> EnterCamp(params ExpeditionMember[] members)
        {
            Result<IReadOnlyList<ExpeditionMember>> result = CampRules.EnterCamp(members);
            Assert.IsTrue(result.IsSuccess, result.Error);
            return result.Value;
        }

        private ExpeditionMember CreateMember(string unitId, int currentHp, int maxHp)
        {
            AbilityDef ability = AbilityDef.CreateRuntime(
                unitId + "-rest-test",
                AbilityTag.None,
                0,
                1,
                AbilityTargetType.Enemy);
            createdObjects.Add(ability);
            CharacterDef definition = CharacterDef.CreateRuntime(
                unitId,
                unitId,
                maxHp,
                1,
                0,
                5,
                DispositionType.Aggressive,
                new[] { ability });
            createdObjects.Add(definition);
            Result<CharacterState> state = CharacterState.Create(definition, currentHp, slotCount: 1);
            Assert.IsTrue(state.IsSuccess, state.Error);
            Result<ExpeditionMember> member = ExpeditionMember.Create(unitId, state.Value);
            Assert.IsTrue(member.IsSuccess, member.Error);
            return member.Value;
        }

        private sealed class RecordingCampEventHook : ICampEventHook
        {
            public int CallCount { get; private set; }

            public IReadOnlyList<ExpeditionMember> LastParty { get; private set; }

            public void OnCampEntered(IReadOnlyList<ExpeditionMember> party)
            {
                CallCount++;
                LastParty = party.ToArray();
            }
        }
    }
}
