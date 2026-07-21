using Tower.Core;

namespace Tower.Combat
{
    public sealed class AbilityFeelResolver
    {
        public static readonly ResolvedAbilityFeel Normal = new ResolvedAbilityFeel(
            70, 0.18f, DamagePopupStyle.Normal, AbilityApproachTween.None, string.Empty);

        public static readonly ResolvedAbilityFeel Consume = new ResolvedAbilityFeel(
            120, 0.38f, DamagePopupStyle.Consume, AbilityApproachTween.Lunge, string.Empty);

        public static readonly ResolvedAbilityFeel Amplify = new ResolvedAbilityFeel(
            85, 0.24f, DamagePopupStyle.Heal, AbilityApproachTween.None, string.Empty);

        public ResolvedAbilityFeel ResolveCommandFeel(UseAbilityCommand command, AbilityFeelCatalog catalog)
        {
            if (command == null)
            {
                return Normal;
            }

            return ResolveAbilityFeel(command.AbilityId, catalog, defeated: false);
        }

        public ResolvedAbilityFeel ResolveDamageFeel(CombatDamageEvent damageEvent, AbilityFeelCatalog catalog)
        {
            return ResolveAbilityFeel(damageEvent.AbilityId, catalog, damageEvent.TargetDefeated);
        }

        private static ResolvedAbilityFeel ResolveAbilityFeel(string abilityId, AbilityFeelCatalog catalog, bool defeated)
        {
            catalog ??= AbilityFeelCatalog.Empty;

            var overrideFeel = catalog.FindOverride(abilityId);
            if (overrideFeel != null)
            {
                return FromDef(overrideFeel, defeated);
            }

            var ability = catalog.FindAbility(abilityId);
            if (ability == null)
            {
                return defeated
                    ? new ResolvedAbilityFeel(95, 0.26f, DamagePopupStyle.Crit, AbilityApproachTween.None, string.Empty)
                    : Normal;
            }

            if (ability.Tag == AbilityTag.Consume)
            {
                return new ResolvedAbilityFeel(
                    Consume.HitstopMs,
                    Consume.ShakeIntensity,
                    DamagePopupStyle.Consume,
                    ability.Range <= 1 ? AbilityApproachTween.Lunge : AbilityApproachTween.Projectile,
                    Consume.SfxCue);
            }

            if (ability.Tag == AbilityTag.Amplify)
            {
                return new ResolvedAbilityFeel(
                    Amplify.HitstopMs,
                    Amplify.ShakeIntensity,
                    Amplify.PopupStyle,
                    ability.Range > 1 ? AbilityApproachTween.Projectile : Amplify.ApproachTween,
                    Amplify.SfxCue);
            }

            var tween = ability.Range > 1 ? AbilityApproachTween.Projectile : AbilityApproachTween.Lunge;
            var style = defeated ? DamagePopupStyle.Crit : DamagePopupStyle.Normal;
            var hitstop = defeated ? 95 : Normal.HitstopMs;
            var shake = defeated ? 0.26f : Normal.ShakeIntensity;
            return new ResolvedAbilityFeel(hitstop, shake, style, tween, string.Empty);
        }

        private static ResolvedAbilityFeel FromDef(AbilityFeelDef feel, bool defeated)
        {
            var style = defeated && feel.PopupStyle == DamagePopupStyle.Normal
                ? DamagePopupStyle.Crit
                : feel.PopupStyle;
            return new ResolvedAbilityFeel(
                feel.HitstopMs,
                feel.ShakeIntensity,
                style,
                feel.ApproachTween,
                feel.SfxCue);
        }
    }
}
