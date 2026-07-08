using System.Collections;
using System.Collections.Generic;
using Tower.Core;
using Tower.Gen;
using Tower.UI;
using UnityEngine;

namespace Tower.Floor
{
    // T40b: renders the 1층계(숲) as a WALKABLE space and drives visible fork traversal
    // + Ascend. Consumes FloorGraph (Tower.Gen) + an IFloorLayoutSource (decoupled from
    // Core FloorLayout) + BiomeTheme.Forest. Per node it builds terrain (HeightFieldFactory
    // + FloorTerrainMeshBuilder) and deterministic forest content (ForestContentPlanner),
    // then renders the two RouteType-tinted fork trails as in-world diegetic choices.
    [DisallowMultipleComponent]
    public sealed class ForestFloorRenderer : MonoBehaviour
    {
        public const string GeneratedRootName = "_GeneratedForestFloor";

        [Header("Floor generation")]
        [SerializeField] private int seed = 777;
        [SerializeField] private BiomeId biomeId = BiomeId.Forest;
        [SerializeField, Range(3, 5)] private int nodeCount = 4;
        [SerializeField] private bool isBossFloor;
        [SerializeField] private bool includeCamp = true;

        [Header("Layout (stub) tuning")]
        [SerializeField, Min(8f)] private float travelLength = 26f;
        [SerializeField, Min(4f)] private float crossWidth = 15f;
        [SerializeField, Min(0f)] private float gap = 10f;
        [SerializeField] private float forkBow = 5.5f;

        [Header("Terrain")]
        [SerializeField, Range(8, 64)] private int meshResolution = 28;
        [SerializeField] private Vector2 uvScale = new Vector2(4f, 4f);
        [SerializeField] private Material terrainMaterial;

