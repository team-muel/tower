using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // T5 companion/enemy AI core: enumerates candidate turns (reachable
    // position x usable ability x valid target), scores each one against the
    // actor's disposition weights, and returns the best plan. Ties break
    // deterministically: higher score, then action kind (Ability < Move <
    // Skip), then ability id, then target id, then destination (Y, then X).
    // Pure C#. Dispositions live in DispositionWeights data — the scorer has
    // no per-disposition branches.
    // Spatial queries go through IBattlefield. Analog mode samples a fixed
    // deterministic candidate set (see AnalogBattlefield.GetMoveCandidates).
    public sealed class ActionScorer
    {
        private readonly IBattlefield battlefield;
        private readonly StatusBoard statusBoard;
        private readonly IReadOnlyDictionary<DispositionType, DispositionWeights> weightTable;

        private ActionScorer(
            IBattlefield battlefield,
            StatusBoard statusBoard,
            IReadOnlyDictionary<DispositionType, DispositionWeights> weightTable)
        {
            this.battlefield = battlefield;
            this.statusBoard = statusBoard;
            this.weightTable = weightTable;
        }

        public static Result<ActionScorer> Create(
            IBattlefield battlefield,
            StatusBoard statusBoard,
            IReadOnlyDictionary<DispositionType, DispositionWeights> weightTable = null)
        {
            if (battlefield == null)
            {
                return Result<ActionScorer>.Failure("Battlefield is required.");
            }

            if (statusBoard == null)
            {
                return Result<ActionScorer>.Failure("Status board is required.");
            }

            return Result<ActionScorer>.Success(
                new ActionScorer(battlefield, statusBoard, weightTable ?? DispositionWeights.CreateDefaultTable()));
        }

        public Result<AiPlan> ChooseAction(CombatState state, string unitId)
        {
            return ChooseAction(state, unitId, false, null);
        }

        public Result<AiPlan> ChoosePendingAction(CombatState state, string unitId, string pendingAbilityId)
        {
            return ChooseAction(state, unitId, true, pendingAbilityId);
        }

        private Result<AiPlan> ChooseAction(CombatState state, string unitId, bool restrictToPending, string pendingAbilityId)
        {
            if (state == null)
            {
                return Result<AiPlan>.Failure("Combat state is required.");
            }

            var actor = state.GetCombatant(unitId);
            if (actor == null)
            {
                return Result<AiPlan>.Failure("Unknown unit.");
            }

            if (!state.IsAlive(unitId))
            {
                return Result<AiPlan>.Failure("Unit is defeated.");
            }

            var actorPosition = battlefield.FindOccupant(unitId);
            if (!actorPosition.HasValue)
            {
                return Result<AiPlan>.Failure("Unit is not on the battlefield.");
            }

            var disposition = actor.State.Definition.Disposition;
            if (!weightTable.TryGetValue(disposition, out var weights))
            {
                return Result<AiPlan>.Failure($"No weights configured for disposition '{disposition}'.");
            }

            var movementBudget = CombatState.DefaultMovementBudget;
            var hasAction = true;
            var elapsedSeconds = state.ElapsedSeconds;

            var context = BuildContext(state, actor);
            var candidates = battlefield.GetMoveCandidates(unitId, actorPosition.Value, movementBudget);
            var preferredRange = ComputePreferredRange(actor);

            AiPlan best = null;
            foreach (var candidate in candidates)
            {
                var position = candidate.Position;
                var moveDistance = candidate.Cost;
                var positionScore = ScorePosition(position, weights, preferredRange, context);

                var repositionKind = moveDistance > 0f ? AiPlanKind.Move : AiPlanKind.Skip;
                Consider(ref best, new AiPlan(repositionKind, position, moveDistance, null, null, null, positionScore));

                if (!hasAction)
                {
                    continue;
                }

                foreach (var ability in actor.State.Loadout.Abilities)
                {
                    if (ability == null || ability.Tag == AbilityTag.None)
                    {
                        continue;
                    }

                    // T18: abilities on cooldown are never candidates.
                    if (actor.State.RemainingCooldownSeconds(ability.Id) > 0f)
                    {
                        continue;
                    }

                    if (restrictToPending && !StringComparer.Ordinal.Equals(ability.Id, pendingAbilityId))
                    {
                        continue;
                    }

                    foreach (var target in EnumerateTargets(ability, context))
                    {
                        if (battlefield.Distance(position, target.Position) > ability.Range)
                        {
                            continue;
                        }

                        if (!battlefield.HasLineOfSight(position, target.Position))
                        {
                            continue;
                        }

                        var actionScore = ScoreAbilityUse(actor, ability, target.Unit, weights, elapsedSeconds, context);
                        Consider(ref best, new AiPlan(
                            AiPlanKind.Ability,
                            position,
                            moveDistance,
                            ability.Id,
                            target.Unit.UnitId,
                            target.UseCellTarget ? target.Position : (BattlePos?)null,
                            positionScore + actionScore));
                    }
                }
            }

            return best != null
                ? Result<AiPlan>.Success(best)
                : Result<AiPlan>.Failure("No candidate actions available.");
        }

        private TurnContext BuildContext(CombatState state, CombatantRef actor)
        {
            var enemies = new List<UnitInfo>();
            var teammates = new List<UnitInfo>();

            foreach (var id in state.LivingUnitIds)
            {
                if (!state.IsAlive(id))
                {
                    continue;
                }

                var unit = state.GetCombatant(id);
                var position = battlefield.FindOccupant(id);
                if (unit == null || !position.HasValue)
                {
                    continue;
                }

                var info = new UnitInfo(unit, position.Value);
                if (unit.Team == actor.Team)
                {
                    teammates.Add(info);
                }
                else
                {
                    enemies.Add(info);
                }
            }

            return new TurnContext(
                enemies,
                teammates,
                FindProtectTargetPosition(teammates, actor.UnitId),
                FindNextActingAlly(state, actor));
        }

        // Protect target: the living teammate (excluding the actor) with the
        // lowest current HP; ties break on unit id for determinism.
        private static BattlePos? FindProtectTargetPosition(List<UnitInfo> teammates, string actorUnitId)
        {
            UnitInfo? best = null;
            foreach (var mate in teammates)
            {
                if (StringComparer.Ordinal.Equals(mate.Unit.UnitId, actorUnitId))
                {
                    continue;
                }

                if (!best.HasValue
                    || mate.Unit.State.CurrentHp < best.Value.Unit.State.CurrentHp
                    || (mate.Unit.State.CurrentHp == best.Value.Unit.State.CurrentHp
                        && string.CompareOrdinal(mate.Unit.UnitId, best.Value.Unit.UnitId) < 0))
                {
                    best = mate;
                }
            }

            return best?.Position;
        }

        // The next living teammate after the actor in registration order.
        private static string FindNextActingAlly(CombatState state, CombatantRef actor)
        {
            var order = state.LivingUnitIds;
            var actorIndex = -1;
            for (var index = 0; index < order.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(order[index], actor.UnitId))
                {
                    actorIndex = index;
                    break;
                }
            }

            if (actorIndex < 0)
            {
                return null;
            }

            for (var offset = 1; offset < order.Count; offset++)
            {
                var id = order[(actorIndex + offset) % order.Count];
                if (!state.IsAlive(id))
                {
                    continue;
                }

                var unit = state.GetCombatant(id);
                if (unit != null && unit.Team == actor.Team)
                {
                    return id;
                }
            }

            return null;
        }

        // Preferred engagement distance: the longest range among the actor's
        // damaging abilities, falling back to any tagged ability, then melee.
        private static int ComputePreferredRange(CombatantRef actor)
        {
            var damaging = 0;
            var fallback = 0;
            foreach (var ability in actor.State.Loadout.Abilities)
            {
                if (ability == null || ability.Tag == AbilityTag.None)
                {
                    continue;
                }

                fallback = Math.Max(fallback, ability.Range);
                if (ability.Tag != AbilityTag.Amplify && ability.BasePower > 0)
                {
                    damaging = Math.Max(damaging, ability.Range);
                }
            }

            var preferred = damaging > 0 ? damaging : fallback;
            return preferred > 0 ? preferred : 1;
        }

        private float ScorePosition(BattlePos position, DispositionWeights weights, int preferredRange, TurnContext context)
        {
            var score = 0f;
            if (context.Enemies.Count > 0)
            {
                var nearest = float.MaxValue;
                var adjacentEnemies = 0;
                foreach (var enemy in context.Enemies)
                {
                    var distance = battlefield.Distance(position, enemy.Position);
                    nearest = Math.Min(nearest, distance);
                    if (battlefield.AreAdjacent(position, enemy.Position))
                    {
                        adjacentEnemies++;
                    }
                }

                score -= weights.RangeKeepingWeight * Math.Abs(nearest - preferredRange);
                score -= weights.DangerPenalty * adjacentEnemies;
            }

            if (context.ProtectTargetPosition.HasValue)
            {
                score -= weights.AllyProtectionWeight
                    * battlefield.Distance(position, context.ProtectTargetPosition.Value);
            }

            return score;
        }

        private float ScoreAbilityUse(
            CombatantRef actor,
            AbilityDef ability,
            CombatantRef target,
            DispositionWeights weights,
            float elapsedSeconds,
            TurnContext context)
        {
            var score = 0f;
            var isEnemyTarget = target.Team != actor.Team;
            var damage = isEnemyTarget ? PredictDamage(actor, ability, target, elapsedSeconds) : 0;
            score += weights.DamageWeight * damage;

            if (isEnemyTarget && damage > 0 && damage >= target.State.CurrentHp)
            {
                score += weights.KillBonus;
            }

            if (ability.Tag == AbilityTag.Consume
                && ability.TargetMark != null
                && statusBoard.HasMark(target.UnitId, ability.TargetMark.Id, elapsedSeconds))
            {
                score += weights.ConsumeMarkedBonus;
            }

            if (ability.Tag == AbilityTag.Apply
                && ability.TargetMark != null
                && isEnemyTarget
                && TeamHasMatchingConsume(context.Teammates, ability.TargetMark.Id))
            {
                score += weights.ApplyComboBonus;
            }

            if (ability.Tag == AbilityTag.Amplify
                && !isEnemyTarget
                && context.NextActingAllyId != null
                && StringComparer.Ordinal.Equals(target.UnitId, context.NextActingAllyId))
            {
                score += weights.AmplifyNextAllyBonus;
            }

            return score;
        }

        // Mirrors AbilityResolver.DealPowerDamage so scores predict real outcomes.
        private int PredictDamage(CombatantRef actor, AbilityDef ability, CombatantRef target, float elapsedSeconds)
        {
            if (ability.Tag == AbilityTag.Amplify || ability.BasePower <= 0)
            {
                return 0;
            }

            var tagMultiplier = ability.Tag == AbilityTag.Consume
                && ability.TargetMark != null
                && statusBoard.HasMark(target.UnitId, ability.TargetMark.Id, elapsedSeconds)
                ? AbilityResolver.ConsumeBonusMultiplier
                : 1f;
            var amplifyMultiplier = statusBoard.GetAmplifyMultiplier(actor.UnitId, elapsedSeconds);
            var power = ability.BasePower * tagMultiplier * amplifyMultiplier;
            var raw = power + actor.State.Definition.Attack - target.State.Definition.Defense;
            return Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
        }

        private static bool TeamHasMatchingConsume(List<UnitInfo> teammates, string markId)
        {
            foreach (var mate in teammates)
            {
                foreach (var ability in mate.Unit.State.Loadout.Abilities)
                {
                    if (ability != null
                        && ability.Tag == AbilityTag.Consume
                        && ability.TargetMark != null
                        && StringComparer.Ordinal.Equals(ability.TargetMark.Id, markId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<TargetCandidate> EnumerateTargets(AbilityDef ability, TurnContext context)
        {
            switch (ability.TargetType)
            {
                case AbilityTargetType.Enemy:
                    foreach (var enemy in context.Enemies)
                    {
                        yield return new TargetCandidate(enemy.Unit, enemy.Position, false);
                    }

                    break;
                case AbilityTargetType.Ally:
                    foreach (var mate in context.Teammates)
                    {
                        yield return new TargetCandidate(mate.Unit, mate.Position, false);
                    }

                    break;
                case AbilityTargetType.Cell:
                    // Amplify needs an ally unit target; a cell-targeted
                    // amplify would fail in the resolver.
                    if (ability.Tag == AbilityTag.Amplify)
                    {
                        yield break;
                    }

                    // v0: only enemy-occupied cells are worth targeting.
                    foreach (var enemy in context.Enemies)
                    {
                        yield return new TargetCandidate(enemy.Unit, enemy.Position, true);
                    }

                    break;
            }
        }

        private static void Consider(ref AiPlan best, AiPlan candidate)
        {
            if (IsBetter(candidate, best))
            {
                best = candidate;
            }
        }

        private static bool IsBetter(AiPlan candidate, AiPlan best)
        {
            if (best == null)
            {
                return true;
            }

            if (candidate.Score != best.Score)
            {
                return candidate.Score > best.Score;
            }

            if (candidate.Kind != best.Kind)
            {
                return candidate.Kind < best.Kind;
            }

            var abilityComparison = string.CompareOrdinal(candidate.AbilityId ?? string.Empty, best.AbilityId ?? string.Empty);
            if (abilityComparison != 0)
            {
                return abilityComparison < 0;
            }

            var targetComparison = string.CompareOrdinal(candidate.TargetUnitId ?? string.Empty, best.TargetUnitId ?? string.Empty);
            if (targetComparison != 0)
            {
                return targetComparison < 0;
            }

            if (candidate.MovePosition.Y != best.MovePosition.Y)
            {
                return candidate.MovePosition.Y < best.MovePosition.Y;
            }

            return candidate.MovePosition.X < best.MovePosition.X;
        }

        private readonly struct UnitInfo
        {
            public UnitInfo(CombatantRef unit, BattlePos position)
            {
                Unit = unit;
                Position = position;
            }

            public CombatantRef Unit { get; }
            public BattlePos Position { get; }
        }

        private readonly struct TargetCandidate
        {
            public TargetCandidate(CombatantRef unit, BattlePos position, bool useCellTarget)
            {
                Unit = unit;
                Position = position;
                UseCellTarget = useCellTarget;
            }

            public CombatantRef Unit { get; }
            public BattlePos Position { get; }
            public bool UseCellTarget { get; }
        }

        private sealed class TurnContext
        {
            public TurnContext(
                List<UnitInfo> enemies,
                List<UnitInfo> teammates,
                BattlePos? protectTargetPosition,
                string nextActingAllyId)
            {
                Enemies = enemies;
                Teammates = teammates;
                ProtectTargetPosition = protectTargetPosition;
                NextActingAllyId = nextActingAllyId;
            }

            public List<UnitInfo> Enemies { get; }
            public List<UnitInfo> Teammates { get; }
            public BattlePos? ProtectTargetPosition { get; }
            public string NextActingAllyId { get; }
        }
    }
}
