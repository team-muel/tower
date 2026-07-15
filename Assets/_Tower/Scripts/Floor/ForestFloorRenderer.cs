using System;
using System.Collections;
using System.Collections.Generic;
using Tower.Combat;
using Tower.Core;
using Tower.Gen;
using UnityEngine;

namespace Tower.Floor
{
    [Serializable]
    public struct AnchorPrefabEntry
    {
        public InteractableKind Kind;
        public GameObject Prefab;
    }

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

        [Header("Run event selection")]
        [SerializeField] private int runSeed = 777;
        [SerializeField, Range(0, RunEventPlan.FloorCount)] private int runFloorNumber;

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

        [Header("Prefabs (optional - falls back to primitives when empty)")]
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private GameObject[] rockPrefabs;
        [SerializeField] private GameObject[] bushPrefabs;
        [SerializeField] private AnchorPrefabEntry[] anchorPrefabs;
        [SerializeField] private GameObject exitMarkerPrefab;
        [SerializeField] private GameObject bossMarkerPrefab;
        [SerializeField] private GameObject eventMarkerPrefab;
        [SerializeField] private GameObject playerPrefab;

        [Header("Generated encounter entry")]
        [SerializeField] private CharacterDef playerDefinition;
        [SerializeField] private CompanionVisualProfile[] companionProfiles;
        [SerializeField] private EnemyCombatProfile[] enemyCombatProfiles;
        [SerializeField] private EncounterRewardProfile encounterRewardProfile;
        [SerializeField, Min(0.01f)] private float encounterTriggerRadius = 7f;
        [SerializeField, Min(0.01f)] private float encounterIntroHoldSeconds = 0.45f;
        [SerializeField, Min(0.1f)] private float encounterResultSeconds = 3f;

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
        private FloorGenParams _generationParameters;
        private RunLifecycle _run;
        private SaveRepository _runSaveRepository;
        private bool _resetRunInteractionsOnRebuild;
        private MetaProgress _meta;
        private MetaProgressRepository _metaRepository;
        private RunEventSlot _scheduledRunEvent;
        private FloorNodeContent _scheduledNodeContent;
        private int _encounterNodeId = -1;
        private GeneratedFloorEncounterHost _activeEncounter;
        private ForestPlayerController _playerMovement;
        private CompanionPartySpawner _partySpawner;
        private IReadOnlyList<CompanionEntity> _companions = Array.Empty<CompanionEntity>();
        private EncounterResultPresenter _resultPresenter;

        public FloorGraph Graph => _graph;
        public FloorExploration Exploration => _exploration;
        public int CurrentNodeId { get; private set; }
        public RunEventSlot ScheduledRunEvent => _scheduledRunEvent;
        public int EncounterNodeId => _encounterNodeId;
        public bool IsEncounterBlocking => _activeEncounter != null && !_activeEncounter.IsResolved;
        public Transform CameraTransform => cameraTransform;
        public Transform PlayerTransform => playerTransform;
        public GeneratedFloorEncounterHost ActiveEncounter => _activeEncounter;
        public RunLifecycle RunLifecycle => _run;
        public MetaProgress MetaProgress => _meta;
        public IFloorLayoutSource Layout => _layout;
        public RunEventProgress RunEventProgress => _run?.Progress;
        public RunRewardInventory RewardInventory => _run?.Rewards;
        public EncounterResultPresenter ResultPresenter => _resultPresenter;

        // Allows the orchestrator's Core->interface adapter to inject a real layout.
        public void SetLayoutSource(IFloorLayoutSource source)
        {
            _layout = source;
            _layoutInjected = source != null;
        }

        private bool _layoutInjected;

        private void Start()
        {
            if (buildOnStart) Rebuild();
        }

        private void Update()
        {
            TryEnterNearestForkAtExit();
        }

