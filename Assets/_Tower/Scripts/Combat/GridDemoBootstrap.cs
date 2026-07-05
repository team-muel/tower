using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public sealed class GridDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private int _width = 12;
        [SerializeField] private int _height = 12;

        private GridView _gridView;
        private TileHighlighter _highlighter;
        private UnitToken _playerToken;
        private Camera _camera;

        private void Start()
        {
            GridMap map = new GridMap(_width, _height);
            AddDemoObstacles(map);

            GameObject gridObject = new GameObject("Runtime Grid");
            _gridView = gridObject.AddComponent<GridView>();
            _gridView.Build(map);

            _highlighter = gridObject.AddComponent<TileHighlighter>();
            _highlighter.Initialize(_gridView);

            _playerToken = UnitToken.Spawn(_gridView, new GridPos(1, 1), "regressor", new Color(0.2f, 0.55f, 1f, 1f));
            UnitToken.Spawn(_gridView, new GridPos(2, 1), "ally-a", new Color(0.25f, 0.9f, 0.55f, 1f));
            UnitToken.Spawn(_gridView, new GridPos(1, 2), "ally-b", new Color(0.9f, 0.55f, 0.25f, 1f));

            GameObject cameraRigObject = new GameObject("Iso Camera Rig");
            IsoCameraRig cameraRig = cameraRigObject.AddComponent<IsoCameraRig>();
            cameraRig.Focus(_gridView, new GridPos(_width / 2, _height / 2));
            _camera = cameraRig.Camera;
        }

        private void Update()
        {
            if (_gridView == null || _camera == null)
            {
                return;
            }

            GridPos hover;
            if (TryGetMouseCell(out hover) && _gridView.Map.InBounds(hover))
            {
                _highlighter.SetHover(hover);

                if (Input.GetMouseButtonDown(0))
                {
                    MovePlayerTo(hover);
                }
            }
            else
            {
                _highlighter.SetHover(null);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _playerToken.CompleteMoveImmediately();
            }
        }

        private void MovePlayerTo(GridPos destination)
        {
            IReadOnlyList<GridPos> path = Pathfinder.FindPath(_gridView.Map, _playerToken.Position, destination, _playerToken.OccupantId);
            if (path.Count == 0)
            {
                _highlighter.SetSelected(null);
                return;
            }

            _highlighter.SetSelected(destination);
            _highlighter.SetRange(path);
            _playerToken.MoveAlong(path);
        }

        private bool TryGetMouseCell(out GridPos pos)
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                pos = _gridView.WorldToCell(hit.point);
                return true;
            }

            pos = new GridPos();
            return false;
        }

        private static void AddDemoObstacles(GridMap map)
        {
            for (int y = 3; y < 9; y++)
            {
                if (y == 6)
                {
                    continue;
                }

                map.SetBlocked(new GridPos(5, y), true);
            }

            map.SetBlocked(new GridPos(8, 4), true);
            map.SetBlocked(new GridPos(8, 5), true);
            map.SetBlocked(new GridPos(8, 6), true);
        }
    }
}
