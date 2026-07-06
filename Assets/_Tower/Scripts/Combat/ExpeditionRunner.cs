using System;
using System.Collections.Generic;
using System.Linq;
using Tower.Core;
using Tower.Gen;
using CoreAiTurnDriver = Tower.Core.AiTurnDriver;

namespace Tower.Combat
{
    // T8 macro-loop wiring: generates each floor with FloorGenerator, plays
    // every encounter room with the T3/T4/T5 combat stack (TurnEngine +
    // AbilityResolver + ActionScorer/AiTurnDriver), then applies the
    // advance/retreat rules and persists checkpoints through SaveRepository.
    // Pure C# — the demo bootstrap owns the MonoBehaviour side.
    public sealed class ExpeditionRunner
    {
        // Safety valve for AI-vs-AI battles; a healthy encounter ends far
        // below this.
        public const int MaxTurnsPerEncounter = 500;

        private readonly SaveRepository repository;
        private readonly IExpeditionEnemyFactory enemyFactory;
        private readonly int baseSeed;
        private ExpeditionState checkpoint;

        private ExpeditionRunner(
            ExpeditionState state,
            ExpeditionState checkpoint,
            SaveRepository repository,
            IExpeditionEnemyFactory enemyFactory,
            int baseSeed)
        {
            State = state;
            this.checkpoint = checkpoint;
            this.repository = repository;
            this.enemyFactory = enemyFactory;
            this.baseSeed = baseSeed;
        }

        public ExpeditionState State { get; private set; }

        // Creates a runner and writes the initial checkpoint save, so the
        // very first retreat has a defined rollback target.
        public static Result<ExpeditionRunner> Create(
            ExpeditionState state,
            SaveRepository repository,
            IExpeditionEnemyFactory enemyFactory,
            int baseSeed)
        {
            if (state == null)
            {
                return Result<ExpeditionRunner>.Failure("Expedition state is required.");
            }

            if (repository == null)
            {
                return Result<ExpeditionRunner>.Failure("Save repository is required.");
            }

            if (enemyFactory == null)
            {
                return Result<ExpeditionRunner>.Failure("Enemy factory is required.");
            }

            var runner = new ExpeditionRunner(state, state, repository, enemyFactory, baseSeed);
            var saved = runner.SaveCheckpoint(state);
            return saved.IsSuccess
                ? Result<ExpeditionRunner>.Success(runner)
                : Result<ExpeditionRunner>.Failure(saved.Error);
        }

        // Restores a runner from the checkpoint save file.
        public static Result<ExpeditionRunner> Load(
            SaveRepository repository,
            Func<string, CharacterDef> characterSource,
            IExpeditionEnemyFactory enemyFactory,
            int baseSeed)
        {
            if (repository == null)
            {
                return Result<ExpeditionRunner>.Failure("Save repository is required.");
            }

            if (enemyFactory == null)
            {
                return Result<ExpeditionRunner>.Failure("Enemy factory is required.");
            }

            var loaded = repository.Load();
            if (loaded.IsFailure)
            {
                return Result<ExpeditionRunner>.Failure(loaded.Error);
            }

            var state = ExpeditionSaveMapper.ToState(loaded.Value, characterSource);
            if (state.IsFailure)
            {
                return Result<ExpeditionRunner>.Failure(state.Error);
            }

            return Result<ExpeditionRunner>.Success(
                new ExpeditionRunner(state.Value, state.Value, repository, enemyFactory, baseSeed));
        }

        // Plays the current floor end to end: shortcut gate, floor
        // generation, every encounter room in depth order, then the
        // advance/floor-clear (or retreat on a party wipe).
        public Result<ExpeditionProgress> PlayCurrentFloor()
        {
            if (State.IsComplete)
            {
                return Result<ExpeditionProgress>.Failure("Expedition is already complete.");
            }

            if (ExpeditionRules.IsPartyWiped(State))
            {
                return Result<ExpeditionProgress>.Failure("Party is wiped; retreat instead.");
            }

            var gated = ExpeditionRules.ApplyShortcutGate(State);
            if (gated.IsFailure)
            {
                return Result<ExpeditionProgress>.Failure(gated.Error);
            }

            State = gated.Value;

            var seed = baseSeed + (State.StairwayIndex * 1000) + State.FloorIndex;
            var layout = FloorGenerator.Generate(new FloorGenParams(seed, State.FloorIndex == State.FloorCount));

            foreach (var room in layout.Rooms.OrderBy(candidate => candidate.Depth).ThenBy(candidate => candidate.Id))
            {
                if (!room.Encounter.HasEncounter)
                {
                    continue;
                }

                var encounter = RunEncounter(room);
                if (encounter.IsFailure)
                {
                    return Result<ExpeditionProgress>.Failure(encounter.Error);
                }

                if (encounter.Value != CombatTeam.Player)
                {
                    // Party wiped mid-floor: the expedition falls back to the
                    // last checkpoint.
                    return Retreat();
                }
            }

            var progress = ExpeditionRules.ClearFloor(State);
            if (progress.IsFailure)
            {
                return progress;
            }

            return Apply(progress.Value);
        }

