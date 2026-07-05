namespace Tower.Core
{
    // T5: drives one AI-controlled turn end to end — asks the ActionScorer for
    // the best plan, applies the reposition on the grid, and submits the
    // resulting commands to the turn engine. Companions and enemies share this
    // driver; behaviour differences come exclusively from DispositionWeights.
    public sealed class AiTurnDriver
    {
        private readonly TurnEngine engine;
        private readonly GridMap map;
        private readonly ActionScorer scorer;

        private AiTurnDriver(TurnEngine engine, GridMap map, ActionScorer scorer)
        {
            this.engine = engine;
            this.map = map;
            this.scorer = scorer;
        }

        public static Result<AiTurnDriver> Create(TurnEngine engine, GridMap map, ActionScorer scorer)
        {
            if (engine == null)
            {
                return Result<AiTurnDriver>.Failure("Turn engine is required.");
            }

            if (map == null)
            {
                return Result<AiTurnDriver>.Failure("Grid map is required.");
            }

            if (scorer == null)
            {
                return Result<AiTurnDriver>.Failure("Action scorer is required.");
            }

            return Result<AiTurnDriver>.Success(new AiTurnDriver(engine, map, scorer));
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
            var plan = scorer.ChooseAction(engine, unitId);
            if (plan.IsFailure)
            {
                return Result.Failure(plan.Error);
            }

            var chosen = plan.Value;
            if (chosen.MoveDistance > 0)
            {
                var origin = map.FindOccupant(unitId);
                if (!origin.HasValue)
                {
                    return Result.Failure("Unit is not on the grid.");
                }

                if (!map.TryMoveOccupant(origin.Value, chosen.MoveDestination, unitId))
                {
                    return Result.Failure("Planned destination cannot be entered.");
                }

                var moved = engine.Submit(new MoveCommand(unitId, chosen.MoveDistance));
                if (moved.IsFailure)
                {
                    return moved;
                }
            }

            if (chosen.Kind == AiPlanKind.Ability)
            {
                return engine.Submit(new UseAbilityCommand(unitId, chosen.AbilityId, chosen.TargetUnitId, chosen.TargetCell));
            }

            return engine.Submit(new SkipTurnCommand(unitId));
        }
    }
}
