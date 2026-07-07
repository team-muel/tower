using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tower.Combat;
using Tower.Core;
using Tower.Gen;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CoreAiTurnDriver = Tower.Core.AiTurnDriver;

namespace Tower.UI
{
    public sealed class PlayableExpeditionController : MonoBehaviour
    {
        private const string ReturnerId = "regressor";
        private const int BaseSeed = 20260706;
        private const string QaStateKey = "expedition";
        private const string CommandModeButtonName = "Command Mode";
        private const float AiStepSeconds = 0.25f;

        private readonly Dictionary<string, UnitToken> tokens = new Dictionary<string, UnitToken>(StringComparer.Ordinal);
        private readonly List<FloorRoom> encounterRooms = new List<FloorRoom>();
        private readonly List<string> logLines = new List<string>();
        private readonly List<string> qaButtonNames = new List<string>();
        private readonly List<Button> doorButtons = new List<Button>();

        private TowerSliceContent content;
        private SaveRepository repository;
        private ExpeditionState state;
        private ExpeditionState checkpoint;
        private GridView gridView;
        private TileHighlighter highlighter;
        // T20: analog battlefield path (default). The grid path above remains
        // as the CombatSpaceMode.Grid rollback.
        private AnalogBattlefield analogBattlefield;
        private AnalogBattlefieldView analogView;
        private AnalogPlayerTurnController analogController;
        private CombatSpaceMode spaceMode = CombatSpaceSettings.DefaultMode;
        private TurnEngine engine;
        private CoreAiTurnDriver aiDriver;
        private PlayerTurnController playerController;
        private OrderBoard orderBoard;
        private StatusBoard statusBoard;
        private Camera sceneCamera;
        private Text statusText;
        private Text explorationText;
        private Text turnText;
        private Text initiativeText;
        private Text unitText;
        private Text resultText;
        private Text logText;
        private InitiativeRibbonController initiativeRibbon;
        private Button moveButton;
        private Button proceedButton;
        private readonly List<Button> abilityButtons = new List<Button>();
        private int currentAbilityCount;
        private int encounterIndex;
        private bool awaitingNextFloor;
        private float nextAiStepTime;
        private string currentPhase = "booting";
        private string nextRoomPreview = string.Empty;
        private string lastOutcome = string.Empty;
        private readonly CommandModeState commandMode = new CommandModeState();
        private CommandModeOverlay commandOverlay;
        private CombatJuicePresenter juicePresenter;
        private OrbitCameraRig orbitRig;
        private string focusedUnitId;
        private Tower.Gen.FloorLayout currentLayout;
        private GameObject dungeonMapOverlay;
        private bool isDungeonMapOpen;
        private GameObject explorationRoomRoot;
        private readonly List<ExplorationPortalAnchor> explorationPortals = new List<ExplorationPortalAnchor>();
        private ExplorationPortalAnchor hoveredPortal;

        private void Start()
        {
            RuntimeSceneUi.EnsureClearCamera();
            content = TowerSliceContent.Create();
            BuildUi();
            CreateLighting();
            OpenRepository();
            OpenExpedition();
            QaRuntime.RegisterStateContributor(QaStateKey, FillQaState);
            qaButtonNames.Add(CommandModeButtonName);
            QaRuntime.RegisterButton(CommandModeButtonName, ToggleCommandMode);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M))
            {
                ToggleDungeonMap();
            }

            if (engine == null || awaitingNextFloor)
            {
                if (engine == null && awaitingNextFloor && StringComparer.Ordinal.Equals(currentPhase, "exploration"))
                {
                    HandleExplorationPortalInput();
                }

                if (commandMode.SyncCombatActive(false))
                {
                    OnCommandModeChanged();
                }

                return;
            }

            if (engine.IsCombatEnded)
            {
                ResolveEncounter();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                ToggleCommandMode();
            }

            UpdateCombatFocus();
            RefreshTurnUi();
            if (engine.CurrentTurn == null)
            {
                return;
            }

            if (!StringComparer.Ordinal.Equals(engine.CurrentTurn.UnitId, ReturnerId))
            {
                if (Time.time >= nextAiStepTime)
                {
                    RunAiTurn();
                    nextAiStepTime = Time.time + (AiStepSeconds / (juicePresenter != null ? juicePresenter.PlaybackFactor : 1f));
                }

                return;
            }

