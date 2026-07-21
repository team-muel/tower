using System;
using System.Collections.Generic;
using Tower.Core;
using Tower.Gen;
using UnityEngine;

namespace Tower.Combat
{
    // Generated-floor bridge from data-authored combatants to the deterministic
    // real-time Core driver. Player position stays externally controlled while
    // companions/enemies use utility-AI movement and every body projects HP.
    public sealed class GeneratedFloorEncounterHost : MonoBehaviour
    {
        private const float ArenaSize = 28f;
        private const float ArenaHalf = ArenaSize * 0.5f;
        private const float SimulationTickSeconds = 0.1f;
        private const float MovementUnitsPerSecond = 3f;
        private const int MaxCatchUpSteps = 8;

        private readonly List<GameObject> enemies = new List<GameObject>();
        private readonly List<EnemyCombatProfile> spawnedProfiles = new List<EnemyCombatProfile>();
        private readonly List<PillbugBrain> brains = new List<PillbugBrain>();
        private readonly List<CompanionEntity> companions = new List<CompanionEntity>();
        private readonly List<CombatantWorldView> views = new List<CombatantWorldView>();
        private readonly Dictionary<string, Transform> bodies =
            new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, PillbugBrain> enemyBrains =
            new Dictionary<string, PillbugBrain>(StringComparer.Ordinal);

        private EncounterEngagementController engagement;
        private AnalogBattlefield battlefield;
        private AutonomousCombatDriver driver;
        private Action<GeneratedEncounterResult> resolved;
        private Action<string> defeated;
        private Transform player;
        private Behaviour playerMovement;
        private Vector3 arenaCenter;
        private string eventId;
        private string playerUnitId;
        private RealtimeCommandBoard commandBoard;
        private CombatPresentationObserver presentationObserver;
        private CombatFeedbackPresenter feedbackPresenter;
        private bool secondaryBrainsEnabled;
        private float simulationAccumulator;

        public int EnemyCount => enemies.Count;
        public bool IsCombatActive => engagement != null && engagement.IsCombatActive;

        // T58: the run lifecycle reacts to enemy-team victory (retreat).
        // Kept separate from Configure so existing call sites stay intact.
        public void SetDefeatedHandler(Action<string> handler) => defeated = handler;

        // T61: meta slot unlocks change the player's starting slot count.
        // Must be set before Configure; zero means "use the definition".
        private int playerSlotOverride;
        public void SetPlayerSlotOverride(int slotCount) => playerSlotOverride = slotCount;
        public bool IsResolved { get; private set; }
        public bool IsPlayerDefeated { get; private set; }
        public IReadOnlyList<GameObject> Enemies => enemies;
        public IReadOnlyList<CombatantWorldView> Views => views;
        public IReadOnlyList<CompanionEntity> Companions => companions;
        public CombatState CombatState { get; private set; }
        public CombatMetrics Metrics { get; private set; }
        public string PlayerUnitId => playerUnitId;
        public RealtimeCommandBoard CommandBoard => commandBoard;
        public CommandTelemetrySnapshot CommandTelemetry => commandBoard == null
            ? default
            : commandBoard.Telemetry;

        // T71: fills the engine-neutral QA DTO without exposing mutable combat
        // collections. Unit order, intents, and precise orders are all
        // deterministic so a state poll can be compared across runs.
        public void PopulateQaCombatSnapshot(QaCombatSnapshot snapshot, bool commandWindowActive)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.round = 0;
            snapshot.activeUnitId = string.Empty;
            snapshot.remainingOrders = commandBoard == null ? 0 : commandBoard.PreciseOrders.Count;
            snapshot.commandMode = commandWindowActive;
            snapshot.spaceMode = battlefield == null ? string.Empty : battlefield.Mode.ToString();
            snapshot.elapsedSeconds = CombatState == null ? 0f : CombatState.ElapsedSeconds;
            snapshot.encounterActive = IsCombatActive;
            snapshot.encounterResolved = IsResolved;
            snapshot.playerDefeated = IsPlayerDefeated;
            snapshot.winningTeam = CombatState == null || !CombatState.WinningTeam.HasValue
                ? string.Empty
                : CombatState.WinningTeam.Value.ToString();
            snapshot.feedbackPopupCount = feedbackPresenter == null
                ? 0
                : feedbackPresenter.ActivePopupCount;