        [ContextMenu("Rebuild Forest Floor")]
        public void Rebuild()
        {
            if (_resetRunInteractionsOnRebuild)
            {
                // Retreat/regression: run-scoped anchor state resets with the run.
                _resetRunInteractionsOnRebuild = false;
                _interactionRuntimeStore.Clear();
            }
            else
            {
                CaptureInteractionState();
            }

            ClearGeneratedRoot();
            _nodeHeight.Clear();
            _nodeOrigin.Clear();
            _registries.Clear();

            EnsureRunLifecycle();
            int effectiveFloor = runFloorNumber == 0
                ? FastForwardToNextEventFloor()
                : runFloorNumber;
            _scheduledRunEvent = null;
            IReadOnlyList<RunEventSlot> slots = _run.Progress.Plan.Slots;
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index].FloorNumber == effectiveFloor
                    && index >= _run.Progress.CompletedCount)
                {
                    _scheduledRunEvent = slots[index];
                    break;
                }
            }

            _scheduledNodeContent = null;
            _encounterNodeId = -1;
            _activeEncounter = null;

            bool effectiveBossFloor = isBossFloor
                || (_scheduledRunEvent != null && _scheduledRunEvent.Kind == RunEventKind.Boss);
            // T59: every floor derives its own deterministic terrain seed and
            // layout stretch so the ten floors stop being clones of one map.
            int floorSeed = FloorSeeds.TerrainSeed(seed, effectiveFloor);
            _generationParameters = new FloorGenParams(
                floorSeed,
                new IntRange(nodeCount, nodeCount),
                effectiveBossFloor,
                new IntRange(8, 14),
                new[] { "melee", "ranged", "elite" },
                "boss",
                includeCamp,
                biomeId);

            _graph = FloorGenerator.Generate(_generationParameters);
            _biomeDef = BiomeDef.For(biomeId);
            _theme = _graph.BiomeTheme;
            if (!_layoutInjected)
            {
                float stretch = FloorSeeds.TravelStretch(seed, effectiveFloor);
                _layout = new LinearStubLayout(_graph, travelLength * stretch, crossWidth, gap, forkBow);
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

            SelectScheduledEncounterNode();

            for (int i = 0; i < _graph.Nodes.Count; i++)
            {
                BuildForks(_graph.Nodes[i].Id);
            }

            InitTraversal();
        }

        private void SelectScheduledEncounterNode()
        {
            if (_scheduledRunEvent == null)
            {
                return;
            }

            for (int index = 0; index < _graph.Nodes.Count; index++)
            {
                FloorNode node = _graph.Nodes[index];
                FloorNodeContent content = FloorNodeBinder.Bind(_graph, node, _generationParameters);
                if (!content.Encounter.HasEncounter)
                {
                    continue;
                }

                bool needsBoss = _scheduledRunEvent.Kind == RunEventKind.Boss;
                if (content.Encounter.IsBoss != needsBoss)
                {
                    continue;
                }

                _encounterNodeId = node.Id;
                _scheduledNodeContent = content;
                Debug.Log(
                    $"[FloorRun] event={_scheduledRunEvent.EventId} floor={_scheduledRunEvent.FloorNumber} "
                    + $"node={_encounterNodeId} kind={_scheduledRunEvent.Kind}.",
                    this);
                return;
            }

            Debug.LogError(
                $"No generated encounter node matched run event {_scheduledRunEvent.EventId}.",
                this);
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
            for (int i = 0; i < plan.Trees.Count; i++) BuildTree(node.Id, i, plan.Trees[i], trees);

            Transform rocks = new GameObject("Rocks").transform;
            rocks.SetParent(parent, false);
            for (int i = 0; i < plan.Rocks.Count; i++) BuildRock(node.Id, i, plan.Rocks[i], rocks);

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
            GameObject prefab = ResolveAnchorPrefab(a.Kind);
            if (prefab != null)
            {
                float prefabGy = GroundY(nodeId, a.Position.x, a.Position.z);
                GameObject prefabAnchor = Instantiate(prefab, parent);
                prefabAnchor.name = $"Anchor_{a.Def.Id}";
                if (TryGroundPrefabInstance(prefabAnchor, a.Position.x, prefabGy, a.Position.z))
                {
                    AnchorMarker prefabMarker = prefabAnchor.GetComponent<AnchorMarker>() ??
                                                prefabAnchor.AddComponent<AnchorMarker>();
                    prefabMarker.Bind(nodeId, a.Def.Id, a.Kind, color);
                    return;
                }

                DestroyGenerated(prefabAnchor);
            }

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

        private void BuildTree(int nodeId, int slot, ForestProp t, Transform parent)
        {
            if (TryBuildPropPrefab(treePrefabs, nodeId, slot, t, "Tree", parent)) return;

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

        private void BuildRock(int nodeId, int slot, ForestProp r, Transform parent)
        {
            if (TryBuildPropPrefab(rockPrefabs, nodeId, slot, r, "Rock", parent)) return;

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
            GameObject markerPrefab = ResolveMarkerPrefab(node);
            if (markerPrefab != null)
            {
                float prefabGy = GroundY(node.Id, field.Center.x, field.Center.z);
                GameObject marker = Instantiate(markerPrefab, parent);
                marker.name = node.IsExit ? "ExitMarker" : (node.IsBossRoom ? "BossMarker" : "EventMarker");
                if (TryGroundPrefabInstance(marker, field.Center.x, prefabGy, field.Center.z)) return;
                DestroyGenerated(marker);
            }

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
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
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
            EnsureParty();
            EnsureInteractionController();
            FloorFieldRect entry = _layout.GetField(CurrentNodeId);
            if (playerTransform != null)
            {
                Vector3 p = entry.EntryPoint;
                playerTransform.position = new Vector3(p.x, GroundY(CurrentNodeId, p.x, p.z) + 1f, p.z);
            }

            _exploration.MarkVisited(CurrentNodeId);
            EnsureHud();
            EnsureSlowMo();
            EnsureCameraReadability();
            TryStartCurrentNodeEncounter();
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            if (QaCommandLine.HasAutoEncounterFlag(commandLineArgs))
            {
                QaEnterScheduledEncounter();
            }

            if (QaCommandLine.HasAutoRunFlag(commandLineArgs))
            {
                QaAutoRunDriver autoRun = gameObject.GetComponent<QaAutoRunDriver>();
                if (autoRun == null)
                {
                    autoRun = gameObject.AddComponent<QaAutoRunDriver>();
                }

                autoRun.Configure(this);
            }
        }

        private PlayRunHud _hud;
        private RunSlowMo _slowMo;

        // T64: Left Shift bullet-time (CombatSpike stack) in the run loop.
        private void EnsureSlowMo()
        {
            if (!Application.isPlaying || _slowMo != null)
            {
                return;
            }

            _slowMo = gameObject.GetComponent<RunSlowMo>();
            if (_slowMo == null)
            {
                _slowMo = gameObject.AddComponent<RunSlowMo>();
            }
        }

        // T60: run/combat HUD reads live Core state through the composer.
        private void EnsureHud()
        {
            if (_hud != null)
            {
                return;
            }

            _hud = gameObject.GetComponent<PlayRunHud>();
            if (_hud == null)
            {
                _hud = gameObject.AddComponent<PlayRunHud>();
            }

            _hud.Configure(() => PlayRunHudComposer.Compose(
                _run,
                HudPlayerCombatant(),
                _meta,
                _slowMo == null ? -1f : _slowMo.Charge));
        }

        private CombatantRef HudPlayerCombatant()
        {
            if (_activeEncounter == null || _activeEncounter.IsResolved
                || _activeEncounter.CombatState == null
                || string.IsNullOrEmpty(_activeEncounter.PlayerUnitId))
            {
                return null;
            }

            return _activeEncounter.CombatState.GetCombatant(_activeEncounter.PlayerUnitId);
        }

        // T60 readability: the runtime fallback camera fades occluders
        // between itself and the player (reuses the T39 controller).
        private void EnsureCameraReadability()
        {
            if (cameraTransform == null || playerTransform == null)
            {
                return;
            }

            Camera followCamera = cameraTransform.GetComponent<Camera>();
            if (followCamera == null)
            {
                return;
            }

            CameraOcclusionFadeController fade =
                cameraTransform.GetComponent<CameraOcclusionFadeController>();
            if (fade == null)
            {
                fade = cameraTransform.gameObject.AddComponent<CameraOcclusionFadeController>();
            }

            fade.SetCamera(followCamera);
            fade.SetTarget(playerTransform);
        }

        public bool QaEnterScheduledEncounter()
        {
            if (_layout == null || _encounterNodeId < 0 || playerTransform == null)
            {
                return false;
            }

            CurrentNodeId = _encounterNodeId;
            _exploration.MarkVisited(CurrentNodeId);
            FloorFieldRect field = _layout.GetField(CurrentNodeId);
            Vector3 center = field.Center;
            playerTransform.position = new Vector3(
                center.x,
                GroundY(CurrentNodeId, center.x, center.z) + 1f,
                center.z - Mathf.Min(3f, field.TravelLength * 0.25f));
            for (int index = 0; index < _companions.Count; index++)
            {
                float lateral = (index - ((_companions.Count - 1) * 0.5f)) * 1.4f;
                Vector3 companionPosition = new Vector3(
                    center.x + lateral,
                    0f,
                    playerTransform.position.z - 1.5f);
                companionPosition.y = GroundY(
                    CurrentNodeId,
                    companionPosition.x,
                    companionPosition.z) + 1f;
                _companions[index].transform.position = companionPosition;
            }
            Debug.Log($"[FloorQA] Auto-entered encounter node={CurrentNodeId}.", this);
            TryStartCurrentNodeEncounter();
            return _activeEncounter != null;
        }

        private void EnsurePlayer()
        {
            if (playerTransform == null)
            {
                GameObject player = playerPrefab != null
                    ? Instantiate(playerPrefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "ForestPlayer";
                if (playerPrefab == null)
                {
                    player.transform.SetParent(transform, false);
                }

                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb == null) rb = player.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                if (playerPrefab == null)
                {
                    Paint(player, new Color(0.95f, 0.9f, 0.6f));
                }

                playerTransform = player.transform;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform == null)
            {
                GameObject cameraObject = new GameObject("GeneratedFloorCamera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(transform, false);
                Camera generatedCamera = cameraObject.AddComponent<Camera>();
                generatedCamera.clearFlags = CameraClearFlags.Skybox;
                cameraObject.AddComponent<AudioListener>();
                // T63: the owner-tuned Prototype orbit camera is the canon
                // (2026-07-12 values baked into IsoCameraFollow defaults).
                IsoCameraFollow follow = cameraObject.AddComponent<IsoCameraFollow>();
                follow.target = playerTransform;
                cameraTransform = cameraObject.transform;
            }

            _playerMovement = playerTransform.GetComponent<ForestPlayerController>();
            if (_playerMovement == null)
            {
                _playerMovement = playerTransform.gameObject.AddComponent<ForestPlayerController>();
            }

            _playerMovement.Configure(cameraTransform);
            EnsurePlayerVisual();
        }

        private void EnsurePlayerVisual()
        {
            if (playerTransform == null || playerPrefab == null
                || playerTransform.Find("SharedPlayerVisual") != null)
            {
                return;
            }

            foreach (Renderer renderer in playerTransform.GetComponents<Renderer>())
            {
                renderer.enabled = false;
            }

            GameObject visual = Instantiate(playerPrefab, playerTransform);
            visual.name = "SharedPlayerVisual";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void EnsureParty()
        {
            if (_companions != null && _companions.Count > 0)
            {
                return;
            }

            if (playerTransform == null || companionProfiles == null || companionProfiles.Length == 0)
            {
                return;
            }

            _partySpawner = GetComponent<CompanionPartySpawner>();
            if (_partySpawner == null)
            {
                _partySpawner = gameObject.AddComponent<CompanionPartySpawner>();
            }

            _partySpawner.Configure(playerTransform, companionProfiles, Array.Empty<Transform>());
            Result<IReadOnlyList<CompanionEntity>> spawned = _partySpawner.SpawnNow();
            if (spawned.IsFailure)
            {
                Debug.LogError(spawned.Error, this);
                return;
            }

            _companions = spawned.Value;
        }

        // True when the collider belongs to the traversal player (physics path).
        public bool IsPlayer(Collider other)
        {
            return other != null && playerTransform != null &&
                   (other.transform == playerTransform || other.transform.IsChildOf(playerTransform));
        }

        // Called by a ForkTrailTrigger when the player walks into a fork mouth.
        public bool OnTrailEntered(ForkTrailTrigger trigger)
        {
            if (_busy || IsEncounterBlocking || trigger == null || _graph == null) return false;
            if (trigger.FromNodeId != CurrentNodeId) return false; // only the current node's forks are live

            return BeginTraverse(trigger.RouteId);
        }

        // Transform-driven kinematic players do not produce trigger callbacks
        // consistently on every standalone backend. The node-exit proximity is
        // the canonical spatial fallback and selects the fork nearest player X.
        public bool TryEnterNearestForkAtExit()
        {
            if (_busy || IsEncounterBlocking || playerTransform == null || _graph == null || _layout == null)
            {
                return false;
            }

            FloorFieldRect field = _layout.GetField(CurrentNodeId);
            Vector3 player = playerTransform.position;
            Vector3 exit = field.ExitPoint;
            player.y = 0f;
            exit.y = 0f;
            if (Vector3.Distance(player, exit) > 1.5f)
            {
                return false;
            }

            IReadOnlyList<FloorForkTrail> forks = _layout.GetForks(CurrentNodeId);
            if (forks.Count == 0)
            {
                return false;
            }

            FloorForkTrail closest = forks[0];
            float closestDistance = ForkLateralDistance(closest, player.x);
            for (int index = 1; index < forks.Count; index++)
            {
                float candidateDistance = ForkLateralDistance(forks[index], player.x);
                if (candidateDistance < closestDistance)
                {
                    closest = forks[index];
                    closestDistance = candidateDistance;
                }
            }

            return BeginTraverse(closest.RouteId);
        }

        private bool BeginTraverse(int routeId)
        {
            if (_busy || IsEncounterBlocking || _graph == null)
            {
                return false;
            }

            TrailNavigator.ForkResolution res = TrailNavigator.ResolveByRouteId(_graph, routeId);
            if (!res.Found) return false;

            _exploration.MarkScouted(res.RouteId);
            List<Vector3> path = TrailPointsFor(res.FromNodeId, res.RouteId);
            StartCoroutine(Traverse(res, path));
            return true;
        }

        private static float ForkLateralDistance(FloorForkTrail fork, float playerX)
        {
            int sample = Mathf.Min(1, fork.Waypoints.Count - 1);
            return Mathf.Abs(fork.Waypoints[sample].x - playerX);
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
            if (_playerMovement != null) _playerMovement.enabled = false;
            for (int i = 0; i < path.Count; i++)
            {
                yield return WalkTo(path[i]);
            }

            FloorFieldRect dest = _layout.GetField(res.ToNodeId);
            yield return WalkTo(dest.EntryPoint);

            CurrentNodeId = res.ToNodeId;
            _exploration.MarkVisited(res.ToNodeId);
            _busy = false;
            if (_playerMovement != null) _playerMovement.enabled = true;
            Debug.Log($"[FloorTraversal] Arrived node={res.ToNodeId} route={res.RouteId}.", this);
            TryStartCurrentNodeEncounter();

            if (res.ArrivesAtExit && !IsEncounterBlocking)
            {
                yield return WalkTo(dest.Center);
                TriggerAscend();
            }
        }

        private void TryStartCurrentNodeEncounter()
        {
            if (_activeEncounter != null || _scheduledNodeContent == null
                || CurrentNodeId != _encounterNodeId || playerTransform == null)
            {
                return;
            }

            if (encounterRewardProfile == null)
            {
                Debug.LogError("Generated encounter requires an encounter reward profile.", this);
                return;
            }

            Result rewardProfileValid = encounterRewardProfile.Validate();
            if (rewardProfileValid.IsFailure)
            {
                Debug.LogError(rewardProfileValid.Error, this);
                return;
            }

            FloorFieldRect field = _layout.GetField(CurrentNodeId);
            Vector3 center = field.Center;
            Vector3 spawnCenter = new Vector3(center.x, GroundY(CurrentNodeId, center.x, center.z), center.z);
            GameObject host = new GameObject($"GeneratedEncounter_{_scheduledRunEvent.EventId}");
            host.transform.SetParent(_root, false);
            _activeEncounter = host.AddComponent<GeneratedFloorEncounterHost>();
            EnsureMeta();
            _activeEncounter.SetPlayerSlotOverride(_meta.SlotCountFor(playerDefinition));
            Result configured = _activeEncounter.Configure(
                playerTransform,
                _playerMovement,
                playerDefinition,
                _companions,
                enemyCombatProfiles,
                _scheduledNodeContent.Encounter,
                _scheduledRunEvent,
                spawnCenter,
                OnEncounterResolved,
                encounterTriggerRadius,
                encounterIntroHoldSeconds);
            if (configured.IsFailure)
            {
                Debug.LogError(configured.Error, this);
                DestroyGenerated(host);
                _activeEncounter = null;
                return;
            }

            _activeEncounter.SetDefeatedHandler(OnEncounterDefeated);
        }

        private void OnEncounterResolved(GeneratedEncounterResult combatResult)
        {
            if (combatResult == null)
            {
                Debug.LogError("Generated encounter returned no combat result.", this);
                return;
            }

            EnsureRunLifecycle();
            Result<EncounterReward> reward = encounterRewardProfile.CreateReward(_scheduledRunEvent);
            if (reward.IsFailure)
            {
                Debug.LogError(reward.Error, this);
                return;
            }

            Result<bool> granted = _run.ResolveEvent(combatResult.EventId, reward.Value);
            if (granted.IsFailure)
            {
                Debug.LogError(granted.Error, this);
                return;
            }

            if (_resultPresenter == null)
            {
                _resultPresenter = gameObject.AddComponent<EncounterResultPresenter>();
            }

            Result presented = _resultPresenter.Present(
                combatResult,
                reward.Value,
                _run.Progress.CompletedCount,
                _run.Progress.Plan.Slots.Count,
                encounterResultSeconds);
            if (presented.IsFailure)
            {
                Debug.LogError(presented.Error, this);
                return;
            }

            Debug.Log(
                $"[EncounterOutcome] event={combatResult.EventId} "
                + $"progress={_run.Progress.CompletedCount}/{_run.Progress.Plan.Slots.Count} "
                + $"reward={reward.Value.Type}+{reward.Value.Amount} newlyGranted={granted.Value} "
                + $"actions={combatResult.ActionCount} duration={combatResult.DurationSeconds:0.0}s.",
                this);
            SaveRun();
        }

        private void OnEncounterDefeated(string defeatedEventId)
        {
            EnsureRunLifecycle();
            Result<RunOutcome> retreat = _run.Retreat();
            if (retreat.IsFailure)
            {
                Debug.LogError(retreat.Error, this);
                return;
            }

            _resetRunInteractionsOnRebuild = true;
            SaveRun();
            Debug.Log(
                $"[RunLifecycle] {retreat.Value} after defeat event={defeatedEventId} "
                + $"retreats={_run.RetreatCount}; returning to floor=1.",
                this);
            if (_playerMovement != null)
            {
                _playerMovement.enabled = true;
            }

            runFloorNumber = 0;
            Rebuild();
        }

        // Advances the run one floor (Ascend completion path). Public so QA
        // and tests can drive the lifecycle without the ascend animation.
        public Result<RunOutcome> AdvanceRunFloor()
        {
            EnsureRunLifecycle();
            Result<RunOutcome> advanced = _run.AdvanceFloor();
            if (advanced.IsFailure)
            {
                Debug.LogError(advanced.Error, this);
                return advanced;
            }

            SaveRun();
            if (advanced.Value == RunOutcome.Conquered)
            {
                Debug.Log(
                    $"[RunLifecycle] Conquered stair-step; {_run.Rewards.ClaimCount} reward claims held.",
                    this);
                EnsureMeta();
                Result<int> paid = _meta.RecordConquest(0);
                if (paid.IsSuccess)
                {
                    SaveMeta();
                    Debug.Log(
                        $"[MetaProgress] Conquest recorded; platinum={paid.Value} "
                        + $"conquests={_meta.ConquestCount} shortcut=stairway-0.",
                        this);
                }

                return advanced;
            }

            Debug.Log($"[RunLifecycle] Advancing to floor={_run.FloorNumber}.", this);
            runFloorNumber = 0;
            Rebuild();
            return advanced;
        }

        private void EnsureRunLifecycle()
        {
            if (_run != null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                _runSaveRepository = CreateRunSaveRepository();
                string[] args = Environment.GetCommandLineArgs();
                if (QaCommandLine.HasFreshRunFlag(args))
                {
                    _runSaveRepository.Delete();
                }
                else if (_runSaveRepository.HasSave)
                {
                    Result<SaveGame> loaded = _runSaveRepository.Load();
                    Result<RunLifecycle> restored = loaded.IsSuccess
                        ? RunLifecycle.Restore(loaded.Value.runLifecycle)
                        : Result<RunLifecycle>.Failure(loaded.Error);
                    if (restored.IsSuccess)
                    {
                        _run = restored.Value;
                        Debug.Log(
                            $"[RunLifecycle] Resumed floor={_run.FloorNumber} "
                            + $"progress={_run.Progress.CompletedCount}/{_run.Progress.Plan.Slots.Count} "
                            + $"retreats={_run.RetreatCount} conquered={_run.IsConquered}.",
                            this);
                        return;
                    }

                    Debug.LogWarning($"[RunLifecycle] Save ignored: {restored.Error}", this);
                }
            }

            _run = RunLifecycle.CreateNew(runSeed);
            Debug.Log(
                $"[RunLifecycle] New run seed={runSeed} events={_run.Progress.Plan.Slots.Count}.",
                this);
        }

        // v0: floors without a scheduled event have no authored content yet
        // (T59 owns non-event floor content), so the default flow advances
        // straight to the next pending event floor.
        private int FastForwardToNextEventFloor()
        {
            while (!_run.IsConquered && _run.NextPendingEvent != null
                && _run.FloorNumber < _run.NextPendingEvent.FloorNumber)
            {
                Result<RunOutcome> advanced = _run.AdvanceFloor();
                if (advanced.IsFailure)
                {
                    Debug.LogError(advanced.Error, this);
                    break;
                }
            }

            return _run.FloorNumber;
        }

        private SaveRepository CreateRunSaveRepository()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "run-lifecycle.json");
            return SaveRepository.Create(path).Value;
        }

        // T61: platinum/unlocks live apart from the run save and pierce
        // retreat, the great regression, and -qaFreshRun.
        private void EnsureMeta()
        {
            if (_meta != null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, "meta-progress.json");
                _metaRepository = MetaProgressRepository.Create(path).Value;
                if (_metaRepository.HasSave)
                {
                    Result<MetaProgressSnapshot> loaded = _metaRepository.Load();
                    Result<MetaProgress> restored = loaded.IsSuccess
                        ? MetaProgress.Restore(loaded.Value)
                        : Result<MetaProgress>.Failure(loaded.Error);
                    if (restored.IsSuccess)
                    {
                        _meta = restored.Value;
                        Debug.Log(
                            $"[MetaProgress] Loaded platinum={_meta.Platinum} "
                            + $"conquests={_meta.ConquestCount}.",
                            this);
                        return;
                    }

                    Debug.LogWarning($"[MetaProgress] Meta save ignored: {restored.Error}", this);
                }
            }

            _meta = MetaProgress.Restore(null).Value;
        }

        private void SaveMeta()
        {
            if (!Application.isPlaying || _meta == null || _metaRepository == null)
            {
                return;
            }

            Result written = _metaRepository.Save(_meta.Capture());
            if (written.IsFailure)
            {
                Debug.LogError(written.Error, this);
            }
        }

        private void SaveRun()
        {
            if (!Application.isPlaying || _run == null)
            {
                return;
            }

            if (_runSaveRepository == null)
            {
                _runSaveRepository = CreateRunSaveRepository();
            }

            var save = new SaveGame { runLifecycle = _run.Capture() };
            Result written = _runSaveRepository.Save(save);
            if (written.IsFailure)
            {
                Debug.LogError(written.Error, this);
                return;
            }

            Debug.Log(
                $"[RunLifecycle] Saved floor={_run.FloorNumber} "
                + $"progress={_run.Progress.CompletedCount}/{_run.Progress.Plan.Slots.Count}.",
                this);
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
            ascend.Play(party, cameraTransform, () => AdvanceRunFloor());
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

        private bool TryBuildPropPrefab(GameObject[] prefabs, int nodeId, int slot, ForestProp prop,
                                        string objectName, Transform parent)
        {
            int index = PropPrefabSelector.PickIndex(_graph.Seed, nodeId, slot, prefabs != null ? prefabs.Length : 0);
            if (index < 0) return false;

            GameObject prefab = prefabs[index];
            if (prefab == null) return false;

            GameObject go = Instantiate(prefab, parent);
            go.name = objectName;
            go.transform.rotation = Quaternion.Euler(0f, prop.YawDegrees, 0f);
            if (!TryEncapsulateRenderers(go, out Bounds bounds))
            {
                DestroyGenerated(go);
                return false;
            }

            float scale = bounds.size.y > 0.0001f ? prop.Height / bounds.size.y : 1f;
            go.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.3f, 2.5f);
            if (!TryEncapsulateRenderers(go, out bounds))
            {
                DestroyGenerated(go);
                return false;
            }

            float gy = GroundY(nodeId, prop.Position.x, prop.Position.z);
            go.transform.position += new Vector3(
                prop.Position.x - go.transform.position.x,
                gy - bounds.min.y,
                prop.Position.z - go.transform.position.z);
            return true;
        }

        private GameObject ResolveAnchorPrefab(InteractableKind kind)
        {
            if (anchorPrefabs == null) return null;
            for (int i = 0; i < anchorPrefabs.Length; i++)
            {
                if (anchorPrefabs[i].Kind == kind && anchorPrefabs[i].Prefab != null)
                {
                    return anchorPrefabs[i].Prefab;
                }
            }

            return null;
        }

        private GameObject ResolveMarkerPrefab(FloorNode node)
        {
            if (node.IsExit) return exitMarkerPrefab;
            if (node.IsBossRoom) return bossMarkerPrefab;
            if (TrailNavigator.IsEventNode(node)) return eventMarkerPrefab;
            return null;
        }

        private static bool TryGroundPrefabInstance(GameObject go, float x, float groundY, float z)
        {
            if (!TryEncapsulateRenderers(go, out Bounds bounds))
            {
                return false;
            }

            go.transform.position += new Vector3(
                x - go.transform.position.x,
                groundY - bounds.min.y,
                z - go.transform.position.z);
            return true;
        }

        private static bool TryEncapsulateRenderers(GameObject go, out Bounds bounds)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled) continue;
                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return hasBounds;
        }

        private static void DestroyGenerated(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
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
