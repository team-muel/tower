using UnityEngine;

namespace Tower.Floor
{
    // Attached to each visible fork-trail strip's entry volume. When the transform-
    // driven player enters, it reports the carried RouteEdge id back to the renderer,
    // which resolves the next node and moves the party. Diegetic selection: no menu.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ForkTrailTrigger : MonoBehaviour
    {
        private ForestFloorRenderer _renderer;
        private int _routeId;
        private int _fromNodeId;
        private bool _consumed;

        public int RouteId => _routeId;
        public int FromNodeId => _fromNodeId;

        public void Bind(ForestFloorRenderer renderer, int fromNodeId, int routeId)
        {
            _renderer = renderer;
            _fromNodeId = fromNodeId;
            _routeId = routeId;
            _consumed = false;
        }

        // Called by physics when the player collider enters, or directly by the
        // renderer for headless/tests. Idempotent per activation.
        public void Enter(Collider other)
        {
            if (_consumed || _renderer == null) return;
            if (!_renderer.IsPlayer(other)) return;
            _consumed = true;
            _renderer.OnTrailEntered(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            Enter(other);
        }
    }
}
