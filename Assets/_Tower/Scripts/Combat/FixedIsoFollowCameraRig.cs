using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // T33: combat camera policy after 75 DC-3. Fixed iso angle, target follow,
    // scroll zoom, no yaw/pitch rotation input. Combat focuses the active turn
    // unit; presentation offset remains for shake/juice.
    public sealed class FixedIsoFollowCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _fixedYaw = FixedIsoCameraMath.DefaultYaw;
        [SerializeField] private float _pitch = FixedIsoCameraMath.DefaultPitch;
        [SerializeField] private float _distance = FixedIsoCameraMath.DefaultDistance;
        [SerializeField] private float _minPitch = FixedIsoCameraMath.MinPitch;
        [SerializeField] private float _maxPitch = FixedIsoCameraMath.MaxPitch;
        [SerializeField] private float _minDistance = FixedIsoCameraMath.MinDistance;
        [SerializeField] private float _maxDistance = FixedIsoCameraMath.MaxDistance;
        [SerializeField] private float _zoomStep = FixedIsoCameraMath.DefaultZoomStep;
        [SerializeField] private float _focusDamping = FixedIsoCameraMath.DefaultFocusDamping;
        [SerializeField] private float _fov = CameraTuning.DefaultFov;
        [SerializeField] private CameraOcclusionFadeController _occlusionFade;

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
            float scroll = Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(scroll, 0f))
            {
                _distance = FixedIsoCameraMath.ClampDistance(_distance - (scroll * _zoomStep), _minDistance, _maxDistance);
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

        public void SetFocusTarget(Transform target)
        {
            _focusTarget = target;
            EnsureOcclusionFade();
            _occlusionFade.SetTarget(target);
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
                _hasFocused = true;
                _focusPoint = _focusGoal;
                _focusVelocity = Vector3.zero;
            }

            ApplyTransform();
        }

        public void SetDistance(float distance)
        {
            _distance = FixedIsoCameraMath.ClampDistance(distance, _minDistance, _maxDistance);
            ApplyTransform();
        }

        public void ApplyTuning(CameraTuningState state)
        {
            _pitch = FixedIsoCameraMath.ClampPitch(state.Pitch, _minPitch, _maxPitch);
            _distance = FixedIsoCameraMath.ClampDistance(state.Distance, _minDistance, _maxDistance);
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
                    GameObject cameraObject = new GameObject("Fixed Iso Camera");
                    cameraObject.transform.SetParent(transform, false);
                    _camera = cameraObject.AddComponent<Camera>();
                }
            }

            _camera.orthographic = false;
            EnsureOcclusionFade();
            _occlusionFade.SetCamera(_camera);
        }

        private void ApplyTransform()
        {
            EnsureCamera();

            CameraOffset offset = FixedIsoCameraMath.ComputeOffset(_fixedYaw, _pitch, _distance);
            transform.position = _focusPoint + _presentationOffset + new Vector3(offset.X, offset.Y, offset.Z);
            transform.rotation = Quaternion.Euler(_pitch, _fixedYaw, 0f);
            _camera.transform.localPosition = Vector3.zero;
            _camera.transform.localRotation = Quaternion.identity;
            _camera.fieldOfView = _fov;
        }

        private void EnsureOcclusionFade()
        {
            if (_occlusionFade == null)
            {
                _occlusionFade = GetComponent<CameraOcclusionFadeController>();
                if (_occlusionFade == null)
                {
                    _occlusionFade = gameObject.AddComponent<CameraOcclusionFadeController>();
                }
            }
        }
    }
}
