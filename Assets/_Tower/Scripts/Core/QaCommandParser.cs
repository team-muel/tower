using System;

namespace Tower.Core
{
    // Line protocol: "press <buttonName>", "state", "scene <name>", "quit".
    // Verbs are case-insensitive; arguments keep their original casing and may
    // contain spaces (button GameObject names do).
    public static class QaCommandParser
    {
        public static Result<QaCommand> Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return Result<QaCommand>.Failure("Empty command line.");
            }

            var trimmed = line.Trim();
            var separatorIndex = trimmed.IndexOf(' ');
            var verb = separatorIndex < 0 ? trimmed : trimmed.Substring(0, separatorIndex);
            var argument = separatorIndex < 0 ? string.Empty : trimmed.Substring(separatorIndex + 1).Trim();

            if (IsVerb(verb, "press"))
            {
                return WithRequiredArgument(QaCommandKind.Press, argument, "press requires a button name.");
            }

            if (IsVerb(verb, "scene"))
            {
                return WithRequiredArgument(QaCommandKind.Scene, argument, "scene requires a scene name.");
            }

            if (IsVerb(verb, "state"))
            {
                return WithoutArgument(QaCommandKind.State, argument, "state takes no argument.");
            }

            if (IsVerb(verb, "quit"))
            {
                return WithoutArgument(QaCommandKind.Quit, argument, "quit takes no argument.");
            }

            return Result<QaCommand>.Failure($"Unknown command '{verb}'. Expected press/state/scene/quit.");
        }

        private static bool IsVerb(string candidate, string verb)
        {
            return string.Equals(candidate, verb, StringComparison.OrdinalIgnoreCase);
        }

        private static Result<QaCommand> WithRequiredArgument(QaCommandKind kind, string argument, string missingError)
        {
            return string.IsNullOrEmpty(argument)
                ? Result<QaCommand>.Failure(missingError)
                : Result<QaCommand>.Success(new QaCommand(kind, argument));
        }

        private static Result<QaCommand> WithoutArgument(QaCommandKind kind, string argument, string extraError)
        {
            return string.IsNullOrEmpty(argument)
                ? Result<QaCommand>.Success(new QaCommand(kind, string.Empty))
                : Result<QaCommand>.Failure(extraError);
        }
    }
}