            CommandTelemetrySnapshot telemetry = CommandTelemetry;
            snapshot.stanceCommands = telemetry.StanceCommands;
            snapshot.preciseOrdersIssued = telemetry.PreciseOrdersIssued;
            snapshot.preciseOrdersReplaced = telemetry.PreciseOrdersReplaced;
            snapshot.preciseOrdersConsumed = telemetry.PreciseOrdersConsumed;
            snapshot.preciseOrdersExpired = telemetry.PreciseOrdersExpired;
            snapshot.preciseOrderFallbacks = telemetry.PreciseOrderFallbacks;

            snapshot.initiativeOrder = snapshot.initiativeOrder ?? new List<string>();
            snapshot.units = snapshot.units ?? new List<QaUnitSnapshot>();
            snapshot.intents = snapshot.intents ?? new List<QaIntentSnapshot>();
            snapshot.preciseOrders = snapshot.preciseOrders ?? new List<QaPreciseOrderSnapshot>();
            snapshot.initiativeOrder.Clear();
            snapshot.units.Clear();
            snapshot.intents.Clear();
            snapshot.preciseOrders.Clear();

            if (CombatState == null)
            {
                return;
            }

            foreach (string unitId in CombatState.UnitIds)
            {
                CombatantRef combatant = CombatState.GetCombatant(unitId);
                if (combatant == null || combatant.State == null)
                {
                    continue;
                }

                snapshot.initiativeOrder.Add(unitId);
                var unit = new QaUnitSnapshot
                {
                    unitId = unitId,
                    team = combatant.Team.ToString(),
                    currentHp = combatant.State.CurrentHp,
                    maxHp = combatant.State.Definition.MaxHp,
                    alive = combatant.IsAlive,
                    x = -1f,
                    y = -1f,
                    disposition = combatant.State.Definition.Disposition.ToString()
                };

                BattlePos? position = battlefield == null ? null : battlefield.FindOccupant(unitId);
                if (position.HasValue)
                {
                    unit.x = position.Value.X;
                    unit.y = position.Value.Y;
                }

                IReadOnlyList<string> marks = CombatState.StatusBoard.GetActiveMarkIds(
                    unitId,
                    CombatState.ElapsedSeconds);
                for (int markIndex = 0; markIndex < marks.Count; markIndex++)
                {
                    unit.marks.Add(marks[markIndex]);
                }

                if (driver != null
                    && driver.TryGetActiveIntent(unitId, out AutonomousCombatIntent intent))
                {
                    string action = intent.Plan.Kind == AiPlanKind.Ability
                        ? intent.Plan.AbilityId
                        : intent.Plan.Kind.ToString();
                    unit.pendingAbility = intent.Plan.AbilityId ?? string.Empty;
                    unit.intent = action ?? string.Empty;

                    snapshot.intents.Add(new QaIntentSnapshot
                    {
                        unitId = intent.UnitId,
                        planKind = intent.Plan.Kind.ToString(),
                        abilityId = intent.Plan.AbilityId ?? string.Empty,
                        targetUnitId = intent.Plan.TargetUnitId ?? string.Empty,
                        disposition = intent.Disposition.ToString(),
                        stance = CommandStanceRules.DisplayName(intent.Stance),
                        executeAtSeconds = intent.ExecuteAtSeconds,
                        precise = intent.IsPreciseOrder
                    });
                }

                snapshot.units.Add(unit);
            }

            if (commandBoard == null)
            {
                return;
            }

            var orderIds = new List<string>(commandBoard.PreciseOrders.Keys);
            orderIds.Sort(StringComparer.Ordinal);
            for (int index = 0; index < orderIds.Count; index++)
            {
                PreciseOrder order = commandBoard.PreciseOrders[orderIds[index]];
                snapshot.preciseOrders.Add(new QaPreciseOrderSnapshot
                {
                    unitId = order.UnitId,
                    abilityId = order.AbilityId,
                    targetUnitId = order.TargetUnitId,
                    expiresAtSeconds = order.ExpiresAtSeconds
                });
            }
        }

