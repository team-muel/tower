namespace Tower.Core
{
    public interface ICombatObserver
    {
        void OnCombatStarted(CombatState state);
        void OnAbilityResolved(CombatState state, UseAbilityCommand command);
        void OnDamageApplied(CombatState state, CombatDamageEvent damageEvent);
        void OnCombatEnded(CombatState state);
    }
}
