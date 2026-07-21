using UnityEngine;

namespace Tower.Floor
{
    // Minimal direct-control bridge for the generated floor. Traversal
    // coroutines temporarily disable it, and T52 disables only this component
    // during the encounter intro hold.
    public sealed class ForestPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 12f;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 4f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 12f;

        private Transform cameraTransform;
        private bool movementLogged;
        private Vector3 lastDistanceLogPosition;

        public void Configure(Transform viewTransform)
        {
            cameraTransform = viewTransform;
        }

        private void Update()
        {
            Vector3 forward = cameraTransform == null ? Vector3.forward : cameraTransform.forward;
            Vector3 right = cameraTransform == null ? Vector3.right : cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            float horizontal = 0f;
            float vertical = 0f;
            if (Input.GetKey(KeyCode.W)) vertical += 1f;
            if (Input.GetKey(KeyCode.S)) vertical -= 1f;
            if (Input.GetKey(KeyCode.D)) horizontal += 1f;
            if (Input.GetKey(KeyCode.A)) horizontal -= 1f;

            Vector3 direction = (forward * vertical) + (right * horizontal);
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 next = transform.position + (direction * moveSpeed * Time.deltaTime);
            Vector3 probe = next + (Vector3.up * groundProbeHeight);
            RaycastHit[] hits = Physics.RaycastAll(
                probe,
                Vector3.down,
                groundProbeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float highestGround = float.NegativeInfinity;
            for (int index = 0; index < hits.Length; index++)
            {
                if (hits[index].transform == transform || hits[index].transform.IsChildOf(transform))
                {
                    continue;
                }

                highestGround = Mathf.Max(highestGround, hits[index].point.y);
            }

            if (float.IsNegativeInfinity(highestGround))
            {
                return;
            }

            next.y = highestGround + 1f;

            transform.position = next;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                turnSpeed * Time.deltaTime);

            if (!movementLogged)
            {
                movementLogged = true;
                lastDistanceLogPosition = next;
                Debug.Log($"[FloorInput] Direct movement started at {next}.", this);
            }
            else if (PlanarDistance(lastDistanceLogPosition, next) >= 10f)
            {
                lastDistanceLogPosition = next;
                Debug.Log($"[FloorInput] Advanced 10m to {next}.", this);
            }
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
