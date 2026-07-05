using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public sealed class IsoCameraRig : MonoBehaviour
    {
        private static readonly float[] Yaws = { 45f, 135f, 225f, 315f };
        private static readonly float[] OrthographicSizes = { 7f, 11f };

        [SerializeField] private Camera _camera;
        [SerializeField] private float _pitch = 55f;
        [SerializeField] private float _distance = 12f;
        [SerializeField] private float _panSpeed = 8f;

        private int _yawIndex;
        private int _zoomIndex;
        private Vector3 _pivot;

        public Camera Camera
        {
            get { return _camera; }
        }

        private void Awake()
        {
            EnsureCamera();
            ApplyTransform();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                RotateCounterClockwise();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                RotateClockwise();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                SetZoomLevel(0);
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                SetZoomLevel(1);
            }

            Vector2 pan = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (pan.sqrMagnitude > 0f)
            {
                Pan(pan.normalized * _panSpeed * Time.deltaTime);
            }
        }

        public void Focus(GridView gridView, GridPos pos)
        {
            if (gridView == null)
            {
                return;
            }

            _pivot = gridView.CellToWorld(pos);
            ApplyTransform();
        }

        public void Pan(Vector2 delta)
        {
            Quaternion yaw = Quaternion.Euler(0f, Yaws[_yawIndex], 0f);
            Vector3 right = yaw * Vector3.right;
            Vector3 forward = yaw * Vector3.forward;
            _pivot += (right * delta.x) + (forward * delta.y);
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
            _zoomIndex = Mathf.Clamp(index, 0, OrthographicSizes.Length - 1);
            ApplyTransform();
        }

        public void ToggleZoom()
        {
            SetZoomLevel(_zoomIndex == 0 ? 1 : 0);
        }

        private void EnsureCamera()
        {
            if (_camera != null)
            {
                return;
            }

            _camera = GetComponentInChildren<Camera>();
            if (_camera == null)
            {
                GameObject cameraObject = new GameObject("Iso Camera");
                cameraObject.transform.SetParent(transform, false);
                _camera = cameraObject.AddComponent<Camera>();
            }

            _camera.orthographic = true;
        }

        private void ApplyTransform()
        {
            EnsureCamera();

            Quaternion rotation = Quaternion.Euler(_pitch, Yaws[_yawIndex], 0f);
            transform.position = _pivot;
            transform.rotation = rotation;
            _camera.transform.localPosition = new Vector3(0f, 0f, -_distance);
            _camera.transform.localRotation = Quaternion.identity;
            _camera.orthographicSize = OrthographicSizes[_zoomIndex];
        }
    }
}
