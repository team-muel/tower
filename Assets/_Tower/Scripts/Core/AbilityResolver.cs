using System;
using System.Linq;

namespace Tower.Core
{
    // Resolves an AbilityDef use by a combatant on a unit or cell target:
    // validates target type, Manhattan range, and Bresenham line of sight,
    // then applies tag semantics (Apply/Consume/Amplify) and damage.
    public sealed class AbilityResolver : IAbilityExecutor
    {
        // v0: consuming a mark multiplies base power by this bonus. Kept as a
        // constant for now; move to data (AbilityDef/MarkDef) when tuning starts.
        public const float ConsumeBonusMultiplier = 1.5f;

        private readonly GridMap map;
        private readonly StatusBoard statusBoard;
        private readonly ICombatObserver combatObserver;

        private AbilityResolver(GridMap map, StatusBoard statusBoard, ICombatObserver combatObserver)
        {
            this.map = map;
            this.statusBoard = statusBoard;
            this.combatObserver = combatObserver;
        }

        public StatusBoard StatusBoard => statusBoard;

        public static Result<AbilityResolver> Create(GridMap map, StatusBoard statusBoard, ICombatObserver combatObserver = null)
        {
            if (map == null)
            {
                return Result<AbilityResolver>.Failure("Grid map is required.");
            }

            if (statusBoard == null)
            {
                return Result<AbilityResolver>.Failure("Status board is required.");
            }

            return Result<AbilityResolver>.Success(new AbilityResolver(map, statusBoard, combatObserver));
        }

        public Result Execute(TurnEngine engine, UseAbilityCommand command)
        {
            if (engine == null)
            {
                return Result.Failure("Turn engine is required.");
            }

            if (command == null)
            {
                return Result.Failure("Command is required.");
            }

            var caster = engine.GetCombatant(command.UnitId);
            if (caster == null)
            {
                return Result.Failure("Unknown caster.");
            }

            if (!engine.IsAlive(command.UnitId))
            {
                return Result.Failure("Caster is defeated.");
            }

            var ability = FindAbility(caster, command.AbilityId);
            if (ability == null)
            {
                return Result.Failure($"Caster does not have ability '{command.AbilityId}'.");
            }

            var currentRound = engine.RoundNumber;
            statusBoard.OnRoundAdvanced(currentRound);

            var casterPosition = FindOccupant(command.UnitId);
            if (!casterPosition.HasValue)
            {
                return Result.Failure("Caster is not on the grid.");
            }

            var target = ResolveTarget(engine, caster, ability, command);
            if (target.IsFailure)
            {
                return Result.Failure(target.Error);
            }

            if (GridDistance.Manhattan(casterPosition.Value, target.Value.Position) > ability.Range)
            {
                return Result.Failure("Target is out of range.");
            }

            if (!LineOfSight.IsClear(map, casterPosition.Value, target.Value.Position))
            {
                return Result.Failure("Line of sight is blocked.");
            }

            switch (ability.Tag)
            {
                case AbilityTag.Apply:
                    return ExecuteApply(engine, caster, ability, target.Value, currentRound);
                case AbilityTag.Consume:
                    return ExecuteConsume(engine, caster, ability, target.Value, currentRound);
                case AbilityTag.Amplify:
                    return ExecuteAmplify(caster, ability, target.Value, currentRound);
                default:
                    return Result.Failure("Ability tag is not executable.");
            }
        }

        private Result ExecuteApply(TurnEngine engine, CombatantRef caster, AbilityDef ability, TargetContext target, int currentRound)
        {
            if (target.Unit == null)
            {
                // Cell target without an occupant: nothing to affect.
                return Result.Success();
            }

            if (ability.TargetMark != null)
            {
                var marked = statusBoard.ApplyMark(target.Unit.UnitId, ability.TargetMark, currentRound);
                if (marked.IsFailure)
                {
                    return marked;
                }
            }

            return DealPowerDamage(engine, caster, target.Unit, ability, 1f, currentRound);
        }

        private Result ExecuteConsume(TurnEngine engine, CombatantRef caster, AbilityDef ability, TargetContext target, int currentRound)
        {
            if (target.Unit == null)
            {
                return Result.Success();
            }

            var consumedMark = ability.TargetMark != null
                && statusBoard.HasMark(target.Unit.UnitId, ability.TargetMark.Id, currentRound)
                && statusBoard.RemoveMark(target.Unit.UnitId, ability.TargetMark.Id);

            // Consuming without the mark is not a failure: base power still applies.
            var tagMultiplier = consumedMark ? ConsumeBonusMultiplier : 1f;
            return DealPowerDamage(engine, caster, target.Unit, ability, tagMultiplier, currentRound);
        }

        private Result ExecuteAmplify(CombatantRef caster, AbilityDef ability, TargetContext target, int currentRound)
        {
            if (target.Unit == null)
            {
                return Result.Failure("Amplify requires a unit target.");
            }

            if (target.Unit.Team != caster.Team)
            {
                return Result.Failure("Amplify must target an ally.");
            }

            // v0: re-applying refreshes the status instead of stacking.
            return statusBoard.ApplyAmplify(target.Unit.UnitId, ability.AmplificationMultiplier, currentRound);
        }

