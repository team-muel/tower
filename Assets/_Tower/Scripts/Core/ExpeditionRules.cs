using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    // T8 advance/retreat economy (Design Pillars §5), pure C#:
    // - Advance (top floor cleared): dead members are permanently removed,
    //   the stairway shortcut is gained, and a checkpoint save is required.
    // - Retreat: roll back to the last checkpoint; members who died since it
    //   return alive with death count +1. Retreat count +1.
    // - Three deaths: the member goes missing instead of returning (kept in
    //   the missing record, excluded from the roster).
    // - Third retreat: great regression — stairway 1 floor 1, roster reset
    //   from the initial template (missing stay excluded), retreat count
    //   reset. Shortcuts are kept (v0 decision).
    //
    // T12 hidden-missing rule (never surface the difference in UI):
    // - Preset companions only *pretend* to obey the three-death rule. At
    //   three deaths they enter MissingIds like everyone else (externally
    //   identical), but are also tracked in HiddenMissingIds.
    // - The great regression returns hidden-missing presets to the roster
    //   (death count reset to zero — v0 decision). Generated companions stay
    //   permanently missing, as before.
    public static class ExpeditionRules
    {
        public const int MissingDeathThreshold = 3;
        public const int GreatRegressionRetreatThreshold = 3;

        public static bool IsPartyWiped(ExpeditionState state)
        {
            return state == null || state.Roster.Count == 0 || state.Roster.All(member => member.IsDead);
        }

        // Syncs a member's post-combat state (HP changes, deaths) into the
        // expedition. Dead members stay on the roster until the next advance
        // or retreat resolves them.
        public static Result<ExpeditionState> UpdateMemberState(ExpeditionState state, string unitId, CharacterState newState)
        {
            if (state == null)
            {
                return Result<ExpeditionState>.Failure("Expedition state is required.");
            }

            if (newState == null)
            {
                return Result<ExpeditionState>.Failure("Character state is required.");
            }

            var index = FindMemberIndex(state.Roster, unitId);
            if (index < 0)
            {
                return Result<ExpeditionState>.Failure($"Unknown expedition member '{unitId}'.");
            }

            var roster = new List<ExpeditionMember>(state.Roster);
            roster[index] = roster[index].WithState(newState);
            return ExpeditionState.Restore(
                state.StairwayCount,
                state.StairwayIndex,
                state.FloorCount,
                state.FloorIndex,
                state.RetreatCount,
                state.IsComplete,
                roster,
                new List<ExpeditionMember>(state.InitialRoster),
                new List<string>(state.MissingIds),
                new List<string>(state.HiddenMissingIds),
                new List<string>(state.FallenIds),
                new HashSet<int>(state.ShortcutStairways));
        }

        // Clears the current floor. Intermediate floors just move the party
        // up; clearing the top floor is an advance.
        public static Result<ExpeditionProgress> ClearFloor(ExpeditionState state)
        {
            if (state == null)
            {
                return Result<ExpeditionProgress>.Failure("Expedition state is required.");
            }

            if (state.IsComplete)
            {
                return Result<ExpeditionProgress>.Failure("Expedition is already complete.");
            }

            if (IsPartyWiped(state))
            {
                return Result<ExpeditionProgress>.Failure("A wiped party cannot clear a floor.");
            }

            if (state.FloorIndex < state.FloorCount)
            {
                var moved = ExpeditionState.Restore(
                    state.StairwayCount,
                    state.StairwayIndex,
                    state.FloorCount,
                    state.FloorIndex + 1,
                    state.RetreatCount,
                    false,
                    new List<ExpeditionMember>(state.Roster),
                    new List<ExpeditionMember>(state.InitialRoster),
                    new List<string>(state.MissingIds),
                    new List<string>(state.HiddenMissingIds),
                    new List<string>(state.FallenIds),
                    new HashSet<int>(state.ShortcutStairways));
                return moved.IsSuccess
                    ? Result<ExpeditionProgress>.Success(new ExpeditionProgress(ExpeditionOutcome.FloorCleared, moved.Value))
                    : Result<ExpeditionProgress>.Failure(moved.Error);
            }

            return Advance(state);
        }

        // Advance: deaths are locked in (removed from the roster, recorded as
        // fallen), the stairway shortcut is gained, and the party moves to
        // the next stairway (or the expedition completes).
        private static Result<ExpeditionProgress> Advance(ExpeditionState state)
        {
            var confirmedDead = state.Roster.Where(member => member.IsDead).Select(member => member.UnitId).ToList();
            var survivors = state.Roster.Where(member => !member.IsDead).ToList();

            var fallen = new List<string>(state.FallenIds);
            fallen.AddRange(confirmedDead);

            var shortcuts = new HashSet<int>(state.ShortcutStairways) { state.StairwayIndex };

            var isComplete = state.StairwayIndex >= state.StairwayCount;
            var nextStairway = isComplete ? state.StairwayIndex : state.StairwayIndex + 1;
            var nextFloor = isComplete ? state.FloorIndex : 1;

            var advanced = ExpeditionState.Restore(
                state.StairwayCount,
                nextStairway,
                state.FloorCount,
                nextFloor,
                state.RetreatCount,
                isComplete,
                survivors,
                new List<ExpeditionMember>(state.InitialRoster),
                new List<string>(state.MissingIds),
                new List<string>(state.HiddenMissingIds),
                fallen,
                shortcuts);
            return advanced.IsSuccess
                ? Result<ExpeditionProgress>.Success(new ExpeditionProgress(
                    ExpeditionOutcome.Advanced,
                    advanced.Value,
                    confirmedDeadIds: confirmedDead))
                : Result<ExpeditionProgress>.Failure(advanced.Error);
        }

        // Retreat (party wipe or voluntary): rolls the expedition back to the
        // checkpoint state. Members who died since the checkpoint return with
        // death count +1, or go missing at the threshold. The third retreat
        // triggers the great regression instead.
        public static Result<ExpeditionProgress> Retreat(ExpeditionState current, ExpeditionState checkpoint)
        {
            if (current == null)
            {
                return Result<ExpeditionProgress>.Failure("Current expedition state is required.");
            }

            if (checkpoint == null)
            {
                return Result<ExpeditionProgress>.Failure("Checkpoint state is required.");
            }

            if (current.IsComplete)
            {
                return Result<ExpeditionProgress>.Failure("A completed expedition cannot retreat.");
            }

            var newRetreatCount = current.RetreatCount + 1;
            var deadIds = current.Roster.Where(member => member.IsDead).Select(member => member.UnitId).ToList();

            // Death counts are the regressor's memory: they survive rollback.
            var newlyMissing = new List<string>();
            var newlyHidden = new List<string>();
            var deathCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var member in current.Roster)
            {
                var deathCount = member.State.DeathCount + (member.IsDead ? 1 : 0);
                deathCounts[member.UnitId] = deathCount;
                if (member.IsDead && deathCount >= MissingDeathThreshold)
                {
                    newlyMissing.Add(member.UnitId);

                    // T12: presets go missing in appearance only. Track them
                    // internally so the great regression can bring them back.
                    if (member.State.Definition.IsPreset)
                    {
                        newlyHidden.Add(member.UnitId);
                    }
                }
            }

            var missing = new List<string>(current.MissingIds);
            missing.AddRange(newlyMissing.Where(id => !missing.Contains(id, StringComparer.Ordinal)));

            var hiddenMissing = new List<string>(current.HiddenMissingIds);
            hiddenMissing.AddRange(newlyHidden.Where(id => !hiddenMissing.Contains(id, StringComparer.Ordinal)));

            if (newRetreatCount >= GreatRegressionRetreatThreshold)
            {
                return GreatRegression(current, missing, hiddenMissing, newlyMissing);
            }

            var revived = new List<string>();
            var roster = new List<ExpeditionMember>();
            foreach (var checkpointMember in checkpoint.Roster)
            {
                if (missing.Contains(checkpointMember.UnitId, StringComparer.Ordinal))
                {
                    continue;
                }

                if (!deathCounts.TryGetValue(checkpointMember.UnitId, out var deathCount))
                {
                    deathCount = checkpointMember.State.DeathCount;
                }

                var diedThisRun = deadIds.Contains(checkpointMember.UnitId, StringComparer.Ordinal);
                var hp = diedThisRun
                    ? Math.Max(1, checkpointMember.State.CurrentHp)
                    : checkpointMember.State.CurrentHp;
                var restored = RebuildState(checkpointMember.State, hp, deathCount);
                if (restored.IsFailure)
                {
                    return Result<ExpeditionProgress>.Failure(restored.Error);
                }

                roster.Add(checkpointMember.WithState(restored.Value));
                if (diedThisRun)
                {
                    revived.Add(checkpointMember.UnitId);
                }
            }

            var retreated = ExpeditionState.Restore(
                checkpoint.StairwayCount,
                checkpoint.StairwayIndex,
                checkpoint.FloorCount,
                checkpoint.FloorIndex,
                newRetreatCount,
                false,
                roster,
                new List<ExpeditionMember>(current.InitialRoster),
                missing,
                hiddenMissing,
                new List<string>(current.FallenIds),
                new HashSet<int>(current.ShortcutStairways));
            return retreated.IsSuccess
                ? Result<ExpeditionProgress>.Success(new ExpeditionProgress(
                    ExpeditionOutcome.Retreated,
                    retreated.Value,
                    revivedIds: revived,
                    newlyMissingIds: newlyMissing))
                : Result<ExpeditionProgress>.Failure(retreated.Error);
        }

        // Great regression: back to stairway 1 floor 1 with the roster reset
        // from the initial template. Missing members stay excluded (their
        // record is permanent); fallen members return fresh — time rolls back
        // past their deaths (v0 decision). Shortcuts are kept.
        //
        // T12: hidden-missing presets return to the roster here — the
        // regression rolls back past their disappearance too. Their death
        // count resets to zero (v0 decision). Only presets present in the
        // initial template can return (presets recruited mid-run are out of
        // scope for v0 and would stay missing).
        private static Result<ExpeditionProgress> GreatRegression(
            ExpeditionState current,
            List<string> missing,
            List<string> hiddenMissing,
            List<string> newlyMissing)
        {
            var returning = hiddenMissing
                .Where(id => current.InitialRoster.Any(member => StringComparer.Ordinal.Equals(member.UnitId, id)))
                .ToList();

            var remainingMissing = missing
                .Where(id => !returning.Contains(id, StringComparer.Ordinal))
                .ToList();
            var remainingHidden = hiddenMissing
                .Where(id => !returning.Contains(id, StringComparer.Ordinal))
                .ToList();

            var roster = new List<ExpeditionMember>();
            foreach (var member in current.InitialRoster)
            {
                if (remainingMissing.Contains(member.UnitId, StringComparer.Ordinal))
                {
                    continue;
                }

                if (returning.Contains(member.UnitId, StringComparer.Ordinal))
                {
                    var restored = RebuildState(member.State, Math.Max(1, member.State.CurrentHp), 0);
                    if (restored.IsFailure)
                    {
                        return Result<ExpeditionProgress>.Failure(restored.Error);
                    }

                    roster.Add(member.WithState(restored.Value));
                    continue;
                }

                roster.Add(member);
            }

            var regressed = ExpeditionState.Restore(
                current.StairwayCount,
                1,
                current.FloorCount,
                1,
                0,
                false,
                roster,
                new List<ExpeditionMember>(current.InitialRoster),
                remainingMissing,
                remainingHidden,
                new List<string>(),
                new HashSet<int>(current.ShortcutStairways));
            return regressed.IsSuccess
                ? Result<ExpeditionProgress>.Success(new ExpeditionProgress(
                    ExpeditionOutcome.GreatRegression,
                    regressed.Value,
                    newlyMissingIds: newlyMissing))
                : Result<ExpeditionProgress>.Failure(regressed.Error);
        }

        // Shortcut v0: re-entering a conquered stairway skips its cleared
        // floors — the party starts at the top (anchor) floor, which must
        // still be cleared to re-advance.
        public static Result<ExpeditionState> ApplyShortcutGate(ExpeditionState state)
        {
            if (state == null)
            {
                return Result<ExpeditionState>.Failure("Expedition state is required.");
            }

            if (state.IsComplete || state.FloorIndex != 1 || !state.HasShortcut(state.StairwayIndex))
            {
                return Result<ExpeditionState>.Success(state);
            }

            return ExpeditionState.Restore(
                state.StairwayCount,
                state.StairwayIndex,
                state.FloorCount,
                state.FloorCount,
                state.RetreatCount,
                false,
                new List<ExpeditionMember>(state.Roster),
                new List<ExpeditionMember>(state.InitialRoster),
                new List<string>(state.MissingIds),
                new List<string>(state.HiddenMissingIds),
                new List<string>(state.FallenIds),
                new HashSet<int>(state.ShortcutStairways));
        }

        private static Result<CharacterState> RebuildState(CharacterState template, int currentHp, int deathCount)
        {
            return CharacterState.Create(
                template.Definition,
                currentHp,
                deathCount,
                template.SpeedModifier,
                template.Loadout.SlotCount,
                template.Loadout.Abilities.ToArray());
        }

        private static int FindMemberIndex(IReadOnlyList<ExpeditionMember> roster, string unitId)
        {
            for (var index = 0; index < roster.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(roster[index].UnitId, unitId))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
