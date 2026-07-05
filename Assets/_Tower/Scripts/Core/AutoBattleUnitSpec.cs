namespace Tower.Core
{
    public sealed class AutoBattleUnitSpec
    {
        public AutoBattleUnitSpec(string unitId, CombatTeam team, CharacterDef definition)
        {
            UnitId = unitId;
            Team = team;
            Definition = definition;
        }

        public string UnitId { get; }
        public CombatTeam Team { get; }
        public CharacterDef Definition { get; }

        public Result<CombatantRef> CreateCombatant()
        {
            if (Definition == null)
            {
                return Result<CombatantRef>.Failure("Character definition is required.");
            }

            var abilities = Definition.DefaultAbilities;
            var state = CharacterState.Create(
                Definition,
                slotCount: abilities.Length,
                assignedAbilities: abilities);
            if (state.IsFailure)
            {
                return Result<CombatantRef>.Failure(state.Error);
            }

            return CombatantRef.Create(UnitId, Team, state.Value);
        }
    }
}
