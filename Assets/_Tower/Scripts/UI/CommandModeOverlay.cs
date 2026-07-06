using System;
using System.Collections.Generic;
using Tower.Combat;
using Tower.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tower.UI
{
    // T19: bullet-time command overlay. Shown while command mode is active:
    // a clear mode banner ("지휘 중 — Space로 해제"), per-ally markers above the
    // unit tokens showing the pending ability, and a click-to-open slot popup
    // (1-4) whose cooling entries are grayed with the remaining rounds.
    // Selecting an entry calls TurnEngine.SetPendingAbility; failures surface
    // as a short toast. Focus-drag targeting is out of scope - hint only.
    internal sealed class CommandModeOverlay : MonoBehaviour
    {
        private const float ToastSeconds = 2.5f;
        private const float MarkerWidth = 190f;
        private const float MarkerHeight = 32f;
        private const float MarkerWorldHeight = 1.7f;

        private sealed class AllyMarker
        {
            public UnitToken Token;
            public GameObject Root;
            public RectTransform Rect;
            public Text Label;
        }

        private readonly List<AllyMarker> markers = new List<AllyMarker>();
        private TurnEngine engine;
        private Camera viewCamera;
        private Action<string> log;
        private Text toastText;
        private Image toastBackground;
        private float toastHideAt;
        private RectTransform popupPanel;
        private string popupUnitId;
        private string popupActiveUnitId;

        public static CommandModeOverlay Create(
            TurnEngine engine,
            Camera viewCamera,
            IReadOnlyList<UnitToken> allyTokens,
            Action<string> log)
        {
            var canvas = RuntimeSceneUi.CreateCanvas("Command Canvas");
            canvas.sortingOrder = 40;
            var overlay = canvas.gameObject.AddComponent<CommandModeOverlay>();
            overlay.engine = engine;
            overlay.viewCamera = viewCamera;
            overlay.log = log;
            overlay.Build(allyTokens ?? Array.Empty<UnitToken>());
            overlay.gameObject.SetActive(false);
            return overlay;
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            ClosePopup();
            HideToast();
            gameObject.SetActive(false);
        }

        private void Build(IReadOnlyList<UnitToken> allyTokens)
        {
            // Mode banner: unmistakable enter/exit indicator (top center).
            var banner = CreateBox("Command Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -34f), new Vector2(460f, 48f), new Color(0.10f, 0.07f, 0.16f, 0.92f));
            var bannerText = RuntimeSceneUi.AddText(banner.transform, "Banner Label", "지휘 중 — Space로 해제", 22, TextAnchor.MiddleCenter);
            Stretch(bannerText.rectTransform);
            bannerText.color = new Color(1f, 0.85f, 0.4f, 1f);

            // UX rule 3: focus-drag targeting is not in this slice - gray hint.
            var hint = CreateBox("Focus Drag Hint", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -74f), new Vector2(300f, 26f), new Color(0f, 0f, 0f, 0.35f));
            var hintText = RuntimeSceneUi.AddText(hint.transform, "Hint Label", "점사 타겟 드래그 (준비 중)", 14, TextAnchor.MiddleCenter);
            Stretch(hintText.rectTransform);
            hintText.color = new Color(0.55f, 0.55f, 0.58f, 1f);

            // Toast: SetPendingAbility failure reasons (bottom center).
            var toast = CreateBox("Command Toast", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 96f), new Vector2(520f, 40f), new Color(0.35f, 0.09f, 0.09f, 0.92f));
            toastBackground = toast.GetComponent<Image>();
            toastText = RuntimeSceneUi.AddText(toast.transform, "Toast Label", string.Empty, 16, TextAnchor.MiddleCenter);
            Stretch(toastText.rectTransform);
            toast.SetActive(false);

            foreach (var token in allyTokens)
            {
                if (token == null)
                {
                    continue;
                }

                markers.Add(CreateMarker(token));
            }
        }

        private AllyMarker CreateMarker(UnitToken token)
        {
            var root = new GameObject("Pending Marker " + token.OccupantId);
            root.transform.SetParent(transform, false);

            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(MarkerWidth, MarkerHeight);

            var image = root.AddComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            var unitId = token.OccupantId;
            button.onClick.AddListener(() => OpenPopup(unitId));

            var label = RuntimeSceneUi.AddText(root.transform, "Label", unitId, 14, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.raycastTarget = false;

            return new AllyMarker { Token = token, Root = root, Rect = rect, Label = label };
        }

        private void LateUpdate()
        {
            if (engine == null || viewCamera == null)
            {
                return;
            }

            if (toastText != null && toastBackground != null && toastBackground.gameObject.activeSelf && Time.unscaledTime >= toastHideAt)
            {
                HideToast();
            }

            var activeUnitId = engine.CurrentTurn == null ? string.Empty : engine.CurrentTurn.UnitId;
            if (popupPanel != null && !StringComparer.Ordinal.Equals(popupActiveUnitId, activeUnitId))
            {
                // The turn moved on under the popup; its options are stale.
                ClosePopup();
            }

            foreach (var marker in markers)
            {
                if (marker.Token == null || !marker.Token.gameObject.activeSelf
                    || !engine.IsAlive(marker.Token.OccupantId))
                {
                    marker.Root.SetActive(false);
                    continue;
                }

                var world = marker.Token.transform.position + (Vector3.up * MarkerWorldHeight);
                var screen = viewCamera.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                {
                    marker.Root.SetActive(false);
                    continue;
                }

                marker.Root.SetActive(true);
                marker.Rect.position = new Vector3(screen.x, screen.y, 0f);
                RefreshMarkerLabel(marker, activeUnitId);
            }
        }

        private void RefreshMarkerLabel(AllyMarker marker, string activeUnitId)
        {
            var unitId = marker.Token.OccupantId;
            if (StringComparer.Ordinal.Equals(unitId, activeUnitId))
            {
                var pendingName = ResolveAbilityName(unitId, engine.PendingAbilityId);
                marker.Label.text = unitId + " ▶ " + (pendingName ?? "예비 없음");
                marker.Label.color = new Color(1f, 0.9f, 0.55f, 1f);
            }
            else
            {
                marker.Label.text = unitId + " · 대기";
                marker.Label.color = new Color(0.8f, 0.84f, 0.88f, 1f);
            }
        }

        private string ResolveAbilityName(string unitId, string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return null;
            }

            var combatant = engine.GetCombatant(unitId);
            if (combatant == null)
            {
                return abilityId;
            }

            foreach (var ability in combatant.State.Loadout.Abilities)
            {
                if (ability != null && StringComparer.Ordinal.Equals(ability.Id, abilityId))
                {
                    return ability.DisplayName;
                }
            }

            return abilityId;
        }

        private void OpenPopup(string unitId)
        {
            ClosePopup();

            var combatant = engine.GetCombatant(unitId);
            if (combatant == null)
            {
                ShowToast("Unknown combatant.");
                return;
            }

            popupUnitId = unitId;
            popupActiveUnitId = engine.CurrentTurn == null ? string.Empty : engine.CurrentTurn.UnitId;
            var isActiveUnit = StringComparer.Ordinal.Equals(unitId, popupActiveUnitId);
            var options = PendingAbilityBinding.BuildOptions(
                combatant.State,
                isActiveUnit ? engine.PendingAbilityId : null);

            var panelObject = new GameObject("Pending Popup");
            panelObject.transform.SetParent(transform, false);
            popupPanel = panelObject.AddComponent<RectTransform>();
            popupPanel.anchorMin = new Vector2(0.5f, 0.5f);
            popupPanel.anchorMax = new Vector2(0.5f, 0.5f);
            popupPanel.pivot = new Vector2(0.5f, 0.5f);
            popupPanel.sizeDelta = new Vector2(320f, 72f + (options.Count * 52f));
            popupPanel.anchoredPosition = new Vector2(240f, 0f);

            var background = panelObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.97f);

            var layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            RuntimeSceneUi.AddText(panelObject.transform, "Popup Title", unitId + " — 예비 능력 교체", 16, TextAnchor.MiddleLeft);

            foreach (var option in options)
            {
                var abilityId = option.AbilityId;
                var button = RuntimeSceneUi.AddButton(
                    panelObject.transform,
                    PendingAbilityBinding.FormatOptionLabel(option),
                    () => OnOptionClicked(abilityId));
                if (!option.IsSelectable)
                {
                    button.interactable = false;
                    var text = button.GetComponentInChildren<Text>();
                    if (text != null)
                    {
                        text.color = new Color(0.5f, 0.5f, 0.52f, 1f);
                    }
                }
            }

            RuntimeSceneUi.AddButton(panelObject.transform, "닫기", ClosePopup);
        }

        private void OnOptionClicked(string abilityId)
        {
            if (string.IsNullOrEmpty(popupUnitId))
            {
                return;
            }

            var result = engine.SetPendingAbility(popupUnitId, abilityId);
            if (result.IsFailure)
            {
                ShowToast(result.Error);
                return;
            }

            log?.Invoke("예비 능력 교체: " + popupUnitId + " -> " + abilityId);
            ClosePopup();
        }

        private void ClosePopup()
        {
            if (popupPanel != null)
            {
                Destroy(popupPanel.gameObject);
                popupPanel = null;
            }

            popupUnitId = null;
            popupActiveUnitId = null;
        }

        private void ShowToast(string message)
        {
            if (toastText == null || toastBackground == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            toastText.text = message;
            toastBackground.gameObject.SetActive(true);
            toastHideAt = Time.unscaledTime + ToastSeconds;
        }

        private void HideToast()
        {
            if (toastBackground != null)
            {
                toastBackground.gameObject.SetActive(false);
            }
        }

        private GameObject CreateBox(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var box = new GameObject(name);
            box.transform.SetParent(transform, false);
            var rect = box.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMin.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = box.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return box;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 2f);
            rect.offsetMax = new Vector2(-8f, -2f);
        }
    }
}
