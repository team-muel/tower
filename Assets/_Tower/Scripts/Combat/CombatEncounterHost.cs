using System.Collections.Generic;
using System.Linq;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    /// <summary>
    /// Owns the isolated preview encounter. T51 replaces the anonymous visual
    /// follower with real roster-backed companion entities while deliberately
    /// leaving ability execution and HP mapping for later tasks.
    /// </summary>
    public sealed class CombatEncounterHost : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private CharacterDef playerDefinition;
        [SerializeField] private CompanionVisualProfile[] companionProfiles;
        [Header("Provisional spawn positions (metres)")]
        [SerializeField, Min(0f)] private float pillbugSpawnDistance = 8f;
        [Header("Provisional pillbug timing and distance")]
        [SerializeField, Min(0f)] private float awarenessRadius = 12f;
        [SerializeField, Min(0f)] private float windupTriggerDistance = 2.5f;
        [SerializeField, Min(0f)] private float dashRange = 3f;
        [SerializeField, Min(0.01f)] private float windupSeconds = 0.9f;
        [SerializeField, Min(0.01f)] private float commitSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float recoverSeconds = 0.6f;
        [Header("World-space debug presentation")]
        [SerializeField, Min(0f)] private float contactDistance = 0.7f;
        [SerializeField, Min(0f)] private float contactFlashSeconds = 0.12f;
        [SerializeField, Min(0f)] private float ringMinRadius = 0.4f;
        [SerializeField, Min(0f)] private float ringMaxRadius = 1.1f;
        [SerializeField, Min(0f)] private float ringWidth = 0.08f;
        [SerializeField, Min(3)] private int ringSegments = 32;
        [SerializeField] private Color cleanCoverageColor = new Color(0.15f, 1f, 0.35f, 1f);
        [SerializeField] private Color insufficientCoverageColor = new Color(1f, 0.75f, 0.1f, 1f);
        [SerializeField] private Color missedCoverageColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color pillbugColor = new Color(0.35f, 0.12f, 0.08f, 1f);
        [SerializeField] private Color contactFlashColor = Color.white;

        private readonly List<Object> runtimeDefinitions = new List<Object>();

        public CombatState CombatState { get; private set; }
        public PillbugBrain Pillbug { get; private set; }
        public IReadOnlyList<CompanionEntity> Companions { get; private set; }

        private void Start()
        {
            if (player == null)
            {
                var playerObject = GameObject.Find("Player");
                player = playerObject == null ? null : playerObject.transform;
            }

            if (player == null || playerDefinition == null
                || companionProfiles == null || companionProfiles.Length == 0)
            {
                Debug.LogError("Combat spike requires a player definition and companion profiles.", this);
                enabled = false;
                return;
            }

            SpawnPreviewBodies();
            CreatePreviewCombatState();
            var cameraRig = FindFirstObjectByType<FixedIsoFollowCameraRig>();
            if (cameraRig != null)
            {
                cameraRig.SetFocusTarget(player);
            }
        }

        private void OnDestroy()
        {
            foreach (var definition in runtimeDefinitions)
            {
                if (definition != null)
                {
                    Destroy(definition);
                }
            }

            runtimeDefinitions.Clear();
        }

        private void SpawnPreviewBodies()
        {
            var forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            var pillbugObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pillbugObject.name = "Pillbug";
            pillbugObject.transform.position = player.position + (forward.normalized * pillbugSpawnDistance) + (Vector3.up * 0.5f);
            Pillbug = pillbugObject.AddComponent<PillbugBrain>();

            var spawner = gameObject.AddComponent<CompanionPartySpawner>();
            spawner.Configure(player, companionProfiles, new[] { Pillbug.transform });
            var spawned = spawner.SpawnNow();
            if (spawned.IsFailure)
            {
                Debug.LogError(spawned.Error, this);
                enabled = false;
                return;
            }

            Companions = spawned.Value;
            Pillbug.Configure(
                player,
                Companions.Select(companion => companion.transform).ToArray(),
                BuildPillbugTuning());
            var slowMoInput = GetComponent<SlowMoInput>();
            if (slowMoInput != null)
            {
                slowMoInput.SetPillbug(Pillbug);
            }
        }

        private PillbugTuning BuildPillbugTuning()
        {
            return new PillbugTuning(
                awarenessRadius,
                windupTriggerDistance,
                dashRange,
                new TelegraphDurations(windupSeconds, commitSeconds, recoverSeconds),
                contactDistance,
                contactFlashSeconds,
                ringMinRadius,
                ringMaxRadius,
                ringWidth,
                ringSegments,
                cleanCoverageColor,
                insufficientCoverageColor,
                missedCoverageColor,
                pillbugColor,
                contactFlashColor);
        }

        private void CreatePreviewCombatState()
        {
            var combatants = new List<CombatantRef>();
            if (!TryAddCombatant(combatants, playerDefinition, CombatTeam.Player))
            {
                return;
            }

            foreach (var profile in companionProfiles)
            {
                if (!TryAddCombatant(combatants, profile.CharacterDefinition, CombatTeam.Player))
                {
                    return;
                }
            }

            var pillbugAbility = Track(AbilityDef.CreateRuntime("spike-pillbug-noop", AbilityTag.None, 0, 0, AbilityTargetType.Enemy));
            var pillbugDefinition = Track(CharacterDef.CreateRuntime(
                "spike-pillbug", "Pillbug", 1, 0, 0, 1, DispositionType.Aggressive, new[] { pillbugAbility }));
            if (!TryAddCombatant(combatants, pillbugDefinition, CombatTeam.Enemy))
            {
                return;
            }

            var created = CombatState.Create(combatants);
            if (created.IsFailure)
            {
                Debug.LogError(created.Error, this);
                return;
            }

            CombatState = created.Value;
        }

        private bool TryAddCombatant(
            ICollection<CombatantRef> combatants,
            CharacterDef definition,
            CombatTeam team)
        {
            if (definition == null || definition.DefaultAbilities == null
                || definition.DefaultAbilities.Length < AbilityLoadout.MinSlots
                || definition.DefaultAbilities.Length > AbilityLoadout.MaxSlots)
            {
                Debug.LogError("Combat preview character requires one to four default abilities.", this);
                return false;
            }

            var state = CharacterState.Create(
                definition,
                slotCount: definition.DefaultAbilities.Length,
                assignedAbilities: definition.DefaultAbilities);
            if (state.IsFailure)
            {
                Debug.LogError(state.Error, this);
                return false;
            }

            var combatant = CombatantRef.Create(definition.Id, team, state.Value);
            if (combatant.IsFailure)
            {
                Debug.LogError(combatant.Error, this);
                return false;
            }

            combatants.Add(combatant.Value);
            return true;
        }

        private T Track<T>(T definition) where T : Object
        {
            runtimeDefinitions.Add(definition);
            return definition;
        }
    }
}