        public Result Configure(
            Transform playerTransform,
            Behaviour playerMovementBehaviour,
            CharacterDef playerDefinition,
            IReadOnlyList<CompanionEntity> companionEntities,
            IReadOnlyList<EnemyCombatProfile> enemyProfiles,
            FloorEncounter encounter,
            RunEventSlot runEvent,
            Vector3 spawnCenter,
            Action<GeneratedEncounterResult> onResolved,
            float triggerRadius = 7f,
            float introHoldSeconds = 0.45f,
            RealtimeCommandBoard realtimeCommandBoard = null)
        {
            if (playerTransform == null || playerMovementBehaviour == null || playerDefinition == null
                || encounter == null || runEvent == null)
            {
                return Result.Failure(
                    "Generated encounter requires player, movement, player definition, encounter, and run event.");
            }

            if (!encounter.HasEncounter)
            {
                return Result.Failure("Generated encounter cannot present an empty composition.");
            }

            if ((runEvent.Kind == RunEventKind.Boss) != encounter.IsBoss)
            {
                return Result.Failure("Run event kind must match the generated encounter composition.");
            }

            Result playerValid = ValidateDefinition(playerDefinition, "Player");
            if (playerValid.IsFailure)
            {
                return playerValid;
            }

            var profilesByKind = new Dictionary<string, EnemyCombatProfile>(StringComparer.Ordinal);
            if (enemyProfiles != null)
            {
                for (int index = 0; index < enemyProfiles.Count; index++)
                {
                    EnemyCombatProfile profile = enemyProfiles[index];
                    if (profile == null)
                    {
                        return Result.Failure("Enemy combat profiles cannot contain null entries.");
                    }

                    Result valid = profile.Validate();
                    if (valid.IsFailure)
                    {
                        return valid;
                    }

                    if (profilesByKind.ContainsKey(profile.KindSlot))
                    {
                        return Result.Failure("Enemy combat profile kind slots must be unique.");
                    }

                    profilesByKind.Add(profile.KindSlot, profile);
                }
            }

            for (int index = 0; index < encounter.EnemySlots.Count; index++)
            {
                if (!profilesByKind.ContainsKey(encounter.EnemySlots[index].KindSlot))
                {
                    return Result.Failure(
                        $"No enemy combat profile is registered for '{encounter.EnemySlots[index].KindSlot}'.");
                }
            }

            player = playerTransform;
            playerMovement = playerMovementBehaviour;
            playerUnitId = playerDefinition.Id;
            commandBoard = realtimeCommandBoard ?? new RealtimeCommandBoard();
            eventId = runEvent.EventId;
            resolved = onResolved;
            arenaCenter = spawnCenter;
            companions.Clear();
            if (companionEntities != null)
            {
                for (int index = 0; index < companionEntities.Count; index++)
                {
                    CompanionEntity companion = companionEntities[index];
                    if (companion == null || companion.CharacterDefinition == null)
                    {
                        return Result.Failure("Generated encounter companions require character-backed entities.");
                    }

                    Result valid = ValidateDefinition(companion.CharacterDefinition, "Companion");
                    if (valid.IsFailure)
                    {
                        return valid;
                    }

                    companions.Add(companion);
                }
            }

            for (int index = 0; index < encounter.EnemySlots.Count; index++)
            {
                FloorEnemySlot slot = encounter.EnemySlots[index];
                SpawnEnemy(slot, profilesByKind[slot.KindSlot], index, encounter.EnemySlots.Count, spawnCenter);
            }

            Result runtime = CreateCombatRuntime(playerDefinition, encounter);
            if (runtime.IsFailure)
            {
                CleanupPresentation();
                return runtime;
            }

            engagement = gameObject.AddComponent<EncounterEngagementController>();
            Result configured = engagement.Configure(
                player,
                enemies[0].transform,
                playerMovement,
                brains[0],
                triggerRadius,
                introHoldSeconds);
            if (configured.IsFailure)
            {
                CleanupPresentation();
                return configured;
            }

            Transform[] enemyTransforms = EnemyTransforms();
            for (int index = 0; index < companions.Count; index++)
            {
                companions[index].SetEnemyTargets(enemyTransforms);
            }

            Debug.Log(
                $"[GeneratedEncounter] Spawned event={eventId} floor={runEvent.FloorNumber} "
                + $"kind={runEvent.Kind} enemies={encounter.EnemyCount}; combat runtime ready.",
                this);
            return Result.Success();
        }

        // Deterministic test seam: advances entry using real/unscaled time and,
        // once active, advances the scaled combat clock by the supplied delta.
        public void Tick(float deltaSeconds)
        {
            if (engagement == null || IsResolved || IsPlayerDefeated)
            {
                return;
            }

            engagement.Tick(deltaSeconds);
            EnableCombatPresentationIfActive();
            TickCombat(deltaSeconds);
        }

