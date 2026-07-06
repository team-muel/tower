using System;
using System.IO;
using UnityEngine;

namespace Tower.Core
{
    public static class SimCli
    {
        public const string DefaultOutputPath = @"C:\dev\_setup\sim-result.json";

        public static Result<SimCliResult> Run(string[] args)
        {
            var options = new AutoBattleOptions();
            var outputPath = DefaultOutputPath;
            ParseArgs(args ?? Array.Empty<string>(), options, ref outputPath);

            var simulator = new AutoBattleSimulator();
            var simulation = simulator.Run(options);
            if (simulation.IsFailure)
            {
                return Result<SimCliResult>.Failure(simulation.Error);
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, JsonUtility.ToJson(simulation.Value, true));
            return Result<SimCliResult>.Success(new SimCliResult(outputPath, simulation.Value));
        }

        private static void ParseArgs(string[] args, AutoBattleOptions options, ref string outputPath)
        {
            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (string.IsNullOrEmpty(arg))
                {
                    continue;
                }

                if (TryReadValue(args, ref index, arg, "-simOutput", "--sim-output", out var output))
                {
                    outputPath = output;
                    continue;
                }

                if (TryReadValue(args, ref index, arg, "-simSeed", "--sim-seed", out var seedText)
                    && int.TryParse(seedText, out var seed))
                {
                    options.seed = seed;
                    continue;
                }

                if (TryReadValue(args, ref index, arg, "-simBattles", "--sim-battles", out var battlesText)
                    && int.TryParse(battlesText, out var battles))
                {
                    options.battles = battles;
                    continue;
                }

                if (TryReadValue(args, ref index, arg, "-simMaxRounds", "--sim-max-rounds", out var maxRoundsText)
                    && int.TryParse(maxRoundsText, out var maxRounds))
                {
                    options.maxRounds = maxRounds;
                    continue;
                }

                // T20: battlefield space selector ("grid" | "analog").
                if (TryReadValue(args, ref index, arg, "-simSpace", "--sim-space", out var spaceText))
                {
                    if (string.Equals(spaceText, "grid", StringComparison.OrdinalIgnoreCase))
                    {
                        options.spaceMode = CombatSpaceMode.Grid;
                    }
                    else if (string.Equals(spaceText, "analog", StringComparison.OrdinalIgnoreCase))
                    {
                        options.spaceMode = CombatSpaceMode.Analog;
                    }
                }
            }
        }

        private static bool TryReadValue(
            string[] args,
            ref int index,
            string arg,
            string shortName,
            string longName,
            out string value)
        {
            value = null;
            if (StringComparer.Ordinal.Equals(arg, shortName)
                || StringComparer.Ordinal.Equals(arg, longName))
            {
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                index++;
                value = args[index];
                return true;
            }

            var longPrefix = longName + "=";
            if (arg.StartsWith(longPrefix, StringComparison.Ordinal))
            {
                value = arg.Substring(longPrefix.Length);
                return true;
            }

            return false;
        }
    }
}
