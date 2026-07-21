using System;

namespace Tower.Core
{
    /// <summary>
    /// Player-authored, always-on companion posture. Disposition remains the
    /// companion's personality; stance is the current commander's bias.
    /// </summary>
    public enum CommandStance
    {
        Assault = 0,
        Guard = 1,
        Focus = 2
    }

    public readonly struct CommandStanceAssignment
    {
        public CommandStanceAssignment(CommandStance stance, string focusTargetId)
        {
            Stance = stance;
            FocusTargetId = focusTargetId ?? string.Empty;
        }

        public CommandStance Stance { get; }
        public string FocusTargetId { get; }
    }

    public static class CommandStanceRules
    {
        public static CommandStance DefaultFor(DispositionType disposition)
        {
            return disposition == DispositionType.Protective
                ? CommandStance.Guard
                : CommandStance.Assault;
        }

        public static string DisplayName(CommandStance stance)
        {
            switch (stance)
            {
                case CommandStance.Assault:
                    return "Assault";
                case CommandStance.Guard:
                    return "Guard";
                case CommandStance.Focus:
                    return "Focus";
                default:
                    throw new ArgumentOutOfRangeException(nameof(stance), stance, null);
            }
        }
    }
}
