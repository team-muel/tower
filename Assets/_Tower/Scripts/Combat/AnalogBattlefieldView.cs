using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // T20: renders the analog battlefield — a flat floor sized to the room
    // plus the manual-move affordances (movement-radius ring + destination
    // marker) used by the regressor's click-to-move flow. Replaces GridView +
    // TileHighlighter when CombatSpaceMode.Analog is active; the grid view
    // path remains untouched for rollback.
    public sealed class AnalogBattlefieldView : MonoBehaviour
    {
        private const int RingSegments = 64;
        private const float RingHeight = 0.06f;

        [SerializeField] private Color _floorColor = new Color(0.18f, 0.2f, 0.22f, 1f);
        [SerializeField] private Color _ringColor = new Color(0.25f, 0.8f, 0.45f, 1f);
        [SerializeField] private Color _targetRingColor = new Color(0.7f, 0.18f, 0.35f, 1f);
        [SerializeField] private Color _markerColor = new Color(0.95f, 0.85f, 0.25f, 1f);

        private GameObject _floor;
        private LineRenderer _ring;
        private GameObject _marker;

        public AnalogBattlefield Battlefield { get; private set; }

        public void Build(AnalogBattlefield battlefield)
        {
            Battlefield = battlefield;
            if (_floor != null)
            {
                Destroy(_floor);
                _floor = null;
            }

            if (battlefield == null)
            {
                return;
            }

            _floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _floor.name = "Analog Floor";
            _floor.transform.SetParent(transform, false);
            _floor.transform.localPosition = new Vector3(battlefield.Width * 0.5f, 0f, battlefield.Height * 0.5f);
            _floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _floor.transform.localScale = new Vector3(battlefield.Width, battlefield.Height, 1f);

            var renderer = _floor.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Runtime Analog Floor Material", color = _floorColor };
            renderer.sharedMaterial = material;
        }

        public Vector3 ToWorld(BattlePos pos)
        {
            return transform.TransformPoint(new Vector3(pos.X, 0f, pos.Y));
        }

        public bool TryGetBattlePos(Vector3 worldPosition, out BattlePos pos)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            pos = new BattlePos(local.x, local.z);
            return Battlefield != null && Battlefield.Contains(pos);
        }

        // Movement-radius ring for the regressor's manual move (click inside
        // the ring to move). Also reused as a range hint for ability targeting.
        public void ShowRing(BattlePos center, float radius, bool targeting = false)
        {
            if (radius <= 0f)
            {
                HideRing();
                return;
            }

            EnsureRing();
            _ring.startColor = targeting ? _targetRingColor : _ringColor;
            _ring.endColor = _ring.startColor;
            var centerWorld = ToWorld(center);
            for (var index = 0; index <= RingSegments; index++)
            {
                var angle = (Mathf.PI * 2f * index) / RingSegments;
                _ring.SetPosition(index, centerWorld + new Vector3(Mathf.Cos(angle) * radius, RingHeight, Mathf.Sin(angle) * radius));
            }

            _ring.gameObject.SetActive(true);
        }

        public void HideRing()
        {
            if (_ring != null)
            {
                _ring.gameObject.SetActive(false);
            }
        }

        public void ShowDestinationMarker(BattlePos pos)
        {
            if (_marker == null)
            {
                _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _marker.name = "Destination Marker";
                _marker.transform.SetParent(transform, false);
                _marker.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
                var collider = _marker.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                var renderer = _marker.GetComponent<Renderer>();
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                renderer.sharedMaterial = new Material(shader) { name = "Destination Marker Material", color = _markerColor };
            }

            _marker.transform.position = ToWorld(pos) + Vector3.up * RingHeight;
            _marker.SetActive(true);
        }

        public void HideDestinationMarker()
        {
            if (_marker != null)
            {
                _marker.SetActive(false);
            }
        }

        private void EnsureRing()
        {
            if (_ring != null)
            {
                return;
            }

            var ringObject = new GameObject("Move Range Ring");
            ringObject.transform.SetParent(transform, false);
            _ring = ringObject.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.loop = false;
            _ring.positionCount = RingSegments + 1;
            _ring.startWidth = 0.07f;
            _ring.endWidth = 0.07f;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            _ring.material = new Material(shader) { name = "Move Range Ring Material" };
        }
    }
}
