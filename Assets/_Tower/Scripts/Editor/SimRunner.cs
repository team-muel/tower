using System;
using Tower.Core;
using UnityEditor;
using UnityEngine;

namespace Tower.EditorTools
{
    public static class SimRunner
    {
        public static void RunDefault()
        {
            var result = SimCli.Run(Environment.GetCommandLineArgs());
            if (result.IsFailure)
            {
                Debug.LogError("Tower sim failed: " + result.Error);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log(
                $"Tower sim wrote {result.Value.OutputPath}: " +
                $"{result.Value.Simulation.battles} battles, " +
                $"{result.Value.Simulation.playerWinRate:P0} player win rate, " +
                $"{result.Value.Simulation.averageRounds:0.00} avg rounds, " +
                $"{result.Value.Simulation.guardedBattles} guarded.");
        }
    }
}
