using System;
using System.Collections.Generic;
using Tower.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tower.UI
{
    // T23: initiative / turn-order ribbon. A horizontal strip on a screen edge
    // that makes "whose turn is it / who's next" instantly readable for large
    // parties. Reads a pure ribbon snapshot (InitiativeRibbonModel) and renders
    // color-coded tokens: the current actor pulses + scales + gets an outline,
    // the next actor gets a soft outline, dead units dim.
    //
    // Display-only. No Physics, no Time.timeScale — the pulse is driven off
    // unscaled time so it never touches the sim clock or determinism.
    public sealed class InitiativeRibbonController : MonoBehaviour
    {
        private const float PulsePeriod = 0.9f;
        private const float PulseMinScale = 1.0f;
        private const float PulseMaxScale = 1.18f;
        private const float TokenWidth = 46f;
        private const float TokenHeight = 46f;
        private const float TokenSpacing = 6f;

        private RectTransform _content;
        private Func<string, CombatTeam, Color> _colorLookup;
        private readonly List<TokenView> _tokens = new List<TokenView>();
        private RectTransform _currentPulseTarget;

        private sealed class TokenView
        {
            public RectTransform Rect;
            public Image Background;
            public Image Outline;
            public Text Label;
            public bool IsCurrent;
        }

        /// <summary>
        /// Builds the ribbon under an existing HUD canvas root, anchored to the
        /// top edge so it never covers the 3D viewport. <paramref name="colorLookup"/>
        /// resolves a unit's team color (the controller supplies the same
        /// palette the tokens/HUD already use).
        /// </summary>
        public static InitiativeRibbonController Create(Transform parent, Func<string, CombatTeam, Color> colorLookup)
        {
            var host = new GameObject("InitiativeRibbon");
            host.transform.SetParent(parent, false);

            var rect = host.AddComponent<RectTransform>();
            // Top edge, centered, just under the top bar. Narrow band → viewport free.
            rect.anchorMin = new Vector2(0.12f, 0.88f);
            rect.anchorMax = new Vector2(0.88f, 0.945f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backdrop = host.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.07f, 0.42f);
            backdrop.raycastTarget = false;

            var contentObject = new GameObject("Content");
            contentObject.transform.SetParent(host.transform, false);
            var content = contentObject.AddComponent<RectTransform>();
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(8f, 4f);
            content.offsetMax = new Vector2(-8f, -4f);

            var layout = contentObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = TokenSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var controller = host.AddComponent<InitiativeRibbonController>();
            controller._content = content;
            controller._colorLookup = colorLookup ?? ((_, team) => team == CombatTeam.Enemy
                ? new Color(0.7f, 0.2f, 0.22f, 1f)
                : new Color(0.25f, 0.85f, 0.5f, 1f));
            return controller;
        }

        /// <summary>
        /// Rebuilds the ribbon from the current turn-order snapshot. Dead units
        /// are kept in place and dimmed. Safe to call every turn handoff.
        /// </summary>
        public void Refresh(
            IReadOnlyList<string> roundOrder,
            string currentUnitId,
            Func<string, bool> isAlive,
            Func<string, CombatTeam> teamOf)
        {
            var items = InitiativeRibbonModel.Build(roundOrder, currentUnitId, isAlive, teamOf, includeDead: true);
            RenderItems(items);
        }

        /// <summary>Hides the ribbon (e.g. outside combat).</summary>
        public void Clear()
        {
            RenderItems(Array.Empty<InitiativeRibbonItem>());
        }

        private void RenderItems(IReadOnlyList<InitiativeRibbonItem> items)
        {
            for (int index = _tokens.Count - 1; index >= 0; index--)
            {
                if (_tokens[index].Rect != null)
                {
                    Destroy(_tokens[index].Rect.gameObject);
                }
            }

            _tokens.Clear();
            _currentPulseTarget = null;

            if (_content == null || items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                var token = BuildToken(item);
                _tokens.Add(token);
                if (item.IsCurrent)
                {
                    _currentPulseTarget = token.Rect;
                }
            }
        }

        private TokenView BuildToken(InitiativeRibbonItem item)
        {
            var tokenObject = new GameObject("Token_" + item.UnitId);
            tokenObject.transform.SetParent(_content, false);

            var rect = tokenObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(TokenWidth, TokenHeight);

            var layoutElement = tokenObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = TokenWidth;
            layoutElement.preferredHeight = TokenHeight;

            // Outline sits behind the background and is grown to form a border
            // ring for the current/next actor.
            var outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(tokenObject.transform, false);
            var outlineRect = outlineObject.AddComponent<RectTransform>();
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = new Vector2(-4f, -4f);
            outlineRect.offsetMax = new Vector2(4f, 4f);
            var outline = outlineObject.AddComponent<Image>();
            outline.raycastTarget = false;

            var background = tokenObject.AddComponent<Image>();
            background.raycastTarget = false;

            var teamColor = _colorLookup(item.UnitId, item.Team);
            if (item.IsDead)
            {
                // Dim dead units heavily; still show their slot for continuity.
                teamColor = new Color(teamColor.r * 0.35f, teamColor.g * 0.35f, teamColor.b * 0.35f, 0.45f);
            }

            background.color = teamColor;

            // Outline styling: strong bright ring for current, soft for next,
            // otherwise invisible.
            if (item.IsCurrent)
            {
                outline.color = new Color(1f, 0.95f, 0.55f, 1f);
            }
            else if (item.IsNext)
            {
                outline.color = new Color(0.9f, 0.9f, 0.95f, 0.7f);
            }
            else
            {
                outline.color = new Color(0f, 0f, 0f, 0f);
            }

            var label = BuildLabel(tokenObject.transform, InitialFor(item.UnitId), item.IsDead);

            return new TokenView
            {
                Rect = rect,
                Background = background,
                Outline = outline,
                Label = label,
                IsCurrent = item.IsCurrent
            };
        }

        private static Text BuildLabel(Transform parent, string value, bool isDead)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            var labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = value;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = isDead ? new Color(0.85f, 0.85f, 0.85f, 0.5f) : new Color(0.05f, 0.05f, 0.07f, 1f);
            text.raycastTarget = false;
            return text;
        }

        // A short readable token from the unit id: uppercase leading letters
        // plus any trailing digit so "companion-2" reads as "C2".
        private static string InitialFor(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return "?";
            }

            var letter = char.ToUpperInvariant(unitId[0]);
            for (int index = unitId.Length - 1; index >= 0; index--)
            {
                if (char.IsDigit(unitId[index]))
                {
                    return string.Concat(letter, unitId[index]);
                }
            }

            return letter.ToString();
        }

        private void Update()
        {
            if (_currentPulseTarget == null)
            {
                return;
            }

            // Unscaled so the pulse never depends on (or perturbs) the sim clock.
            var phase = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / PulsePeriod)) + 1f) * 0.5f;
            var scale = Mathf.Lerp(PulseMinScale, PulseMaxScale, phase);
            _currentPulseTarget.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
