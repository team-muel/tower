using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // Provisional result banner. T60 owns final UI composition and styling.
    public sealed class EncounterResultPresenter : MonoBehaviour
    {
        private float remainingSeconds;
        private GUIStyle headlineStyle;
        private GUIStyle detailStyle;

        public bool IsVisible => remainingSeconds > 0f;
        public string Headline { get; private set; } = string.Empty;
        public string Detail { get; private set; } = string.Empty;

        public Result Present(
            GeneratedEncounterResult combatResult,
            EncounterReward reward,
            int completedEvents,
            int totalEvents,
            float visibleSeconds = 3f)
        {
            if (combatResult == null || reward == null)
            {
                return Result.Failure("Encounter result presentation requires combat and reward results.");
            }

            if (combatResult.WinningTeam != CombatTeam.Player)
            {
                return Result.Failure("Only player victory can present a completed encounter reward.");
            }

            if (completedEvents < 1 || totalEvents < completedEvents || visibleSeconds <= 0f)
            {
                return Result.Failure("Encounter result presentation values are invalid.");
            }

            Headline = $"VICTORY  {completedEvents}/{totalEvents}";
            Detail = $"{reward.DisplayName} +{reward.Amount}   |   "
                + $"{combatResult.ActionCount} actions / {combatResult.DurationSeconds:0.0}s";
            remainingSeconds = visibleSeconds;
            return Result.Success();
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (!IsVisible || unscaledDeltaSeconds <= 0f
                || float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds))
            {
                return;
            }

            remainingSeconds = Mathf.Max(0f, remainingSeconds - unscaledDeltaSeconds);
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (!IsVisible)
            {
                return;
            }

            EnsureStyles();
            const float width = 560f;
            const float height = 112f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, 34f, width, height);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 12f, width - 32f, 40f), Headline, headlineStyle);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 58f, width - 32f, 34f), Detail, detailStyle);
        }

        private void EnsureStyles()
        {
            if (headlineStyle != null)
            {
                return;
            }

            headlineStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.86f, 0.38f) }
            };
            detailStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                normal = { textColor = Color.white }
            };
        }
    }
}
