using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // Perspective iso rig using the v0 tuning values from Tower.Core.CameraTuning.
    // Mouse scroll zoom is always active; -devcam adds CameraTuningModeController
    // on top of this rig for live pitch/distance/FOV adjustment.
    public sealed class IsoCameraRig : MonoBehaviour
    {
        private static readonly float[] Yaws = { 45f, 135f, 225f, 315f };
        private static readonly float[] ZoomPresetDistances = { 10f, 16f };
        private const float ScrollZoomStep = 1.5f;

        [SerializeField] private Camera _camera;
        [SerializeField] private float _pitch = CameraTuning.DefaultPitch;
        [SerializeField] private float _distance = CameraTuning.DefaultDistance;
        [SerializeField] private float _fov = CameraTuning.DefaultFov;
        [SerializeField] private float _followDamping = CameraTuning.DefaultFollowDamping;
        [SerializeField] private float _panSpeed = 8f;

        private int _yawIndex;
        private int _zoomIndex;
        private bool _hasFocused;
        private Vector3 _pivot;
        private Vector3 _pivotTarget;
        private Vector3 _pivotVelocity;
        private Transform _followTarget;

        public Camera Camera
        {
            get { return _camera; }
        }

        public CameraTuningState Tuning
        {
            get { return new CameraTuningState(_pitch, _distance, _fov, _followDamping); }
        }

        private void Awake()
        {
            EnsureCamera();
            _pivot = _pivotTarget;
            ApplyTransform();
        }

        private void Update()
        {
            // Follow mode (camp): WASD/arrows and Q/E belong to the followed
            // actor's own controller, so the rig only keeps scroll zoom live.
            if (_followTarget == null)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    RotateCounterClockwise();
                }

                if (Input.GetKeyDown(KeyCode.E))
                {
                    RotateClockwise();
                }

                Vector2 pan = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (pan.sqrMagnitude > 0f)
                {
                    Pan(pan.normalized * _panSpeed * Time.deltaTime);
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                SetZoomLevel(0);
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                SetZoomLevel(1);
            }

            // Player scroll zoom stays active in every build, dev or not.
            float scroll = Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(scroll, 0f))
            {
                SetDistance(_distance - (scroll * ScrollZoomStep));
            }
        }

        private void LateUpdate()
        {
            if (_followTarget != null)
            {
                _pivotTarget = _followTarget.position;
            }

            // Follow damping: ease the rig pivot toward the focus target.
            if (_followDamping <= 0f)
            {
                _pivot = _pivotTarget;
            }
            else
            {
                _pivot = Vector3.SmoothDamp(_pivot, _pivotTarget, ref _pivotVelocity, _followDamping);
            }

            ApplyTransform();
        }

        public void Focus(GridView gridView, GridPos pos)
        {
            if (gridView == null)
            {
                return;
            }

            FocusWorld(gridView.CellToWorld(pos));
        }

        public void FocusWorld(Vector3 worldPosition)
        {
            _pivotTarget = worldPosition;
            if (!_hasFocused)
            {
                // First focus snaps so scene start does not swoop in from origin.
                _hasFocused = true;
                _pivot = _pivotTarget;
                _pivotVelocity = Vector3.zero;
            }

            ApplyTransform();
        }

        // Continuous follow (camp hub): the rig tracks the target every frame
        // with the usual damping. Pass null to return to manual focus/pan mode.
        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            if (target != null)
            {
                FocusWorld(target.position);
            }
        }

        public void Pan(Vector2 delta)
        {
            Quaternion yaw = Quaternion.Euler(0f, Yaws[_yawIndex], 0f);
            Vector3 right = yaw * Vector3.right;
            Vector3 forward = yaw * Vector3.forward;
            Vector3 offset = (right * delta.x) + (forward * delta.y);
            _pivotTarget += offset;
            _pivot += offset;
            ApplyTransform();
        }

        public void RotateClockwise()
        {
            _yawIndex = (_yawIndex + 1) % Yaws.Length;
            ApplyTransform();
        }

        public void RotateCounterClockwise()
        {
            _yawIndex = (_yawIndex + Yaws.Length - 1) % Yaws.Length;
            ApplyTransform();
        }

        public void SetRotationSnap(int index)
        {
            int wrapped = index % Yaws.Length;
            if (wrapped < 0)
            {
                wrapped += Yaws.Length;
            }

            _yawIndex = wrapped;
            ApplyTransform();
        }

        public void SetZoomLevel(int index)
        {
            _zoomIndex = Mathf.Clamp(index, 0, ZoomPresetDistances.Length - 1);
            SetDistance(ZoomPresetDistances[_zoomIndex]);
        }

        public void ToggleZoom()
        {
            SetZoomLevel(_zoomIndex == 0 ? 1 : 0);
        }

        public void SetDistance(float distance)
        {
            _distance = CameraTuning.ClampDistance(distance);
            ApplyTransform();
        }

        public void ApplyTuning(CameraTuningState state)
        {
            var clamped = CameraTuning.Clamp(state);
            _pitch = clamped.Pitch;
            _distance = clamped.Distance;
            _fov = clamped.Fov;
            _followDamping = clamped.FollowDamping;
            ApplyTransform();
        }

        private void EnsureCamera()
        {
            if (_camera == null)
            {
                _camera = GetComponentInChildren<Camera>();
                if (_camera == null)
                {
                    GameObject cameraObject = new GameObject("Iso Camera");
                    cameraObject.transform.SetParent(transform, false);
                    _camera = cameraObject.AddComponent<Camera>();
                }
            }

            _camera.orthographic = false;
        }

        private void ApplyTransform()
        {
            EnsureCamera();

            Quaternion rotation = Quaternion.Euler(_pitch, Yaws[_yawIndex], 0f);
            transform.position = _pivot;
            transform.rotation = rotation;
            _camera.transform.localPosition = new Vector3(0f, 0f, -_distance);
            _camera.transform.localRotation = Quaternion.identity;
            _camera.fieldOfView = _fov;
        }
    }
}
