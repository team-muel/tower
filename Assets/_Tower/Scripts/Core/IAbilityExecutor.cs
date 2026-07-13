namespace Tower.Core
{
    // T4 seam: ability resolution stays outside the combat state container.
    public interface IAbilityExecutor
    {
        Result Execute(CombatState state, UseAbilityCommand command);
    }
}
