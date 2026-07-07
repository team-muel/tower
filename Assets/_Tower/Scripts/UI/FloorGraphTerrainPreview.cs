using Tower.Gen;
using UnityEngine;

namespace Tower.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FloorGraphTerrainPreview : MonoBehaviour
    {
        public const string GeneratedRootName = "_GeneratedFloorGraphTerrain";

        [SerializeField] private int seed = 777;
        [SerializeField] private BiomeId biomeId = BiomeId.Forest;
        [SerializeField, Range(3, 5)] private int nodeCount = 3;
        [SerializeField] private bool isBossFloor;
        [SerializeField] private bool includeCamp;
        [SerializeField, Range(8, 96)] private int meshResolution = 32;
        [SerializeField, Min(4f)] private float segmentSize = 24f;
        [SerializeField, Min(0f)] private float segmentGap = 7f;
        [SerializeField] private Vector2 uvScale = new Vector2(4f, 4f);
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private bool rebuildOnEnable = true;

        public FloorGraph LastGraph { get; private set; }

        private void OnEnable()
        {
            if (rebuildOnEnable)
            {
                Rebuild();
            }
        }

        private void OnValidate()
        {
            nodeCount = Mathf.Clamp(nodeCount, 3, 5);
            meshResolution = Mathf.Max(2, meshResolution);
            segmentSize = Mathf.Max(4f, segmentSize);
            segmentGap = Mathf.Max(0f, segmentGap);
        }

        [ContextMenu("Rebuild Floor Graph Terrain")]
        public void Rebuild()
        {
            ClearGeneratedRoot();

            FloorGenParams parameters = new FloorGenParams(
                seed,
                new IntRange(nodeCount, nodeCount),
                isBossFloor,
                new IntRange(8, 14),
                new[] { "melee", "ranged", "elite" },
                "boss",
                includeCamp,
                biomeId);

            LastGraph = FloorGenerator.Generate(parameters);
            BiomeDef biome = BiomeDef.For(biomeId);
            Material material = ResolveMaterial();
            FloorTerrainMeshBuilder meshBuilder = new FloorTerrainMeshBuilder();

            GameObject root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, false);

            for (int i = 0; i < LastGraph.Nodes.Count; i++)
            {
                FloorNode node = LastGraph.Nodes[i];
                BuildNodeTerrain(meshBuilder, biome, material, root.transform, node);
            }
        }

        private void BuildNodeTerrain(
            FloorTerrainMeshBuilder meshBuilder,
            BiomeDef biome,
            Material material,
            Transform root,
            FloorNode node)
        {
            Mesh mesh = meshBuilder.Build(
                HeightFieldFactory.ForNode(LastGraph.Seed, node, biome),
                meshResolution,
                segmentSize,
                segmentSize,
                uvScale);
            mesh.name = $"FloorTerrain_Node{node.Id}_{node.Kind}";

            GameObject segment = new GameObject($"Node_{node.Id:00}_{node.Kind}");
            segment.transform.SetParent(root, false);
            segment.transform.localPosition = new Vector3(
                -segmentSize * 0.5f,
                0f,
                node.Depth * (segmentSize + segmentGap));

            MeshFilter meshFilter = segment.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = segment.AddComponent<MeshRenderer>();
            if (material != null)
            {
                meshRenderer.sharedMaterial = material;
            }

            MeshCollider meshCollider = segment.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
        }

        private Material ResolveMaterial()
        {
            if (terrainMaterial != null)
            {
                return terrainMaterial;
            }

            return Resources.Load<Material>("TowerRuntimeLit");
        }

        private void ClearGeneratedRoot()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }
    }
}
