using UnityEngine;

namespace Tower.Floor
{
    public sealed class ForestFloorCamera : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float distance = 18f;
        // LinearStubLayout advances along +Z, so W reads as forward traversal
        // in the standalone generated-floor build.
        [SerializeField] private float yaw;
        [SerializeField, Range(10f, 70f)] private float pitch = 25f;
        [SerializeField, Min(1f)] private float orthographicSize = 12f;
        [SerializeField, Min(0.1f)] private float followLerp = 10f;

        private Transform target;
        private Camera view;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            view = GetComponent<Camera>();
            if (view != null)
            {
                view.orthographic = true;
                view.orthographicSize = orthographicSize;
            }

            Snap();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 lookPoint = target.position + (Vector3.up * 1.1f);
            Vector3 desired = lookPoint - (rotation * Vector3.forward * distance);
            transform.position = Vector3.Lerp(
                transform.position,
                desired,
                1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime));
            transform.rotation = rotation;
        }

        private void Snap()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 lookPoint = target.position + (Vector3.up * 1.1f);
            transform.position = lookPoint - (rotation * Vector3.forward * distance);
            transform.rotation = rotation;
        }
    }
}
