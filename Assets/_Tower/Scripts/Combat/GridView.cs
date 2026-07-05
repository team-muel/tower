using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public sealed class GridView : MonoBehaviour
    {
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private Material _tileMaterial;
        [SerializeField] private Color _baseColor = new Color(0.18f, 0.2f, 0.22f, 1f);
        [SerializeField] private Color _blockedColor = new Color(0.08f, 0.08f, 0.08f, 1f);

        private readonly Dictionary<GridPos, Renderer> _tiles = new Dictionary<GridPos, Renderer>();

        public GridMap Map { get; private set; }

        public float TileSize
        {
            get { return _tileSize; }
        }

        public void Build(GridMap map)
        {
            Map = map;
            ClearTiles();

            if (Map == null)
            {
                return;
            }

            Material material = GetOrCreateTileMaterial();
            foreach (GridPos pos in Map.Positions)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.name = string.Format("Tile {0},{1}", pos.X, pos.Y);
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = CellToLocal(pos);
                tile.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                tile.transform.localScale = Vector3.one * _tileSize * 0.95f;

                Renderer renderer = tile.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                _tiles[pos] = renderer;
                SetTileColor(pos, Map.IsPassable(pos) ? _baseColor : _blockedColor);
            }
        }

        public Vector3 CellToWorld(GridPos pos)
        {
            return transform.TransformPoint(CellToLocal(pos));
        }

        public GridPos WorldToCell(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            int x = Mathf.RoundToInt(local.x / _tileSize);
            int y = Mathf.RoundToInt(local.z / _tileSize);
            return new GridPos(x, y);
        }

        public bool TryGetTile(GridPos pos, out Renderer renderer)
        {
            return _tiles.TryGetValue(pos, out renderer);
        }

        public bool TrySetBlocked(GridPos pos, bool blocked)
        {
            if (Map == null || !Map.InBounds(pos))
            {
                return false;
            }

            Map.SetBlocked(pos, blocked);
            SetTileColor(pos, blocked ? _blockedColor : _baseColor);
            return true;
        }

        public void SetTileColor(GridPos pos, Color color)
        {
            Renderer renderer;
            if (!_tiles.TryGetValue(pos, out renderer))
            {
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        public void ResetTileColor(GridPos pos)
        {
            if (Map == null || !Map.InBounds(pos))
            {
                return;
            }

            SetTileColor(pos, Map.IsPassable(pos) ? _baseColor : _blockedColor);
        }

        private Vector3 CellToLocal(GridPos pos)
        {
            return new Vector3(pos.X * _tileSize, 0f, pos.Y * _tileSize);
        }

        private Material GetOrCreateTileMaterial()
        {
            if (_tileMaterial != null)
            {
                return _tileMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _tileMaterial = new Material(shader);
            _tileMaterial.name = "Runtime Grid Tile Material";
            return _tileMaterial;
        }

        private void ClearTiles()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            _tiles.Clear();
        }
    }
}
