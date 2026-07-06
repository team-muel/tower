using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tower.Combat;
using Tower.Core;
using Tower.Gen;
using UnityEngine;
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
        private BattleHudPresenter hudPresenter;
        private OrbitCameraRig orbitRig;
        private string focusedUnitId;

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
            if (engine == null || awaitingNextFloor)
            {
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
                    nextAiStepTime = Time.time + (AiStepSeconds / (hudPresenter != null ? hudPresenter.PlaybackFactor : 1f));
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
                commandMode = commandMode.IsActive
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

                var position = gridView != null && gridView.Map != null ? gridView.Map.FindOccupant(unitId) : null;
                unit.x = position.HasValue ? position.Value.X : -1;
                unit.y = position.HasValue ? position.Value.Y : -1;
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

            var sidePanel = RuntimeSceneUi.CreatePanel(
                canvas.transform,
                "Expedition Panel",
                new Vector2(0f, 0f),
                new Vector2(0.32f, 1f),
                new Vector2(12f, 12f),
                new Vector2(-12f, -12f));

            statusText = RuntimeSceneUi.AddText(sidePanel, "Status", "", 18, TextAnchor.UpperLeft);
            explorationText = RuntimeSceneUi.AddText(sidePanel, "Exploration", "", 16, TextAnchor.UpperLeft);
            turnText = RuntimeSceneUi.AddText(sidePanel, "Turn", "", 16, TextAnchor.UpperLeft);
            initiativeText = RuntimeSceneUi.AddText(sidePanel, "Initiative", "", 14, TextAnchor.UpperLeft);
            unitText = RuntimeSceneUi.AddText(sidePanel, "Units", "", 14, TextAnchor.UpperLeft);
            moveButton = RegisterQaButton(RuntimeSceneUi.AddButton(sidePanel, "Move", () => playerController?.EnterMoveMode()));

            for (var index = 0; index < 2; index++)
            {
                var slot = index;
                abilityButtons.Add(RegisterQaButton(RuntimeSceneUi.AddButton(sidePanel, "Ability " + (index + 1), () => playerController?.EnterAbilityMode(slot))));
            }

            RegisterQaButton(RuntimeSceneUi.AddButton(sidePanel, "Order: Focus Nearest", IssueFocusOrder));
            RegisterQaButton(RuntimeSceneUi.AddButton(sidePanel, "Skip Turn", () => playerController?.Skip()));
            RegisterQaButton(RuntimeSceneUi.AddButton(sidePanel, "Retreat", Retreat));
            AddDoorButton(sidePanel, "North Door");
            AddDoorButton(sidePanel, "East Door");
            AddDoorButton(sidePanel, "West Door");
            proceedButton = RegisterQaButton(RuntimeSceneUi.AddButton(sidePanel, "Next Floor", BeginNextFloor));
            proceedButton.gameObject.SetActive(false);
            RegisterQaButton(RuntimeSceneUi.AddButton(sidePanel, "Main Menu", () => SceneManager.LoadScene(TowerSceneNames.Boot)));
            resultText = RuntimeSceneUi.AddText(sidePanel, "Result", "", 15, TextAnchor.UpperLeft);
            logText = RuntimeSceneUi.AddText(sidePanel, "Log", "", 14, TextAnchor.UpperLeft);
            HideDoorButtons();
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
            encounterRooms.Clear();
            encounterRooms.AddRange(layout.Rooms
                .Where(room => room.Encounter.HasEncounter)
                .OrderBy(room => room.Depth)
                .ThenBy(room => room.Id));
            encounterIndex = 0;

            AddLog($"Entered stairway {state.StairwayIndex}, floor {state.FloorIndex}. Rooms: {layout.Rooms.Count}, encounters: {encounterRooms.Count}.");
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
            if (explorationText != null)
            {
                explorationText.text = $"탐험 | {state.StairwayIndex}층계 · {state.FloorIndex}층 · 방 {encounterIndex + 1}/{encounterRooms.Count}\n"
                    + $"다음 방: {nextRoomPreview}\n문을 골라 다음 방으로 이동";
            }

            if (resultText != null)
            {
                resultText.text = string.Empty;
            }

            RefreshCombatHud();
            ShowDoorButtons();
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
            var map = room.Map;
            var party = state.Roster.Where(member => !member.IsDead).ToList();
            var spawnCells = map.Positions.Where(position => map.CanEnter(position)).ToList();
            if (spawnCells.Count < party.Count + room.Encounter.EnemyCount)
            {
                AddLog("Room has too few cells for combat.");
                return;
            }

            var gridObject = new GameObject("Runtime Grid");
            gridView = gridObject.AddComponent<GridView>();
            gridView.Build(map);
            highlighter = gridObject.AddComponent<TileHighlighter>();
            highlighter.Initialize(gridView);

            var combatants = new List<CombatantRef>();
            for (var index = 0; index < party.Count; index++)
            {
                var member = party[index];
                var cell = spawnCells[index];
                var token = UnitToken.Spawn(gridView, cell, member.UnitId, UnitColor(member.UnitId, CombatTeam.Player));
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
                var token = UnitToken.Spawn(gridView, cell, unitId, UnitColor(unitId, CombatTeam.Enemy));
                tokens[unitId] = token;

                var combatant = CombatantRef.Create(unitId, CombatTeam.Enemy, enemyState.Value);
                if (combatant.IsFailure)
                {
                    AddLog(combatant.Error);
                    return;
                }

                combatants.Add(combatant.Value);
            }

            statusBoard = new StatusBoard();
            var resolver = AbilityResolver.Create(map, statusBoard);
            if (resolver.IsFailure)
            {
                AddLog(resolver.Error);
                return;
            }

            var presenter = new BattleHudPresenter(AddLog);
            hudPresenter = presenter;
            var engineResult = TurnEngine.Create(combatants, presenter, resolver.Value);
            if (engineResult.IsFailure)
            {
                AddLog(engineResult.Error);
                return;
            }

            engine = engineResult.Value;

            var scorer = ActionScorer.Create(map, statusBoard);
            if (scorer.IsFailure)
            {
                AddLog(scorer.Error);
                return;
            }

            var driver = CoreAiTurnDriver.Create(engine, map, scorer.Value);
            if (driver.IsFailure)
            {
                AddLog(driver.Error);
                return;
            }

            aiDriver = driver.Value;
            orderBoard = OrderBoard.CreateDefault();

            var playerToken = tokens.TryGetValue(ReturnerId, out var tokenResult) ? tokenResult : null;
            var enemyTokens = tokens.Values.Where(token => token.OccupantId.StartsWith("enemy-", StringComparison.Ordinal)).ToArray();
            var abilities = engine.GetCombatant(ReturnerId)?.State.Loadout.Abilities ?? Array.Empty<AbilityDef>();
            playerController = new PlayerTurnController(engine, gridView, highlighter, playerToken, enemyTokens, orderBoard, ReturnerId, abilities, presenter);

            CreateCamera(room.Map.Width, room.Map.Height);
            CreateCommandOverlay(party);
            RefreshAbilityButtons(abilities);
            RefreshStatus();
            RefreshCombatHud();
            AddLog($"Encounter {room.Id} started. Keys: M, 1, 2 · Space 지휘 모드.");
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

            SyncTokensToMap();
            RefreshStatus();
            RefreshCombatHud();
        }

        private void HandlePlayerInput()
        {
            if (gridView == null || sceneCamera == null || playerController == null)
            {
                return;
            }

            if (commandMode.IsActive)
            {
                // 지휘 모드: 셀 클릭/모드 키 대신 오버레이 버튼이 입력을 받는다.
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
                    SyncTokensToMap();
                    RefreshStatus();
                    RefreshCombatHud();
                }
            }
            else
            {
                highlighter.SetHover(null);
            }
        }

        private void ResolveEncounter()
        {
            TearDownCommandMode();
            var winner = engine.WinningTeam;
            SyncPartyState();
            AddLog(winner == CombatTeam.Player ? "Encounter cleared." : "Party wiped.");
            engine = null;
            playerController = null;
            aiDriver = null;

            if (winner == CombatTeam.Player)
            {
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
            ShowProceedButton("Return to Menu", () => SceneManager.LoadScene(TowerSceneNames.Boot));
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

        private void IssueFocusOrder()
        {
            if (engine == null || playerController == null || !playerController.IsPlayerTurn())
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

            playerController.EnterOrderMode(target.UnitId);
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
            turnText.text = $"Round {engine.RoundNumber} | Active: {active}";
            var isPlayerTurn = playerController != null && playerController.IsPlayerTurn();
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
                + string.Join("\n", state.Roster.Select(member => $"{member.UnitId}: {member.State.CurrentHp}/{member.State.Definition.MaxHp} HP, deaths {member.State.DeathCount}"));
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
            while (logLines.Count > 12)
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

            ShowProceedButton("Return to Menu", () => SceneManager.LoadScene(TowerSceneNames.Boot));
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

            gridView = null;
            highlighter = null;
            statusBoard = null;
            engine = null;
            aiDriver = null;
            playerController = null;
            hudPresenter = null;
        }

        // T19: combat runs on the orbit rig; camp/exploration scenes keep the
        // iso follow rig (the rigs coexist, one per scene mode).
        private void CreateCamera(int width, int height)
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
            orbitRig.FocusWorld(gridView.CellToWorld(new GridPos(width / 2, height / 2)));
            sceneCamera = orbitRig.Camera;
            focusedUnitId = null;
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
            if (hudPresenter != null)
            {
                hudPresenter.PlaybackFactor = commandMode.PlaybackFactor;
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
            if (hudPresenter != null)
            {
                hudPresenter.PlaybackFactor = 1f;
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
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
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
    }
}
