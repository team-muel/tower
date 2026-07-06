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

        private static readonly string[] Scenes =
        {
            "Assets/_Tower/Scenes/Boot.unity",
            "Assets/_Tower/Scenes/Camp.unity",
            "Assets/_Tower/Scenes/Loadout.unity",
            "Assets/_Tower/Scenes/Expedition.unity"
        };

        public static void BuildWindows64()
        {
            PlayableSceneWiring.WireScenes();

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
