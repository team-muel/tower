using System.Collections.Generic;

namespace Tower.Core
{
    /// <summary>
    /// Data-like scoring modifiers for the commander's current posture. These
    /// are deliberately separate from DispositionWeights: personality explains
    /// why a companion prefers an action; stance explains the owner's current
    /// tactical bias.
    /// </summary>
    public sealed class CommandStanceWeights
    {
        public CommandStanceWeights(
            float damageMultiplier,
            float killBonus,
            float rangeKeepingMultiplier,
            float dangerMultiplier,
            float allyProtectionMultiplier,
            float focusTargetBonus)
        {
            DamageMultiplier = damageMultiplier;
            KillBonus = killBonus;
            RangeKeepingMultiplier = rangeKeepingMultiplier;
            DangerMultiplier = dangerMultiplier;
            AllyProtectionMultiplier = allyProtectionMultiplier;
            FocusTargetBonus = focusTargetBonus;
        }

        public float DamageMultiplier { get; }
        public float KillBonus { get; }
        public float RangeKeepingMultiplier { get; }
        public float DangerMultiplier { get; }
        public float AllyProtectionMultiplier { get; }
        public float FocusTargetBonus { get; }

        public static IReadOnlyDictionary<CommandStance, CommandStanceWeights> CreateDefaultTable()
        {
            return new Dictionary<CommandStance, CommandStanceWeights>
            {
                [CommandStance.Assault] = new CommandStanceWeights(
                    damageMultiplier: 1.2f,
                    killBonus: 15f,
                    rangeKeepingMultiplier: 0.8f,
                    dangerMultiplier: 0.65f,
                    allyProtectionMultiplier: 0.6f,
                    focusTargetBonus: 0f),
                [CommandStance.Guard] = new CommandStanceWeights(
                    damageMultiplier: 0.85f,
                    killBonus: 0f,
                    rangeKeepingMultiplier: 1.2f,
                    dangerMultiplier: 1.5f,
                    allyProtectionMultiplier: 1.6f,
                    focusTargetBonus: 0f),
                [CommandStance.Focus] = new CommandStanceWeights(
                    damageMultiplier: 1f,
                    killBonus: 5f,
                    rangeKeepingMultiplier: 1f,
                    dangerMultiplier: 1f,
                    allyProtectionMultiplier: 0.8f,
                    focusTargetBonus: 45f)
            };
        }
    }
}
