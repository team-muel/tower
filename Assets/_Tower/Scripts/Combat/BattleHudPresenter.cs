using System;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // v0 temporary presenter that logs turn events within the 5-second bound.
    public sealed class BattleHudPresenter : IActionPresenter
    {
        private readonly Action<string> logSink;

        public BattleHudPresenter(Action<string> logSink = null)
        {
            this.logSink = logSink;
        }

        // T19: presenter-local playback factor for bullet-time command mode.
        // 1 = normal cadence, smaller = slower presentation. Consumers (AI
        // step pacing, token animation) divide their delays by this factor;
        // global Time.timeScale is never touched.
        public float PlaybackFactor { get; set; } = 1f;

        // Mode line for the player-turn HUD (v0: routed to the same log sink).
        public void SetMode(string mode)
        {
            if (!string.IsNullOrEmpty(mode))
            {
                logSink?.Invoke("[MODE] " + mode);
            }
        }

        public void Present(TurnPresentationEvent presentationEvent, Action completion)
        {
            string message = presentationEvent.Type switch
            {
                TurnPresentationEventType.Move => $"{presentationEvent.UnitId} moved {presentationEvent.MoveDistance:0.##}",
                TurnPresentationEventType.Ability => $"{presentationEvent.UnitId} used {presentationEvent.AbilityId} -> {presentationEvent.TargetUnitId}",
                TurnPresentationEventType.Skip => $"{presentationEvent.UnitId} skipped",
                _ => $"{presentationEvent.UnitId} acted"
            };

            logSink?.Invoke(message);
            completion?.Invoke();
        }
    }
}
