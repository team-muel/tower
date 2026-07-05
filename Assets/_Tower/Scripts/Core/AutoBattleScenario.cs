using System.Collections.Generic;

namespace Tower.Core
{
    public sealed class AutoBattleScenario
    {
        public AutoBattleScenario(
            int width,
            int height,
            IReadOnlyList<AutoBattleUnitSpec> playerUnits,
            IReadOnlyList<AutoBattleUnitSpec> enemyUnits)
        {
            Width = width;
            Height = height;
            PlayerUnits = playerUnits;
            EnemyUnits = enemyUnits;
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<AutoBattleUnitSpec> PlayerUnits { get; }
        public IReadOnlyList<AutoBattleUnitSpec> EnemyUnits { get; }

        public static AutoBattleScenario CreateDefault()
        {
            var strike = AbilityDef.CreateRuntime(
                "sim-strike",
                AbilityTag.Apply,
                basePower: 5,
                range: 1,
                targetType: AbilityTargetType.Enemy,
                displayName: "Sim Strike");
            var bolt = AbilityDef.CreateRuntime(
                "sim-bolt",
                AbilityTag.Apply,
                basePower: 3,
                range: 3,
                targetType: AbilityTargetType.Enemy,
                displayName: "Sim Bolt");
            var rally = AbilityDef.CreateRuntime(
                "sim-rally",
                AbilityTag.Amplify,
                basePower: 0,
                range: 3,
                targetType: AbilityTargetType.Ally,
                amplificationMultiplier: 1.5f,
                displayName: "Sim Rally");

            var playerUnits = new[]
            {
                new AutoBattleUnitSpec(
                    "player-vanguard",
                    CombatTeam.Player,
                    CharacterDef.CreateRuntime("sim-player-vanguard", "Player Vanguard", 24, 2, 1, 8, DispositionType.Aggressive, new[] { strike, bolt })),
                new AutoBattleUnitSpec(
                    "player-warden",
                    CombatTeam.Player,
                    CharacterDef.CreateRuntime("sim-player-warden", "Player Warden", 28, 1, 2, 5, DispositionType.Protective, new[] { strike, rally })),
                new AutoBattleUnitSpec(
                    "player-glass",
                    CombatTeam.Player,
                    CharacterDef.CreateRuntime("sim-player-glass", "Player Glass", 18, 4, 0, 7, DispositionType.Aggressive, new[] { strike, bolt }))
            };

            var enemyUnits = new[]
            {
                new AutoBattleUnitSpec(
                    "enemy-vanguard",
                    CombatTeam.Enemy,
                    CharacterDef.CreateRuntime("sim-enemy-vanguard", "Enemy Vanguard", 24, 2, 1, 8, DispositionType.Aggressive, new[] { strike, bolt })),
                new AutoBattleUnitSpec(
                    "enemy-warden",
                    CombatTeam.Enemy,
                    CharacterDef.CreateRuntime("sim-enemy-warden", "Enemy Warden", 28, 1, 2, 5, DispositionType.Protective, new[] { strike, rally })),
                new AutoBattleUnitSpec(
                    "enemy-glass",
                    CombatTeam.Enemy,
                    CharacterDef.CreateRuntime("sim-enemy-glass", "Enemy Glass", 18, 4, 0, 7, DispositionType.Aggressive, new[] { strike, bolt }))
            };

            return new AutoBattleScenario(8, 5, playerUnits, enemyUnits);
        }
    }
}
