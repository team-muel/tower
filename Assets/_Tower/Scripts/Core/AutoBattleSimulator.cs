using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public sealed class AutoBattleSimulator
    {
        public Result<AutoBattleSimulationResult> Run(AutoBattleOptions options = null)
        {
            return Run(AutoBattleScenario.CreateDefault(), options);
        }

        public Result<AutoBattleSimulationResult> Run(AutoBattleScenario scenario, AutoBattleOptions options = null)
        {
            options = options ?? new AutoBattleOptions();
            var validation = Validate(scenario, options);
            if (validation.IsFailure)
            {
                return Result<AutoBattleSimulationResult>.Failure(validation.Error);
            }

            var result = new AutoBattleSimulationResult
            {
                seed = options.seed,
                battles = options.battles,
                maxRounds = options.maxRounds
            };

            var unitAggregates = new Dictionary<string, AutoBattleUnitAggregate>(StringComparer.Ordinal);
            var totalRounds = 0;
            var totalPlayerSurvivors = 0;
            var totalEnemySurvivors = 0;
            var totalWinningSurvivors = 0;

            for (var battleIndex = 0; battleIndex < options.battles; battleIndex++)
            {
                var battle = CreateBattle(scenario, options.seed, battleIndex);
                if (battle.IsFailure)
                {
                    return Result<AutoBattleSimulationResult>.Failure(battle.Error);
                }

                var context = battle.Value;
                var guarded = false;
                while (!context.Engine.IsCombatEnded)
                {
                    if (context.Engine.RoundNumber > options.maxRounds)
                    {
                        guarded = true;
                        break;
                    }

                    var turn = context.Driver.TakeTurn();
                    if (turn.IsFailure)
                    {
                        return Result<AutoBattleSimulationResult>.Failure($"Battle {battleIndex + 1}: {turn.Error}");
                    }
                }

                var rounds = Math.Min(context.Metrics.RoundCount, options.maxRounds);
                totalRounds += rounds;
                if (guarded)
                {
                    result.guardedBattles++;
                    result.draws++;
                }
                else if (context.Engine.WinningTeam == CombatTeam.Player)
                {
                    result.playerWins++;
                }
                else if (context.Engine.WinningTeam == CombatTeam.Enemy)
                {
                    result.enemyWins++;
                }
                else
                {
                    result.draws++;
                }

                var playerSurvivors = CountLiving(context.Engine, scenario.PlayerUnits);
                var enemySurvivors = CountLiving(context.Engine, scenario.EnemyUnits);
                totalPlayerSurvivors += playerSurvivors;
                totalEnemySurvivors += enemySurvivors;
                if (!guarded && context.Engine.WinningTeam == CombatTeam.Player)
                {
                    totalWinningSurvivors += playerSurvivors;
                }
                else if (!guarded && context.Engine.WinningTeam == CombatTeam.Enemy)
                {
                    totalWinningSurvivors += enemySurvivors;
                }

                MergeMetrics(unitAggregates, context.Metrics, context.UnitTeams);
            }

            result.playerWinRate = result.playerWins / (float)options.battles;
            result.enemyWinRate = result.enemyWins / (float)options.battles;
            result.averageRounds = totalRounds / (float)options.battles;
            result.averagePlayerSurvivors = totalPlayerSurvivors / (float)options.battles;
            result.averageEnemySurvivors = totalEnemySurvivors / (float)options.battles;
            var endedBattles = result.playerWins + result.enemyWins;
            result.averageWinningSurvivors = endedBattles > 0 ? totalWinningSurvivors / (float)endedBattles : 0f;
            result.unitStats = unitAggregates.Values.OrderBy(unit => unit.unitId, StringComparer.Ordinal).ToList();
            return Result<AutoBattleSimulationResult>.Success(result);
        }

        private static Result Validate(AutoBattleScenario scenario, AutoBattleOptions options)
        {
            if (scenario == null)
            {
                return Result.Failure("Scenario is required.");
            }

            if (scenario.PlayerUnits == null || scenario.PlayerUnits.Count == 0)
            {
                return Result.Failure("Scenario requires at least one player unit.");
            }

            if (scenario.EnemyUnits == null || scenario.EnemyUnits.Count == 0)
            {
                return Result.Failure("Scenario requires at least one enemy unit.");
            }

            if (scenario.PlayerUnits.Count > scenario.Height || scenario.EnemyUnits.Count > scenario.Height)
            {
                return Result.Failure("Scenario height must fit both starting lines.");
            }

            if (options.battles <= 0)
            {
                return Result.Failure("Battle count must be positive.");
            }

            if (options.maxRounds <= 0)
            {
                return Result.Failure("Max rounds must be positive.");
            }

            return Result.Success();
        }

        private static Result<BattleContext> CreateBattle(AutoBattleScenario scenario, int seed, int battleIndex)
        {
            var map = new GridMap(scenario.Width, scenario.Height);
            var combatants = new List<CombatantRef>();
            var unitTeams = new Dictionary<string, CombatTeam>(StringComparer.Ordinal);

            var playerRows = PickRows(scenario.Height, scenario.PlayerUnits.Count, seed, battleIndex, 17);
            var enemyRows = PickRows(scenario.Height, scenario.EnemyUnits.Count, seed, battleIndex, 53);
            for (var index = 0; index < scenario.PlayerUnits.Count; index++)
            {
                var spec = scenario.PlayerUnits[index];
                var combatant = spec.CreateCombatant();
                if (combatant.IsFailure)
                {
                    return Result<BattleContext>.Failure(combatant.Error);
                }

                combatants.Add(combatant.Value);
                unitTeams[spec.UnitId] = spec.Team;
                if (!map.TrySetOccupant(new GridPos(0, playerRows[index]), spec.UnitId))
                {
                    return Result<BattleContext>.Failure($"Could not place unit '{spec.UnitId}'.");
                }
            }

            for (var index = 0; index < scenario.EnemyUnits.Count; index++)
            {
                var spec = scenario.EnemyUnits[index];
                var combatant = spec.CreateCombatant();
                if (combatant.IsFailure)
                {
                    return Result<BattleContext>.Failure(combatant.Error);
                }

                combatants.Add(combatant.Value);
                unitTeams[spec.UnitId] = spec.Team;
                if (!map.TrySetOccupant(new GridPos(scenario.Width - 1, enemyRows[index]), spec.UnitId))
                {
                    return Result<BattleContext>.Failure($"Could not place unit '{spec.UnitId}'.");
                }
            }

            var statusBoard = new StatusBoard();
            var metrics = new CombatMetrics();
            var resolver = AbilityResolver.Create(map, statusBoard, metrics);
            if (resolver.IsFailure)
            {
                return Result<BattleContext>.Failure(resolver.Error);
            }

            var engine = TurnEngine.Create(
                combatants,
                abilityExecutor: resolver.Value,
                combatObserver: metrics,
                seed: unchecked(seed + battleIndex * 7919));
            if (engine.IsFailure)
            {
                return Result<BattleContext>.Failure(engine.Error);
            }

            var scorer = ActionScorer.Create(map, statusBoard);
            if (scorer.IsFailure)
            {
                return Result<BattleContext>.Failure(scorer.Error);
            }

            var driver = AiTurnDriver.Create(engine.Value, map, scorer.Value);
            if (driver.IsFailure)
            {
                return Result<BattleContext>.Failure(driver.Error);
            }

            return Result<BattleContext>.Success(new BattleContext(engine.Value, driver.Value, metrics, unitTeams));
        }

        private static int[] PickRows(int height, int count, int seed, int battleIndex, int salt)
        {
            var rows = Enumerable.Range(0, height).ToList();
            var random = new Random(seed + battleIndex * 1009 + salt);
            for (var index = rows.Count - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                var temp = rows[index];
                rows[index] = rows[swap];
                rows[swap] = temp;
            }

            rows.Sort(0, count, Comparer<int>.Default);
            return rows.Take(count).ToArray();
        }

        private static int CountLiving(TurnEngine engine, IReadOnlyList<AutoBattleUnitSpec> units)
        {
            var count = 0;
            foreach (var unit in units)
            {
                if (engine.IsAlive(unit.UnitId))
                {
                    count++;
                }
            }

            return count;
        }

        private static void MergeMetrics(
            Dictionary<string, AutoBattleUnitAggregate> aggregates,
            CombatMetrics metrics,
            IReadOnlyDictionary<string, CombatTeam> unitTeams)
        {
            foreach (var entry in metrics.Units)
            {
                if (!aggregates.TryGetValue(entry.Key, out var aggregate))
                {
                    aggregate = new AutoBattleUnitAggregate
                    {
                        unitId = entry.Key,
                        team = unitTeams.TryGetValue(entry.Key, out var team) ? team.ToString() : string.Empty
                    };
                    aggregates.Add(entry.Key, aggregate);
                }

                aggregate.battles++;
                aggregate.kills += entry.Value.Kills;
                aggregate.damageDealt += entry.Value.DamageDealt;
                aggregate.damageTaken += entry.Value.DamageTaken;
                aggregate.actionsTaken += entry.Value.ActionsTaken;
            }
        }

        private sealed class BattleContext
        {
            public BattleContext(
                TurnEngine engine,
                AiTurnDriver driver,
                CombatMetrics metrics,
                IReadOnlyDictionary<string, CombatTeam> unitTeams)
            {
                Engine = engine;
                Driver = driver;
                Metrics = metrics;
                UnitTeams = unitTeams;
            }

            public TurnEngine Engine { get; }
            public AiTurnDriver Driver { get; }
            public CombatMetrics Metrics { get; }
            public IReadOnlyDictionary<string, CombatTeam> UnitTeams { get; }
        }
    }
}
