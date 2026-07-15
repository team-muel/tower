using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // T66: full-screen fade + banner for run transitions (retreat, great
    // regression, conquest). Unscaled time so it reads through bullet-time.
    // Provisional presentation; final styling belongs to the art pass.
    public sealed class RunTransitionPresenter : MonoBehaviour
    {
        private const float FadeInSeconds = 0.35f;
        private const float FadeOutSeconds = 0.6f;

        private float remainingSeconds;
        private float totalSeconds;
        private GUIStyle headlineStyle;
        private GUIStyle subStyle;

        public bool IsVisible => remainingSeconds > 0f;
        public string Headline { get; private set; } = string.Empty;
        public string Subline { get; private set; } = string.Empty;

        public Result Show(string headline, string subline, float visibleSeconds = 2.6f)
        {
            if (string.IsNullOrWhiteSpace(headline) || visibleSeconds <= 0f)
            {
                return Result.Failure("Run transition needs a headline and positive duration.");
            }

            Headline = headline;
            Subline = subline ?? string.Empty;
            totalSeconds = visibleSeconds;
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

        // 0..1 overlay opacity: quick fade in, hold, slow fade out.
        public float CurrentAlpha()
        {
            if (!IsVisible)
            {
                return 0f;
            }

            float elapsed = totalSeconds - remainingSeconds;
            if (elapsed < FadeInSeconds)
            {
                return Mathf.Clamp01(elapsed / FadeInSeconds);
            }

            if (remainingSeconds < FadeOutSeconds)
            {
                return Mathf.Clamp01(remainingSeconds / FadeOutSeconds);
            }

            return 1f;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            float alpha = CurrentAlpha();
            if (alpha <= 0f)
            {
                return;
            }

            EnsureStyles();
            GUI.color = new Color(0f, 0f, 0f, 0.82f * alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(
                new Rect(0f, (Screen.height * 0.5f) - 44f, Screen.width, 52f),
                Headline,
                headlineStyle);
            if (!string.IsNullOrEmpty(Subline))
            {
                GUI.Label(
                    new Rect(0f, (Screen.height * 0.5f) + 10f, Screen.width, 30f),
                    Subline,
                    subStyle);
            }

            GUI.color = Color.white;
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
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
        }
    }
}