        [Header("Traversal")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float walkSpeed = 9f;
        [SerializeField] private bool buildOnStart = true;

        [Header("Ascend")]
        [SerializeField] private AscendController ascend;

        private FloorGraph _graph;
        private IFloorLayoutSource _layout;
        private BiomeDef _biomeDef;
        private BiomeTheme _theme;
        private readonly FloorExploration _exploration = new FloorExploration();
        private readonly Dictionary<int, HeightField> _nodeHeight = new Dictionary<int, HeightField>();
        private readonly Dictionary<int, Vector3> _nodeOrigin = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, InteractableRegistry> _registries = new Dictionary<int, InteractableRegistry>();
        private readonly InteractionRuntimeStore _interactionRuntimeStore = new InteractionRuntimeStore();
        private Transform _root;
        private Material _terrainMat;
        private bool _busy;

        public FloorGraph Graph => _graph;
        public FloorExploration Exploration => _exploration;
        public int CurrentNodeId { get; private set; }

        // Allows the orchestrator's Core->interface adapter to inject a real layout.
        public void SetLayoutSource(IFloorLayoutSource source) => _layout = source;

        private void Start()
        {
            if (buildOnStart) Rebuild();
        }

        [ContextMenu("Rebuild Forest Floor")]
        public void Rebuild()
        {
            CaptureInteractionState();
            ClearGeneratedRoot();
            _nodeHeight.Clear();
            _nodeOrigin.Clear();
            _registries.Clear();

            FloorGenParams parameters = new FloorGenParams(
                seed,
                new IntRange(nodeCount, nodeCount),
                isBossFloor,
                new IntRange(8, 14),
                new[] { "melee", "ranged", "elite" },
                "boss",
                includeCamp,
                biomeId);

            _graph = FloorGenerator.Generate(parameters);
            _biomeDef = BiomeDef.For(biomeId);
            _theme = _graph.BiomeTheme;
            if (_layout == null)
            {
                _layout = new LinearStubLayout(_graph, travelLength, crossWidth, gap, forkBow);
            }

            _terrainMat = ResolveTerrainMaterial();
            ApplyBiomeAtmosphere();

            GameObject rootGo = new GameObject(GeneratedRootName);
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;

            for (int i = 0; i < _graph.Nodes.Count; i++)
            {
                BuildNode(_graph.Nodes[i]);
            }

            for (int i = 0; i < _graph.Nodes.Count; i++)
            {
                BuildForks(_graph.Nodes[i].Id);
            }

            InitTraversal();
        }

        private void BuildNode(FloorNode node)
        {
            FloorFieldRect field = _layout.GetField(node.Id);
            HeightField height = HeightFieldFactory.ForNode(_graph.Seed, node, _biomeDef);
            _nodeHeight[node.Id] = height;

            // Segment mesh spans local [0,crossWidth] x [0,travelLength]; origin is the
            // field centre shifted by half extents so local (0,0) maps to a world point.
            Vector3 origin = new Vector3(field.MinX, field.Center.y, field.MinZ);
            _nodeOrigin[node.Id] = origin;

            GameObject segment = new GameObject($"Node_{node.Id:00}_{node.Kind}");
            segment.transform.SetParent(_root, false);
            segment.transform.localPosition = origin;

            Mesh mesh = new FloorTerrainMeshBuilder().Build(
                height, meshResolution, field.CrossWidth, field.TravelLength, uvScale);
            mesh.name = $"ForestTerrain_Node{node.Id}";
            segment.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = segment.AddComponent<MeshRenderer>();
            if (_terrainMat != null) mr.sharedMaterial = _terrainMat;
            segment.AddComponent<MeshCollider>().sharedMesh = mesh;

            BuildContent(node, field, segment.transform);
            if (TrailNavigator.IsEventNode(node) || node.IsExit || node.IsBossRoom)
            {
                BuildMarker(node, field, segment.transform);
            }
        }

        private void BuildContent(FloorNode node, FloorFieldRect field, Transform parent)
        {
            ForestContentPlan plan = ForestContentPlanner.Build(_graph.Seed, node.Id, field);

            Transform trees = new GameObject("Trees").transform;
            trees.SetParent(parent, false);
            foreach (ForestProp t in plan.Trees) BuildTree(node.Id, t, trees);

            Transform rocks = new GameObject("Rocks").transform;
            rocks.SetParent(parent, false);
            foreach (ForestProp r in plan.Rocks) BuildRock(node.Id, r, rocks);

            BuildPathStrip(node.Id, plan.PathWaypoints, parent);
            BuildAnchors(node, field, plan.Clearing, parent);
        }

        // T41 (M4): place T29 interaction anchors for the node and bind them into
        // a per-node InteractableRegistry the WorldInteractionController resolves.
        private void BuildAnchors(FloorNode node, FloorFieldRect field, ForestClearing clearing, Transform parent)
        {
            NodeAnchorPlan plan = NodeAnchorPlanner.Build(_graph.Seed, node, field, clearing);
            if (plan.Anchors.Count == 0) return;

            InteractableRegistry registry = new InteractableRegistry();
            Transform anchorsRoot = new GameObject("Anchors").transform;
            anchorsRoot.SetParent(parent, false);
            foreach (PlacedAnchor a in plan.Anchors)
            {
                Result<AnchorRuntime> runtime = _interactionRuntimeStore.RuntimeFor(a.Def);
                AnchorRuntime resolved = runtime.IsSuccess
                    ? runtime.Value
                    : AnchorRuntime.CreateDefault(a.Def.Kind, a.Def.MaxUses);
                if (registry.Add(a.Def, resolved).IsFailure) continue;
                BuildAnchorObject(node.Id, a, anchorsRoot);
            }

            _registries[node.Id] = registry;
        }

        private void BuildAnchorObject(int nodeId, PlacedAnchor a, Transform parent)
        {
            AnchorVisual(a.Kind, out PrimitiveType prim, out Vector3 scale, out Color color);
            float gy = GroundY(nodeId, a.Position.x, a.Position.z);
            GameObject go = GameObject.CreatePrimitive(prim);
            go.name = $"Anchor_{a.Def.Id}";
            go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(a.Position.x, gy + Mathf.Max(0.1f, scale.y * 0.5f), a.Position.z);
            go.transform.localScale = scale;
            Paint(go, color);
            AnchorMarker marker = go.AddComponent<AnchorMarker>();
            marker.Bind(nodeId, a.Def.Id, a.Kind, color);
        }

        // Placeholder kind -> (primitive, scale, tint). Replaced by authored props later (77 §4).
        private static void AnchorVisual(InteractableKind kind, out PrimitiveType prim, out Vector3 scale, out Color color)
        {
            switch (kind)
            {
                case InteractableKind.Chest:
                    prim = PrimitiveType.Cube; scale = new Vector3(1.1f, 0.7f, 0.8f); color = new Color(0.85f, 0.62f, 0.25f); break;
                case InteractableKind.Shrine:
                    prim = PrimitiveType.Sphere; scale = new Vector3(0.9f, 0.9f, 0.9f); color = new Color(0.4f, 0.8f, 0.95f); break;
                case InteractableKind.Grave:
                    prim = PrimitiveType.Cube; scale = new Vector3(0.7f, 1.3f, 0.25f); color = new Color(0.62f, 0.62f, 0.66f); break;
                case InteractableKind.Trap:
                    prim = PrimitiveType.Cylinder; scale = new Vector3(1.2f, 0.1f, 1.2f); color = new Color(0.85f, 0.3f, 0.3f); break;
                case InteractableKind.Resource:
                    prim = PrimitiveType.Sphere; scale = new Vector3(0.7f, 0.7f, 0.7f); color = new Color(0.45f, 0.8f, 0.45f); break;
                case InteractableKind.Inspect:
                default:
                    prim = PrimitiveType.Cube; scale = new Vector3(0.4f, 1.6f, 0.4f); color = new Color(0.9f, 0.82f, 0.35f); break;
            }
        }

        // The node's interaction table, or null before the floor is built.
        public InteractableRegistry RegistryFor(int nodeId)
        {
            return _registries.TryGetValue(nodeId, out InteractableRegistry reg) ? reg : null;
        }

        public IReadOnlyList<AnchorRuntimeSnapshot> CaptureInteractionState()
        {
            foreach (InteractableRegistry registry in _registries.Values)
            {
                _interactionRuntimeStore.Capture(registry);
            }

            return _interactionRuntimeStore.ToSnapshots();
        }

        // World facts the interaction resolves against (v0: no retreat/death yet).
        public InteractionContext BuildContext()
        {
            int depth = 0;
            for (int i = 0; i < _graph.Nodes.Count; i++)
            {
                if (_graph.Nodes[i].Id == CurrentNodeId) { depth = _graph.Nodes[i].Depth; break; }
            }

            return new InteractionContext(depth, false, false, biomeId.ToString(), new[] { "regressor" });
        }

        private void EnsureInteractionController()
        {
            WorldInteractionController wic = GetComponent<WorldInteractionController>();
            if (wic == null) wic = gameObject.AddComponent<WorldInteractionController>();
            wic.Bind(this, playerTransform);
        }

        private void BuildTree(int nodeId, ForestProp t, Transform parent)
        {
            float gy = GroundY(nodeId, t.Position.x, t.Position.z);
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree";
            trunk.transform.SetParent(parent, true);
            trunk.transform.position = new Vector3(t.Position.x, gy + t.Height * 0.5f, t.Position.z);
            trunk.transform.localScale = new Vector3(t.Radius * 2f, t.Height * 0.5f, t.Radius * 2f);
            trunk.transform.rotation = Quaternion.Euler(0f, t.YawDegrees, 0f);
            Paint(trunk, RouteVisuals.ToColor(_theme.TileTintB));

            float canopyBase = gy + t.Height * 0.7f;
            for (int c = 0; c < t.CanopyCount; c++)
            {
                GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = "Canopy";
                DestroyCollider(canopy);
                canopy.transform.SetParent(trunk.transform, true);
                float cy = canopyBase + c * (t.Height * 0.18f);
                float cs = t.Radius * (5.5f - c * 0.9f);
                canopy.transform.position = new Vector3(t.Position.x, cy, t.Position.z);
                canopy.transform.localScale = new Vector3(cs, cs * 0.9f, cs);
                Paint(canopy, RouteVisuals.ToColor(_theme.TileTintA));
            }
        }

        private void BuildRock(int nodeId, ForestProp r, Transform parent)
        {
            float gy = GroundY(nodeId, r.Position.x, r.Position.z);
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(parent, true);
            rock.transform.position = new Vector3(r.Position.x, gy + r.Height * 0.35f, r.Position.z);
            rock.transform.localScale = new Vector3(r.Radius * 2f, r.Height, r.Radius * 2f);
            rock.transform.rotation = Quaternion.Euler(0f, r.YawDegrees, 0f);
            Paint(rock, RouteVisuals.ToColor(_theme.FogColor));
        }

        private void BuildPathStrip(int nodeId, IReadOnlyList<Vector3> waypoints, Transform parent)
        {
            if (waypoints == null || waypoints.Count < 2) return;
            List<Vector3> pts = new List<Vector3>(waypoints.Count);
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 w = waypoints[i];
                pts.Add(new Vector3(w.x, GroundY(nodeId, w.x, w.z) + 0.06f, w.z));
            }

            Mesh mesh = BuildRibbonMesh(pts, 2.2f);
            GameObject path = new GameObject("WindingPath");
            path.transform.SetParent(parent.parent, false); // sibling of segment content (world space verts)
            path.AddComponent<MeshFilter>().sharedMesh = mesh;
            Paint(path.AddComponent<MeshRenderer>(), new Color(0.42f, 0.34f, 0.22f));
        }

