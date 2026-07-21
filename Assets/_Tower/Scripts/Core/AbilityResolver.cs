using System;
using System.Linq;

namespace Tower.Core
{
    // Resolves an AbilityDef use by a combatant on a unit or point target:
    // validates target type, range and line of sight through IBattlefield,
    // then applies tag semantics (Apply/Consume/Amplify) and damage.
    public sealed class AbilityResolver : IAbilityExecutor
    {
        // v0: consuming a mark multiplies base power by this bonus. Kept as a
        // constant for now; move to data (AbilityDef/MarkDef) when tuning starts.
        public const float ConsumeBonusMultiplier = 1.5f;

        private readonly IBattlefield battlefield;
        private readonly StatusBoard statusBoard;
        private readonly ICombatObserver combatObserver;

        private AbilityResolver(IBattlefield battlefield, StatusBoard statusBoard, ICombatObserver combatObserver)
        {
            this.battlefield = battlefield;
            this.statusBoard = statusBoard;
            this.combatObserver = combatObserver;
        }

        public StatusBoard StatusBoard => statusBoard;

        public static Result<AbilityResolver> Create(IBattlefield battlefield, StatusBoard statusBoard, ICombatObserver combatObserver = null)
        {
            if (battlefield == null)
            {
                return Result<AbilityResolver>.Failure("Battlefield is required.");
            }

            if (statusBoard == null)
            {
                return Result<AbilityResolver>.Failure("Status board is required.");
            }

            return Result<AbilityResolver>.Success(new AbilityResolver(battlefield, statusBoard, combatObserver));
        }

        public Result Execute(CombatState state, UseAbilityCommand command)
        {
            if (state == null)
            {
                return Result.Failure("Combat state is required.");
            }

            if (command == null)
            {
                return Result.Failure("Command is required.");
            }

            if (state.IsCombatEnded)
            {
                return Result.Failure("Combat has ended.");
            }

            var caster = state.GetCombatant(command.UnitId);
            if (caster == null)
            {
                return Result.Failure("Unknown caster.");
            }

            if (!state.IsAlive(command.UnitId))
            {
                return Result.Failure("Caster is defeated.");
            }

            var ability = FindAbility(caster, command.AbilityId);
            if (ability == null)
            {
                return Result.Failure($"Caster does not have ability '{command.AbilityId}'.");
            }

            if (caster.State.RemainingCooldownSeconds(ability.Id) > 0f)
            {
                return Result.Failure($"Ability '{command.AbilityId}' is on cooldown.");
            }

            var elapsedSeconds = state.ElapsedSeconds;
            statusBoard.PruneExpired(elapsedSeconds);

            var casterPosition = battlefield.FindOccupant(command.UnitId);
            if (!casterPosition.HasValue)
            {
                return Result.Failure("Caster is not on the battlefield.");
            }

            var target = ResolveTarget(state, caster, ability, command);
            if (target.IsFailure)
            {
                return Result.Failure(target.Error);
            }

            if (battlefield.Distance(casterPosition.Value, target.Value.Position) > ability.Range)
            {
                return Result.Failure("Target is out of range.");
            }

            if (!battlefield.HasLineOfSight(casterPosition.Value, target.Value.Position))
            {
                return Result.Failure("Line of sight is blocked.");
            }

            switch (ability.Tag)
            {
                case AbilityTag.None:
                    return Finish(state, command, ability, ExecuteBasicDamage(state, caster, ability, target.Value, elapsedSeconds));
                case AbilityTag.Apply:
                    return Finish(state, command, ability, ExecuteApply(state, caster, ability, target.Value, elapsedSeconds));
                case AbilityTag.Consume:
                    return Finish(state, command, ability, ExecuteConsume(state, caster, ability, target.Value, elapsedSeconds));
                case AbilityTag.Amplify:
                    return Finish(state, command, ability, ExecuteAmplify(caster, ability, target.Value, elapsedSeconds));
                default:
                    return Result.Failure("Ability tag is not executable.");
            }
        }

        private Result ExecuteBasicDamage(
            CombatState state,
            CombatantRef caster,
            AbilityDef ability,
            TargetContext target,
            float elapsedSeconds)
        {
            if (ability.TargetType != AbilityTargetType.Enemy || target.Unit == null)
            {
                return Result.Failure("Untagged abilities currently support enemy damage only.");
            }

            return DealPowerDamage(state, caster, target.Unit, ability, 1f, elapsedSeconds);
        }

        private Result Finish(CombatState state, UseAbilityCommand command, AbilityDef ability, Result result)
        {
            if (result.IsFailure)
            {
                return result;
            }

            var cooled = state.RecordCooldown(command.UnitId, ability);
            if (cooled.IsFailure)
            {
                return cooled;
            }

            combatObserver?.OnAbilityResolved(state, command);
            return Result.Success();
        }

        private Result ExecuteApply(CombatState state, CombatantRef caster, AbilityDef ability, TargetContext target, float elapsedSeconds)
        {
            if (target.Unit == null)
            {
                // Point target without an occupant: nothing to affect.
                return Result.Success();
            }

            if (ability.TargetMark != null)
            {
                var marked = statusBoard.ApplyMark(target.Unit.UnitId, ability.TargetMark, elapsedSeconds);
                if (marked.IsFailure)
                {
                    return marked;
                }
            }

            return DealPowerDamage(state, caster, target.Unit, ability, 1f, elapsedSeconds);
        }

