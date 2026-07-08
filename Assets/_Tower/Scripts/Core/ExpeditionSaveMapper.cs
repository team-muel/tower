using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    // Maps ExpeditionState <-> SaveGame. Character definitions are stored as
    // ids and resolved through the injected source on load; ability loadouts
    // are rebuilt from the definition's default abilities with the saved slot
    // count (v0 — loadout customisation is not persisted yet). Definition
    // fields (stats, disposition, isPreset, factionId) always come from the
    // resolved CharacterDef, so they are not duplicated in the save file.
    public static class ExpeditionSaveMapper
    {
        public static Result<SaveGame> ToSave(
            ExpeditionState state,
            IEnumerable<AnchorRuntimeSnapshot> anchorStates = null)
        {
            if (state == null)
            {
                return Result<SaveGame>.Failure("Expedition state is required.");
            }

            var save = new SaveGame
            {
                version = SaveGame.CurrentVersion,
                stairwayCount = state.StairwayCount,
                stairwayIndex = state.StairwayIndex,
                floorCount = state.FloorCount,
                floorIndex = state.FloorIndex,
                retreatCount = state.RetreatCount,
                isComplete = state.IsComplete,
                roster = state.Roster.Select(ToSaveMember).ToArray(),
                initialRoster = state.InitialRoster.Select(ToSaveMember).ToArray(),
                missingIds = state.MissingIds.ToArray(),
                hiddenMissingIds = state.HiddenMissingIds.ToArray(),
                fallenIds = state.FallenIds.ToArray(),
                shortcutStairways = state.ShortcutStairways.OrderBy(index => index).ToArray(),
                anchorStates = CopyAnchorSnapshots(anchorStates).ToArray()
            };
            return Result<SaveGame>.Success(save);
        }

        public static Result<ExpeditionState> ToState(SaveGame save, Func<string, CharacterDef> characterSource)
        {
            if (save == null)
            {
                return Result<ExpeditionState>.Failure("Save game is required.");
            }

            if (characterSource == null)
            {
                return Result<ExpeditionState>.Failure("Character source is required.");
            }

            if (save.version != SaveGame.CurrentVersion)
            {
                return Result<ExpeditionState>.Failure($"Unsupported save version {save.version}.");
            }

            var roster = RestoreMembers(save.roster, characterSource);
            if (roster.IsFailure)
            {
                return Result<ExpeditionState>.Failure(roster.Error);
            }

            var initialRoster = RestoreMembers(save.initialRoster, characterSource);
            if (initialRoster.IsFailure)
            {
                return Result<ExpeditionState>.Failure(initialRoster.Error);
            }

            return ExpeditionState.Restore(
                save.stairwayCount,
                save.stairwayIndex,
                save.floorCount,
                save.floorIndex,
                save.retreatCount,
                save.isComplete,
                roster.Value,
                initialRoster.Value,
                new List<string>(save.missingIds ?? new string[0]),
                new List<string>(save.hiddenMissingIds ?? new string[0]),
                new List<string>(save.fallenIds ?? new string[0]),
                new HashSet<int>(save.shortcutStairways ?? new int[0]));
        }

        public static Result<InteractionRuntimeStore> ToInteractionStore(SaveGame save)
        {
            if (save == null)
            {
                return Result<InteractionRuntimeStore>.Failure("Save game is required.");
            }

            if (save.version != SaveGame.CurrentVersion)
            {
                return Result<InteractionRuntimeStore>.Failure($"Unsupported save version {save.version}.");
            }

            var store = new InteractionRuntimeStore();
            foreach (AnchorRuntimeSnapshot snapshot in save.anchorStates ?? new AnchorRuntimeSnapshot[0])
            {
                Result remembered = store.Remember(snapshot);
                if (remembered.IsFailure)
                {
                    return Result<InteractionRuntimeStore>.Failure(remembered.Error);
                }
            }

            return Result<InteractionRuntimeStore>.Success(store);
        }

        private static SaveMember ToSaveMember(ExpeditionMember member)
        {
            return new SaveMember
            {
                unitId = member.UnitId,
                characterId = member.State.Definition.Id,
                currentHp = member.State.CurrentHp,
                deathCount = member.State.DeathCount,
                slotCount = member.State.Loadout.SlotCount
            };
        }

        private static Result<List<ExpeditionMember>> RestoreMembers(
            SaveMember[] saved,
            Func<string, CharacterDef> characterSource)
        {
            var members = new List<ExpeditionMember>();
            foreach (var saveMember in saved ?? new SaveMember[0])
            {
                if (saveMember == null)
                {
                    return Result<List<ExpeditionMember>>.Failure("Save member entries cannot be null.");
                }

                var definition = characterSource(saveMember.characterId);
                if (definition == null)
                {
                    return Result<List<ExpeditionMember>>.Failure(
                        $"Unknown character definition '{saveMember.characterId}'.");
                }

                var state = CharacterState.Create(
                    definition,
                    saveMember.currentHp,
                    saveMember.deathCount,
                    slotCount: saveMember.slotCount);
                if (state.IsFailure)
                {
                    return Result<List<ExpeditionMember>>.Failure(state.Error);
                }

                var member = ExpeditionMember.Create(saveMember.unitId, state.Value);
                if (member.IsFailure)
                {
                    return Result<List<ExpeditionMember>>.Failure(member.Error);
                }

                members.Add(member.Value);
            }

            return Result<List<ExpeditionMember>>.Success(members);
        }

        private static List<AnchorRuntimeSnapshot> CopyAnchorSnapshots(IEnumerable<AnchorRuntimeSnapshot> snapshots)
        {
            var result = new List<AnchorRuntimeSnapshot>();
            if (snapshots == null)
            {
                return result;
            }

            foreach (AnchorRuntimeSnapshot snapshot in snapshots)
            {
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.anchorId))
                {
                    continue;
                }

                result.Add(new AnchorRuntimeSnapshot(
                    snapshot.anchorId,
                    snapshot.kind,
                    snapshot.state,
                    snapshot.usesRemaining));
            }

            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.anchorId, b.anchorId));
            return result;
        }
    }
}
