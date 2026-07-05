namespace Tower.Core
{
    // T4 seam: the turn engine delegates UseAbilityCommand resolution to this
    // interface so rules logic stays outside the engine (AbilityResolver implements it).
    public interface IAbilityExecutor
    {
        Result Execute(TurnEngine engine, UseAbilityCommand command);
    }
}