        public Result ResolveEncounter()
        {
            if (engagement == null || CombatState == null)
            {
                return Result.Failure("Generated encounter is not configured.");
            }

            if (IsResolved)
            {
                return Result.Success();
            }

            if (!CombatState.IsCombatEnded || CombatState.WinningTeam != CombatTeam.Player)
            {
                return Result.Failure("Generated encounter resolves only after player-team victory.");
            }

            Result result = engagement.ResolveEncounter();
            if (result.IsFailure)
            {
                return result;
            }

            IsResolved = true;
            commandBoard?.ClearPreciseOrders();
            EndCombatPresentation();
            var combatResult = new GeneratedEncounterResult(
                eventId,
                CombatState.WinningTeam.Value,
                Metrics.ActionCount,
                CombatState.ElapsedSeconds);
            resolved?.Invoke(combatResult);
            Debug.Log(
                $"[GeneratedEncounter] Resolved event={eventId}; traversal unlocked; "
                + $"actions={Metrics.ActionCount} duration={CombatState.ElapsedSeconds:0.0}s.",
                this);
            CleanupCombatViews();
            CleanupEnemies();
            return Result.Success();
        }

        private void Update()
        {
            if (IsResolved || IsPlayerDefeated)
            {
                return;
            }

            EnableCombatPresentationIfActive();
            TickCombat(Time.deltaTime);
        }

        private void EnableCombatPresentationIfActive()
        {
            if (secondaryBrainsEnabled || !IsCombatActive)
            {
                return;
            }

            secondaryBrainsEnabled = true;
            for (int index = 0; index < brains.Count; index++)
            {
                brains[index].SetEngagementEnabled(true);
                brains[index].SetMotionEnabled(false);
            }

            for (int index = 0; index < companions.Count; index++)
            {
                companions[index].SetCombatDriven(true);
            }
        }

