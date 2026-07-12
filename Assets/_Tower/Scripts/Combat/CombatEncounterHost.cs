using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    /// <summary>
    /// Owns the one-enemy, one-companion preview. CombatState is deliberately a
    /// T49 container only: no scene HP/damage mapping is implemented here.
    /// </summary>
    public sealed class CombatEncounterHost : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject companionPrefab;
        [SerializeField] private RuntimeAnimatorController companionLocomotionController;
        [Header("Provisional spawn positions (metres)")]
        [SerializeField, Min(0f)] private float pillbugSpawnDistance = 8f;
        [SerializeField, Min(0f)] private float companionSpawnDistance = 2f;
        [Header("Provisional pillbug timing and distance")]
        [SerializeField, Min(0f)] private float awarenessRadius = 12f;
        [SerializeField, Min(0f)] private float windupTriggerDistance = 2.5f;
        [SerializeField, Min(0f)] private float dashRange = 3f;
        [SerializeField, Min(0.01f)] private float windupSeconds = 0.9f;
        [SerializeField, Min(0.01f)] private float commitSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float recoverSeconds = 0.6f;
        [Header("Provisional companion")]
        [SerializeField, Min(0f)] private float companionLeashDistance = 4f;
        [SerializeField, Min(0f)] private float companionMoveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float companionTurnSpeed = 10f;
        [SerializeField] private Color companionTint = new Color(0.62f, 0.82f, 1f, 1f);
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
        public CompanionBody Companion { get; private set; }

        private void Start()
        {
            if (player == null)
            {
                var playerObject = GameObject.Find("Player");
                player = playerObject == null ? null : playerObject.transform;
            }

            if (player == null || companionPrefab == null)
            {
                Debug.LogError("Combat spike requires a player and companion prefab.", this);
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

            var right = Vector3.Cross(Vector3.up, forward.normalized);
            var companionObject = Instantiate(companionPrefab, player.position + (right * companionSpawnDistance), Quaternion.identity);
            companionObject.name = "Companion";
            var animator = companionObject.GetComponentInChildren<Animator>();
            if (animator != null && companionLocomotionController != null)
            {
                animator.runtimeAnimatorController = companionLocomotionController;
            }

            Companion = companionObject.AddComponent<CompanionBody>();
            var pillbugObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pillbugObject.name = "Pillbug";
            pillbugObject.transform.position = player.position + (forward.normalized * pillbugSpawnDistance) + (Vector3.up * 0.5f);
            Pillbug = pillbugObject.AddComponent<PillbugBrain>();
            Pillbug.Configure(player, Companion.transform, BuildPillbugTuning());
            Companion.Configure(player, new[] { Pillbug.transform }, new CompanionTuning(companionLeashDistance, companionMoveSpeed, companionTurnSpeed, companionTint));
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
            // These are non-gameplay placeholders required only to hold the
            // extracted T49 state container. They are never mapped to visuals.
            var companionAbility = Track(AbilityDef.CreateRuntime("spike-companion-noop", AbilityTag.None, 0, 0, AbilityTargetType.Enemy));
            var pillbugAbility = Track(AbilityDef.CreateRuntime("spike-pillbug-noop", AbilityTag.None, 0, 0, AbilityTargetType.Enemy));
            var companionDefinition = Track(CharacterDef.CreateRuntime(
                "spike-companion", "Spike Companion", 1, 0, 0, 1, DispositionType.Protective, new[] { companionAbility }));
            var pillbugDefinition = Track(CharacterDef.CreateRuntime(
                "spike-pillbug", "Pillbug", 1, 0, 0, 1, DispositionType.Aggressive, new[] { pillbugAbility }));
            var companionState = CharacterState.Create(companionDefinition, slotCount: 1, assignedAbilities: new[] { companionAbility });
            var pillbugState = CharacterState.Create(pillbugDefinition, slotCount: 1, assignedAbilities: new[] { pillbugAbility });
            if (companionState.IsFailure || pillbugState.IsFailure)
            {
                Debug.LogError("Combat spike could not create its placeholder CombatState.", this);
                return;
            }

            var companionCombatant = CombatantRef.Create("spike-companion", CombatTeam.Player, companionState.Value);
            var pillbugCombatant = CombatantRef.Create("spike-pillbug", CombatTeam.Enemy, pillbugState.Value);
            if (companionCombatant.IsFailure || pillbugCombatant.IsFailure)
            {
                Debug.LogError("Combat spike could not register preview combatants.", this);
                return;
            }

            var created = CombatState.Create(new[] { companionCombatant.Value, pillbugCombatant.Value });
            if (created.IsFailure)
            {
                Debug.LogError(created.Error, this);
                return;
            }

            CombatState = created.Value;
        }

        private T Track<T>(T definition) where T : Object
        {
            runtimeDefinitions.Add(definition);
            return definition;
        }
    }
}