        private Result ExecuteConsume(CombatState state, CombatantRef caster, AbilityDef ability, TargetContext target, float elapsedSeconds)
        {
            if (target.Unit == null)
            {
                return Result.Success();
            }

            var consumedMark = ability.TargetMark != null
                && statusBoard.HasMark(target.Unit.UnitId, ability.TargetMark.Id, elapsedSeconds)
                && statusBoard.RemoveMark(target.Unit.UnitId, ability.TargetMark.Id);

            // Consuming without the mark is not a failure: base power still applies.
            var tagMultiplier = consumedMark ? ConsumeBonusMultiplier : 1f;
            return DealPowerDamage(state, caster, target.Unit, ability, tagMultiplier, elapsedSeconds);
        }

        private Result ExecuteAmplify(CombatantRef caster, AbilityDef ability, TargetContext target, float elapsedSeconds)
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
            return statusBoard.ApplyAmplify(target.Unit.UnitId, ability.AmplificationMultiplier, elapsedSeconds);
        }

        private Result DealPowerDamage(CombatState state, CombatantRef caster, CombatantRef target, AbilityDef ability, float tagMultiplier, float elapsedSeconds)
        {
            if (ability.BasePower <= 0)
            {
                // v0: zero base power is a pure status ability — it deals no damage
                // and does not consume the caster's amplified status.
                return Result.Success();
            }

            var amplifyMultiplier = statusBoard.TryConsumeAmplify(caster.UnitId, elapsedSeconds, out var consumedMultiplier)
                ? consumedMultiplier
                : 1f;
            var power = ability.BasePower * tagMultiplier * amplifyMultiplier;
            var raw = power + caster.State.Definition.Attack - target.State.Definition.Defense;
            var damage = Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
            return ApplyDamage(state, caster, target, ability, damage);
        }

        private Result ApplyDamage(CombatState state, CombatantRef caster, CombatantRef target, AbilityDef ability, int damage)
        {
            var targetState = target.State;
            var appliedDamage = Math.Min(targetState.CurrentHp, damage);
            var newHp = Math.Max(0, targetState.CurrentHp - damage);
            var updated = CharacterState.Create(
                targetState.Definition,
                newHp,
                targetState.DeathCount,
                targetState.SpeedModifier,
                targetState.Loadout.SlotCount,
                targetState.Loadout.Abilities.ToArray(),
                targetState.AbilityCooldowns);
            if (updated.IsFailure)
            {
                return Result.Failure(updated.Error);
            }

            var applied = state.UpdateCombatantState(target.UnitId, updated.Value);
            if (applied.IsFailure)
            {
                return applied;
            }

            combatObserver?.OnDamageApplied(
                state,
                new CombatDamageEvent(
                    caster.UnitId,
                    target.UnitId,
                    ability.Id,
                    appliedDamage,
                    newHp <= 0));

            if (newHp <= 0)
            {
                statusBoard.ClearUnit(target.UnitId);
                battlefield.RemoveOccupant(target.UnitId);
            }

            return Result.Success();
        }

        private Result<TargetContext> ResolveTarget(CombatState state, CombatantRef caster, AbilityDef ability, UseAbilityCommand command)
        {
            switch (ability.TargetType)
            {
                case AbilityTargetType.Enemy:
                case AbilityTargetType.Ally:
                    return ResolveUnitTarget(state, caster, ability, command);
                case AbilityTargetType.Cell:
                    return ResolvePointTarget(state, command);
                default:
                    return Result<TargetContext>.Failure("Unsupported target type.");
            }
        }

        private Result<TargetContext> ResolveUnitTarget(CombatState state, CombatantRef caster, AbilityDef ability, UseAbilityCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.TargetUnitId))
            {
                return Result<TargetContext>.Failure("Ability requires a unit target.");
            }

            var target = state.GetCombatant(command.TargetUnitId);
            if (target == null)
            {
                return Result<TargetContext>.Failure("Unknown target unit.");
            }

            if (!state.IsAlive(command.TargetUnitId))
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

            var position = battlefield.FindOccupant(target.UnitId);
            if (!position.HasValue)
            {
                return Result<TargetContext>.Failure("Target is not on the battlefield.");
            }

            return Result<TargetContext>.Success(new TargetContext(target, position.Value));
        }

        private Result<TargetContext> ResolvePointTarget(CombatState state, UseAbilityCommand command)
        {
            BattlePos? point = command.TargetPoint;

            if (!point.HasValue)
            {
                return Result<TargetContext>.Failure("Ability requires a point target.");
            }

            if (!battlefield.Contains(point.Value))
            {
                return Result<TargetContext>.Failure("Target point is out of bounds.");
            }

            var occupantId = battlefield.GetOccupantAt(point.Value);
            var occupant = string.IsNullOrEmpty(occupantId) ? null : state.GetCombatant(occupantId);
            if (occupant != null && !state.IsAlive(occupantId))
            {
                occupant = null;
            }

            return Result<TargetContext>.Success(new TargetContext(occupant, point.Value));
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

        private readonly struct TargetContext
        {
            public TargetContext(CombatantRef unit, BattlePos position)
            {
                Unit = unit;
                Position = position;
            }

            public CombatantRef Unit { get; }
            public BattlePos Position { get; }
        }
    }
}
