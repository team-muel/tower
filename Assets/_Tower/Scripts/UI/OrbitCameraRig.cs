using UnityEngine;

namespace Tower.UI
{
    public sealed class OrbitCameraRig : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform targetAnchor;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

        [Header("Rotation Settings")]
        [SerializeField] private float keyboardRotateSpeed = 80f;
        [SerializeField] private float mouseRotateSpeed = 3f;
        [SerializeField] private float minPitch = 15f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private float rotationSmoothing = 5f;

        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 25f;
        [SerializeField] private float zoomSmoothing = 8f;

        private float currentYaw = 45f;
        private float currentPitch = 45f;
        private float currentDistance = 15f;

        private float targetYaw;
        private float targetPitch;
        private float targetDistance;

        private void Start()
        {
            targetYaw = currentYaw;
            targetPitch = currentPitch;
            targetDistance = currentDistance;
        }

        public void SetTarget(Transform newTarget)
        {
            targetAnchor = newTarget;
        }

        private void LateUpdate()
        {
            if (targetAnchor == null)
            {
                return;
            }

            HandleInputs();
            SmoothParameters();
            UpdateCameraPosition();
        }

        private void HandleInputs()
        {
            // Keyboard Rotation (Q / E)
            if (Input.GetKey(KeyCode.Q))
            {
                targetYaw -= keyboardRotateSpeed * Time.unscaledDeltaTime;
            }
            if (Input.GetKey(KeyCode.E))
            {
                targetYaw += keyboardRotateSpeed * Time.unscaledDeltaTime;
            }

            // Mouse Rotation (Right Click Drag)
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                targetYaw += mouseX * mouseRotateSpeed * 10f;
                targetPitch -= mouseY * mouseRotateSpeed * 10f;
            }

            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

            // Mouse Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetDistance -= scroll * zoomSpeed * 10f;
            }

            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        private void SmoothParameters()
        {
            // Use LerpAngle for Yaw to prevent wrapping issues
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, rotationSmoothing * Time.unscaledDeltaTime);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, rotationSmoothing * Time.unscaledDeltaTime);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, zoomSmoothing * Time.unscaledDeltaTime);
        }

        private void UpdateCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 targetPosition = targetAnchor.position + targetOffset;
            Vector3 position = targetPosition - (rotation * Vector3.forward * currentDistance);

            transform.position = position;
            transform.LookAt(targetPosition);
        }
    }
}