        private void BuildMarker(FloorNode node, FloorFieldRect field, Transform parent)
        {
            // v0 placeholder: a distinct post + sign cube for event/exit/boss slots.
            float gy = GroundY(node.Id, field.Center.x, field.Center.z);
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = node.IsExit ? "ExitMarker" : (node.IsBossRoom ? "BossMarker" : "EventMarker");
            post.transform.SetParent(parent, true);
            post.transform.position = new Vector3(field.Center.x, gy + 1.6f, field.Center.z);
            post.transform.localScale = new Vector3(0.4f, 3.2f, 0.4f);

            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "Sign";
            sign.transform.SetParent(post.transform, true);
            sign.transform.position = new Vector3(field.Center.x, gy + 2.9f, field.Center.z);
            sign.transform.localScale = new Vector3(2.2f, 1.2f, 0.2f);
            Color c = node.IsExit ? new Color(0.4f, 0.85f, 0.95f)
                : node.IsBossRoom ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.95f, 0.82f, 0.35f);
            Paint(post, new Color(0.3f, 0.25f, 0.18f));
            Paint(sign, c);
        }

        private void BuildForks(int fromNodeId)
        {
            IReadOnlyList<FloorForkTrail> forks = _layout.GetForks(fromNodeId);
            for (int i = 0; i < forks.Count; i++)
            {
                FloorForkTrail trail = forks[i];
                List<Vector3> pts = new List<Vector3>(trail.Waypoints.Count);
                for (int w = 0; w < trail.Waypoints.Count; w++)
                {
                    Vector3 p = trail.Waypoints[w];
                    pts.Add(new Vector3(p.x, GroundYAny(p.x, p.z) + 0.08f, p.z));
                }

                Mesh mesh = BuildRibbonMesh(pts, 2.6f);
                GameObject go = new GameObject($"ForkTrail_{fromNodeId}_r{trail.RouteId}_{trail.RouteType}");
                go.transform.SetParent(_root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                Paint(go.AddComponent<MeshRenderer>(), RouteVisuals.Tint(trail.RouteType));
                AddTrailTrigger(go.transform, pts, fromNodeId, trail.RouteId);
            }
        }

        private void AddTrailTrigger(Transform parent, List<Vector3> pts, int fromNodeId, int routeId)
        {
            Vector3 mouth = pts[0];
            Vector3 next = pts[Mathf.Min(1, pts.Count - 1)];
            Vector3 dir = next - mouth; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            GameObject vol = new GameObject("TrailTrigger");
            vol.transform.SetParent(parent, false);
            vol.transform.position = mouth + dir * 1.6f + Vector3.up * 0.9f;
            vol.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            BoxCollider box = vol.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3.2f, 2.2f, 3.4f);
            ForkTrailTrigger trigger = vol.AddComponent<ForkTrailTrigger>();
            trigger.Bind(this, fromNodeId, routeId);
        }

