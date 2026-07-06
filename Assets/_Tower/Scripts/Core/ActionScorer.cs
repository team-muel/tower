using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // T5 companion/enemy AI core: enumerates candidate turns (reachable cell x
    // usable ability x valid target), scores each one against the actor's
    // disposition weights, and returns the best plan. Ties break
    // deterministically: higher score, then action kind (Ability < Move <
    // Skip), then ability id, then target id, then destination (Y, then X).
    // Pure C#. Dispositions live in DispositionWeights data — the scorer has
    // no per-disposition branches.
    public sealed class ActionScorer
    {
        private static readonly GridPos[] Directions =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        private readonly GridMap map;
        private readonly StatusBoard statusBoard;
        private readonly IReadOnlyDictionary<DispositionType, DispositionWeights> weightTable;

        private ActionScorer(
            GridMap map,
            StatusBoard statusBoard,
            IReadOnlyDictionary<DispositionType, DispositionWeights> weightTable)
        {
            this.map = map;
            this.statusBoard = statusBoard;
            this.weightTable = weightTable;
        }

        public static Result<ActionScorer> Create(
            GridMap map,
            StatusBoard statusBoard,
            IReadOnlyDictionary<DispositionType, DispositionWeights> weightTable = null)
        {
            if (map == null)
            {
                return Result<ActionScorer>.Failure("Grid map is required.");
            }

            if (statusBoard == null)
            {
                return Result<ActionScorer>.Failure("Status board is required.");
            }

            return Result<ActionScorer>.Success(
                new ActionScorer(map, statusBoard, weightTable ?? DispositionWeights.CreateDefaultTable()));
        }

        public Result<AiPlan> ChooseAction(TurnEngine engine, string unitId)
        {
            return ChooseAction(engine, unitId, false, null);
        }

        // T18: same disposition scoring for movement and targeting, but the
        // only ability candidate is the engine's pending ability for this
        // unit. A null pending id means the unit may only move or skip.
        public Result<AiPlan> ChoosePendingAction(TurnEngine engine, string unitId, string pendingAbilityId)
        {
            return ChooseAction(engine, unitId, true, pendingAbilityId);
        }

        private Result<AiPlan> ChooseAction(TurnEngine engine, string unitId, bool restrictToPending, string pendingAbilityId)
        {
            if (engine == null)
            {
                return Result<AiPlan>.Failure("Turn engine is required.");
            }

            var actor = engine.GetCombatant(unitId);
            if (actor == null)
            {
                return Result<AiPlan>.Failure("Unknown unit.");
            }

            if (!engine.IsAlive(unitId))
            {
                return Result<AiPlan>.Failure("Unit is defeated.");
            }

            var actorPosition = map.FindOccupant(unitId);
            if (!actorPosition.HasValue)
            {
                return Result<AiPlan>.Failure("Unit is not on the grid.");
            }

            var disposition = actor.State.Definition.Disposition;
            if (!weightTable.TryGetValue(disposition, out var weights))
            {
                return Result<AiPlan>.Failure($"No weights configured for disposition '{disposition}'.");
            }

            var isActiveUnit = engine.CurrentTurn != null
                && StringComparer.Ordinal.Equals(engine.CurrentTurn.UnitId, unitId);
            var movementBudget = isActiveUnit
                ? engine.CurrentTurn.RemainingMovement
                : TurnEngine.DefaultMovementPerTurn;
            var hasAction = !isActiveUnit || engine.CurrentTurn.HasAction;
            var currentRound = engine.RoundNumber;

            var context = BuildContext(engine, actor);
            var reachable = ComputeReachableCells(actorPosition.Value, movementBudget, unitId);
            var preferredRange = ComputePreferredRange(actor);

            AiPlan best = null;
            foreach (var reached in reachable)
            {
                var cell = reached.Key;
                var moveDistance = reached.Value;
                var positionScore = ScorePosition(cell, weights, preferredRange, context);

                var repositionKind = moveDistance > 0 ? AiPlanKind.Move : AiPlanKind.Skip;
                Consider(ref best, new AiPlan(repositionKind, cell, moveDistance, null, null, null, positionScore));

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
                    if (actor.State.RemainingCooldown(ability.Id) > 0)
                    {
                        continue;
                    }

                    if (restrictToPending && !StringComparer.Ordinal.Equals(ability.Id, pendingAbilityId))
                    {
                        continue;
                    }

                    foreach (var target in EnumerateTargets(ability, context))
                    {
                        if (GridDistance.Manhattan(cell, target.Position) > ability.Range)
                        {
                            continue;
                        }

                        if (!LineOfSight.IsClear(map, cell, target.Position))
                        {
                            continue;
                        }

                        var actionScore = ScoreAbilityUse(actor, ability, target.Unit, weights, currentRound, context);
                        Consider(ref best, new AiPlan(
                            AiPlanKind.Ability,
                            cell,
                            moveDistance,
                            ability.Id,
                            target.Unit.UnitId,
                            target.UseCellTarget ? target.Position : (GridPos?)null,
                            positionScore + actionScore));
                    }
                }
            }

            return best != null
                ? Result<AiPlan>.Success(best)
                : Result<AiPlan>.Failure("No candidate actions available.");
        }

        private TurnContext BuildContext(TurnEngine engine, CombatantRef actor)
        {
            var enemies = new List<UnitInfo>();
            var teammates = new List<UnitInfo>();

            // CurrentRoundOrder covers every living combatant in a stable,
            // deterministic order (speed desc, then unit id).
            foreach (var id in engine.CurrentRoundOrder)
            {
                if (!engine.IsAlive(id))
                {
                    continue;
                }

                var unit = engine.GetCombatant(id);
                var position = map.FindOccupant(id);
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
                FindNextActingAlly(engine, actor));
        }

        // Protect target: the living teammate (excluding the actor) with the
        // lowest current HP; ties break on unit id for determinism.
        private static GridPos? FindProtectTargetPosition(List<UnitInfo> teammates, string actorUnitId)
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

        // The next living teammate after the actor in the round order,
        // wrapping around into the next round.
        private static string FindNextActingAlly(TurnEngine engine, CombatantRef actor)
        {
            var order = engine.CurrentRoundOrder;
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
                if (!engine.IsAlive(id))
                {
                    continue;
                }

                var unit = engine.GetCombatant(id);
                if (unit != null && unit.Team == actor.Team)
                {
                    return id;
                }
            }

            return null;
        }

        // Breadth-first flood fill over enterable cells within the movement
        // budget; values are path distances usable as MoveCommand distances.
        private Dictionary<GridPos, int> ComputeReachableCells(GridPos start, int budget, string unitId)
        {
            var distances = new Dictionary<GridPos, int> { [start] = 0 };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var distance = distances[current];
                if (distance >= budget)
                {
                    continue;
                }

                foreach (var direction in Directions)
                {
                    var next = new GridPos(current.X + direction.X, current.Y + direction.Y);
                    if (distances.ContainsKey(next) || !map.CanEnter(next, unitId))
                    {
                        continue;
                    }

                    distances[next] = distance + 1;
                    queue.Enqueue(next);
                }
            }

            return distances;
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

        private static float ScorePosition(GridPos cell, DispositionWeights weights, int preferredRange, TurnContext context)
        {
            var score = 0f;
            if (context.Enemies.Count > 0)
            {
                var nearest = int.MaxValue;
                var adjacentEnemies = 0;
                foreach (var enemy in context.Enemies)
                {
                    var distance = GridDistance.Manhattan(cell, enemy.Position);
                    nearest = Math.Min(nearest, distance);
                    if (distance == 1)
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
                    * GridDistance.Manhattan(cell, context.ProtectTargetPosition.Value);
            }

            return score;
        }

        private float ScoreAbilityUse(
            CombatantRef actor,
            AbilityDef ability,
            CombatantRef target,
            DispositionWeights weights,
            int currentRound,
            TurnContext context)
        {
            var score = 0f;
            var isEnemyTarget = target.Team != actor.Team;
            var damage = isEnemyTarget ? PredictDamage(actor, ability, target, currentRound) : 0;
            score += weights.DamageWeight * damage;

            if (isEnemyTarget && damage > 0 && damage >= target.State.CurrentHp)
            {
                score += weights.KillBonus;
            }

            if (ability.Tag == AbilityTag.Consume
                && ability.TargetMark != null
                && statusBoard.HasMark(target.UnitId, ability.TargetMark.Id, currentRound))
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
        private int PredictDamage(CombatantRef actor, AbilityDef ability, CombatantRef target, int currentRound)
        {
            if (ability.Tag == AbilityTag.Amplify || ability.BasePower <= 0)
            {
                return 0;
            }

            var tagMultiplier = ability.Tag == AbilityTag.Consume
                && ability.TargetMark != null
                && statusBoard.HasMark(target.UnitId, ability.TargetMark.Id, currentRound)
                ? AbilityResolver.ConsumeBonusMultiplier
                : 1f;
            var amplifyMultiplier = statusBoard.GetAmplifyMultiplier(actor.UnitId, currentRound);
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

            if (candidate.MoveDestination.Y != best.MoveDestination.Y)
            {
                return candidate.MoveDestination.Y < best.MoveDestination.Y;
            }

            return candidate.MoveDestination.X < best.MoveDestination.X;
        }

        private readonly struct UnitInfo
        {
            public UnitInfo(CombatantRef unit, GridPos position)
            {
                Unit = unit;
                Position = position;
            }

            public CombatantRef Unit { get; }
            public GridPos Position { get; }
        }

        private readonly struct TargetCandidate
        {
            public TargetCandidate(CombatantRef unit, GridPos position, bool useCellTarget)
            {
                Unit = unit;
                Position = position;
                UseCellTarget = useCellTarget;
            }

            public CombatantRef Unit { get; }
            public GridPos Position { get; }
            public bool UseCellTarget { get; }
        }

        private sealed class TurnContext
        {
            public TurnContext(
                List<UnitInfo> enemies,
                List<UnitInfo> teammates,
                GridPos? protectTargetPosition,
                string nextActingAllyId)
            {
                Enemies = enemies;
                Teammates = teammates;
                ProtectTargetPosition = protectTargetPosition;
                NextActingAllyId = nextActingAllyId;
            }

            public List<UnitInfo> Enemies { get; }
            public List<UnitInfo> Teammates { get; }
            public GridPos? ProtectTargetPosition { get; }
            public string NextActingAllyId { get; }
        }
    }
}
