using System;
using System.Collections.Generic;

namespace Tower.Core
{
    public sealed class CombatMetrics : ICombatObserver
    {
        private readonly Dictionary<string, UnitCombatMetrics> units =
            new Dictionary<string, UnitCombatMetrics>(StringComparer.Ordinal);

        public DateTimeOffset? StartedAtUtc { get; private set; }
        public DateTimeOffset? EndedAtUtc { get; private set; }
        public int RoundCount { get; private set; }
        public int ActionCount { get; private set; }
        public CombatTeam? WinningTeam { get; private set; }
        public IReadOnlyDictionary<string, UnitCombatMetrics> Units => units;

        public void OnCombatStarted(TurnEngine engine)
        {
            StartedAtUtc = DateTimeOffset.UtcNow;
            EndedAtUtc = null;
            WinningTeam = null;
            RoundCount = engine?.RoundNumber ?? 0;
            ActionCount = 0;
            units.Clear();
        }

        public void OnRoundStarted(TurnEngine engine, int roundNumber, IReadOnlyList<string> roundOrder)
        {
            RoundCount = Math.Max(RoundCount, roundNumber);
            if (roundOrder == null)
            {
                return;
            }

            foreach (var unitId in roundOrder)
            {
                EnsureUnit(unitId);
            }
        }

        public void OnCommandCommitted(TurnEngine engine, TurnCommand command)
        {
            if (command == null)
            {
                return;
            }

            var unit = EnsureUnit(command.UnitId);
            if (command is UseAbilityCommand || command is SkipTurnCommand)
            {
                ActionCount++;
                unit.ActionsTaken++;
            }
        }

        public void OnDamageApplied(TurnEngine engine, CombatDamageEvent damageEvent)
        {
            if (damageEvent.Damage <= 0)
            {
                return;
            }

            var source = EnsureUnit(damageEvent.SourceUnitId);
            var target = EnsureUnit(damageEvent.TargetUnitId);
            source.DamageDealt += damageEvent.Damage;
            target.DamageTaken += damageEvent.Damage;
            if (damageEvent.TargetDefeated)
            {
                source.Kills++;
            }
        }

        public void OnCombatEnded(TurnEngine engine)
        {
            EndedAtUtc = DateTimeOffset.UtcNow;
            WinningTeam = engine?.WinningTeam;
            if (engine != null)
            {
                RoundCount = Math.Max(RoundCount, engine.RoundNumber);
            }
        }

        private UnitCombatMetrics EnsureUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                unitId = "<unknown>";
            }

            if (!units.TryGetValue(unitId, out var unit))
            {
                unit = new UnitCombatMetrics(unitId);
                units.Add(unitId, unit);
            }

            return unit;
        }
    }
}