        private static Mesh BuildRibbonMesh(List<Vector3> center, float width)
        {
            int n = center.Count;
            Vector3[] verts = new Vector3[n * 2];
            Vector2[] uv = new Vector2[n * 2];
            int[] tris = new int[(n - 1) * 6];
            float half = width * 0.5f;
            for (int i = 0; i < n; i++)
            {
                Vector3 fwd = i < n - 1 ? center[i + 1] - center[i] : center[i] - center[i - 1];
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 side = new Vector3(-fwd.z, 0f, fwd.x);
                verts[i * 2] = center[i] - side * half;
                verts[i * 2 + 1] = center[i] + side * half;
                float v = i / (float)(n - 1);
                uv[i * 2] = new Vector2(0f, v);
                uv[i * 2 + 1] = new Vector2(1f, v);
            }

            int t = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = i * 2 + 2, d = i * 2 + 3;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }

            Mesh mesh = new Mesh { name = "Ribbon" };
            mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private float GroundY(int nodeId, float x, float z)
        {
            if (_nodeHeight.TryGetValue(nodeId, out HeightField hf) && _nodeOrigin.TryGetValue(nodeId, out Vector3 o))
            {
                return o.y + hf.Sample(x - o.x, z - o.z);
            }

            return 0f;
        }

        private float GroundYAny(float x, float z)
        {
            for (int i = 0; i < _graph.Nodes.Count; i++)
            {
                int id = _graph.Nodes[i].Id;
                if (_layout.GetField(id).ContainsXZ(x, z)) return GroundY(id, x, z);
            }

            return 0f;
        }

