using System;
using System.Collections.Generic;
using Tower.Core;

namespace Tower.Combat
{
    // v0 temporary per-turn driver for non-player units. Exposed only for
    // demo/bootstrap use; T5 companion AI will replace this later.
    public static class AiTurnDriver
    {
        private static readonly Random Random = new Random();

        public static TurnCommand ChooseCommand(TurnEngine engine, string activeUnitId)
        {
            var turn = engine.CurrentTurn;
            if (turn == null || !string.Equals(turn.UnitId, activeUnitId, StringComparison.Ordinal))
            {
                return new SkipTurnCommand(activeUnitId);
            }

            if (turn.RemainingMovement > 0 && Random.NextDouble() < 0.5)
            {
                int distance = Math.Min(turn.RemainingMovement, Random.Next(0, turn.RemainingMovement + 1));
                return new MoveCommand(activeUnitId, distance);
            }

            if (turn.HasAction && Random.NextDouble() < 0.65)
            {
                return new UseAbilityCommand(activeUnitId, "strike");
            }

            return new SkipTurnCommand(activeUnitId);
        }
    }
}
