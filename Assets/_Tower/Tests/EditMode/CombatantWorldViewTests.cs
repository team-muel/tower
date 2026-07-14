using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class CombatantWorldViewTests
    {
        [Test]
        public void Refresh_ProjectsHpRatioAndHidesDefeatedBody()
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            GameObject anchor = new GameObject("ViewAnchor");
            AbilityDef ability = null;
            CharacterDef definition = null;
            try
            {
                anchor.transform.SetParent(body.transform, false);
                var view = anchor.AddComponent<CombatantWorldView>();
                Assert.That(view.Configure("unit", body.transform, 2f).IsSuccess, Is.True);

                ability = AbilityDef.CreateRuntime("strike", AbilityTag.Apply, 1, 1, AbilityTargetType.Enemy);
                definition = CharacterDef.CreateRuntime(
                    "unit", "Unit", 10, 1, 1, 10, DispositionType.Aggressive, new[] { ability });
                CharacterState wounded = CharacterState.Create(
                    definition, 5, slotCount: 1, assignedAbilities: new[] { ability }).Value;
                CharacterState defeated = CharacterState.Create(
                    definition, 0, slotCount: 1, assignedAbilities: new[] { ability }).Value;

                Assert.That(view.Refresh(wounded).IsSuccess, Is.True);
                Assert.That(view.FillRatio, Is.EqualTo(0.5f));
                Assert.That(view.IsAlive, Is.True);

                Assert.That(view.Refresh(defeated).IsSuccess, Is.True);
                Assert.That(view.FillRatio, Is.EqualTo(0f));
                Assert.That(view.IsAlive, Is.False);
                Assert.That(body.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(body.GetComponent<Collider>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(body);
                if (ability != null) Object.DestroyImmediate(ability);
                if (definition != null) Object.DestroyImmediate(definition);
            }
        }
    }
}