        private void InitTraversal()
        {
            CurrentNodeId = _graph.EntranceNodeId;
            EnsurePlayer();
            EnsureInteractionController();
            FloorFieldRect entry = _layout.GetField(CurrentNodeId);
            if (playerTransform != null)
            {
                Vector3 p = entry.EntryPoint;
                playerTransform.position = new Vector3(p.x, GroundY(CurrentNodeId, p.x, p.z) + 1f, p.z);
            }

            _exploration.MarkVisited(CurrentNodeId);
        }

        private void EnsurePlayer()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "ForestPlayer";
                player.transform.SetParent(transform, false);
                Rigidbody rb = player.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                Paint(player, new Color(0.95f, 0.9f, 0.6f));
                playerTransform = player.transform;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        // True when the collider belongs to the traversal player (physics path).
        public bool IsPlayer(Collider other)
        {
            return other != null && playerTransform != null &&
                   (other.transform == playerTransform || other.transform.IsChildOf(playerTransform));
        }

        // Called by a ForkTrailTrigger when the player walks into a fork mouth.
        public void OnTrailEntered(ForkTrailTrigger trigger)
        {
            if (_busy || trigger == null || _graph == null) return;
            if (trigger.FromNodeId != CurrentNodeId) return; // only the current node's forks are live

            TrailNavigator.ForkResolution res = TrailNavigator.ResolveByRouteId(_graph, trigger.RouteId);
            if (!res.Found) return;

            _exploration.MarkScouted(res.RouteId);
            List<Vector3> path = TrailPointsFor(res.FromNodeId, res.RouteId);
            StartCoroutine(Traverse(res, path));
        }

