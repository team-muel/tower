namespace Tower.Core
{
    // A roster entry inside an expedition: a stable unit id plus the
    // character's current state (HP, death count, loadout). Immutable —
    // ExpeditionRules produces new members instead of mutating.
    public sealed class ExpeditionMember
    {
        private ExpeditionMember(string unitId, CharacterState state)
        {
            UnitId = unitId;
            State = state;
        }

        public string UnitId { get; }
        public CharacterState State { get; }
        public bool IsDead => State.CurrentHp <= 0;

        public static Result<ExpeditionMember> Create(string unitId, CharacterState state)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return Result<ExpeditionMember>.Failure("Expedition member unit id is required.");
            }

            if (state == null)
            {
                return Result<ExpeditionMember>.Failure("Expedition member state is required.");
            }

            return Result<ExpeditionMember>.Success(new ExpeditionMember(unitId, state));
        }

        public ExpeditionMember WithState(CharacterState state)
        {
            return new ExpeditionMember(UnitId, state);
        }
    }
}
