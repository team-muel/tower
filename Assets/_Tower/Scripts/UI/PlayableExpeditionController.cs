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

        private readonly Dictionary<string, UnitToken> tokens = new Dictionary<string, UnitToken>(StringComparer.Ordinal);
        private readonly List<FloorRoom> encounterRooms = new List<FloorRoom>();
        private readonly List<string> logLines = new List<string>();

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
        private Camera sceneCamera;
        private Text statusText;
        private Text turnText;
        private Text logText;
        private Button moveButton;
        private Button proceedButton;
        private readonly List<Button> abilityButtons = new List<Button>();
        private int currentAbilityCount;
        private int encounterIndex;
        private bool awaitingNextFloor;
        private float nextAiStepTime;

        private void Start()
        {
            content = TowerSliceContent.Create();
            BuildUi();
            CreateLighting();
            OpenRepository();
            OpenExpedition();
        }

        private void Update()
        {
            if (engine == null || awaitingNextFloor)
            {
                return;
            }

            if (engine.IsCombatEnded)
            {
                ResolveEncounter();
                return;
            }

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
                    nextAiStepTime = Time.time + 0.25f;
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
            turnText = RuntimeSceneUi.AddText(sidePanel, "Turn", "", 16, TextAnchor.UpperLeft);
            moveButton = RuntimeSceneUi.AddButton(sidePanel, "Move", () => playerController?.EnterMoveMode());

            for (var index = 0; index < 2; index++)
            {
                var slot = index;
                abilityButtons.Add(RuntimeSceneUi.AddButton(sidePanel, "Ability " + (index + 1), () => playerController?.EnterAbilityMode(slot)));
            }

            RuntimeSceneUi.AddButton(sidePanel, "Order: Focus Nearest", IssueFocusOrder);
            RuntimeSceneUi.AddButton(sidePanel, "Skip Turn", () => playerController?.Skip());
            RuntimeSceneUi.AddButton(sidePanel, "Retreat", Retreat);
            proceedButton = RuntimeSceneUi.AddButton(sidePanel, "Next Floor", BeginNextFloor);
            proceedButton.gameObject.SetActive(false);
            RuntimeSceneUi.AddButton(sidePanel, "Main Menu", () => SceneManager.LoadScene(TowerSceneNames.Boot));
            logText = RuntimeSceneUi.AddText(sidePanel, "Log", "", 14, TextAnchor.UpperLeft);
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
            StartNextEncounterOrClearFloor();
        }

        private void StartNextEncounterOrClearFloor()
        {
            if (encounterIndex >= encounterRooms.Count)
            {
                ClearFloor();
                return;
            }

            var room = encounterRooms[encounterIndex++];
            StartEncounter(room);
        }

        private void StartEncounter(FloorRoom room)
        {
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

            var statusBoard = new StatusBoard();
            var resolver = AbilityResolver.Create(map, statusBoard);
            if (resolver.IsFailure)
            {
                AddLog(resolver.Error);
                return;
            }

            var presenter = new BattleHudPresenter(AddLog);
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
            RefreshAbilityButtons(abilities);
            RefreshStatus();
            AddLog($"Encounter {room.Id} started. Use buttons or keys: M, 1, 2, Space.");
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
        }

        private void HandlePlayerInput()
        {
            if (gridView == null || sceneCamera == null || playerController == null)
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

            if (Input.GetKeyDown(KeyCode.Space))
            {
                playerController.Skip();
            }

            if (TryGetMouseCell(out var hover) && gridView.Map.InBounds(hover))
            {
                highlighter.SetHover(hover);
                if (Input.GetMouseButtonDown(0))
                {
                    playerController.OnCellClicked(hover);
                    SyncTokensToMap();
                    RefreshStatus();
                }
            }
            else
            {
                highlighter.SetHover(null);
            }
        }

        private void ResolveEncounter()
        {
            var winner = engine.WinningTeam;
            SyncPartyState();
            AddLog(winner == CombatTeam.Player ? "Encounter cleared." : "Party wiped.");
            engine = null;
            playerController = null;
            aiDriver = null;

            if (winner == CombatTeam.Player)
            {
                StartNextEncounterOrClearFloor();
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
            AddLog("Floor cleared. Press Next Floor to continue.");
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
            AddLog(progress.Value.Outcome == ExpeditionOutcome.GreatRegression
                ? "Great regression: back to floor 1."
                : "Retreated to the last checkpoint.");
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
            RefreshStatus();
            AddLog("Stairway conquered. Shortcut saved.");
            ShowProceedButton("Return to Menu", () => SceneManager.LoadScene(TowerSceneNames.Boot));
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
            engine = null;
            aiDriver = null;
            playerController = null;
        }

        private void CreateCamera(int width, int height)
        {
            var existing = FindFirstObjectByType<IsoCameraRig>();
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            var cameraRigObject = new GameObject("Iso Camera Rig");
            var cameraRig = cameraRigObject.AddComponent<IsoCameraRig>();
            cameraRig.Focus(gridView, new GridPos(width / 2, height / 2));
            sceneCamera = cameraRig.Camera;
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
