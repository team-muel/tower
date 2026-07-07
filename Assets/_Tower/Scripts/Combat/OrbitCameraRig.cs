using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // T19: combat-only orbit camera. Coexists with IsoCameraRig - camp and
    // exploration keep the iso follow rig, combat encounters swap in this rig.
    // Right-drag orbits yaw/pitch, Q/E steps yaw, scroll zooms; pitch and zoom
    // are clamped. Every parameter is a serialized field so -devcam tuning and
    // future data promotion can adjust them without code changes. The focus
    // point eases toward the focus target (active turn unit) with damping, so
    // turn handoffs glide instead of snapping.
    public sealed class OrbitCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _yaw = OrbitCameraMath.DefaultYaw;
        [SerializeField] private float _pitch = OrbitCameraMath.DefaultPitch;
        [SerializeField] private float _distance = OrbitCameraMath.DefaultDistance;
        [SerializeField] private float _minPitch = OrbitCameraMath.MinPitch;
        [SerializeField] private float _maxPitch = OrbitCameraMath.MaxPitch;
        [SerializeField] private float _minDistance = OrbitCameraMath.MinDistance;
        [SerializeField] private float _maxDistance = OrbitCameraMath.MaxDistance;
        [SerializeField] private float _yawStepDegrees = OrbitCameraMath.YawStepDegrees;
        [SerializeField] private float _orbitSensitivity = OrbitCameraMath.DefaultOrbitSensitivity;
        [SerializeField] private float _zoomStep = OrbitCameraMath.DefaultZoomStep;
        [SerializeField] private float _focusDamping = OrbitCameraMath.DefaultFocusDamping;
        [SerializeField] private float _fov = CameraTuning.DefaultFov;

        private Transform _focusTarget;
        private Vector3 _focusPoint;
        private Vector3 _focusGoal;
        private Vector3 _focusVelocity;
        private Vector3 _presentationOffset;
        private bool _hasFocused;

        public Camera Camera
        {
            get { return _camera; }
        }

        // -devcam compatibility: expose the shared tuning value set so
        // CameraTuningModeController can read and drive this rig too.
        public CameraTuningState Tuning
        {
            get { return new CameraTuningState(_pitch, _distance, _fov, _focusDamping); }
        }

        private void Awake()
        {
            EnsureCamera();
            _focusPoint = _focusGoal;
            ApplyTransform();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                StepYaw(-_yawStepDegrees);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                StepYaw(_yawStepDegrees);
            }

            // Right-drag = orbit (yaw with mouse X, pitch with mouse Y).
            if (Input.GetMouseButton(1))
            {
                var yawDelta = Input.GetAxis("Mouse X") * _orbitSensitivity;
                var pitchDelta = -Input.GetAxis("Mouse Y") * _orbitSensitivity;
                if (!Mathf.Approximately(yawDelta, 0f) || !Mathf.Approximately(pitchDelta, 0f))
                {
                    _yaw = OrbitCameraMath.NormalizeYaw(_yaw + yawDelta);
                    _pitch = OrbitCameraMath.ClampPitch(_pitch + pitchDelta, _minPitch, _maxPitch);
                }
            }

            var scroll = Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(scroll, 0f))
            {
                _distance = OrbitCameraMath.ClampDistance(_distance - (scroll * _zoomStep), _minDistance, _maxDistance);
            }
        }

        private void LateUpdate()
        {
            if (_focusTarget != null)
            {
                _focusGoal = _focusTarget.position;
            }

            if (_focusDamping <= 0f)
            {
                _focusPoint = _focusGoal;
            }
            else
            {
                _focusPoint = Vector3.SmoothDamp(_focusPoint, _focusGoal, ref _focusVelocity, _focusDamping);
            }

            ApplyTransform();
        }

        // Continuous focus on a unit; pass null to hold the last focus point.
        public void SetFocusTarget(Transform target)
        {
            _focusTarget = target;
            if (target != null)
            {
                FocusWorld(target.position);
            }
        }

        public void FocusWorld(Vector3 worldPosition)
        {
            _focusGoal = worldPosition;
            if (!_hasFocused)
            {
                // First focus snaps so combat start does not swoop in from origin.
                _hasFocused = true;
                _focusPoint = _focusGoal;
                _focusVelocity = Vector3.zero;
            }

            ApplyTransform();
        }

        public void StepYaw(float degrees)
        {
            _yaw = OrbitCameraMath.NormalizeYaw(_yaw + degrees);
            ApplyTransform();
        }

        public void SetDistance(float distance)
        {
            _distance = OrbitCameraMath.ClampDistance(distance, _minDistance, _maxDistance);
            ApplyTransform();
        }

        // -devcam compatibility: apply a tuning set, clamped to orbit ranges.
        public void ApplyTuning(CameraTuningState state)
        {
            _pitch = OrbitCameraMath.ClampPitch(state.Pitch, _minPitch, _maxPitch);
            _distance = OrbitCameraMath.ClampDistance(state.Distance, _minDistance, _maxDistance);
            _fov = CameraTuning.ClampFov(state.Fov);
            _focusDamping = state.FollowDamping < 0f || float.IsNaN(state.FollowDamping) ? 0f : state.FollowDamping;
            ApplyTransform();
        }

        public void SetPresentationOffset(Vector3 offset)
        {
            _presentationOffset = offset;
            ApplyTransform();
        }

        private void EnsureCamera()
        {
            if (_camera == null)
            {
                _camera = GetComponentInChildren<Camera>();
                if (_camera == null)
                {
                    GameObject cameraObject = new GameObject("Orbit Camera");
                    cameraObject.transform.SetParent(transform, false);
                    _camera = cameraObject.AddComponent<Camera>();
                }
            }

            _camera.orthographic = false;
        }

        private void ApplyTransform()
        {
            EnsureCamera();

            var offset = OrbitCameraMath.ComputeOffset(_yaw, _pitch, _distance);
            transform.position = _focusPoint + _presentationOffset + new Vector3(offset.X, offset.Y, offset.Z);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.localPosition = Vector3.zero;
            _camera.transform.localRotation = Quaternion.identity;
            _camera.fieldOfView = _fov;
        }
    }
}