        private Result DealPowerDamage(TurnEngine engine, CombatantRef caster, CombatantRef target, AbilityDef ability, float tagMultiplier, int currentRound)
        {
            if (ability.BasePower <= 0)
            {
                // v0: zero base power is a pure status ability — it deals no damage
                // and does not consume the caster's amplified status.
                return Result.Success();
            }

            var amplifyMultiplier = statusBoard.TryConsumeAmplify(caster.UnitId, currentRound, out var consumedMultiplier)
                ? consumedMultiplier
                : 1f;
            var power = ability.BasePower * tagMultiplier * amplifyMultiplier;
            var raw = power + caster.State.Definition.Attack - target.State.Definition.Defense;
            var damage = Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
            return ApplyDamage(engine, caster, target, ability, damage);
        }

        private Result ApplyDamage(TurnEngine engine, CombatantRef caster, CombatantRef target, AbilityDef ability, int damage)
        {
            var state = target.State;
            var appliedDamage = Math.Min(state.CurrentHp, damage);
            var newHp = Math.Max(0, state.CurrentHp - damage);
            var updated = CharacterState.Create(
                state.Definition,
                newHp,
                state.DeathCount,
                state.SpeedModifier,
                state.Loadout.SlotCount,
                state.Loadout.Abilities.ToArray());
            if (updated.IsFailure)
            {
                return Result.Failure(updated.Error);
            }

            var applied = engine.UpdateCombatantState(target.UnitId, updated.Value);
            if (applied.IsFailure)
            {
                return applied;
            }

            combatObserver?.OnDamageApplied(
                engine,
                new CombatDamageEvent(
                    caster.UnitId,
                    target.UnitId,
                    ability.Id,
                    appliedDamage,
                    newHp <= 0));

            if (newHp <= 0)
            {
                statusBoard.ClearUnit(target.UnitId);
                var position = FindOccupant(target.UnitId);
                if (position.HasValue)
                {
                    map.ClearOccupant(position.Value, target.UnitId);
                }
            }

            return Result.Success();
        }

        private Result<TargetContext> ResolveTarget(TurnEngine engine, CombatantRef caster, AbilityDef ability, UseAbilityCommand command)
        {
            switch (ability.TargetType)
            {
                case AbilityTargetType.Enemy:
                case AbilityTargetType.Ally:
                    return ResolveUnitTarget(engine, caster, ability, command);
                case AbilityTargetType.Cell:
                    return ResolveCellTarget(engine, command);
                default:
                    return Result<TargetContext>.Failure("Unsupported target type.");
            }
        }

        private Result<TargetContext> ResolveUnitTarget(TurnEngine engine, CombatantRef caster, AbilityDef ability, UseAbilityCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.TargetUnitId))
            {
                return Result<TargetContext>.Failure("Ability requires a unit target.");
            }

            var target = engine.GetCombatant(command.TargetUnitId);
            if (target == null)
            {
                return Result<TargetContext>.Failure("Unknown target unit.");
            }

            if (!engine.IsAlive(command.TargetUnitId))
            {
                return Result<TargetContext>.Failure("Target is defeated.");
            }

            if (ability.TargetType == AbilityTargetType.Enemy && target.Team == caster.Team)
            {
                return Result<TargetContext>.Failure("Ability must target an enemy.");
            }

            if (ability.TargetType == AbilityTargetType.Ally && target.Team != caster.Team)
            {
                return Result<TargetContext>.Failure("Ability must target an ally.");
            }

            var position = FindOccupant(target.UnitId);
            if (!position.HasValue)
            {
                return Result<TargetContext>.Failure("Target is not on the grid.");
            }

            return Result<TargetContext>.Success(new TargetContext(target, position.Value));
        }

        private Result<TargetContext> ResolveCellTarget(TurnEngine engine, UseAbilityCommand command)
        {
            if (!command.TargetCell.HasValue)
            {
                return Result<TargetContext>.Failure("Ability requires a cell target.");
            }

            var cell = command.TargetCell.Value;
            if (!map.InBounds(cell))
            {
                return Result<TargetContext>.Failure("Target cell is out of bounds.");
            }

            var occupantId = map.GetOccupant(cell);
            var occupant = string.IsNullOrEmpty(occupantId) ? null : engine.GetCombatant(occupantId);
            if (occupant != null && !engine.IsAlive(occupantId))
            {
                occupant = null;
            }

            return Result<TargetContext>.Success(new TargetContext(occupant, cell));
        }

        private static AbilityDef FindAbility(CombatantRef caster, string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }

            return caster.State.Loadout.Abilities.FirstOrDefault(
                ability => ability != null && StringComparer.Ordinal.Equals(ability.Id, abilityId));
        }

        private GridPos? FindOccupant(string unitId)
        {
            foreach (var position in map.Positions)
            {
                if (StringComparer.Ordinal.Equals(map.GetOccupant(position), unitId))
                {
                    return position;
                }
            }

            return null;
        }

        private readonly struct TargetContext
        {
            public TargetContext(CombatantRef unit, GridPos position)
            {
                Unit = unit;
                Position = position;
            }

            public CombatantRef Unit { get; }
            public GridPos Position { get; }
        }
    }
}
