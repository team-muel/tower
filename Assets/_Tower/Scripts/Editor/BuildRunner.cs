using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tower.EditorTools
{
    public static class BuildRunner
    {
        public const string OutputPath = @"C:\Users\fancy\Tower\Builds\Tower\Tower.exe";

        // 2026-07-12: repointed to Prototype (the single playable scene / live Build Settings).
        // Old Boot/Camp/Loadout/Expedition menu-slice scenes are deprecated; WireScenes()
        // regenerated them empty and clobbered EditorBuildSettings, so it is no longer called.
        // The runtime URP-Lit magenta guard (TowerRuntimeLit.mat) already lives in Resources.
        // T49: demolition removed the old code-generated Boot/Camp/Loadout/Expedition
        // slice (PlayableSceneWiring + Tower.UI) outright; Prototype remains the single entry.
        private static readonly string[] Scenes =
        {
            "Assets/_Tower/Scenes/Prototype.unity"
        };

        public static void BuildWindows64()
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
            Debug.Log($"[BuildRunner] Result={summary.result} Errors={summary.totalErrors} Warnings={summary.totalWarnings} Size={summary.totalSize} Output={OutputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Windows64 build failed: {summary.result}");
            }
        }
    }
}
