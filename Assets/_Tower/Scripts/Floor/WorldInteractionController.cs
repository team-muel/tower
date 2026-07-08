using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Floor
{
    // T41 (M4): thin world glue that turns proximity + a key press into a Core
    // interaction. It holds NO rules: focus = nearest eligible anchor in the
    // current node, and Use is delegated to the node's InteractableRegistry
    // (resolved against an InteractionContext the renderer builds). AX2-safe:
    // walk up + press a key, no button menu, no grid. Locked anchors still show
    // their reason (Resolver contract), never silent.
    [DisallowMultipleComponent]
    public sealed class WorldInteractionController : MonoBehaviour
    {
        [SerializeField] private ForestFloorRenderer floor;
        [SerializeField] private Transform player;
        [SerializeField] private float interactRadius = 3.2f;
        [SerializeField] private KeyCode useKey = KeyCode.E;
        [SerializeField] private bool showHudPrompt = true;

        private readonly List<AnchorMarker> _nodeMarkers = new List<AnchorMarker>();
        private int _cachedNodeId = int.MinValue;
        private AnchorMarker _focused;
        private string _hudLine = string.Empty;

        public void Bind(ForestFloorRenderer renderer, Transform playerTransform)
        {
            floor = renderer;
            player = playerTransform;
        }

        private void Update()
        {
            if (floor == null || player == null)
            {
                return;
            }

            InteractableRegistry registry = floor.RegistryFor(floor.CurrentNodeId);
            if (registry == null)
            {
                _focused = null;
                _hudLine = string.Empty;
                return;
            }

            RefreshMarkerCache();
            InteractionContext ctx = floor.BuildContext();
            IReadOnlyList<InteractionState> states = registry.ResolveAll(ctx);
            Dictionary<string, InteractionState> byId = new Dictionary<string, InteractionState>();
            foreach (InteractionState s in states)
            {
                byId[s.Id] = s;
            }

            _focused = NearestEligible(byId);
            ApplyReflection(byId);
            UpdateHud(registry, byId);

            if (_focused != null && Input.GetKeyDown(useKey))
            {
                Result<InteractionState> used = registry.Use(_focused.AnchorId, ctx);
                _hudLine = used.IsSuccess
                    ? $"[{_focused.Kind}] 사용됨"
                    : $"[{_focused.Kind}] {used.Error}";
            }
        }

        private void RefreshMarkerCache()
        {
            if (_cachedNodeId == floor.CurrentNodeId && _nodeMarkers.Count > 0)
            {
                return;
            }

            _cachedNodeId = floor.CurrentNodeId;
            _nodeMarkers.Clear();
            AnchorMarker[] all = floor.GetComponentsInChildren<AnchorMarker>(true);
            foreach (AnchorMarker m in all)
            {
                if (m.NodeId == floor.CurrentNodeId)
                {
                    _nodeMarkers.Add(m);
                }
            }
        }

        private AnchorMarker NearestEligible(Dictionary<string, InteractionState> byId)
        {
            AnchorMarker best = null;
            float bestSq = interactRadius * interactRadius;
            Vector3 p = player.position;
            foreach (AnchorMarker m in _nodeMarkers)
            {
                if (m == null || !byId.TryGetValue(m.AnchorId, out InteractionState st))
                {
                    continue;
                }

                if (!st.Visible)
                {
                    continue;
                }

                float d = (m.transform.position - p).sqrMagnitude;
                if (d <= bestSq)
                {
                    bestSq = d;
                    best = m;
                }
            }

            return best;
        }

        private void ApplyReflection(Dictionary<string, InteractionState> byId)
        {
            foreach (AnchorMarker m in _nodeMarkers)
            {
                if (m == null || !byId.TryGetValue(m.AnchorId, out InteractionState st))
                {
                    continue;
                }

                bool spent = st.Visible && !st.Enabled;
                m.gameObject.SetActive(st.Visible);
                m.Reflect(m == _focused, spent);
            }
        }

        private void UpdateHud(InteractableRegistry registry, Dictionary<string, InteractionState> byId)
        {
            if (_focused == null)
            {
                _hudLine = string.Empty;
                return;
            }

            InteractableRegistry.Entry entry = registry.Find(_focused.AnchorId);
            if (entry == null || !byId.TryGetValue(_focused.AnchorId, out InteractionState st))
            {
                _hudLine = string.Empty;
                return;
            }

            string tail = st.Enabled
                ? (string.IsNullOrEmpty(st.Preview) ? $"[{useKey}] " : $"{st.Preview}  [{useKey}] ")
                : $"잠김: {st.DisabledReason}";
            _hudLine = $"{entry.Def.Prompt}  —  {tail}";
        }

        private void OnGUI()
        {
            if (!showHudPrompt || string.IsNullOrEmpty(_hudLine))
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = Color.white;
            Rect r = new Rect(0f, Screen.height - 96f, Screen.width, 40f);
            GUI.Label(r, _hudLine, style);
        }
    }
}
