using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tower.EditorTools
{
    // T48 owner-preview lane (2026-07-13): the owner evaluates ONLY desktop-runnable
    // builds, so the _CombatSpike scene gets its own standalone exe. This deliberately
    // does NOT touch EditorBuildSettings / the canonical Prototype-only Build Settings;
    // the scene list is passed explicitly per build.
    public static class SpikeBuildRunner
    {
        public const string OutputPath = @"C:\Users\fancy\Tower\Builds\CombatSpike\TowerCombatSpike.exe";

        private static readonly string[] Scenes =
        {
            "Assets/_Tower/Scenes/_CombatSpike.unity"
        };

        public static void BuildCombatSpikeWindows64()
        {
            var directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[SpikeBuildRunner] Result={summary.result} Errors={summary.totalErrors} Warnings={summary.totalWarnings} Size={summary.totalSize} Output={OutputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"CombatSpike Windows64 build failed: {summary.result}");
            }
        }
    }
}