        private List<Vector3> TrailPointsFor(int fromNodeId, int routeId)
        {
            IReadOnlyList<FloorForkTrail> forks = _layout.GetForks(fromNodeId);
            for (int i = 0; i < forks.Count; i++)
            {
                if (forks[i].RouteId == routeId)
                {
                    return new List<Vector3>(forks[i].Waypoints);
                }
            }

            return new List<Vector3>();
        }

        private IEnumerator Traverse(TrailNavigator.ForkResolution res, List<Vector3> path)
        {
            _busy = true;
            for (int i = 0; i < path.Count; i++)
            {
                yield return WalkTo(path[i]);
            }

            FloorFieldRect dest = _layout.GetField(res.ToNodeId);
            yield return WalkTo(dest.EntryPoint);

            CurrentNodeId = res.ToNodeId;
            _exploration.MarkVisited(res.ToNodeId);
            _busy = false;

            if (res.ArrivesAtExit)
            {
                yield return WalkTo(dest.Center);
                TriggerAscend();
            }
        }

        private IEnumerator WalkTo(Vector3 target)
        {
            if (playerTransform == null) yield break;
            float groundedX = target.x, groundedZ = target.z;
            while (true)
            {
                Vector3 pos = playerTransform.position;
                float gy = GroundYAny(pos.x, pos.z) + 1f;
                Vector3 goal = new Vector3(groundedX, GroundYAny(groundedX, groundedZ) + 1f, groundedZ);
                Vector3 flat = new Vector3(goal.x - pos.x, 0f, goal.z - pos.z);
                if (flat.magnitude <= 0.15f)
                {
                    playerTransform.position = new Vector3(goal.x, goal.y, goal.z);
                    yield break;
                }

                Vector3 step = Vector3.MoveTowards(new Vector3(pos.x, gy, pos.z), goal, walkSpeed * Time.deltaTime);
                playerTransform.position = step;
                yield return null;
            }
        }

        private void TriggerAscend()
        {
            if (ascend == null) ascend = GetComponent<AscendController>() ?? gameObject.AddComponent<AscendController>();
            Transform party = playerTransform != null ? playerTransform : transform;
            ascend.Play(party, cameraTransform);
        }

        private readonly Dictionary<Color, Material> _matCache = new Dictionary<Color, Material>();

        private void Paint(GameObject go, Color color)
        {
            Paint(go.GetComponent<MeshRenderer>(), color);
        }

        private void Paint(MeshRenderer mr, Color color)
        {
            if (mr == null) return;
            if (!_matCache.TryGetValue(color, out Material mat))
            {
                mat = MakeMaterial(color);
                _matCache[color] = mat;
            }

            mr.sharedMaterial = mat;
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader) { name = $"Forest_{color}" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        private static void DestroyCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
        }

        private Material ResolveTerrainMaterial()
        {
            if (terrainMaterial != null) return terrainMaterial;
            Material res = Resources.Load<Material>("TowerRuntimeLit");
            return res != null ? res : MakeMaterial(RouteVisuals.ToColor(_theme.TileTintA));
        }

        private void ApplyBiomeAtmosphere()
        {
            RenderSettings.ambientLight = RouteVisuals.ToColor(_theme.AmbientColor);
            RenderSettings.fog = _theme.FogDensity > 0f;
            RenderSettings.fogColor = RouteVisuals.ToColor(_theme.FogColor);
            RenderSettings.fogDensity = _theme.FogDensity;
        }

        private void ClearGeneratedRoot()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing == null) return;
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }
}