        private void TickCombat(float deltaSeconds)
        {
            if (!IsCombatActive || CombatState == null || CombatState.IsCombatEnded
                || deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            simulationAccumulator += deltaSeconds;
            int catchUpSteps = 0;
            while (simulationAccumulator + 0.0001f >= SimulationTickSeconds
                && catchUpSteps < MaxCatchUpSteps && !CombatState.IsCombatEnded)
            {
                simulationAccumulator -= SimulationTickSeconds;
                catchUpSteps++;
                SyncPlayerPosition();
                Result<AutonomousCombatTick> stepped = driver.Step();
                if (stepped.IsFailure)
                {
                    Debug.LogError(stepped.Error, this);
                    enabled = false;
                    return;
                }

                if (feedbackPresenter != null && presentationObserver != null
                    && presentationObserver.Events.PendingDamageCount > 0)
                {
                    feedbackPresenter.Present(presentationObserver.Events.DrainDamageEvents());
                }

                SyncWorldViews();
            }

            if (!CombatState.IsCombatEnded)
            {
                return;
            }

            if (CombatState.WinningTeam == CombatTeam.Player)
            {
                Result resolvedResult = ResolveEncounter();
                if (resolvedResult.IsFailure)
                {
                    Debug.LogError(resolvedResult.Error, this);
                }
            }
            else
            {
                HandlePlayerDefeat();
            }
        }

        private Result CreateCombatRuntime(CharacterDef playerDefinition, FloorEncounter encounter)
        {
            battlefield = new AnalogBattlefield(ArenaSize, ArenaSize);
            var statusBoard = new StatusBoard();
            Metrics = new CombatMetrics();
            presentationObserver = new CombatPresentationObserver(Metrics);
            var combatants = new List<CombatantRef>();

            Result playerAdded = AddCombatant(
                combatants,
                playerUnitId,
                CombatTeam.Player,
                playerDefinition,
                player,
                2.15f,
                playerSlotOverride);
            if (playerAdded.IsFailure) return playerAdded;

            for (int index = 0; index < companions.Count; index++)
            {
                CompanionEntity companion = companions[index];
                Result companionAdded = AddCombatant(
                    combatants,
                    companion.UnitId,
                    CombatTeam.Player,
                    companion.CharacterDefinition,
                    companion.transform,
                    2.15f);
                if (companionAdded.IsFailure) return companionAdded;
            }

            for (int index = 0; index < encounter.EnemySlots.Count; index++)
            {
                string unitId = EnemyUnitId(index);
                EnemyCombatProfile profile = spawnedProfiles[index];
                Result enemyAdded = AddCombatant(
                    combatants,
                    unitId,
                    CombatTeam.Enemy,
                    profile.CharacterDefinition,
                    enemies[index].transform,
                    profile.HealthBarHeight);
                if (enemyAdded.IsFailure) return enemyAdded;
            }

            Result<CombatState> state = CombatState.Create(combatants, statusBoard, presentationObserver);
            if (state.IsFailure) return Result.Failure(state.Error);
            CombatState = state.Value;

            feedbackPresenter = gameObject.AddComponent<CombatFeedbackPresenter>();
            feedbackPresenter.Configure(bodies, combatants);

            Result<ActionScorer> scorer = ActionScorer.Create(battlefield, statusBoard);
            if (scorer.IsFailure) return Result.Failure(scorer.Error);
            Result<AbilityResolver> resolver = AbilityResolver.Create(battlefield, statusBoard, presentationObserver);
            if (resolver.IsFailure) return Result.Failure(resolver.Error);
            Result<AutonomousCombatDriver> createdDriver = AutonomousCombatDriver.Create(
                CombatState,
                battlefield,
                scorer.Value,
                resolver.Value,
                SimulationTickSeconds,
                MovementUnitsPerSecond,
                new[] { playerUnitId },
                commandBoard);
            if (createdDriver.IsFailure) return Result.Failure(createdDriver.Error);
            driver = createdDriver.Value;
            SyncWorldViews();
            return Result.Success();
        }

        private Result AddCombatant(
            ICollection<CombatantRef> combatants,
            string unitId,
            CombatTeam team,
            CharacterDef definition,
            Transform body,
            float healthBarHeight,
            int slotOverride = 0)
        {
            if (bodies.ContainsKey(unitId))
            {
                return Result.Failure("Combatant unit ids must be unique.");
            }

            int slotCount = slotOverride > 0
                ? Mathf.Clamp(slotOverride, definition.DefaultAbilities.Length, AbilityLoadout.MaxSlots)
                : definition.DefaultAbilities.Length;
            Result<CharacterState> state = CharacterState.Create(
                definition,
                slotCount: slotCount,
                assignedAbilities: definition.DefaultAbilities);
            if (state.IsFailure) return Result.Failure(state.Error);
            Result<CombatantRef> combatant = CombatantRef.Create(unitId, team, state.Value);
            if (combatant.IsFailure) return Result.Failure(combatant.Error);

            BattlePos desired = WorldToBattle(body.position);
            if (!TryPlaceOpen(unitId, desired))
            {
                return Result.Failure($"Could not place combatant '{unitId}' in the generated arena.");
            }

            combatants.Add(combatant.Value);
            bodies.Add(unitId, body);
            GameObject viewObject = new GameObject("CombatView_" + unitId);
            viewObject.transform.SetParent(body, false);
            CombatantWorldView view = viewObject.AddComponent<CombatantWorldView>();
            Result configured = view.Configure(unitId, body, healthBarHeight);
            if (configured.IsFailure) return configured;
            views.Add(view);
            return Result.Success();
        }

        private void SpawnEnemy(
            FloorEnemySlot slot,
            EnemyCombatProfile profile,
            int index,
            int count,
            Vector3 spawnCenter)
        {
            Vector3 offset = FormationOffset(index, count);
            string enemyName = $"GeneratedEnemy_{slot.KindSlot}_{slot.Index:00}";
            GameObject enemy;
            if (profile.BodyStyle == EnemyBodyStyle.Pillbug)
            {
                enemy = PillbugBodyBuilder.Build(enemyName, profile.BodyColor);
            }
            else
            {
                enemy = GameObject.CreatePrimitive(profile.BodyPrimitive);
                enemy.name = enemyName;
                Renderer renderer = enemy.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = TowerRuntimeMaterials.CreateLit(enemy.name + " Material", profile.BodyColor);
                }
            }

            enemy.transform.SetParent(transform, true);
            enemy.transform.position = spawnCenter + offset + (Vector3.up * 0.5f);
            enemy.transform.localScale = profile.BodyScale;

            PillbugBrain brain = enemy.AddComponent<PillbugBrain>();
            var companionTargets = new Transform[companions.Count];
            for (int companionIndex = 0; companionIndex < companions.Count; companionIndex++)
            {
                companionTargets[companionIndex] = companions[companionIndex].transform;
            }

            brain.Configure(player, companionTargets, DefaultTuning(profile));
            brain.SetMotionEnabled(false);
            brain.SetEngagementEnabled(false);
            brain.enabled = profile.PillbugTelegraph;
            enemies.Add(enemy);
            spawnedProfiles.Add(profile);
            brains.Add(brain);
            enemyBrains.Add(EnemyUnitId(index), brain);
        }

