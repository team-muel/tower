using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    /// <summary>
    /// Unity bridge for map-to-combat entry. It pauses only player locomotion
    /// during the intro hold, then restores direct control and enables the
    /// enemy's real-time brain. Global time and companion simulation keep their
    /// normal clocks.
    /// </summary>
    public sealed class EncounterEngagementController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform enemy;
        [SerializeField] private Behaviour playerMovement;
        [SerializeField] private PillbugBrain pillbug;
        [SerializeField, Min(0.01f)] private float triggerRadius = 7f;
        [SerializeField, Min(0.01f)] private float introHoldSeconds = 0.45f;

        private EncounterTransition transition;
        private bool movementWasEnabled;
        private bool movementHeld;

        public EncounterPhase Phase => transition == null
            ? EncounterPhase.Exploring
            : transition.Phase;
        public float HoldProgress => transition == null ? 0f : transition.HoldProgress;
        public bool IsPlayerHeld => transition != null && transition.IsPlayerHeld;
        public bool IsCombatActive => transition != null && transition.IsCombatActive;

        public Result Configure(
            Transform playerTransform,
            Transform enemyTransform,
            Behaviour playerMovementBehaviour,
            PillbugBrain enemyBrain,
            float encounterTriggerRadius,
            float encounterIntroHoldSeconds)
        {
            player = playerTransform;
            enemy = enemyTransform;
            playerMovement = playerMovementBehaviour;
            pillbug = enemyBrain;
            triggerRadius = encounterTriggerRadius;
            introHoldSeconds = encounterIntroHoldSeconds;
            return Initialize();
        }

        public void Tick(float realDeltaSeconds)
        {
            if (transition == null)
            {
                return;
            }

            if (transition.Phase == EncounterPhase.Exploring
                && transition.TryBegin(PlanarDistance(player.position, enemy.position)))
            {
                HoldPlayer();
                Debug.Log(
                    $"[Encounter] IntroHold started ({introHoldSeconds:0.00}s local player hold).",
                    this);
            }

            if (transition.Tick(realDeltaSeconds))
            {
                RestorePlayer();
                pillbug.SetEngagementEnabled(true);
                Debug.Log("[Encounter] Active real-time combat; direct player control restored.", this);
            }
        }

        public Result ResolveEncounter()
        {
            if (transition == null)
            {
                return Result.Failure("Encounter transition is not configured.");
            }

            var result = transition.Resolve();
            if (result.IsSuccess)
            {
                RestorePlayer();
                pillbug.SetEngagementEnabled(false);
                Debug.Log("[Encounter] Resolved.", this);
            }

            return result;
        }

        private void Start()
        {
            if (transition != null)
            {
                return;
            }

            var result = Initialize();
            if (result.IsFailure)
            {
                Debug.LogError(result.Error, this);
                enabled = false;
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            RestorePlayer();
        }

        private Result Initialize()
        {
            if (player == null || enemy == null || playerMovement == null || pillbug == null)
            {
                return Result.Failure(
                    "Encounter engagement requires player, enemy, player movement, and enemy brain.");
            }

            var created = EncounterTransition.Create(triggerRadius, introHoldSeconds);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            transition = created.Value;
            pillbug.SetEngagementEnabled(false);
            return Result.Success();
        }

        private void HoldPlayer()
        {
            if (movementHeld)
            {
                return;
            }

            movementWasEnabled = playerMovement.enabled;
            playerMovement.enabled = false;
            movementHeld = true;
        }

        private void RestorePlayer()
        {
            if (!movementHeld || playerMovement == null)
            {
                return;
            }

            playerMovement.enabled = movementWasEnabled;
            movementHeld = false;
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