            HandlePlayerInput();
        }

        public void BeginNextFloor()
        {
            awaitingNextFloor = false;
            StartCurrentFloor();
        }

        private void OnDestroy()
        {
            QaRuntime.UnregisterStateContributor(QaStateKey);
            foreach (var name in qaButtonNames)
            {
                QaRuntime.UnregisterButton(name);
            }

            qaButtonNames.Clear();
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

        private void FillQaState(QaStateSnapshot snapshot)
        {
            if (state != null)
            {
                snapshot.expedition = new QaExpeditionSnapshot
                {
                    stairwayIndex = state.StairwayIndex,
                    stairwayCount = state.StairwayCount,
                    floorIndex = state.FloorIndex,
                    floorCount = state.FloorCount,
                    roomIndex = encounterIndex,
                    roomCount = encounterRooms.Count,
                    retreatCount = state.RetreatCount,
                    isComplete = state.IsComplete,
                    phase = currentPhase,
                    nextRoomPreview = nextRoomPreview,
                    lastOutcome = lastOutcome
                };
            }

            if (engine == null)
            {
                return;
            }

            var combat = new QaCombatSnapshot
            {
                round = engine.RoundNumber,
                activeUnitId = engine.CurrentTurn == null ? string.Empty : engine.CurrentTurn.UnitId,
                remainingOrders = orderBoard == null ? 0 : orderBoard.RemainingOrders(),
                commandMode = commandMode.IsActive,
                spaceMode = analogBattlefield != null
                    ? CombatSpaceMode.Analog.ToString()
                    : CombatSpaceMode.Grid.ToString()
            };
            combat.initiativeOrder.AddRange(engine.CurrentRoundOrder);
            foreach (var unitId in tokens.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                var combatant = engine.GetCombatant(unitId);
                if (combatant == null)
                {
                    continue;
                }

                var unit = new QaUnitSnapshot
                {
                    unitId = unitId,
                    team = combatant.Team.ToString(),
                    currentHp = combatant.State.CurrentHp,
                    maxHp = combatant.State.Definition.MaxHp,
                    alive = engine.IsAlive(unitId),
                    pendingAbility = engine.CurrentTurn != null && StringComparer.Ordinal.Equals(engine.CurrentTurn.UnitId, unitId)
                        ? engine.PendingAbilityId ?? string.Empty
                        : string.Empty
                };

                if (analogBattlefield != null)
                {
                    // T20: continuous float coordinates in analog mode.
                    var position = analogBattlefield.FindOccupant(unitId);
                    unit.x = position.HasValue ? position.Value.X : -1f;
                    unit.y = position.HasValue ? position.Value.Y : -1f;
                }
                else
                {
                    var position = gridView != null && gridView.Map != null ? gridView.Map.FindOccupant(unitId) : null;
                    unit.x = position.HasValue ? position.Value.X : -1f;
                    unit.y = position.HasValue ? position.Value.Y : -1f;
                }

                if (statusBoard != null)
                {
                    unit.marks.AddRange(statusBoard.GetActiveMarkIds(unitId, engine.RoundNumber));
                }

                combat.units.Add(unit);
            }

            snapshot.combat = combat;
        }

        private void BuildUi()
        {
            var canvas = RuntimeSceneUi.CreateCanvas("Expedition Canvas");

            // Root overlay: no Image, no LayoutGroup → 3D viewport shines through.
            var root = RuntimeSceneUi.CreateOverlayPanel(
                canvas.transform, "HUD Root",
                Vector2.zero, Vector2.one);

            // ── Top Bar (height ~5%) ──────────────────────────────────
            var topBar = RuntimeSceneUi.CreateOverlayPanel(
                root, "TopBar",
                new Vector2(0f, 0.95f), Vector2.one,
                new Color(0.08f, 0.08f, 0.10f, 0.6f));

            // Menu button (top-left, compact)
            var menuBtn = RegisterQaButton(RuntimeSceneUi.AddButton(topBar, "Menu", () => SceneSequenceManager.Instance.LoadSceneWithSequence(TowerSceneNames.Boot)));
            SetAnchors(menuBtn, new Vector2(0.01f, 0.1f), new Vector2(0.08f, 0.9f));

            // Retreat button
            var retreatBtn = RegisterQaButton(RuntimeSceneUi.AddButton(topBar, "Retreat", Retreat));
            SetAnchors(retreatBtn, new Vector2(0.09f, 0.1f), new Vector2(0.17f, 0.9f));

            // Exploration / status text (center of top bar)
            explorationText = RuntimeSceneUi.AddText(topBar, "Exploration", "", 14, TextAnchor.MiddleCenter);
            SetAnchors(explorationText, new Vector2(0.20f, 0f), new Vector2(0.80f, 1f));

            // Turn info text (right of exploration)
            turnText = RuntimeSceneUi.AddText(topBar, "Turn", "", 13, TextAnchor.MiddleCenter);
            SetAnchors(turnText, new Vector2(0.80f, 0f), new Vector2(0.91f, 1f));

            // Map button (top-right, compact)
            var mapBtn = RegisterQaButton(RuntimeSceneUi.AddButton(topBar, "Map", ToggleDungeonMap));
            SetAnchors(mapBtn, new Vector2(0.92f, 0.1f), new Vector2(0.99f, 0.9f));

            // T23: initiative ribbon — top-edge strip, under the top bar, so
            // it reads at a glance without covering the 3D viewport.
            initiativeRibbon = InitiativeRibbonController.Create(root, UnitColor);
            initiativeRibbon.Clear();

            // ── Bottom-Left Panel: Initiative + Units (~22% wide, ~18% tall) ──
            var bottomLeft = RuntimeSceneUi.CreateOverlayPanel(
                root, "BottomLeft",
                new Vector2(0.01f, 0.02f), new Vector2(0.25f, 0.24f),
                new Color(0.06f, 0.06f, 0.08f, 0.50f));

            initiativeText = RuntimeSceneUi.AddText(bottomLeft, "Initiative", "", 12, TextAnchor.UpperLeft);
            SetAnchors(initiativeText, new Vector2(0f, 0.5f), Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

            unitText = RuntimeSceneUi.AddText(bottomLeft, "Units", "", 11, TextAnchor.UpperLeft);
            SetAnchors(unitText, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(6f, 2f), new Vector2(-6f, -2f));

            // ── Bottom-Center Panel: Status text (~30% wide, ~10% tall) ──
            var bottomCenter = RuntimeSceneUi.CreateOverlayPanel(
                root, "BottomCenter",
                new Vector2(0.27f, 0.02f), new Vector2(0.55f, 0.16f),
                new Color(0.06f, 0.06f, 0.08f, 0.50f));

            statusText = RuntimeSceneUi.AddText(bottomCenter, "Status", "", 12, TextAnchor.UpperLeft);
            SetAnchors(statusText, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

            // ── Bottom-Right Panel: Result + Log (~42% wide, ~18% tall) ──
            var bottomRight = RuntimeSceneUi.CreateOverlayPanel(
                root, "BottomRight",
                new Vector2(0.57f, 0.02f), new Vector2(0.99f, 0.24f),
                new Color(0.06f, 0.06f, 0.08f, 0.50f));

            resultText = RuntimeSceneUi.AddText(bottomRight, "Result", "", 12, TextAnchor.UpperLeft);
            SetAnchors(resultText, new Vector2(0f, 0.5f), Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

            logText = RuntimeSceneUi.AddText(bottomRight, "Log", "", 11, TextAnchor.UpperLeft);
            SetAnchors(logText, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(6f, 2f), new Vector2(-6f, -2f));

            // ── Center Door Panel (transparent, shown during exploration only) ──
            var centerDoorPanel = RuntimeSceneUi.CreateOverlayPanel(
                root, "CenterDoorPanel",
                new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.65f));

            AddDoorButton(centerDoorPanel, "North Door");
            AddDoorButton(centerDoorPanel, "East Door");
            AddDoorButton(centerDoorPanel, "West Door");

            for (int i = 0; i < doorButtons.Count; i++)
            {
                SetAnchors(doorButtons[i],
                    new Vector2(0.1f, 0.7f - (i * 0.3f)),
                    new Vector2(0.9f, 0.9f - (i * 0.3f)));
            }

            proceedButton = RegisterQaButton(RuntimeSceneUi.AddButton(centerDoorPanel.transform, "Next Floor", BeginNextFloor));
            SetAnchors(proceedButton, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.4f));
            proceedButton.gameObject.SetActive(false);

            // ── Hidden container for QA-registered combat action buttons ──
            var dummyContainer = new GameObject("Dummy Button Container");
            dummyContainer.transform.SetParent(root, false);
            dummyContainer.SetActive(false);

            moveButton = RegisterQaButton(RuntimeSceneUi.AddButton(dummyContainer.transform, "Move", EnterMoveMode));
            for (var index = 0; index < 2; index++)
            {
                var slot = index;
                abilityButtons.Add(RegisterQaButton(RuntimeSceneUi.AddButton(dummyContainer.transform, "Ability " + (index + 1), () => EnterAbilityMode(slot))));
            }
            RegisterQaButton(RuntimeSceneUi.AddButton(dummyContainer.transform, "Order: Focus Nearest", IssueFocusOrder));
            RegisterQaButton(RuntimeSceneUi.AddButton(dummyContainer.transform, "Skip Turn", SkipTurn));

            HideDoorButtons();
        }

        /// <summary>
        /// Helper: set RectTransform anchors (and optional offsets) on a UI
        /// component, bypassing any LayoutElement the component may have.
        /// </summary>
        private static void SetAnchors(Component component, Vector2 anchorMin, Vector2 anchorMax,
            Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            if (component == null) return;
            var rect = component.GetComponent<RectTransform>();
            if (rect == null) return;
            // Disable LayoutElement so anchors take priority.
            var layout = component.GetComponent<LayoutElement>();
            if (layout != null) layout.ignoreLayout = true;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin ?? Vector2.zero;
            rect.offsetMax = offsetMax ?? Vector2.zero;
        }

        private void OpenRepository()
        {
            var path = Path.Combine(Application.persistentDataPath, TowerSceneNames.SaveFileName);
            var created = SaveRepository.Create(path);
            if (created.IsFailure)
            {
                AddLog(created.Error);
                return;
            }

            repository = created.Value;
        }

        private void OpenExpedition()
        {
            var forceNew = PlayerPrefs.GetInt(TowerSceneNames.NewExpeditionPref, 1) == 1;
            PlayerPrefs.SetInt(TowerSceneNames.NewExpeditionPref, 0);
            PlayerPrefs.Save();

            Result<ExpeditionState> opened;
            if (!forceNew && repository != null && repository.HasSave)
            {
                var loaded = repository.Load();
                opened = loaded.IsSuccess
                    ? ExpeditionSaveMapper.ToState(loaded.Value, content.ResolveCharacter)
                    : Result<ExpeditionState>.Failure(loaded.Error);
            }
            else
            {
                opened = ExpeditionState.CreateNew(content.CreateRosterFromLoadout());
            }

            if (opened.IsFailure)
            {
                AddLog(opened.Error);
                return;
            }

            state = opened.Value;
            checkpoint = state;
            SaveCheckpoint();
            AddLog(forceNew ? "New expedition prepared." : "Checkpoint loaded.");
            StartCurrentFloor();
        }

        private void StartCurrentFloor()
        {
            if (proceedButton != null)
            {
                proceedButton.gameObject.SetActive(false);
            }

            ClearBattleObjects();
            if (state == null)
            {
                return;
            }

            if (state.IsComplete)
            {
                ShowComplete();
                return;
            }

            var gated = ExpeditionRules.ApplyShortcutGate(state);
            if (gated.IsFailure)
            {
                AddLog(gated.Error);
                return;
            }

            state = gated.Value;
            var seed = BaseSeed + (state.StairwayIndex * 1000) + state.FloorIndex + UnityEngine.Random.Range(0, 997);
            var layout = FloorGenerator.Generate(new FloorGenParams(seed, state.FloorIndex == state.FloorCount));
            currentLayout = layout;
            encounterRooms.Clear();
            encounterRooms.AddRange(layout.Rooms
                .Where(room => room.Encounter.HasEncounter)
                .OrderBy(room => room.Depth)
                .ThenBy(room => room.Id));
            encounterIndex = 0;

            AddLog($"Entered stairway {state.StairwayIndex}, floor {state.FloorIndex}. Rooms: {layout.Rooms.Count}, encounters: {encounterRooms.Count}.");

            // Ensure the 3D camera exists during exploration so the scene is
            // visible while selecting doors (instead of a black screen).
            if (sceneCamera == null)
            {
                CreateCamera(Vector3.zero);
            }

            ShowDoorChoiceOrClearFloor();
        }

        private void ShowDoorChoiceOrClearFloor()
        {
            if (encounterIndex >= encounterRooms.Count)
            {
                ClearFloor();
                return;
            }

            currentPhase = "exploration";
            awaitingNextFloor = true;
            var room = encounterRooms[encounterIndex];
            nextRoomPreview = BuildRoomPreview(room);
            BuildExplorationRoom(room);
            if (explorationText != null)
            {
                explorationText.text = $"탐험 | {state.StairwayIndex}층계 · {state.FloorIndex}층 · 방 {encounterIndex + 1}/{encounterRooms.Count} | 다음: {nextRoomPreview}";
            }

            if (resultText != null)
            {
                resultText.text = "문 오브젝트를 클릭해 다음 방으로 이동";
            }

            RefreshCombatHud();
            ShowDoorButtons();
        }

        private void BuildExplorationRoom(FloorRoom room)
        {
            ClearExplorationRoom();

            explorationRoomRoot = new GameObject("Exploration Room");
            var root = explorationRoomRoot.transform;

            CreateRoomBlock(root, "Room Floor", new Vector3(0f, -0.08f, 0.6f), new Vector3(10.5f, 0.16f, 8.4f), new Color(0.19f, 0.18f, 0.16f, 1f));
            CreateRoomBlock(root, "Back Wall", new Vector3(0f, 1.4f, 4.65f), new Vector3(10.8f, 2.8f, 0.24f), new Color(0.13f, 0.12f, 0.12f, 1f));
            CreateRoomBlock(root, "Left Wall", new Vector3(-5.3f, 1f, 0.6f), new Vector3(0.22f, 2f, 8.3f), new Color(0.11f, 0.11f, 0.12f, 1f));
            CreateRoomBlock(root, "Right Wall", new Vector3(5.3f, 1f, 0.6f), new Vector3(0.22f, 2f, 8.3f), new Color(0.11f, 0.11f, 0.12f, 1f));

            for (var x = -4; x <= 4; x += 2)
            {
                CreateRoomBlock(root, $"Floor Seam X{x}", new Vector3(x, 0.01f, 0.6f), new Vector3(0.035f, 0.02f, 8.1f), new Color(0.27f, 0.25f, 0.22f, 1f));
            }

            for (var z = -3; z <= 3; z += 2)
            {
                CreateRoomBlock(root, $"Floor Seam Z{z}", new Vector3(0f, 0.012f, z), new Vector3(10f, 0.02f, 0.035f), new Color(0.27f, 0.25f, 0.22f, 1f));
            }

            CreatePortal(root, 0, "North Door", "북쪽 문", room, new Vector3(0f, 0f, 3.65f), Quaternion.Euler(0f, 180f, 0f));
            CreatePortal(root, 1, "East Door", "동쪽 문", room, new Vector3(3.75f, 0f, 1.05f), Quaternion.Euler(0f, -125f, 0f));
            CreatePortal(root, 2, "West Door", "서쪽 문", room, new Vector3(-3.75f, 0f, 1.05f), Quaternion.Euler(0f, 125f, 0f));

            CreateOrbCue(root, "Memory Orb Cue", new Vector3(-2.3f, 0.72f, -1.9f), new Color(0.42f, 0.82f, 1f, 1f), "기억 오브");
            CreateRoomBlock(root, "Relic Container", new Vector3(2.4f, 0.28f, -2.05f), new Vector3(0.95f, 0.55f, 0.65f), new Color(0.55f, 0.42f, 0.22f, 1f));
            CreateRoomBlock(root, "Hazard Marker", new Vector3(0f, 0.025f, -2.6f), new Vector3(2.2f, 0.05f, 0.42f), new Color(0.55f, 0.12f, 0.12f, 1f));

            if (sceneCamera == null)
            {
                CreateCamera(new Vector3(0f, 0f, 0.8f));
            }
            else if (orbitRig != null)
            {
                orbitRig.FocusWorld(new Vector3(0f, 0f, 0.8f));
                orbitRig.SetDistance(12.5f);
            }

            foreach (var portal in explorationPortals)
            {
                portal.SetCamera(sceneCamera);
            }
        }

        private GameObject CreateRoomBlock(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = TowerRuntimeMaterials.CreateLit(name + " Material", color);
            var collider = block.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return block;
        }

        private void CreatePortal(Transform parent, int doorIndex, string qaLabel, string displayLabel, FloorRoom room, Vector3 position, Quaternion rotation)
        {
            var portal = new GameObject(qaLabel + " Anchor");
            portal.transform.SetParent(parent, false);
            portal.transform.localPosition = position;
            portal.transform.localRotation = rotation;

            var anchor = portal.AddComponent<ExplorationPortalAnchor>();
            anchor.DoorIndex = doorIndex;
            anchor.QaLabel = qaLabel;
            anchor.DisplayLabel = displayLabel;
            anchor.Preview = BuildRoomPreview(room);
            anchor.BaseColor = PortalColor(room, false);
            anchor.HoverColor = PortalColor(room, true);
            explorationPortals.Add(anchor);

            CreatePortalBlock(portal.transform, "Left Pillar", new Vector3(-0.72f, 0.78f, 0f), new Vector3(0.22f, 1.55f, 0.25f), anchor.BaseColor, anchor);
            CreatePortalBlock(portal.transform, "Right Pillar", new Vector3(0.72f, 0.78f, 0f), new Vector3(0.22f, 1.55f, 0.25f), anchor.BaseColor, anchor);
            CreatePortalBlock(portal.transform, "Lintel", new Vector3(0f, 1.56f, 0f), new Vector3(1.68f, 0.22f, 0.25f), anchor.BaseColor, anchor);
            CreatePortalBlock(portal.transform, "Portal Glow", new Vector3(0f, 0.82f, -0.03f), new Vector3(1.16f, 1.2f, 0.08f), new Color(anchor.BaseColor.r, anchor.BaseColor.g, anchor.BaseColor.b, 0.88f), anchor);

            var hitbox = new GameObject("Portal Hitbox");
            hitbox.transform.SetParent(portal.transform, false);
            hitbox.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            hitbox.transform.localScale = new Vector3(1.9f, 1.9f, 0.6f);
            hitbox.AddComponent<BoxCollider>().isTrigger = true;

            var labelObject = new GameObject("Portal Label");
            labelObject.transform.SetParent(portal.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 2.05f, -0.08f);
            labelObject.transform.localScale = Vector3.one * 0.12f;
            var label = labelObject.AddComponent<TextMesh>();
            label.text = $"{displayLabel}\n{anchor.Preview}";
            label.fontSize = 42;
            label.alignment = TextAlignment.Center;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = new Color(0.95f, 0.96f, 0.92f, 1f);
            labelObject.AddComponent<FaceCamera>().Target = sceneCamera;
            anchor.Label = label;
        }

        private void CreatePortalBlock(Transform parent, string name, Vector3 position, Vector3 scale, Color color, ExplorationPortalAnchor anchor)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            var renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = TowerRuntimeMaterials.CreateLit(name + " Material", color);
            anchor.Register(renderer);
            var collider = block.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private void CreateOrbCue(Transform parent, string name, Vector3 position, Color color, string label)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = name;
            orb.transform.SetParent(parent, false);
            orb.transform.localPosition = position;
            orb.transform.localScale = Vector3.one * 0.38f;
            orb.GetComponent<Renderer>().sharedMaterial = TowerRuntimeMaterials.CreateLit(name + " Material", color);
            var collider = orb.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var textObject = new GameObject(name + " Label");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position + new Vector3(0f, 0.48f, 0f);
            textObject.transform.localScale = Vector3.one * 0.09f;
            var text = textObject.AddComponent<TextMesh>();
            text.text = label;
            text.fontSize = 36;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(0.82f, 0.94f, 1f, 1f);
            textObject.AddComponent<FaceCamera>().Target = sceneCamera;
        }

        private static Color PortalColor(FloorRoom room, bool hover)
        {
            if (room != null && room.Encounter.IsBoss)
            {
                return hover ? new Color(1f, 0.42f, 0.34f, 1f) : new Color(0.62f, 0.16f, 0.12f, 1f);
            }

            if (room != null && room.Encounter.EnemyCount >= 3)
            {
                return hover ? new Color(1f, 0.7f, 0.28f, 1f) : new Color(0.64f, 0.36f, 0.13f, 1f);
            }

            return hover ? new Color(0.36f, 0.82f, 1f, 1f) : new Color(0.16f, 0.48f, 0.68f, 1f);
        }

        private void HandleExplorationPortalInput()
        {
            if (sceneCamera == null || explorationPortals.Count == 0 || isDungeonMapOpen)
            {
                SetHoveredPortal(null);
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                SetHoveredPortal(null);
                return;
            }

            var ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit))
            {
                SetHoveredPortal(null);
                return;
            }

            var portal = hit.collider != null ? hit.collider.GetComponentInParent<ExplorationPortalAnchor>() : null;
            SetHoveredPortal(portal);
            if (portal != null && Input.GetMouseButtonDown(0))
            {
                AddLog($"{portal.DisplayLabel} 선택: {portal.Preview}");
                EnterSelectedDoor();
            }
        }

        private void SetHoveredPortal(ExplorationPortalAnchor portal)
        {
            if (hoveredPortal == portal)
            {
                return;
            }

            if (hoveredPortal != null)
            {
                hoveredPortal.SetHighlighted(false);
            }

            hoveredPortal = portal;
            if (hoveredPortal != null)
            {
                hoveredPortal.SetHighlighted(true);
            }
        }

        private void ClearExplorationRoom()
        {
            SetHoveredPortal(null);
            explorationPortals.Clear();
            if (explorationRoomRoot != null)
            {
                Destroy(explorationRoomRoot);
                explorationRoomRoot = null;
            }
        }

        private void StartEncounter(FloorRoom room)
        {
            currentPhase = "combat";
            awaitingNextFloor = false;
            nextRoomPreview = BuildRoomPreview(room);
            HideDoorButtons();
            if (resultText != null)
            {
                resultText.text = string.Empty;
            }

            ClearBattleObjects();
            // T20: the space mode is latched per encounter so a mid-combat
            // flag change cannot desync the battlefield and the view.
            spaceMode = CombatSpaceSettings.Mode;
            var analog = spaceMode == CombatSpaceMode.Analog;
            var map = room.Map;
            var party = state.Roster.Where(member => !member.IsDead).ToList();
            var spawnCells = map.Positions.Where(position => map.CanEnter(position)).ToList();
            if (spawnCells.Count < party.Count + room.Encounter.EnemyCount)
            {
                AddLog("Room has too few cells for combat.");
                return;
            }

            if (analog)
            {
                analogBattlefield = AnalogBattlefield.FromRoom(map.Width, map.Height);
                var viewObject = new GameObject("Analog Battlefield");
                analogView = viewObject.AddComponent<AnalogBattlefieldView>();
                analogView.Build(analogBattlefield);
            }
            else
            {
                var gridObject = new GameObject("Runtime Grid");
                gridView = gridObject.AddComponent<GridView>();
                gridView.Build(map);
                highlighter = gridObject.AddComponent<TileHighlighter>();
                highlighter.Initialize(gridView);
            }

            var combatants = new List<CombatantRef>();
            for (var index = 0; index < party.Count; index++)
            {
                var member = party[index];
                var cell = spawnCells[index];
                var token = SpawnToken(analog, cell, member.UnitId, UnitColor(member.UnitId, CombatTeam.Player));
                if (token == null)
                {
                    AddLog($"Could not place unit '{member.UnitId}'.");
                    return;
                }

                tokens[member.UnitId] = token;

                var combatant = CombatantRef.Create(member.UnitId, CombatTeam.Player, member.State);
                if (combatant.IsFailure)
                {
                    AddLog(combatant.Error);
                    return;
                }

                combatants.Add(combatant.Value);
            }

            var enemyFactory = new SliceEnemyFactory(content);
            for (var index = 0; index < room.Encounter.EnemySlots.Count; index++)
            {
                var slot = room.Encounter.EnemySlots[index];
                var enemyState = enemyFactory.Create(slot.KindSlot, state.StairwayIndex, state.FloorIndex);
                if (enemyState.IsFailure)
                {
                    AddLog(enemyState.Error);
                    return;
                }

                var unitId = $"enemy-{room.Id}-{slot.Index}";
                var cell = spawnCells[spawnCells.Count - 1 - index];
                var token = SpawnToken(analog, cell, unitId, UnitColor(unitId, CombatTeam.Enemy));
                if (token == null)
                {
                    AddLog($"Could not place unit '{unitId}'.");
                    return;
                }

                tokens[unitId] = token;

                var combatant = CombatantRef.Create(unitId, CombatTeam.Enemy, enemyState.Value);
                if (combatant.IsFailure)
                {
                    AddLog(combatant.Error);
                    return;
                }

                combatants.Add(combatant.Value);
            }

            var focusWorld = analog
                ? analogView.ToWorld(new BattlePos(analogBattlefield.Width * 0.5f, analogBattlefield.Height * 0.5f))
                : gridView.CellToWorld(new GridPos(map.Width / 2, map.Height / 2));
            CreateCamera(focusWorld);

            var presenterObject = new GameObject("Combat Juice Presenter");
            juicePresenter = presenterObject.AddComponent<CombatJuicePresenter>();
            juicePresenter.Initialize(
                AddLog,
                tokens,
                sceneCamera,
                orbitRig,
                AbilityFeelCatalog.FromCombatants(combatants));

            statusBoard = new StatusBoard();
            var resolver = analog
                ? AbilityResolver.Create(analogBattlefield, statusBoard, juicePresenter)
                : AbilityResolver.Create(map, statusBoard, juicePresenter);
            if (resolver.IsFailure)
            {
                AddLog(resolver.Error);
                return;
            }

            var engineResult = TurnEngine.Create(
                combatants,
                juicePresenter,
                resolver.Value,
                juicePresenter,
                allyOrderChain: party.Select(m => m.UnitId).ToList());
            if (engineResult.IsFailure)
            {
                AddLog(engineResult.Error);
                return;
            }

            engine = engineResult.Value;

            var scorer = analog
                ? ActionScorer.Create(analogBattlefield, statusBoard)
                : ActionScorer.Create(map, statusBoard);
            if (scorer.IsFailure)
            {
                AddLog(scorer.Error);
                return;
            }

            var driver = analog
                ? CoreAiTurnDriver.Create(engine, analogBattlefield, scorer.Value)
                : CoreAiTurnDriver.Create(engine, map, scorer.Value);
            if (driver.IsFailure)
            {
                AddLog(driver.Error);
                return;
            }

            aiDriver = driver.Value;
            orderBoard = OrderBoard.CreateDefault();

            var abilities = engine.GetCombatant(ReturnerId)?.State.Loadout.Abilities ?? Array.Empty<AbilityDef>();
            if (analog)
            {
                analogController = new AnalogPlayerTurnController(
                    engine, analogBattlefield, analogView, orderBoard, ReturnerId, abilities, juicePresenter);
            }
            else
            {
                var playerToken = tokens.TryGetValue(ReturnerId, out var tokenResult) ? tokenResult : null;
                var enemyTokens = tokens.Values.Where(token => token.OccupantId.StartsWith("enemy-", StringComparison.Ordinal)).ToArray();
                playerController = new PlayerTurnController(engine, gridView, highlighter, playerToken, enemyTokens, orderBoard, ReturnerId, abilities, juicePresenter);
            }

            CreateCommandOverlay(party);
            RefreshAbilityButtons(abilities);
            RefreshStatus();
            RefreshCombatHud();
            AddLog($"Encounter {room.Id} started ({spaceMode}). Keys: M, 1, 2 · Space 지휘 모드.");
        }

        private UnitToken SpawnToken(bool analog, GridPos cell, string unitId, Color color)
        {
            if (!analog)
            {
                return UnitToken.Spawn(gridView, cell, unitId, color);
            }

            var position = BattleScale.ToBattlePos(cell);
            if (!analogBattlefield.TryPlaceOccupant(unitId, position))
            {
                return null;
            }

            return UnitToken.SpawnAnalog(analogView, position, unitId, color);
        }

        private void RunAiTurn()
        {
            if (aiDriver == null || engine == null || engine.IsCombatEnded)
            {
                return;
            }

            var active = engine.CurrentTurn?.UnitId;
            var result = aiDriver.TakeTurn();
            if (result.IsFailure)
            {
                AddLog(result.Error);
                if (!string.IsNullOrEmpty(active))
                {
                    engine.Submit(new SkipTurnCommand(active));
                }
            }

            SyncTokens();
            RefreshStatus();
            RefreshCombatHud();
        }

        private void HandlePlayerInput()
        {
            if (sceneCamera == null)
            {
                return;
            }

            if (commandMode.IsActive)
            {
                // 지휘 모드: 셀 클릭/모드 키 대신 오버레이 버튼이 입력을 받는다.
                return;
            }

            if (analogController != null)
            {
                HandleAnalogPlayerInput();
                return;
            }

            if (gridView == null || playerController == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                playerController.EnterMoveMode();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                playerController.EnterAbilityMode(0);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                playerController.EnterAbilityMode(1);
            }

            if (TryGetMouseCell(out var hover) && gridView.Map.InBounds(hover))
            {
                highlighter.SetHover(hover);
                if (Input.GetMouseButtonDown(0))
                {
                    playerController.OnCellClicked(hover);
                    SyncTokens();
                    RefreshStatus();
                    RefreshCombatHud();
                }
            }
            else
            {
                highlighter.SetHover(null);
            }
        }

        // T20: analog manual input — M/1/2 mode keys, then a click either on a
        // unit token (targeting) or on the floor (move inside the ring).
        private void HandleAnalogPlayerInput()
        {
            if (analogView == null || analogController == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                analogController.EnterMoveMode();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                analogController.EnterAbilityMode(0);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                analogController.EnterAbilityMode(1);
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            var ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit))
            {
                return;
            }

            var token = hit.collider != null ? hit.collider.GetComponentInParent<UnitToken>() : null;
            if (token != null)
            {
                analogController.OnUnitClicked(token.OccupantId);
            }
            else if (analogView.TryGetBattlePos(hit.point, out var point))
            {
                analogController.OnPointClicked(point);
            }

            SyncTokens();
            RefreshStatus();
            RefreshCombatHud();
        }

        private void ResolveEncounter()
        {
            TearDownCommandMode();
            var winner = engine.WinningTeam;
            SyncPartyState();
            AddLog(winner == CombatTeam.Player ? "Encounter cleared." : "Party wiped.");
            engine = null;
            playerController = null;
            analogController = null;
            aiDriver = null;

            if (winner == CombatTeam.Player)
            {
                ClearBattleObjects();
                ShowDoorChoiceOrClearFloor();
            }
            else
            {
                Retreat();
            }
        }

        private void ClearFloor()
        {
            var progress = ExpeditionRules.ClearFloor(state);
            if (progress.IsFailure)
            {
                AddLog(progress.Error);
                return;
            }

            ApplyProgress(progress.Value);
            if (state.IsComplete)
            {
                ShowComplete();
                return;
            }

            awaitingNextFloor = true;
            currentPhase = "floor-result";
            AddLog("Floor cleared. Press Next Floor to continue.");
            ShowResult(progress.Value);
            ShowProceedButton("Next Floor", BeginNextFloor);
            RefreshStatus();
        }

        private void Retreat()
        {
            SyncPartyState();
            var progress = ExpeditionRules.Retreat(state, checkpoint);
            if (progress.IsFailure)
            {
                AddLog(progress.Error);
                return;
            }

            ApplyProgress(progress.Value);
            ClearBattleObjects();
            awaitingNextFloor = true;
            currentPhase = progress.Value.Outcome == ExpeditionOutcome.GreatRegression
                ? "great-regression-result"
                : "retreat-result";
            AddLog(progress.Value.Outcome == ExpeditionOutcome.GreatRegression
                ? "Great regression: back to floor 1."
                : "Retreated to the last checkpoint.");
            ShowResult(progress.Value);
            ShowProceedButton("Return to Menu", () => SceneSequenceManager.Instance.LoadSceneWithSequence(TowerSceneNames.Boot));
            RefreshStatus();
        }

        private void ApplyProgress(ExpeditionProgress progress)
        {
            state = progress.State;
            if (progress.RequiresSave)
            {
                SaveCheckpoint();
            }

            var message = progress.Outcome.ToString();
            lastOutcome = progress.Outcome.ToString();
            if (progress.ConfirmedDeadIds.Count > 0)
            {
                message += " | fallen: " + string.Join(", ", progress.ConfirmedDeadIds);
            }

            if (progress.RevivedIds.Count > 0)
            {
                message += " | revived: " + string.Join(", ", progress.RevivedIds);
            }

            if (progress.NewlyMissingIds.Count > 0)
            {
                message += " | missing: " + string.Join(", ", progress.NewlyMissingIds);
            }

            AddLog(message);
        }

        private void SyncPartyState()
        {
            if (engine == null || state == null)
            {
                return;
            }

            foreach (var member in state.Roster.ToArray())
            {
                var combatant = engine.GetCombatant(member.UnitId);
                if (combatant == null)
                {
                    continue;
                }

                var synced = ExpeditionRules.UpdateMemberState(state, member.UnitId, combatant.State);
                if (synced.IsSuccess)
                {
                    state = synced.Value;
                }
            }
        }

        private void SaveCheckpoint()
        {
            if (repository == null || state == null)
            {
                return;
            }

            var save = ExpeditionSaveMapper.ToSave(state);
            var saved = save.IsSuccess ? repository.Save(save.Value) : Result.Failure(save.Error);
            if (saved.IsFailure)
            {
                AddLog(saved.Error);
                return;
            }

            checkpoint = state;
        }

        // T20: the manual controllers (grid/analog) share these seams so the
        // UI buttons and QA harness drive whichever mode is active.
        private bool IsManualTurn()
        {
            if (analogController != null)
            {
                return analogController.IsPlayerTurn();
            }

            return playerController != null && playerController.IsPlayerTurn();
        }

        private void EnterMoveMode()
        {
            if (analogController != null)
            {
                analogController.EnterMoveMode();
            }
            else
            {
                playerController?.EnterMoveMode();
            }
        }

        private void EnterAbilityMode(int slot)
        {
            if (analogController != null)
            {
                analogController.EnterAbilityMode(slot);
            }
            else
            {
                playerController?.EnterAbilityMode(slot);
            }
        }

        private void SkipTurn()
        {
            if (analogController != null)
            {
                analogController.Skip();
            }
            else
            {
                playerController?.Skip();
            }
        }

        private void IssueFocusOrder()
        {
            if (engine == null || !IsManualTurn())
            {
                return;
            }

            var target = engine.CurrentRoundOrder
                .Select(id => engine.GetCombatant(id))
                .FirstOrDefault(unit => unit != null && unit.Team == CombatTeam.Enemy && engine.IsAlive(unit.UnitId));
            if (target == null)
            {
                return;
            }

            if (analogController != null)
            {
                analogController.EnterOrderMode(target.UnitId);
            }
            else
            {
                playerController.EnterOrderMode(target.UnitId);
            }

            AddLog("Order issued: focus " + target.UnitId);
        }

        private void RefreshAbilityButtons(IReadOnlyList<AbilityDef> abilities)
        {
            currentAbilityCount = abilities.Count;
            for (var index = 0; index < abilityButtons.Count; index++)
            {
                var label = abilityButtons[index].GetComponentInChildren<Text>();
                if (index < abilities.Count)
                {
                    label.text = abilities[index].DisplayName;
                    abilityButtons[index].interactable = true;
                }
                else
                {
                    label.text = "Ability " + (index + 1);
                    abilityButtons[index].interactable = false;
                }
            }
        }

        private void RefreshTurnUi()
        {
            if (turnText == null || engine == null)
            {
                return;
            }

            var active = engine.CurrentTurn?.UnitId ?? "none";
            turnText.text = $"R{engine.RoundNumber} | {active}";
            var isPlayerTurn = IsManualTurn();
            if (moveButton != null)
            {
                moveButton.interactable = isPlayerTurn;
            }

            foreach (var button in abilityButtons)
            {
                var index = abilityButtons.IndexOf(button);
                button.interactable = isPlayerTurn && index < currentAbilityCount;
            }

            RefreshCombatHud();
        }

        private void RefreshStatus()
        {
            if (statusText == null || state == null)
            {
                return;
            }

            statusText.text = $"Stairway {state.StairwayIndex}/{state.StairwayCount} | Floor {state.FloorIndex}/{state.FloorCount}\n"
                + $"Retreats {state.RetreatCount}/3 | Roster {state.Roster.Count}\n"
                + string.Join(" · ", state.Roster.Select(member => $"{member.UnitId} {member.State.CurrentHp}/{member.State.Definition.MaxHp}"));
        }

        private void RefreshCombatHud()
        {
            if (engine == null)
            {
                if (turnText != null)
                {
                    turnText.text = currentPhase == "exploration" ? "전투 대기" : string.Empty;
                }

                if (initiativeText != null)
                {
                    initiativeText.text = string.Empty;
                }

                if (initiativeRibbon != null)
                {
                    initiativeRibbon.Clear();
                }

                if (unitText != null)
                {
                    unitText.text = string.Empty;
                }

                return;
            }

            if (initiativeText != null)
            {
                initiativeText.text = "이니셔티브: " + string.Join(" -> ", engine.CurrentRoundOrder);
            }

            if (initiativeRibbon != null)
            {
                initiativeRibbon.Refresh(
                    engine.CurrentRoundOrder,
                    engine.CurrentTurn?.UnitId,
                    engine.IsAlive,
                    unitId => engine.GetCombatant(unitId) is { } combatant ? combatant.Team : CombatTeam.Player);
            }

            if (unitText != null)
            {
                var lines = new List<string> { $"오더 잔여 {orderBoard?.RemainingOrders() ?? 0}/{OrderBoard.DefaultCombatOrders}" };
                foreach (var unitId in tokens.Keys.OrderBy(id => id, StringComparer.Ordinal))
                {
                    var combatant = engine.GetCombatant(unitId);
                    if (combatant == null)
                    {
                        continue;
                    }

                    var marks = statusBoard == null
                        ? Array.Empty<string>()
                        : statusBoard.GetActiveMarkIds(unitId, engine.RoundNumber);
                    var markText = marks.Count == 0 ? "-" : string.Join(",", marks);
                    var activePrefix = engine.CurrentTurn != null && StringComparer.Ordinal.Equals(engine.CurrentTurn.UnitId, unitId)
                        ? "> "
                        : "  ";
                    lines.Add($"{activePrefix}{unitId} {combatant.State.CurrentHp}/{combatant.State.Definition.MaxHp} HP | mark {markText}");
                }

                unitText.text = string.Join("\n", lines);
            }
        }

        private void AddLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            logLines.Add(message);
            while (logLines.Count > 4)
            {
                logLines.RemoveAt(0);
            }

            if (logText != null)
            {
                logText.text = string.Join("\n", logLines);
            }

            Debug.Log("[PlayableExpedition] " + message);
        }

        private void ShowComplete()
        {
            ClearBattleObjects();
            awaitingNextFloor = true;
            currentPhase = "advance-result";
            nextRoomPreview = string.Empty;
            RefreshStatus();
            AddLog("Stairway conquered. Shortcut saved.");
            if (resultText != null)
            {
                resultText.text = "전진 결과\n층계 정복. 숏컷이 저장됐다.\n사망 상태 동료는 전진 기록에 확정된다.";
            }

            ShowProceedButton("Return to Menu", () => SceneSequenceManager.Instance.LoadSceneWithSequence(TowerSceneNames.Boot));
        }

        private void AddDoorButton(Transform parent, string label)
        {
            var button = RegisterQaButton(RuntimeSceneUi.AddButton(parent, label, () => EnterSelectedDoor()));
            doorButtons.Add(button);
        }

        private void ShowDoorButtons()
        {
            foreach (var button in doorButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.gameObject.SetActive(true);
                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    var baseLabel = button.gameObject.name.Replace(" Button", string.Empty);
                    text.text = baseLabel + " (" + nextRoomPreview + ")";
                }

                button.gameObject.SetActive(false);
            }
        }

        private void HideDoorButtons()
        {
            foreach (var button in doorButtons)
            {
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                }
            }
        }

        private void EnterSelectedDoor()
        {
            if (encounterIndex >= encounterRooms.Count)
            {
                ClearFloor();
                return;
            }

            var room = encounterRooms[encounterIndex++];
            StartEncounter(room);
        }

        private string BuildRoomPreview(FloorRoom room)
        {
            if (room == null)
            {
                return "출구";
            }

            if (room.Encounter.IsBoss)
            {
                return "보스";
            }

            if (room.Encounter.EnemyCount <= 0)
            {
                return "캠프";
            }

            return room.Encounter.EnemyCount >= 3 ? "강적" : "조우";
        }

        private void ShowResult(ExpeditionProgress progress)
        {
            if (resultText == null || progress == null)
            {
                return;
            }

            var lines = new List<string>();
            switch (progress.Outcome)
            {
                case ExpeditionOutcome.Advanced:
                    lines.Add("전진 결과");
                    lines.Add("층계 정복. 체크포인트와 숏컷 저장.");
                    break;
                case ExpeditionOutcome.Retreated:
                    lines.Add("후퇴 결과");
                    lines.Add("직전 정복 시점으로 돌아간다.");
                    break;
                case ExpeditionOutcome.GreatRegression:
                    lines.Add("대회귀 결과");
                    lines.Add("1층계로 돌아가지만 숏컷과 실종 기록은 유지.");
                    break;
                default:
                    lines.Add("층 정리");
                    lines.Add("다음 층으로 이동 가능.");
                    break;
            }

            lines.Add(progress.RevivedIds.Count == 0
                ? "생환자: 없음"
                : "생환자: " + string.Join(", ", progress.RevivedIds));
            lines.Add(progress.ConfirmedDeadIds.Count == 0
                ? "확정 사망: 없음"
                : "확정 사망: " + string.Join(", ", progress.ConfirmedDeadIds));
            lines.Add(progress.NewlyMissingIds.Count == 0
                ? "실종: 없음"
                : "실종: " + string.Join(", ", progress.NewlyMissingIds));
            resultText.text = string.Join("\n", lines);
        }

        private void ShowProceedButton(string label, Action action)
        {
            if (proceedButton == null)
            {
                return;
            }

            var text = proceedButton.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }

            proceedButton.onClick.RemoveAllListeners();
            proceedButton.onClick.AddListener(() => action());
            proceedButton.gameObject.SetActive(true);
        }

        private void ClearBattleObjects()
        {
            TearDownCommandMode();
            ClearExplorationRoom();
            foreach (var token in tokens.Values)
            {
                if (token != null)
                {
                    Destroy(token.gameObject);
                }
            }

            tokens.Clear();
            if (gridView != null)
            {
                Destroy(gridView.gameObject);
            }

            if (analogView != null)
            {
                Destroy(analogView.gameObject);
            }

            gridView = null;
            highlighter = null;
            analogView = null;
            analogBattlefield = null;
            analogController = null;
            statusBoard = null;
            engine = null;
            aiDriver = null;
            playerController = null;
            if (juicePresenter != null)
            {
                Destroy(juicePresenter.gameObject);
            }

            juicePresenter = null;
        }

        // T19: combat runs on the orbit rig; camp/exploration scenes keep the
        // iso follow rig (the rigs coexist, one per scene mode).
        private void CreateCamera(Vector3 focusWorld)
        {
            var existingIso = FindFirstObjectByType<IsoCameraRig>();
            if (existingIso != null)
            {
                Destroy(existingIso.gameObject);
            }

            var existingOrbit = FindFirstObjectByType<OrbitCameraRig>();
            if (existingOrbit != null)
            {
                Destroy(existingOrbit.gameObject);
            }

            var cameraRigObject = new GameObject("Orbit Camera Rig");
            orbitRig = cameraRigObject.AddComponent<OrbitCameraRig>();
            orbitRig.FocusWorld(focusWorld);
            sceneCamera = orbitRig.Camera;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.055f, 0.06f, 0.07f, 1f);
            focusedUnitId = null;

            foreach (var portal in explorationPortals)
            {
                if (portal != null)
                {
                    portal.SetCamera(sceneCamera);
                }
            }
        }

        private void CreateCommandOverlay(List<ExpeditionMember> party)
        {
            var allyTokens = new List<UnitToken>();
            foreach (var member in party)
            {
                if (tokens.TryGetValue(member.UnitId, out var token))
                {
                    allyTokens.Add(token);
                }
            }

            commandOverlay = CommandModeOverlay.Create(engine, sceneCamera, allyTokens, AddLog);
        }

        // T19: the orbit camera follows the active turn unit; when there is
        // none (or it is gone) it falls back to the regressor.
        private void UpdateCombatFocus()
        {
            if (orbitRig == null || engine == null)
            {
                return;
            }

            var targetId = engine.CurrentTurn?.UnitId;
            if (string.IsNullOrEmpty(targetId) || !tokens.ContainsKey(targetId) || !engine.IsAlive(targetId))
            {
                targetId = tokens.ContainsKey(ReturnerId) && engine.IsAlive(ReturnerId) ? ReturnerId : null;
            }

            if (StringComparer.Ordinal.Equals(targetId, focusedUnitId))
            {
                return;
            }

            focusedUnitId = targetId;
            if (targetId != null)
            {
                orbitRig.SetFocusTarget(tokens[targetId].transform);
            }
        }

        private void ToggleCommandMode()
        {
            var combatActive = engine != null && !engine.IsCombatEnded;
            var toggled = commandMode.Toggle(combatActive);
            if (toggled.IsFailure)
            {
                AddLog("지휘 모드: " + toggled.Error);
                return;
            }

            OnCommandModeChanged();
        }

        private void OnCommandModeChanged()
        {
            if (juicePresenter != null)
            {
                juicePresenter.PlaybackFactor = commandMode.PlaybackFactor;
            }

            if (commandOverlay != null)
            {
                if (commandMode.IsActive)
                {
                    commandOverlay.Show();
                }
                else
                {
                    commandOverlay.Hide();
                }
            }

            AddLog(commandMode.IsActive ? "지휘 중 — Space로 해제" : "지휘 모드 해제");
        }

        private void TearDownCommandMode()
        {
            commandMode.SyncCombatActive(false);
            if (juicePresenter != null)
            {
                juicePresenter.PlaybackFactor = 1f;
            }

            if (commandOverlay != null)
            {
                Destroy(commandOverlay.gameObject);
                commandOverlay = null;
            }
        }

        private void CreateLighting()
        {
            var lightObject = new GameObject("Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.92f, 0.78f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.16f, 0.14f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.09f, 0.08f, 0.08f, 1f);
            RenderSettings.fogDensity = 0.025f;
        }

        private void SyncTokens()
        {
            if (analogBattlefield != null)
            {
                SyncTokensToBattlefield();
                return;
            }

            SyncTokensToMap();
        }

        // T20: tokens mirror the analog battlefield's continuous positions.
        private void SyncTokensToBattlefield()
        {
            foreach (var pair in tokens)
            {
                if (engine != null && !engine.IsAlive(pair.Key))
                {
                    pair.Value.gameObject.SetActive(false);
                    continue;
                }

                var position = analogBattlefield.FindOccupant(pair.Key);
                if (position.HasValue)
                {
                    pair.Value.MoveToAnalog(position.Value);
                    pair.Value.gameObject.SetActive(true);
                }
                else
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
        }

        private void SyncTokensToMap()
        {
            if (gridView == null || gridView.Map == null)
            {
                return;
            }

            foreach (var pair in tokens)
            {
                if (engine != null && !engine.IsAlive(pair.Key))
                {
                    pair.Value.gameObject.SetActive(false);
                    continue;
                }

                foreach (var position in gridView.Map.Positions)
                {
                    if (StringComparer.Ordinal.Equals(gridView.Map.GetOccupant(position), pair.Key))
                    {
                        pair.Value.Place(position);
                        pair.Value.gameObject.SetActive(true);
                        break;
                    }
                }
            }
        }

        private bool TryGetMouseCell(out GridPos pos)
        {
            var ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                pos = gridView.WorldToCell(hit.point);
                return true;
            }

            pos = new GridPos();
            return false;
        }

        private static Color UnitColor(string unitId, CombatTeam team)
        {
            if (team == CombatTeam.Enemy)
            {
                return new Color(0.7f, 0.2f, 0.22f, 1f);
            }

            return StringComparer.Ordinal.Equals(unitId, ReturnerId)
                ? new Color(0.2f, 0.55f, 1f, 1f)
                : new Color(0.25f, 0.85f, 0.5f, 1f);
        }

        private sealed class SliceEnemyFactory : IExpeditionEnemyFactory
        {
            private readonly TowerSliceContent content;

            public SliceEnemyFactory(TowerSliceContent content)
            {
                this.content = content;
            }

            public Result<CharacterState> Create(string kindSlot, int stairwayIndex, int floorIndex)
            {
                var characterId = "enemy-melee";
                if (StringComparer.Ordinal.Equals(kindSlot, "ranged"))
                {
                    characterId = "enemy-ranged";
                }
                else if (StringComparer.Ordinal.Equals(kindSlot, "elite"))
                {
                    characterId = "enemy-elite";
                }
                else if (StringComparer.Ordinal.Equals(kindSlot, "boss"))
                {
                    characterId = "boss";
                }

                var definition = content.ResolveCharacter(characterId);
                return CharacterState.Create(definition, slotCount: 2, assignedAbilities: definition.DefaultAbilities);
            }
        }

        private void ToggleDungeonMap()
        {
            if (dungeonMapOverlay == null)
            {
                BuildDungeonMapOverlay();
            }

            isDungeonMapOpen = !isDungeonMapOpen;
            dungeonMapOverlay.SetActive(isDungeonMapOpen);
            if (isDungeonMapOpen)
            {
                RefreshDungeonMap();
            }
        }

        private void BuildDungeonMapOverlay()
        {
            var canvasGo = GameObject.Find("Expedition Canvas");
            if (canvasGo == null) return;

            dungeonMapOverlay = RuntimeSceneUi.CreateOverlayPanel(
                canvasGo.transform,
                "Dungeon Map Overlay",
                new Vector2(0.15f, 0.15f),
                new Vector2(0.85f, 0.85f),
                new Color(0.05f, 0.05f, 0.05f, 0.95f)).gameObject;
            var mapBackground = dungeonMapOverlay.GetComponent<Image>();
            if (mapBackground != null)
            {
                mapBackground.raycastTarget = true;
            }

            var title = RuntimeSceneUi.AddText(dungeonMapOverlay.transform, "MapTitle", "던전 지도 (Tab/M을 눌러 닫기)", 22, TextAnchor.UpperCenter);
            SetAnchors(title, new Vector2(0f, 0.9f), new Vector2(1f, 1f));

            var container = new GameObject("Map Container");
            container.transform.SetParent(dungeonMapOverlay.transform, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.05f, 0.05f);
            containerRect.anchorMax = new Vector2(0.95f, 0.85f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            dungeonMapOverlay.SetActive(false);
        }

        private void RefreshDungeonMap()
        {
            if (dungeonMapOverlay == null || currentLayout == null) return;

            var container = dungeonMapOverlay.transform.Find("Map Container");
            if (container == null) return;

            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }

            var rooms = currentLayout.Rooms;
            if (rooms == null || rooms.Count == 0) return;

            int maxDepth = 0;
            foreach (var r in rooms)
            {
                if (r.Depth > maxDepth) maxDepth = r.Depth;
            }
            if (maxDepth == 0) maxDepth = 1;

            var depthGroups = new Dictionary<int, List<Tower.Gen.FloorRoom>>();
            foreach (var r in rooms)
            {
                if (!depthGroups.ContainsKey(r.Depth))
                {
                    depthGroups[r.Depth] = new List<Tower.Gen.FloorRoom>();
                }
                depthGroups[r.Depth].Add(r);
            }

            foreach (var r in rooms)
            {
                var siblings = depthGroups[r.Depth];
                int sibIndex = siblings.IndexOf(r);
                int sibCount = siblings.Count;

                float x = (float)r.Depth / maxDepth;
                float y = sibCount > 1 ? (float)sibIndex / (sibCount - 1) : 0.5f;

                float normX = Mathf.Lerp(0.05f, 0.95f, x);
                float normY = Mathf.Lerp(0.15f, 0.85f, y);

                bool isCurrent = false;
                if (encounterIndex >= 0 && encounterIndex < encounterRooms.Count)
                {
                    isCurrent = encounterRooms[encounterIndex].Id == r.Id;
                }

                Color nodeColor;
                if (isCurrent)
                {
                    nodeColor = new Color(0.1f, 0.5f, 0.1f, 0.9f);
                }
                else if (r.Id < (encounterIndex >= 0 && encounterIndex < encounterRooms.Count ? encounterRooms[encounterIndex].Id : 999))
                {
                    nodeColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                }
                else
                {
                    nodeColor = new Color(0.3f, 0.3f, 0.35f, 0.8f);
                }

                var nodePanel = RuntimeSceneUi.CreateOverlayPanel(
                    container,
                    $"RoomNode_{r.Id}",
                    new Vector2(normX - 0.06f, normY - 0.08f),
                    new Vector2(normX + 0.06f, normY + 0.08f),
                    nodeColor);

                string roomType = r.IsBossRoom ? "보스" : (r.IsEntrance ? "입구" : (r.IsExit ? "출구" : (r.Encounter.HasEncounter ? "조우" : "빈 방")));
                string currentLabel = "";
                if (isCurrent)
                {
                    currentLabel = "<color=#FFEB3B>[현재]</color>\n";
                }
                var nodeText = RuntimeSceneUi.AddText(nodePanel, "Label", $"{currentLabel}방 {r.Id}\n({roomType})", 12, TextAnchor.MiddleCenter);
                SetAnchors(nodeText, Vector2.zero, Vector2.one);
            }
        }

        private sealed class ExplorationPortalAnchor : MonoBehaviour
        {
            private readonly List<Renderer> renderers = new List<Renderer>();

            public int DoorIndex;
            public string QaLabel;
            public string DisplayLabel;
            public string Preview;
            public Color BaseColor;
            public Color HoverColor;
            public TextMesh Label;

            public void Register(Renderer renderer)
            {
                if (renderer != null)
                {
                    renderers.Add(renderer);
                }
            }

            public void SetHighlighted(bool highlighted)
            {
                var color = highlighted ? HoverColor : BaseColor;
                foreach (var renderer in renderers)
                {
                    if (renderer != null && renderer.material != null)
                    {
                        renderer.material.color = color;
                    }
                }

                if (Label != null)
                {
                    Label.color = highlighted
                        ? new Color(1f, 0.95f, 0.62f, 1f)
                        : new Color(0.95f, 0.96f, 0.92f, 1f);
                }
            }

            public void SetCamera(Camera camera)
            {
                var faces = GetComponentsInChildren<FaceCamera>();
                foreach (var face in faces)
                {
                    face.Target = camera;
                }
            }
        }

        private sealed class FaceCamera : MonoBehaviour
        {
            public Camera Target;

            private void LateUpdate()
            {
                if (Target == null)
                {
                    return;
                }

                transform.rotation = Quaternion.LookRotation(transform.position - Target.transform.position, Vector3.up);
            }
        }
    }
}
