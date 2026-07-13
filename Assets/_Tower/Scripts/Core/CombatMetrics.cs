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
        public float DurationSeconds { get; private set; }
        public int ActionCount { get; private set; }
        public CombatTeam? WinningTeam { get; private set; }
        public IReadOnlyDictionary<string, UnitCombatMetrics> Units => units;

        public void OnCombatStarted(CombatState state)
        {
            StartedAtUtc = DateTimeOffset.UtcNow;
            EndedAtUtc = null;
            WinningTeam = null;
            DurationSeconds = state?.ElapsedSeconds ?? 0f;
            ActionCount = 0;
            units.Clear();
            if (state == null)
            {
                return;
            }

            foreach (var unitId in state.LivingUnitIds)
            {
                EnsureUnit(unitId);
            }
        }

        public void OnAbilityResolved(CombatState state, UseAbilityCommand command)
        {
            var unit = EnsureUnit(command.UnitId);
            ActionCount++;
            unit.ActionsTaken++;
        }

        public void OnDamageApplied(CombatState state, CombatDamageEvent damageEvent)
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

        public void OnCombatEnded(CombatState state)
        {
            EndedAtUtc = DateTimeOffset.UtcNow;
            WinningTeam = state?.WinningTeam;
            if (state != null)
            {
                DurationSeconds = Math.Max(DurationSeconds, state.ElapsedSeconds);
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
