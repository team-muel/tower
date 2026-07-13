using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class AbilityFeelResolverTests
    {
        [Test]
        public void AbilityFeelDef_CreateRuntime_ClampsNegativeValues()
        {
            var feel = AbilityFeelDef.CreateRuntime(
                hitstopMs: -10,
                shakeIntensity: -2f,
                popupStyle: DamagePopupStyle.Heal,
                approachTween: AbilityApproachTween.Projectile,
                sfxCue: "heal-soft");

            Assert.That(feel.HitstopMs, Is.EqualTo(0));
            Assert.That(feel.ShakeIntensity, Is.EqualTo(0f));
            Assert.That(feel.PopupStyle, Is.EqualTo(DamagePopupStyle.Heal));
            Assert.That(feel.ApproachTween, Is.EqualTo(AbilityApproachTween.Projectile));
            Assert.That(feel.SfxCue, Is.EqualTo("heal-soft"));

            Object.DestroyImmediate(feel);
        }

        [Test]
        public void ResolveDamageFeel_UnknownAbility_UsesNormalProfile()
        {
            var resolver = new AbilityFeelResolver();
            var damageEvent = new CombatDamageEvent("a", "b", "missing", 3, false);

            var feel = resolver.ResolveDamageFeel(damageEvent, AbilityFeelCatalog.Empty);

            Assert.That(feel.HitstopMs, Is.EqualTo(AbilityFeelResolver.Normal.HitstopMs));
            Assert.That(feel.ShakeIntensity, Is.EqualTo(AbilityFeelResolver.Normal.ShakeIntensity));
            Assert.That(feel.PopupStyle, Is.EqualTo(DamagePopupStyle.Normal));
            Assert.That(feel.ApproachTween, Is.EqualTo(AbilityApproachTween.None));
        }

        [Test]
        public void ResolveDamageFeel_ConsumeAbility_UsesStrongConsumeProfile()
        {
            var consume = AbilityDef.CreateRuntime("break", AbilityTag.Consume, 4, 1, AbilityTargetType.Enemy);
            var catalog = new AbilityFeelCatalog();
            catalog.RegisterAbility(consume);
            var resolver = new AbilityFeelResolver();

            var feel = resolver.ResolveDamageFeel(
                new CombatDamageEvent("a", "b", "break", 5, false),
                catalog);

            Assert.That(feel.HitstopMs, Is.EqualTo(AbilityFeelResolver.Consume.HitstopMs));
            Assert.That(feel.ShakeIntensity, Is.EqualTo(AbilityFeelResolver.Consume.ShakeIntensity));
            Assert.That(feel.PopupStyle, Is.EqualTo(DamagePopupStyle.Consume));
            Assert.That(feel.ApproachTween, Is.EqualTo(AbilityApproachTween.Lunge));

            Object.DestroyImmediate(consume);
        }

        [Test]
        public void ResolveCommandFeel_RangedAbility_UsesProjectileTween()
        {
            var bolt = AbilityDef.CreateRuntime("bolt", AbilityTag.Apply, 2, 4, AbilityTargetType.Enemy);
            var catalog = new AbilityFeelCatalog();
            catalog.RegisterAbility(bolt);
            var resolver = new AbilityFeelResolver();

            var feel = resolver.ResolveCommandFeel(
                new UseAbilityCommand("a", "bolt", "b"),
                catalog);

            Assert.That(feel.PopupStyle, Is.EqualTo(DamagePopupStyle.Normal));
            Assert.That(feel.ApproachTween, Is.EqualTo(AbilityApproachTween.Projectile));

            Object.DestroyImmediate(bolt);
        }

        [Test]
        public void ResolveDamageFeel_ExplicitOverride_WinsOverTagDefaults()
        {
            var consume = AbilityDef.CreateRuntime("break", AbilityTag.Consume, 4, 1, AbilityTargetType.Enemy);
            var feelDef = AbilityFeelDef.CreateRuntime(
                hitstopMs: 42,
                shakeIntensity: 0.07f,
                popupStyle: DamagePopupStyle.Heal,
                approachTween: AbilityApproachTween.Projectile,
                sfxCue: "override");
            var catalog = new AbilityFeelCatalog();
            catalog.RegisterAbility(consume);
            catalog.RegisterOverride("break", feelDef);
            var resolver = new AbilityFeelResolver();

            var feel = resolver.ResolveDamageFeel(
                new CombatDamageEvent("a", "b", "break", 5, false),
                catalog);

            Assert.That(feel.HitstopMs, Is.EqualTo(42));
            Assert.That(feel.ShakeIntensity, Is.EqualTo(0.07f));
            Assert.That(feel.PopupStyle, Is.EqualTo(DamagePopupStyle.Heal));
            Assert.That(feel.ApproachTween, Is.EqualTo(AbilityApproachTween.Projectile));
            Assert.That(feel.SfxCue, Is.EqualTo("override"));

            Object.DestroyImmediate(feelDef);
            Object.DestroyImmediate(consume);
        }

        [Test]
        public void ResolveDamageFeel_DefeatedNormalTarget_UsesCritStyle()
        {
            var strike = AbilityDef.CreateRuntime("strike", AbilityTag.Apply, 2, 1, AbilityTargetType.Enemy);
            var catalog = new AbilityFeelCatalog();
            catalog.RegisterAbility(strike);
            var resolver = new AbilityFeelResolver();

            var feel = resolver.ResolveDamageFeel(
                new CombatDamageEvent("a", "b", "strike", 9, true),
                catalog);

            Assert.That(feel.PopupStyle, Is.EqualTo(DamagePopupStyle.Crit));
            Assert.That(feel.HitstopMs, Is.GreaterThan(AbilityFeelResolver.Normal.HitstopMs));
            Assert.That(feel.ShakeIntensity, Is.GreaterThan(AbilityFeelResolver.Normal.ShakeIntensity));

            Object.DestroyImmediate(strike);
        }
    }
}