        private void SyncPlayerPosition()
        {
            if (player == null || battlefield == null)
            {
                return;
            }

            BattlePos desired = WorldToBattle(player.position);
            if (!battlefield.Contains(desired))
            {
                desired = new BattlePos(
                    Mathf.Clamp(desired.X, AnalogBattlefield.DefaultUnitRadius, ArenaSize - AnalogBattlefield.DefaultUnitRadius),
                    Mathf.Clamp(desired.Y, AnalogBattlefield.DefaultUnitRadius, ArenaSize - AnalogBattlefield.DefaultUnitRadius));
            }

            battlefield.TryMoveOccupant(playerUnitId, desired);
        }

        private void SyncWorldViews()
        {
            for (int index = 0; index < views.Count; index++)
            {
                CombatantWorldView view = views[index];
                CombatantRef combatant = CombatState.GetCombatant(view.UnitId);
                if (combatant == null) continue;
                view.Refresh(combatant.State);
                if (driver != null && driver.TryGetActiveIntent(view.UnitId, out AutonomousCombatIntent intent))
                {
                    view.SetIntent(intent);
                }
                else
                {
                    view.ClearIntent();
                }

                if (combatant.IsAlive && !StringComparer.Ordinal.Equals(view.UnitId, playerUnitId)
                    && bodies.TryGetValue(view.UnitId, out Transform body))
                {
                    BattlePos? position = battlefield.FindOccupant(view.UnitId);
                    if (position.HasValue)
                    {
                        Vector3 world = BattleToWorld(position.Value);
                        body.position = new Vector3(world.x, body.position.y, world.z);
                    }
                }

                if (!combatant.IsAlive && enemyBrains.TryGetValue(view.UnitId, out PillbugBrain brain))
                {
                    brain.SetEngagementEnabled(false);
                }
            }
        }

        public Result SetCommandStance(string companionUnitId, CommandStance stance, string focusTargetId = null)
        {
            if (commandBoard == null)
            {
                return Result.Failure("Command board is not ready.");
            }

            if (!IsCompanionUnit(companionUnitId))
            {
                return Result.Failure("Command stance requires a living companion unit.");
            }

            return commandBoard.SetStance(companionUnitId, stance, focusTargetId);
        }

