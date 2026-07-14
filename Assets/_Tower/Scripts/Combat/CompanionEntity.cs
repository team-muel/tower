using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    /// <summary>
    /// One roster member as a real world entity. Identity and presentation are
    /// bound once from data; commands, slot UI, and ability execution belong to
    /// later layers.
    /// </summary>
    public sealed class CompanionEntity : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        // 89 Party Traversal Vision v2 rules 2-3: per-slot distance delays on
        // the shared breadcrumb stream plus small alternating path-normal
        // offsets. Provisional world-unit scaling of the animatic's
        // 1.05-per-slot delays; not a formation.
        private static readonly float[] FollowDistances = { 2.1f, 4.2f, 6.3f };
        private static readonly float[] SlotLateralOffsets = { -0.36f, 0.36f, -0.2f };

        private CompanionVisualProfile profile;
        private Transform leader;
        private BreadcrumbTrail breadcrumbs;
        private float followDistance;
        private float lateralOffset;
        private Transform[] enemies = new Transform[0];
        private GameObject visualRoot;
        private Animator animator;
        private bool animatorSupportsSpeed;

        public CompanionVisualProfile Profile => profile;
        public CharacterDef CharacterDefinition => profile == null ? null : profile.CharacterDefinition;
        public string UnitId => CharacterDefinition == null ? string.Empty : CharacterDefinition.Id;
        public string DisplayName => CharacterDefinition == null ? string.Empty : CharacterDefinition.DisplayName;
        public DispositionType Disposition => CharacterDefinition == null
            ? default
            : CharacterDefinition.Disposition;
        public GameObject VisualRoot => visualRoot;
        public bool IsMoving { get; private set; }

        public Result Configure(
            CompanionVisualProfile companionProfile,
            Transform partyLeader,
            Transform[] enemyTransforms,
            BreadcrumbTrail sharedTrail = null,
            int slotIndex = 0)
        {
            if (companionProfile == null)
            {
                return Result.Failure("Companion profile is required.");
            }

            var valid = companionProfile.Validate();
            if (valid.IsFailure)
            {
                return valid;
            }

            if (partyLeader == null)
            {
                return Result.Failure("Companion party leader is required.");
            }

            profile = companionProfile;
            leader = partyLeader;
            enemies = enemyTransforms ?? new Transform[0];
            breadcrumbs = sharedTrail;
            int clampedSlot = Mathf.Clamp(slotIndex, 0, FollowDistances.Length - 1);
            followDistance = FollowDistances[clampedSlot];
            lateralOffset = SlotLateralOffsets[clampedSlot];
            name = "Companion_" + profile.CharacterDefinition.Id;
            CreateVisual();
            ConfigurePhysics();
            return Result.Success();
        }

        public Vector3 FormationTarget()
        {
            return leader == null
                ? transform.position
                : leader.TransformPoint(profile.FormationOffset);
        }

        public void SetEnemyTargets(Transform[] enemyTransforms)
        {
            enemies = enemyTransforms ?? new Transform[0];
        }

        public void SetCombatDriven(bool value)
        {
            enabled = !value;
            if (animatorSupportsSpeed && value)
            {
                animator.SetFloat(SpeedHash, 0f);
            }
        }

        public void Tick(float deltaTime)
        {
            if (profile == null || leader == null || deltaTime < 0f
                || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            // 89 rule 1-2: consume the leader's breadcrumb stream at this
            // slot's distance delay; the fixed formation offset is only the
            // spawn-moment fallback before any trajectory exists.
            var target = breadcrumbs != null && breadcrumbs.Count >= 2
                ? breadcrumbs.Sample(followDistance, lateralOffset, FormationTarget())
                : FormationTarget();
            target.y = transform.position.y;
            var toTarget = target - transform.position;
            IsMoving = toTarget.magnitude > profile.ArriveDistance;
            if (IsMoving)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target,
                    profile.MoveSpeed * deltaTime);
            }

            var facing = ClosestLivingDirection();
            if (facing.sqrMagnitude <= 0.0001f && IsMoving)
            {
                facing = toTarget;
            }

            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    profile.TurnSpeed * deltaTime);
            }

            if (animatorSupportsSpeed)
            {
                animator.SetFloat(SpeedHash, IsMoving ? 1f : 0f);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void CreateVisual()
        {
            if (visualRoot != null)
            {
                Destroy(visualRoot);
            }

            visualRoot = Instantiate(profile.BodyPrefab, transform);
            visualRoot.name = "Visual";
            visualRoot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator = visualRoot.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (profile.LocomotionController != null)
                {
                    animator.runtimeAnimatorController = profile.LocomotionController;
                }

                foreach (var parameter in animator.parameters)
                {
                    if (parameter.type == AnimatorControllerParameterType.Float
                        && parameter.nameHash == SpeedHash)
                    {
                        animatorSupportsSpeed = true;
                        break;
                    }
                }
            }

            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (profile.BodyMaterial != null)
                {
                    var materialCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                    var materials = new Material[materialCount];
                    for (var index = 0; index < materials.Length; index++)
                    {
                        materials[index] = profile.BodyMaterial;
                    }

                    renderer.sharedMaterials = materials;
                }

                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", profile.AccentColor);
                properties.SetColor("_Color", profile.AccentColor);
                renderer.SetPropertyBlock(properties);
            }
        }

        private void ConfigurePhysics()
        {
            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            foreach (var childBody in visualRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                childBody.isKinematic = true;
                childBody.useGravity = false;
            }
        }

        private Vector3 ClosestLivingDirection()
        {
            Transform closest = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var direction = enemy.position - transform.position;
                direction.y = 0f;
                var distance = direction.sqrMagnitude;
                if (distance < bestDistance)
                {
                    closest = enemy;
                    bestDistance = distance;
                }
            }

            return closest == null ? Vector3.zero : closest.position - transform.position;
        }
    }
}
