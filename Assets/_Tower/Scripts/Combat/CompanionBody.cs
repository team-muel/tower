using UnityEngine;

namespace Tower.Combat
{
    public readonly struct CompanionTuning
    {
        public CompanionTuning(float leashDistance, float moveSpeed, float turnSpeed, Color tint)
        {
            LeashDistance = leashDistance;
            MoveSpeed = moveSpeed;
            TurnSpeed = turnSpeed;
            Tint = tint;
        }

        public float LeashDistance { get; }
        public float MoveSpeed { get; }
        public float TurnSpeed { get; }
        public Color Tint { get; }
    }

    /// <summary>
    /// A physical companion silhouette only. It follows and faces, but owns no
    /// action selection, ability, command, HP, or combat resolution.
    /// </summary>
    public sealed class CompanionBody : MonoBehaviour
    {
        private Transform player;
        private Transform[] enemies;
        private CompanionTuning tuning;
        private Animator animator;
        private int speedHash;
        private bool animatorSupportsSpeed;

        public void Configure(Transform player, Transform[] enemies, CompanionTuning tuning)
        {
            this.player = player;
            this.enemies = enemies;
            this.tuning = tuning;
            ConfigurePhysics();
            ConfigureAnimator();
            ApplyTint();
        }

        private void Update()
        {
            if (player == null)
            {
                return;
            }

            var position = transform.position;
            var playerPosition = player.position;
            var toPlayer = playerPosition - position;
            toPlayer.y = 0f;
            var moving = false;
            if (toPlayer.magnitude > tuning.LeashDistance)
            {
                var targetPosition = playerPosition - (toPlayer.normalized * tuning.LeashDistance);
                transform.position = Vector3.MoveTowards(position, targetPosition, tuning.MoveSpeed * Time.deltaTime);
                moving = true;
            }

            var closestEnemy = FindClosestEnemy();
            if (closestEnemy != null)
            {
                var faceDirection = closestEnemy.position - transform.position;
                faceDirection.y = 0f;
                if (faceDirection.sqrMagnitude > 0.0001f)
                {
                    var targetRotation = Quaternion.LookRotation(faceDirection.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, tuning.TurnSpeed * Time.deltaTime);
                }
            }

            if (animatorSupportsSpeed)
            {
                animator.SetFloat(speedHash, moving ? 1f : 0f);
            }
        }

        private void ConfigurePhysics()
        {
            var bodies = GetComponentsInChildren<Rigidbody>();
            if (bodies.Length == 0)
            {
                bodies = new[] { gameObject.AddComponent<Rigidbody>() };
            }

            foreach (var body in bodies)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void ConfigureAnimator()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            speedHash = Animator.StringToHash("Speed");
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == speedHash)
                {
                    animatorSupportsSpeed = true;
                    break;
                }
            }
        }

        private void ApplyTint()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", tuning.Tint);
                properties.SetColor("_Color", tuning.Tint);
                renderer.SetPropertyBlock(properties);
            }
        }

        private Transform FindClosestEnemy()
        {
            Transform closest = null;
            var bestDistance = float.PositiveInfinity;
            if (enemies == null)
            {
                return null;
            }

            foreach (var enemy in enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(enemy.position - transform.position);
                if (distance < bestDistance)
                {
                    closest = enemy;
                    bestDistance = distance;
                }
            }

            return closest;
        }
    }
}