        public Result IssueBestPreciseOrder(string companionUnitId, bool commandWindowActive)
        {
            if (commandBoard == null || CombatState == null || battlefield == null)
            {
                return Result.Failure("Combat command runtime is not ready.");
            }

            if (!IsCompanionUnit(companionUnitId))
            {
                return Result.Failure("Precise order requires a living companion unit.");
            }

            string targetId = NearestLivingEnemy(companionUnitId);
            if (string.IsNullOrEmpty(targetId))
            {
                return Result.Failure("No living enemy is available for a precise order.");
            }

            CombatantRef companion = CombatState.GetCombatant(companionUnitId);
            string abilityId = null;
            BattlePos? targetPoint = null;
            foreach (AbilityDef ability in companion.State.Loadout.Abilities)
            {
                bool canTargetEnemy = ability != null
                    && ((ability.TargetType == AbilityTargetType.Enemy
                            && ability.Tag != AbilityTag.Amplify)
                        || (ability.TargetType == AbilityTargetType.Cell
                            && (ability.Tag == AbilityTag.Apply || ability.Tag == AbilityTag.Consume)));
                if (canTargetEnemy && companion.State.RemainingCooldownSeconds(ability.Id) <= 0f)
                {
                    abilityId = ability.Id;
                    if (ability.TargetType == AbilityTargetType.Cell)
                    {
                        targetPoint = battlefield.FindOccupant(targetId);
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(abilityId))
            {
                return Result.Failure("Companion has no ready ability for a precise order.");
            }

            return commandBoard.IssuePreciseOrder(
                companionUnitId,
                abilityId,
                targetId,
                targetPoint,
                commandWindowActive,
                CombatState.ElapsedSeconds);
        }

        // T69: the player can choose a concrete loadout slot while bullet-time
        // is active. Target selection stays deliberately lightweight for the
        // first direct-input lane: offensive abilities use the nearest enemy,
        // cell abilities use that enemy's occupied point, and ally abilities
        // target the player. Movement remains external, so an order that is
        // out of range can still fall back safely through the Core driver.
        public Result IssuePlayerPreciseOrder(int abilityIndex, bool commandWindowActive)
        {
            if (commandBoard == null || CombatState == null || battlefield == null)
            {
                return Result.Failure("Combat command runtime is not ready.");
            }

            CombatantRef playerCombatant = CombatState.GetCombatant(playerUnitId);
            if (playerCombatant == null || !CombatState.IsAlive(playerUnitId))
            {
                return Result.Failure("Player is not a living combatant.");
            }

            if (abilityIndex < 0 || abilityIndex >= playerCombatant.State.Loadout.Abilities.Count)
            {
                return Result.Failure("Player ability slot is out of range.");
            }

            AbilityDef ability = playerCombatant.State.Loadout.Abilities[abilityIndex];
            if (ability == null)
            {
                return Result.Failure("Player ability slot is empty.");
            }

            if (playerCombatant.State.RemainingCooldownSeconds(ability.Id) > 0f)
            {
                return Result.Failure($"Ability '{ability.DisplayName}' is on cooldown.");
            }

            if (ability.Tag == AbilityTag.None && ability.TargetType != AbilityTargetType.Enemy)
            {
                return Result.Failure("Untagged player abilities must target an enemy.");
            }

            if (ability.Tag == AbilityTag.Amplify && ability.TargetType != AbilityTargetType.Ally)
            {
                return Result.Failure("Amplify player abilities must target an ally.");
            }

            string targetId;
            BattlePos? targetPoint = null;
            if (ability.TargetType == AbilityTargetType.Ally)
            {
                targetId = playerUnitId;
            }
            else
            {
                targetId = NearestLivingEnemy(playerUnitId);
                if (string.IsNullOrEmpty(targetId))
                {
                    return Result.Failure("No living enemy is available for the player ability.");
                }

                if (ability.TargetType == AbilityTargetType.Cell)
                {
                    targetPoint = battlefield.FindOccupant(targetId);
                }
            }

            return commandBoard.IssuePreciseOrder(
                playerUnitId,
                ability.Id,
                targetId,
                targetPoint,
                commandWindowActive,
                CombatState.ElapsedSeconds);
        }

        public Result SetFocusStanceOnNearestEnemy(string companionUnitId)
        {
            if (commandBoard == null || CombatState == null || battlefield == null)
            {
                return Result.Failure("Combat command runtime is not ready.");
            }

            if (!IsCompanionUnit(companionUnitId))
            {
                return Result.Failure("Focus stance requires a living companion unit.");
            }

            string targetId = NearestLivingEnemy(companionUnitId);
            return string.IsNullOrEmpty(targetId)
                ? Result.Failure("No living enemy is available for focus stance.")
                : commandBoard.SetStance(companionUnitId, CommandStance.Focus, targetId);
        }

        private bool IsCompanionUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || CombatState == null)
            {
                return false;
            }

            for (int index = 0; index < companions.Count; index++)
            {
                if (companions[index] != null
                    && StringComparer.Ordinal.Equals(companions[index].UnitId, unitId)
                    && CombatState.IsAlive(unitId))
                {
                    return true;
                }
            }

            return false;
        }

        private string NearestLivingEnemy(string fromUnitId)
        {
            BattlePos? from = battlefield.FindOccupant(fromUnitId);
            if (!from.HasValue)
            {
                return null;
            }

            string bestId = null;
            float bestDistance = float.PositiveInfinity;
            foreach (string unitId in CombatState.LivingUnitIds)
            {
                CombatantRef candidate = CombatState.GetCombatant(unitId);
                if (candidate == null || candidate.Team != CombatTeam.Enemy)
                {
                    continue;
                }

                BattlePos? position = battlefield.FindOccupant(unitId);
                if (!position.HasValue)
                {
                    continue;
                }

                float distance = battlefield.Distance(from.Value, position.Value);
                if (distance < bestDistance
                    || (Mathf.Approximately(distance, bestDistance)
                        && string.CompareOrdinal(unitId, bestId) < 0))
                {
                    bestDistance = distance;
                    bestId = unitId;
                }
            }

            return bestId;
        }

