using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public sealed class CombatState
    {
        public const float DefaultMovementBudget = 4f;

        private readonly Dictionary<string, CombatantRef> combatants;
        private readonly List<string> registrationOrder;
        private readonly HashSet<string> defeatedUnitIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly ICombatObserver combatObserver;
        private bool combatEndObserved;

        private CombatState(
            Dictionary<string, CombatantRef> combatants,
            List<string> registrationOrder,
            StatusBoard statusBoard,
            ICombatObserver combatObserver,
            float elapsedSeconds)
        {
            this.combatants = combatants;
            this.registrationOrder = registrationOrder;
            StatusBoard = statusBoard ?? new StatusBoard();
            this.combatObserver = combatObserver;
            ElapsedSeconds = Math.Max(0f, elapsedSeconds);
            this.combatObserver?.OnCombatStarted(this);
            UpdateCombatEnd();
        }

        public float ElapsedSeconds { get; private set; }
        public StatusBoard StatusBoard { get; }
        public bool IsCombatEnded { get; private set; }
        public CombatTeam? WinningTeam { get; private set; }

        public IReadOnlyList<string> LivingUnitIds
        {
            get
            {
                var ids = new List<string>();
                for (int i = 0; i < registrationOrder.Count; i++)
                {
                    string unitId = registrationOrder[i];
                    if (IsAlive(unitId))
                    {
                        ids.Add(unitId);
                    }
                }

                return ids;
            }
        }

        public static Result<CombatState> Create(
            IEnumerable<CombatantRef> combatants,
            StatusBoard statusBoard = null,
            ICombatObserver combatObserver = null,
            float elapsedSeconds = 0f)
        {
            if (combatants == null)
            {
                return Result<CombatState>.Failure("Combatants are required.");
            }

            var byId = new Dictionary<string, CombatantRef>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var combatant in combatants)
            {
                if (combatant == null)
                {
                    return Result<CombatState>.Failure("Combatants cannot contain null entries.");
                }

                if (byId.ContainsKey(combatant.UnitId))
                {
                    return Result<CombatState>.Failure("Combatant unit ids must be unique.");
                }

                byId.Add(combatant.UnitId, combatant);
                order.Add(combatant.UnitId);
            }

            if (byId.Count == 0)
            {
                return Result<CombatState>.Failure("At least one combatant is required.");
            }

            if (byId.Values.Where(combatant => combatant.IsAlive).Select(combatant => combatant.Team).Distinct().Count() < 2)
            {
                return Result<CombatState>.Failure("Combat requires at least two living teams.");
            }

            return Result<CombatState>.Success(new CombatState(byId, order, statusBoard, combatObserver, elapsedSeconds));
        }

        public CombatantRef GetCombatant(string unitId)
        {
            return unitId != null && combatants.TryGetValue(unitId, out var combatant) ? combatant : null;
        }

        public bool IsAlive(string unitId)
        {
            return unitId != null && combatants.ContainsKey(unitId) && IsAlive(combatants[unitId]);
        }

        public Result AdvanceElapsed(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                return Result.Failure("Elapsed delta cannot be negative.");
            }

            ElapsedSeconds += deltaSeconds;
            for (int index = 0; index < registrationOrder.Count; index++)
            {
                string unitId = registrationOrder[index];
                CombatantRef combatant = combatants[unitId];
                Result<CharacterState> advanced = combatant.State.WithCooldownsAdvanced(deltaSeconds);
                if (advanced.IsFailure)
                {
                    return Result.Failure(advanced.Error);
                }

                if (!ReferenceEquals(advanced.Value, combatant.State))
                {
                    combatants[unitId] = combatant.WithState(advanced.Value);
                }
            }

            StatusBoard.PruneExpired(ElapsedSeconds);
            return Result.Success();
        }

        public Result UpdateCombatantState(string unitId, CharacterState state)
        {
            if (!combatants.ContainsKey(unitId))
            {
                return Result.Failure("Unknown combatant.");
            }

            if (state == null)
            {
                return Result.Failure("Character state is required.");
            }

            combatants[unitId] = combatants[unitId].WithState(state);
            if (state.CurrentHp <= 0)
            {
                defeatedUnitIds.Add(unitId);
            }

            UpdateCombatEnd();
            return Result.Success();
        }

        public Result DefeatCombatant(string unitId)
        {
            if (!combatants.ContainsKey(unitId))
            {
                return Result.Failure("Unknown combatant.");
            }

            defeatedUnitIds.Add(unitId);
            UpdateCombatEnd();
            return Result.Success();
        }

        public Result RecordCooldown(string unitId, AbilityDef ability)
        {
            if (ability == null || ability.CooldownSeconds <= 0f)
            {
                return Result.Success();
            }

            if (!combatants.TryGetValue(unitId, out var combatant))
            {
                return Result.Failure("Unknown combatant.");
            }

            var cooled = combatant.State.WithAbilityCooldown(ability.Id, ability.CooldownSeconds);
            if (cooled.IsFailure)
            {
                return Result.Failure(cooled.Error);
            }

            combatants[unitId] = combatant.WithState(cooled.Value);
            return Result.Success();
        }

        public void NotifyAbilityResolved(UseAbilityCommand command)
        {
            combatObserver?.OnAbilityResolved(this, command);
        }

        private bool UpdateCombatEnd()
        {
            var livingTeams = combatants.Values
                .Where(IsAlive)
                .Select(combatant => combatant.Team)
                .Distinct()
                .ToArray();

            if (livingTeams.Length > 1)
            {
                return false;
            }

            IsCombatEnded = true;
            WinningTeam = livingTeams.Length == 1 ? livingTeams[0] : (CombatTeam?)null;
            if (!combatEndObserved)
            {
                combatEndObserved = true;
                combatObserver?.OnCombatEnded(this);
            }

            return true;
        }

        private bool IsAlive(CombatantRef combatant)
        {
            return combatant.IsAlive && !defeatedUnitIds.Contains(combatant.UnitId);
        }
    }
}
