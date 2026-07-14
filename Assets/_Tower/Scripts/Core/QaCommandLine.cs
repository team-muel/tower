using System;
using System.Globalization;

namespace Tower.Core
{
    // Command line activation parsing for the dev-only QA features.
    public static class QaCommandLine
    {
        public const string QaPortArg = "-qaPort";
        public const string DevCameraArg = "-devcam";
        public const string AutoEncounterArg = "-qaAutoEncounter";
        public const int MinPort = 1;
        public const int MaxPort = 65535;

        public static bool TryGetQaPort(string[] args, out int port)
        {
            port = 0;
            if (args == null)
            {
                return false;
            }

            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], QaPortArg, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    && parsed >= MinPort
                    && parsed <= MaxPort)
                {
                    port = parsed;
                    return true;
                }

                return false;
            }

            return false;
        }

        public static bool HasDevCameraFlag(string[] args)
        {
            return HasFlag(args, DevCameraArg);
        }

        public static bool HasAutoEncounterFlag(string[] args)
        {
            return HasFlag(args, AutoEncounterArg);
        }

        public static bool HasFlag(string[] args, string flag)
        {
            if (args == null || string.IsNullOrEmpty(flag))
            {
                return false;
            }

            foreach (var arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
