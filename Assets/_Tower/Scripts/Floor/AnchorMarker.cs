using Tower.Core;
using UnityEngine;

namespace Tower.Floor
{
    // T41 (M4): the world handle for one placed interaction anchor. Carries only
    // identity (which node's registry owns it + the anchor id + its kind) plus a
    // cached renderer so the interaction controller can reflect resolved state
    // (focused highlight, spent dim). All decision logic lives in the Core
    // registry/resolver — this component holds no rules.
    [DisallowMultipleComponent]
    public sealed class AnchorMarker : MonoBehaviour
    {
        public int NodeId { get; private set; }

        public string AnchorId { get; private set; }

        public InteractableKind Kind { get; private set; }

        private MeshRenderer _renderer;
        private Color _baseColor = Color.white;

        public void Bind(int nodeId, string anchorId, InteractableKind kind, Color baseColor)
        {
            NodeId = nodeId;
            AnchorId = anchorId;
            Kind = kind;
            _renderer = GetComponentInChildren<MeshRenderer>();
            _baseColor = baseColor;
        }

        // Focused = about to be used (player nearest + eligible). Spent = disabled.
        public void Reflect(bool focused, bool spent)
        {
            if (_renderer == null)
            {
                return;
            }

            Color c = spent ? _baseColor * 0.4f : (focused ? Color.Lerp(_baseColor, Color.white, 0.55f) : _baseColor);
            c.a = 1f;
            if (_renderer.material.HasProperty("_BaseColor"))
            {
                _renderer.material.SetColor("_BaseColor", c);
            }

            if (_renderer.material.HasProperty("_Color"))
            {
                _renderer.material.SetColor("_Color", c);
            }
        }
    }
}