        private void HandlePlayerDefeat()
        {
            IsPlayerDefeated = true;
            commandBoard?.ClearPreciseOrders();
            engagement.ResolveEncounter();
            EndCombatPresentation();
            if (playerMovement != null)
            {
                playerMovement.enabled = false;
            }

            Debug.Log(
                $"[GeneratedEncounter] Player team defeated event={eventId}; run lifecycle takes over.",
                this);
            defeated?.Invoke(eventId);
        }

        private void EndCombatPresentation()
        {
            for (int index = 0; index < brains.Count; index++)
            {
                if (brains[index] != null) brains[index].SetEngagementEnabled(false);
            }

            for (int index = 0; index < companions.Count; index++)
            {
                if (companions[index] != null)
                {
                    companions[index].SetCombatDriven(false);
                    companions[index].SetEnemyTargets(new Transform[0]);
                }
            }
        }

        private void OnDestroy()
        {
            EndCombatPresentation();
            CleanupPresentation();
        }

        private void CleanupPresentation()
        {
            CleanupCombatViews();
            CleanupEnemies();
        }

        private void CleanupCombatViews()
        {
            presentationObserver?.Events.Clear();
            for (int index = 0; index < views.Count; index++)
            {
                if (views[index] == null) continue;
                GameObject viewObject = views[index].gameObject;
                if (Application.isPlaying) Destroy(viewObject); else DestroyImmediate(viewObject);
            }

            views.Clear();
            bodies.Clear();
            enemyBrains.Clear();
        }

        private void CleanupEnemies()
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                GameObject enemy = enemies[index];
                if (enemy == null) continue;
                if (Application.isPlaying) Destroy(enemy); else DestroyImmediate(enemy);
            }

            enemies.Clear();
            spawnedProfiles.Clear();
            brains.Clear();
        }

        private bool TryPlaceOpen(string unitId, BattlePos desired)
        {
            if (battlefield.TryPlaceOccupant(unitId, desired)) return true;
            for (int radius = 1; radius <= 8; radius++)
            {
                float offset = radius * 0.55f;
                BattlePos[] candidates =
                {
                    new BattlePos(desired.X + offset, desired.Y),
                    new BattlePos(desired.X - offset, desired.Y),
                    new BattlePos(desired.X, desired.Y + offset),
                    new BattlePos(desired.X, desired.Y - offset)
                };
                for (int index = 0; index < candidates.Length; index++)
                {
                    if (battlefield.TryPlaceOccupant(unitId, candidates[index])) return true;
                }
            }

            return false;
        }

        private BattlePos WorldToBattle(Vector3 world)
        {
            return new BattlePos(
                world.x - arenaCenter.x + ArenaHalf,
                world.z - arenaCenter.z + ArenaHalf);
        }

        private Vector3 BattleToWorld(BattlePos position)
        {
            return new Vector3(
                position.X + arenaCenter.x - ArenaHalf,
                arenaCenter.y,
                position.Y + arenaCenter.z - ArenaHalf);
        }

        private Transform[] EnemyTransforms()
        {
            var transforms = new Transform[enemies.Count];
            for (int index = 0; index < enemies.Count; index++) transforms[index] = enemies[index].transform;
            return transforms;
        }

        private string EnemyUnitId(int index)
        {
            return $"{eventId}-enemy-{index:00}";
        }

        private static Result ValidateDefinition(CharacterDef definition, string label)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id)
                || definition.DefaultAbilities == null
                || definition.DefaultAbilities.Length < AbilityLoadout.MinSlots
                || definition.DefaultAbilities.Length > AbilityLoadout.MaxSlots)
            {
                return Result.Failure($"{label} definition requires an id and one to four abilities.");
            }

            return Result.Success();
        }

        private static Vector3 FormationOffset(int index, int count)
        {
            if (count <= 1) return Vector3.zero;
            float angle = (Mathf.PI * 2f * index) / count;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.5f;
        }

        private static PillbugTuning DefaultTuning(EnemyCombatProfile profile)
        {
            bool boss = StringComparer.Ordinal.Equals(profile.KindSlot, "boss");
            return new PillbugTuning(
                12f,
                boss ? 3.5f : 2.5f,
                0f,
                new TelegraphDurations(0.9f, 0.25f, 0.6f),
                0.7f,
                0.12f,
                0.4f,
                boss ? 1.5f : 1.1f,
                0.08f,
                32,
                new Color(0.15f, 1f, 0.35f, 1f),
                new Color(1f, 0.75f, 0.1f, 1f),
                new Color(1f, 0.2f, 0.2f, 1f),
                profile.BodyColor,
                Color.white);
        }
    }
}
