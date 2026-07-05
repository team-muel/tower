namespace Tower.Core
{
    public sealed class CombatantRef
    {
        private CombatantRef(string unitId, CombatTeam team, CharacterState state)
        {
            UnitId = unitId;
            Team = team;
            State = state;
        }

        public string UnitId { get; }
        public CombatTeam Team { get; }
        public CharacterState State { get; }
        public bool IsAlive => State != null && State.CurrentHp > 0;

        public static Result<CombatantRef> Create(string unitId, CombatTeam team, CharacterState state)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return Result<CombatantRef>.Failure("Unit id is required.");
            }

            if (state == null)
            {
                return Result<CombatantRef>.Failure("Character state is required.");
            }

            return Result<CombatantRef>.Success(new CombatantRef(unitId, team, state));
        }

        public CombatantRef WithState(CharacterState state)
        {
            return new CombatantRef(UnitId, Team, state);
        }
    }
}
