using System.Collections.Generic;
using UnityEngine;

namespace Tower.Combat
{
    // DC-3 occlusion policy: keep the fixed iso camera, but fade objects that
    // pass between the camera and its current follow target.
    public sealed class CameraOcclusionFadeController : MonoBehaviour
    {
        private const int MaxHits = 32;

        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _target;
        [SerializeField] private LayerMask _occluderLayers = ~0;
        [SerializeField] private float _sphereRadius = 0.18f;
        [SerializeField] private float _fadeAlpha = 0.28f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private readonly HashSet<Renderer> _visibleThisFrame = new HashSet<Renderer>();
        private readonly Dictionary<Renderer, FadeState> _faded = new Dictionary<Renderer, FadeState>();
        private readonly List<Renderer> _restoreBuffer = new List<Renderer>();

        public void SetCamera(Camera camera)
        {
            _camera = camera;
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        public void RefreshOccluders()
        {
            UpdateOccluders();
        }

        private void LateUpdate()
        {
            RefreshOccluders();
        }

        private void OnDisable()
        {
            RestoreAll();
        }

        private void OnDestroy()
        {
            RestoreAll();
        }

        private void UpdateOccluders()
        {
            _visibleThisFrame.Clear();

            if (_camera == null || _target == null)
            {
                RestoreMissing();
                return;
            }

            Vector3 origin = _camera.transform.position;
            Vector3 toTarget = _target.position - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.05f)
            {
                RestoreMissing();
                return;
            }

            Vector3 direction = toTarget / distance;
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                _sphereRadius,
                direction,
                _hits,
                distance,
                _occluderLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Renderer renderer = ResolveRenderer(_hits[i]);
                if (renderer == null || IsTarget(renderer.transform))
                {
                    continue;
                }

                _visibleThisFrame.Add(renderer);
                Fade(renderer);
            }

            RestoreMissing();
        }

        private Renderer ResolveRenderer(RaycastHit hit)
        {
            if (hit.collider == null)
            {
                return null;
            }

            return hit.collider.GetComponentInParent<Renderer>();
        }

        private bool IsTarget(Transform candidate)
        {
            if (candidate == null || _target == null)
            {
                return false;
            }

            return candidate == _target || candidate.IsChildOf(_target) || _target.IsChildOf(candidate);
        }

        private void Fade(Renderer renderer)
        {
            if (_faded.ContainsKey(renderer))
            {
                return;
            }

            Material[] original = renderer.sharedMaterials;
            Material[] fadedMaterials = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                Material clone = original[i] == null ? null : new Material(original[i]);
                if (clone != null)
                {
                    clone.name = original[i].name + " Occlusion Fade";
                    OcclusionFadeMaterial.ConfigureTransparent(clone, _fadeAlpha);
                }

                fadedMaterials[i] = clone;
            }

            _faded[renderer] = new FadeState(original, fadedMaterials);
            renderer.sharedMaterials = fadedMaterials;
        }

        private void RestoreMissing()
        {
            _restoreBuffer.Clear();
            foreach (Renderer renderer in _faded.Keys)
            {
                if (!_visibleThisFrame.Contains(renderer))
                {
                    _restoreBuffer.Add(renderer);
                }
            }

            for (int i = 0; i < _restoreBuffer.Count; i++)
            {
                Restore(_restoreBuffer[i]);
            }
        }

        private void RestoreAll()
        {
            _restoreBuffer.Clear();
            foreach (Renderer renderer in _faded.Keys)
            {
                _restoreBuffer.Add(renderer);
            }

            for (int i = 0; i < _restoreBuffer.Count; i++)
            {
                Restore(_restoreBuffer[i]);
            }
        }

        private void Restore(Renderer renderer)
        {
            if (renderer == null || !_faded.TryGetValue(renderer, out FadeState state))
            {
                return;
            }

            renderer.sharedMaterials = state.OriginalMaterials;
            for (int i = 0; i < state.FadedMaterials.Length; i++)
            {
                if (state.FadedMaterials[i] != null)
                {
                    DestroyMaterial(state.FadedMaterials[i]);
                }
            }

            _faded.Remove(renderer);
        }

        private static void DestroyMaterial(Material material)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
                return;
            }

            DestroyImmediate(material);
        }

        private readonly struct FadeState
        {
            public FadeState(Material[] originalMaterials, Material[] fadedMaterials)
            {
                OriginalMaterials = originalMaterials;
                FadedMaterials = fadedMaterials;
            }

            public Material[] OriginalMaterials { get; }
            public Material[] FadedMaterials { get; }
        }
    }
}
