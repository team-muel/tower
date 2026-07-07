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
        // Reverse map so we can read the chain order straight off the live
        // sibling order after a drag reorders the rows.
        private readonly Dictionary<RectTransform, string> rowToId = new Dictionary<RectTransform, string>();
        private readonly List<string> qaButtonNames = new List<string>();
        private TowerSliceContent content;
        private RuntimeTooltipView tooltip;
        private RectTransform listContent;
        private Button startButton;
        private Button backButton;
        private Text departureStatus;
        private bool departing;

        // Non-interactive order badges (①~④) — kept as labels only; the ▲▼
        // buttons that used to drive them were replaced by drag-and-drop.
        private static readonly string[] ChainSymbols = { "①", "②", "③", "④" };

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
                "행동 순서를 <color=#FFC107>드래그</color>로 바꾼다 — 위로 끌수록 먼저 행동(이니셔티브 ↑) · 능력 위에 마우스를 올리면 상세 툴팁이 보입니다.",
                14,
                TextAnchor.MiddleCenter);

            listContent = CreatePartyScrollList(panel);
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
            bool locked = definition.ChainLocked;

            var entryObject = new GameObject(characterId + " Entry");
            entryObject.transform.SetParent(parent, false);
            // A raycast-target Image on the row root is both the visible card
            // background and the drag surface (drag events from child text
            // bubble up to this handler).
            entryObject.AddComponent<Image>().color = new Color(0.16f, 0.19f, 0.24f, 0.9f);
            var entryLayout = entryObject.AddComponent<VerticalLayoutGroup>();
            entryLayout.spacing = 4f;
            entryLayout.padding = new RectOffset(12, 12, 10, 10);
            entryLayout.childControlWidth = true;
            entryLayout.childForceExpandWidth = true;
            entryLayout.childControlHeight = true;
            entryLayout.childForceExpandHeight = false;

            var entryRect = entryObject.GetComponent<RectTransform>();
            entryTransforms[characterId] = entryRect;
            rowToId[entryRect] = characterId;

            statsLines[characterId] = RuntimeSceneUi.AddText(
                entryObject.transform,
                characterId + " Stats",
                string.Empty,
                17,
                TextAnchor.MiddleLeft);

            // Drag affordance / disabled-reason line (UX gate rule 2). Locked
            // members are not draggable and say why; others get a grip hint.
            var hint = RuntimeSceneUi.AddText(
                entryObject.transform,
                characterId + " Order Hint",
                locked
                    ? "<color=#9E9E9E>고정 — 순서를 바꿀 수 없습니다</color>"
                    : "<color=#8899AA>⠿ 드래그해서 순서 변경</color>",
                13,
                TextAnchor.MiddleLeft);
            hint.raycastTarget = false;

            if (!locked)
            {
                var drag = entryObject.AddComponent<LoadoutRowDragHandler>();
                drag.Configure(
                    listContent,
                    () => !departing,
                    OnRowReorderPreview,
                    CommitRowOrder);
            }

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

        // Live feedback while a row is being dragged: rebuild the chain from the
        // current sibling order and refresh the order badges / initiative, but
        // do NOT re-set sibling indices (the drag owns them) and do NOT persist
        // every frame.
        private void OnRowReorderPreview()
        {
            ApplyAssignments(ReadSiblingOrder());
        }

        // Drag finished: commit the sibling order as the saved chain.
        private void CommitRowOrder()
        {
            var order = ReadSiblingOrder();
            TowerSliceContent.SetLoadoutChain(order);
            ApplyAssignments(order);
        }

        private List<string> ReadSiblingOrder()
        {
            var order = new List<string>();
            if (listContent == null)
            {
                return order;
            }

            for (int i = 0; i < listContent.childCount; i++)
            {
                var child = listContent.GetChild(i) as RectTransform;
                if (child != null && rowToId.TryGetValue(child, out var id))
                {
                    order.Add(id);
                }
            }

            return order;
        }

        private bool IsChainLocked(string id)
        {
            return content.Characters.TryGetValue(id, out var def) && def.ChainLocked;
        }

        // Full refresh: order the rows to match the saved chain, then apply the
        // order badges / initiative and button states. Used on build and after
        // departure; the per-frame drag path uses ApplyAssignments directly.
        private void Refresh()
        {
            var chain = TowerSliceContent.GetLoadoutChain();

            for (int i = 0; i < chain.Count; i++)
            {
                if (entryTransforms.TryGetValue(chain[i], out var rect))
                {
                    rect.SetSiblingIndex(i);
                }
            }

            ApplyAssignments(chain);

            if (startButton != null)
            {
                startButton.interactable = !departing;
            }

            if (backButton != null)
            {
                backButton.interactable = !departing;
            }
        }

        // Map an ordered id list to per-row order badges + initiative using the
        // pure model, so locked members are excluded from the chain and the
        // remaining members are numbered by their position among the unlocked.
        private void ApplyAssignments(IReadOnlyList<string> order)
        {
            var assignments = LoadoutChainModel.BuildAssignments(order, IsChainLocked);
            foreach (var a in assignments)
            {
                if (!statsLines.TryGetValue(a.Id, out var text))
                {
                    continue;
                }

                if (!content.Characters.TryGetValue(a.Id, out var definition))
                {
                    continue;
                }

                if (a.ChainLocked)
                {
                    text.text = AbilityDisplayText.BuildMemberChainLockedStatsLine(
                        definition.DisplayName,
                        definition.MaxHp,
                        definition.Speed);
                    continue;
                }

                var symbol = a.ChainPosition < ChainSymbols.Length ? ChainSymbols[a.ChainPosition] : "·";
                text.text = AbilityDisplayText.BuildMemberChainStatsLine(
                    definition.DisplayName,
                    definition.MaxHp,
                    definition.Speed,
                    symbol,
                    a.Initiative);
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
