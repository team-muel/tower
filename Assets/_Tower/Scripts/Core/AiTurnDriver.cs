namespace Tower.Core
{
    // T5: drives one AI-controlled turn end to end — asks the ActionScorer for
    // the best plan, applies the reposition on the battlefield, and submits
    // the resulting commands to the turn engine. Companions and enemies share
    // this driver; behaviour differences come exclusively from
    // DispositionWeights. T20: the driver only sees IBattlefield.
    public sealed class AiTurnDriver
    {
        private readonly TurnEngine engine;
        private readonly IBattlefield battlefield;
        private readonly ActionScorer scorer;

        private AiTurnDriver(TurnEngine engine, IBattlefield battlefield, ActionScorer scorer)
        {
            this.engine = engine;
            this.battlefield = battlefield;
            this.scorer = scorer;
        }

        public static Result<AiTurnDriver> Create(TurnEngine engine, GridMap map, ActionScorer scorer)
        {
            if (map == null)
            {
                return Result<AiTurnDriver>.Failure("Grid map is required.");
            }

            return Create(engine, new GridBattlefieldAdapter(map), scorer);
        }

        public static Result<AiTurnDriver> Create(TurnEngine engine, IBattlefield battlefield, ActionScorer scorer)
        {
            if (engine == null)
            {
                return Result<AiTurnDriver>.Failure("Turn engine is required.");
            }

            if (battlefield == null)
            {
                return Result<AiTurnDriver>.Failure("Battlefield is required.");
            }

            if (scorer == null)
            {
                return Result<AiTurnDriver>.Failure("Action scorer is required.");
            }

            return Result<AiTurnDriver>.Success(new AiTurnDriver(engine, battlefield, scorer));
        }

        // Plans and plays the active unit's entire turn (move, then ability or
        // skip). The turn always ends when this returns success.
        public Result TakeTurn()
        {
            if (engine.IsCombatEnded)
            {
                return Result.Failure("Combat has ended.");
            }

            if (engine.CurrentTurn == null)
            {
                return Result.Failure("No active turn.");
            }

            var unitId = engine.CurrentTurn.UnitId;
            // T18: the ability considered is only the engine's pending pick;
            // movement and targeting still come from the disposition scorer.
            var plan = scorer.ChoosePendingAction(engine, unitId, engine.PendingAbilityId);
            if (plan.IsFailure)
            {
                return Result.Failure(plan.Error);
            }

            var chosen = plan.Value;
            if (chosen.MoveDistance > 0f)
            {
                var origin = battlefield.FindOccupant(unitId);
                if (!origin.HasValue)
                {
                    return Result.Failure("Unit is not on the grid.");
                }

                if (!battlefield.TryMoveOccupant(unitId, chosen.MovePosition))
                {
                    return Result.Failure("Planned destination cannot be entered.");
                }

                var moved = engine.Submit(new MoveCommand(unitId, chosen.MoveDistance, chosen.MovePosition));
                if (moved.IsFailure)
                {
                    return moved;
                }
            }

            if (chosen.Kind == AiPlanKind.Ability)
            {
                return engine.Submit(new UseAbilityCommand(
                    unitId,
                    chosen.AbilityId,
                    chosen.TargetUnitId,
                    chosen.TargetCell,
                    chosen.TargetPoint));
            }

            return engine.Submit(new SkipTurnCommand(unitId));
        }
    }
}
