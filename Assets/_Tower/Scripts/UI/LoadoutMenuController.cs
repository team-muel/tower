using System.Collections;
using System.Collections.Generic;
using Tower.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class LoadoutMenuController : MonoBehaviour
    {
        private readonly Dictionary<string, Text> statsLines = new Dictionary<string, Text>();
        private readonly Dictionary<string, RectTransform> entryTransforms = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, Button> upButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> downButtons = new Dictionary<string, Button>();
        private readonly List<string> qaButtonNames = new List<string>();
        private TowerSliceContent content;
        private RuntimeTooltipView tooltip;
        private Button startButton;
        private Button backButton;
        private Text departureStatus;
        private bool departing;

        private void Start()
        {
            content = TowerSliceContent.Create();
            BuildLoadout();
        }

        private void OnDestroy()
        {
            foreach (var name in qaButtonNames)
            {
                QaRuntime.UnregisterButton(name);
            }

            qaButtonNames.Clear();
        }

        private void BuildLoadout()
        {
            RuntimeSceneUi.EnsureClearCamera();
            var canvas = RuntimeSceneUi.CreateCanvas("Loadout Canvas");
            // T14: the default 800x600 scaler reference blows text up ~2x at
            // 1080p and pushes the bottom bar off the panel. Anchor at 720p.
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            tooltip = RuntimeTooltipView.Create(canvas.transform);

            var panel = RuntimeSceneUi.CreatePanel(
                canvas.transform,
                "Loadout",
                new Vector2(0.14f, 0.04f),
                new Vector2(0.86f, 0.96f),
                Vector2.zero,
                Vector2.zero);

            RuntimeSceneUi.AddText(panel, "Title", "원정 준비 (Loadout)", 30, TextAnchor.MiddleCenter);
            RuntimeSceneUi.AddText(
                panel,
                "Summary",
                "회귀자 + 동료 3 — 고정 파티 <color=#9E9E9E>(영입은 준비 중)</color>",
                18,
                TextAnchor.MiddleCenter);
            RuntimeSceneUi.AddText(
                panel,
                "Chain Hint",
                "행동 순서를 정하고 출발 — 순서가 연계를 만든다 · 능력 위에 마우스를 올리면 상세 툴팁이 보입니다.",
                14,
                TextAnchor.MiddleCenter);

            var listContent = CreatePartyScrollList(panel);
            var chain = TowerSliceContent.GetLoadoutChain();
            foreach (var id in chain)
            {
                AddMemberEntry(listContent, id);
            }

            BuildBottomBar(panel);
            Refresh();
        }

        // uGUI ScrollRect so all four party entries stay reachable even on
        // short windows; the bottom bar lives outside it and never scrolls.
        private RectTransform CreatePartyScrollList(Transform parent)
        {
            var scrollObject = new GameObject("Party Scroll View");
            scrollObject.transform.SetParent(parent, false);
            var background = scrollObject.AddComponent<Image>();
            background.color = new Color(0.10f, 0.12f, 0.15f, 0.85f);

            var layoutElement = scrollObject.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
            layoutElement.minHeight = 160f;

            var scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Party List Content");
            contentObject.transform.SetParent(viewport.transform, false);
            var contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 10f;
            contentLayout.padding = new RectOffset(12, 12, 12, 12);
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandHeight = false;
            contentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            return contentRect;
        }

        private void AddMemberEntry(Transform parent, string characterId)
        {
            var definition = content.Characters[characterId];

            var entryObject = new GameObject(characterId + " Entry");
            entryObject.transform.SetParent(parent, false);
            entryObject.AddComponent<Image>().color = new Color(0.16f, 0.19f, 0.24f, 0.9f);
            var entryLayout = entryObject.AddComponent<VerticalLayoutGroup>();
            entryLayout.spacing = 4f;
            entryLayout.padding = new RectOffset(12, 12, 10, 10);
            entryLayout.childControlWidth = true;
            entryLayout.childForceExpandWidth = true;
            entryLayout.childControlHeight = true;
            entryLayout.childForceExpandHeight = false;

            entryTransforms[characterId] = entryObject.GetComponent<RectTransform>();

            statsLines[characterId] = RuntimeSceneUi.AddText(
                entryObject.transform,
                characterId + " Stats",
                string.Empty,
                17,
                TextAnchor.MiddleLeft);

            var controls = new GameObject(characterId + " Chain Controls");
            controls.transform.SetParent(entryObject.transform, false);
            var controlsLayout = controls.AddComponent<HorizontalLayoutGroup>();
            controlsLayout.spacing = 8f;
            controlsLayout.childAlignment = TextAnchor.MiddleLeft;
            controlsLayout.childControlWidth = true;
            controlsLayout.childForceExpandWidth = false;
            controlsLayout.childControlHeight = true;
            controlsLayout.childForceExpandHeight = false;
            controls.AddComponent<LayoutElement>().minHeight = 46f;

            var up = RuntimeSceneUi.AddButton(controls.transform, "▲", () => MoveChain(characterId, -1));
            up.gameObject.name = characterId + " Up Button";
            up.GetComponent<LayoutElement>().minWidth = 110f;
            RegisterQaButton(up);
            upButtons[characterId] = up;

            var down = RuntimeSceneUi.AddButton(controls.transform, "▼", () => MoveChain(characterId, 1));
            down.gameObject.name = characterId + " Down Button";
            down.GetComponent<LayoutElement>().minWidth = 110f;
            RegisterQaButton(down);
            downButtons[characterId] = down;

            RuntimeSceneUi.AddText(entryObject.transform, characterId + " Slots Header", "능력 슬롯", 13, TextAnchor.MiddleLeft);
            foreach (var ability in definition.DefaultAbilities)
            {
                // Badge + tooltip text come straight from AbilityDef data;
                // per-tag styling lives in Tower.Core.AbilityDisplayText.
                var slotLine = RuntimeSceneUi.AddText(
                    entryObject.transform,
                    characterId + " " + ability.Id + " Slot",
                    "· " + AbilityDisplayText.BuildAbilityLine(ability.DisplayName, ability.Tag),
                    15,
                    TextAnchor.MiddleLeft);
                TooltipTrigger.Attach(
                    slotLine.gameObject,
                    tooltip,
                    AbilityDisplayText.BuildTooltip(
                        ability.DisplayName,
                        ability.Tag,
                        ability.BasePower,
                        ability.Range,
                        ability.AmplificationMultiplier));
            }
        }

        private void BuildBottomBar(RectTransform panel)
        {
            departureStatus = RuntimeSceneUi.AddText(panel, "Departure Status", string.Empty, 15, TextAnchor.MiddleCenter);

            var bar = new GameObject("Bottom Bar");
            bar.transform.SetParent(panel, false);
            bar.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            var barLayout = bar.AddComponent<HorizontalLayoutGroup>();
            barLayout.spacing = 16f;
            barLayout.padding = new RectOffset(12, 12, 8, 8);
            barLayout.childControlWidth = true;
            barLayout.childForceExpandWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandHeight = true;
            var barElement = bar.AddComponent<LayoutElement>();
            barElement.minHeight = 58f;
            // Horizontal groups with force-expand children report flexible
            // height 1 upward, which would let the bar eat the scroll list's
            // spare space. Pin it: extra space belongs to the party list.
            barElement.flexibleHeight = 0f;

            // Keep the English GameObject names ("Back Button" / "Start
            // Expedition Button") stable for the QA harness; labels are Korean.
            backButton = RuntimeSceneUi.AddButton(bar.transform, "Back", GoBack);
            SetButtonLabel(backButton, "뒤로");
            RegisterQaButton(backButton);

            startButton = RuntimeSceneUi.AddButton(bar.transform, "Start Expedition", BeginDeparture);
            SetButtonLabel(startButton, "원정 출발");
            RegisterQaButton(startButton);
        }

        private Button RegisterQaButton(Button button)
        {
            if (button == null)
            {
                return null;
            }

            var name = button.gameObject.name;
            qaButtonNames.Add(name);
            QaRuntime.RegisterButton(name, () => button.onClick.Invoke());
            return button;
        }

        private void MoveChain(string characterId, int delta)
        {
            if (departing)
            {
                return;
            }

            var chain = TowerSliceContent.GetLoadoutChain();
            int index = chain.IndexOf(characterId);
            if (index < 0) return;

            int targetIndex = index + delta;
            if (targetIndex >= 0 && targetIndex < chain.Count)
            {
                string temp = chain[index];
                chain[index] = chain[targetIndex];
                chain[targetIndex] = temp;
                TowerSliceContent.SetLoadoutChain(chain);
                Refresh();
            }
        }

        private static readonly string[] ChainSymbols = { "①", "②", "③", "④" };
        private static readonly int[] InitiativeValues = { 100, 90, 80, 70 };

        private void Refresh()
        {
            var chain = TowerSliceContent.GetLoadoutChain();

            for (int i = 0; i < chain.Count; i++)
            {
                var id = chain[i];
                var definition = content.Characters[id];

                var symbol = i < ChainSymbols.Length ? ChainSymbols[i] : "·";
                var initiative = i < InitiativeValues.Length ? InitiativeValues[i] : 70;

                if (statsLines.TryGetValue(id, out var text))
                {
                    text.text = AbilityDisplayText.BuildMemberChainStatsLine(
                        definition.DisplayName,
                        definition.MaxHp,
                        definition.Speed,
                        symbol,
                        initiative);
                }

                if (entryTransforms.TryGetValue(id, out var rect))
                {
                    rect.SetSiblingIndex(i);
                }

                if (upButtons.TryGetValue(id, out var up))
                {
                    up.interactable = !departing && i > 0;
                }

                if (downButtons.TryGetValue(id, out var down))
                {
                    down.interactable = !departing && i < chain.Count - 1;
                }
            }
        }

        private void BeginDeparture()
        {
            if (departing)
            {
                return;
            }

            departing = true;
            startButton.interactable = false;
            backButton.interactable = false;
            SetButtonLabel(startButton, "탑을 오르는 중...");
            departureStatus.text = "탑을 오르는 중...";
            Refresh();
            StartCoroutine(DepartAfterFrame());
        }

        private IEnumerator DepartAfterFrame()
        {
            // Let the loading state render at least one frame before the
            // (blocking) scene load kicks in.
            yield return null;
            // T15: Boot decides new-vs-continue; keep the pref it set so the
            // Continue path survives Camp -> Loadout -> Expedition.
            SceneSequenceManager.Instance.LoadSceneWithSequence(TowerSceneNames.Expedition);
        }

        private void GoBack()
        {
            if (departing)
            {
                return;
            }

            SceneSequenceManager.Instance.LoadSceneWithSequence(TowerSceneNames.Camp);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
