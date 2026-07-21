using Tower.Core;
using UnityEngine;

namespace Tower.Floor
{
    // T62 completion harness (build-only QA seam, activated by -qaAutoRun).
    // Auto-plays an entire run through the REAL pipeline: enters each
    // scheduled encounter (detection, intro hold, combat, victory resolution
    // are never bypassed), then advances the floor. Defeat is left to the
    // run lifecycle's retreat path, which this driver simply keeps playing.
    public sealed class QaAutoRunDriver : MonoBehaviour
    {
        private const float SettleSeconds = 1.0f;

        private ForestFloorRenderer floorRenderer;
        private float settleTimer;
        private bool completionLogged;

        public void Configure(ForestFloorRenderer target)
        {
            floorRenderer = target;
        }

        private void Update()
        {
            if (floorRenderer == null || floorRenderer.RunLifecycle == null)
            {
                return;
            }

            RunLifecycle run = floorRenderer.RunLifecycle;
            if (run.IsConquered)
            {
                if (!completionLogged)
                {
                    completionLogged = true;
                    Debug.Log(
                        $"[QaAutoRun] Run conquered: events {run.Progress.CompletedCount}"
                        + $"/{run.Progress.Plan.Slots.Count}, retreats {run.RetreatCount}; "
                        + "harness complete.",
                        this);
                }

                return;
            }

            if (floorRenderer.IsResultBlocking)
            {
                settleTimer = 0f;
                return; // let the victory result finish before advancing
            }

            if (floorRenderer.IsTransitionBlocking)
            {
                settleTimer = 0f;
                return; // let retreat/conquest transition finish before advancing
            }

            var encounter = floorRenderer.ActiveEncounter;
            if (encounter != null && !encounter.IsResolved && !encounter.IsPlayerDefeated)
            {
                settleTimer = 0f;
                return; // real combat in progress
            }

            settleTimer += Time.deltaTime;
            if (settleTimer < SettleSeconds)
            {
                return;
            }

            settleTimer = 0f;
            if (run.CurrentFloorHasPendingEvent)
            {
                if (!floorRenderer.QaEnterScheduledEncounter())
                {
                    Debug.LogWarning("[QaAutoRun] Could not enter the scheduled encounter.", this);
                }

                return;
            }

            Result<RunOutcome> advanced = floorRenderer.AdvanceRunFloor();
            if (advanced.IsSuccess)
            {
                Debug.Log($"[QaAutoRun] {advanced.Value}; floor={run.FloorNumber}.", this);
            }
        }
    }
}
