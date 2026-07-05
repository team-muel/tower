using System.Collections.Generic;

namespace Tower.Core
{
    public interface ICombatObserver
    {
        void OnCombatStarted(TurnEngine engine);
        void OnRoundStarted(TurnEngine engine, int roundNumber, IReadOnlyList<string> roundOrder);
        void OnCommandCommitted(TurnEngine engine, TurnCommand command);
        void OnDamageApplied(TurnEngine engine, CombatDamageEvent damageEvent);
        void OnCombatEnded(TurnEngine engine);
    }
}