        // Voluntary retreat (or forced, after a wipe).
        public Result<ExpeditionProgress> Retreat()
        {
            var progress = ExpeditionRules.Retreat(State, checkpoint);
            if (progress.IsFailure)
            {
                return progress;
            }

            return Apply(progress.Value);
        }

        private Result<ExpeditionProgress> Apply(ExpeditionProgress progress)
        {
            State = progress.State;
            if (progress.RequiresSave)
            {
                var saved = SaveCheckpoint(State);
                if (saved.IsFailure)
                {
                    return Result<ExpeditionProgress>.Failure(saved.Error);
                }
            }

            return Result<ExpeditionProgress>.Success(progress);
        }

        private Result SaveCheckpoint(ExpeditionState state)
        {
            var save = ExpeditionSaveMapper.ToSave(state);
            if (save.IsFailure)
            {
                return Result.Failure(save.Error);
            }

            var written = repository.Save(save.Value);
            if (written.IsFailure)
            {
                return written;
            }

            checkpoint = state;
            return Result.Success();
        }

        // Plays one encounter room with AI on both sides and syncs the party
        // states back into the expedition. Returns the winning team.
        private Result<CombatTeam?> RunEncounter(FloorRoom room)
        {
            var map = room.Map;
            var party = State.Roster.Where(member => !member.IsDead).ToList();

            var spawnCells = map.Positions.Where(position => map.CanEnter(position)).ToList();
            if (spawnCells.Count < party.Count + room.Encounter.EnemyCount)
            {
                return Result<CombatTeam?>.Failure($"Room {room.Id} has too few open cells for the encounter.");
            }

            var combatants = new List<CombatantRef>();
            for (var index = 0; index < party.Count; index++)
            {
                var member = party[index];
                if (!map.TrySetOccupant(spawnCells[index], member.UnitId))
                {
                    return Result<CombatTeam?>.Failure($"Failed to place '{member.UnitId}' in room {room.Id}.");
                }

                var combatant = CombatantRef.Create(member.UnitId, CombatTeam.Player, member.State);
                if (combatant.IsFailure)
                {
                    return Result<CombatTeam?>.Failure(combatant.Error);
                }

                combatants.Add(combatant.Value);
            }

            for (var index = 0; index < room.Encounter.EnemySlots.Count; index++)
            {
                var slot = room.Encounter.EnemySlots[index];
                var enemyState = enemyFactory.Create(slot.KindSlot, State.StairwayIndex, State.FloorIndex);
                if (enemyState.IsFailure)
                {
                    return Result<CombatTeam?>.Failure(enemyState.Error);
                }

                var unitId = $"enemy-r{room.Id}-{slot.Index}";
                var cell = spawnCells[spawnCells.Count - 1 - index];
                if (!map.TrySetOccupant(cell, unitId))
                {
                    return Result<CombatTeam?>.Failure($"Failed to place '{unitId}' in room {room.Id}.");
                }

                var combatant = CombatantRef.Create(unitId, CombatTeam.Enemy, enemyState.Value);
                if (combatant.IsFailure)
                {
                    return Result<CombatTeam?>.Failure(combatant.Error);
                }

                combatants.Add(combatant.Value);
            }

            var statusBoard = new StatusBoard();
            var resolver = AbilityResolver.Create(map, statusBoard);
            if (resolver.IsFailure)
            {
                return Result<CombatTeam?>.Failure(resolver.Error);
            }

            var engine = TurnEngine.Create(combatants, abilityExecutor: resolver.Value, random: new Random(baseSeed + room.Id), disablePendingRules: true);
            if (engine.IsFailure)
            {
                return Result<CombatTeam?>.Failure(engine.Error);
            }

            var scorer = ActionScorer.Create(map, statusBoard);
            if (scorer.IsFailure)
            {
                return Result<CombatTeam?>.Failure(scorer.Error);
            }

            var driver = CoreAiTurnDriver.Create(engine.Value, map, scorer.Value);
            if (driver.IsFailure)
            {
                return Result<CombatTeam?>.Failure(driver.Error);
            }

            var turns = 0;
            while (!engine.Value.IsCombatEnded)
            {
                if (++turns > MaxTurnsPerEncounter)
                {
                    return Result<CombatTeam?>.Failure($"Encounter in room {room.Id} exceeded the turn cap.");
                }

                var turn = driver.Value.TakeTurn();
                if (turn.IsFailure)
                {
                    return Result<CombatTeam?>.Failure(turn.Error);
                }
            }

            foreach (var member in party)
            {
                var combatant = engine.Value.GetCombatant(member.UnitId);
                if (combatant == null)
                {
                    continue;
                }

                var synced = ExpeditionRules.UpdateMemberState(State, member.UnitId, combatant.State);
                if (synced.IsFailure)
                {
                    return Result<CombatTeam?>.Failure(synced.Error);
                }

                State = synced.Value;
            }

            return Result<CombatTeam?>.Success(engine.Value.WinningTeam);
        }
    }
}
