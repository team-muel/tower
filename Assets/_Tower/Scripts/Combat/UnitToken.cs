using System.Collections;
using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public sealed class UnitToken : MonoBehaviour
    {
        [SerializeField] private float _moveStepSeconds = 0.18f;
        [SerializeField] private float _heightOffset = 0.55f;

        private GridView _gridView;
        private AnalogBattlefieldView _analogView;
        private Coroutine _moveCoroutine;
        private IReadOnlyList<GridPos> _activePath;

        public string OccupantId { get; private set; }

        public GridPos Position { get; private set; }

        // T20: continuous position; grid placements report the cell center.
        public BattlePos BattlePosition { get; private set; }

        public static UnitToken Spawn(GridView gridView, GridPos start, string occupantId, Color color)
        {
            UnitToken token = CreateTokenObject(occupantId, color);
            token.Initialize(gridView, start, occupantId);
            return token;
        }

        // T20: spawn at a continuous position on the analog battlefield view.
        public static UnitToken SpawnAnalog(AnalogBattlefieldView view, BattlePos start, string occupantId, Color color)
        {
            UnitToken token = CreateTokenObject(occupantId, color);
            token.InitializeAnalog(view, start, occupantId);
            return token;
        }

        private static UnitToken CreateTokenObject(string occupantId, Color color)
        {
            GameObject tokenObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tokenObject.name = string.IsNullOrEmpty(occupantId) ? "Unit Token" : "Unit Token " + occupantId;

            Renderer renderer = tokenObject.GetComponent<Renderer>();
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.name = tokenObject.name + " Material";
            material.color = color;
            renderer.sharedMaterial = material;

            return tokenObject.AddComponent<UnitToken>();
        }

        public void Initialize(GridView gridView, GridPos start, string occupantId)
        {
            _gridView = gridView;
            _analogView = null;
            OccupantId = occupantId;
            Place(start);
        }

        public void InitializeAnalog(AnalogBattlefieldView view, BattlePos start, string occupantId)
        {
            _analogView = view;
            _gridView = null;
            OccupantId = occupantId;
            PlaceAt(start);
        }

        public void Place(GridPos pos)
        {
            TryPlace(pos);
        }

        public bool TryPlace(GridPos pos)
        {
            if (_gridView != null && _gridView.Map != null && !string.IsNullOrEmpty(OccupantId))
            {
                if (!_gridView.Map.CanEnter(pos, OccupantId))
                {
                    return false;
                }

                _gridView.Map.ClearOccupant(Position, OccupantId);
                _gridView.Map.TrySetOccupant(pos, OccupantId);
            }

            Position = pos;
            BattlePosition = BattleScale.ToBattlePos(pos);
            transform.position = GetWorldPosition(pos);
            return true;
        }

        // T20: view-only continuous placement; battlefield occupancy is owned
        // by AnalogBattlefield (the caller keeps them in sync).
        public void PlaceAt(BattlePos pos)
        {
            BattlePosition = pos;
            Position = BattleScale.ToGridPos(pos);
            transform.position = GetWorldPosition(pos);
        }

        public Coroutine MoveAlong(IReadOnlyList<GridPos> path)
        {
            if (path == null || path.Count == 0)
            {
                return null;
            }

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
            }

            _activePath = path;
            _moveCoroutine = StartCoroutine(MoveAlongRoutine(path));
            return _moveCoroutine;
        }

        public IEnumerator MoveAlongRoutine(IReadOnlyList<GridPos> path)
        {
            if (path == null || path.Count == 0)
            {
                yield break;
            }

            for (int i = 1; i < path.Count; i++)
            {
                GridPos next = path[i];
                Vector3 from = transform.position;
                Vector3 to = GetWorldPosition(next);
                float elapsed = 0f;

                while (elapsed < _moveStepSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = _moveStepSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / _moveStepSeconds);
                    transform.position = Vector3.Lerp(from, to, t);
                    yield return null;
                }

                Place(next);
            }

            _moveCoroutine = null;
            _activePath = null;
        }

        public void CompleteMoveImmediately()
        {
            if (_activePath == null || _activePath.Count == 0)
            {
                return;
            }

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            Place(_activePath[_activePath.Count - 1]);
            _activePath = null;
        }

        public void MoveAlongImmediate(IReadOnlyList<GridPos> path)
        {
            if (path == null || path.Count == 0)
            {
                return;
            }

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            _activePath = null;
            Place(path[path.Count - 1]);
        }

        private Vector3 GetWorldPosition(GridPos pos)
        {
            if (_gridView == null)
            {
                return GetWorldPosition(BattleScale.ToBattlePos(pos));
            }

            return _gridView.CellToWorld(pos) + Vector3.up * _heightOffset;
        }

        private Vector3 GetWorldPosition(BattlePos pos)
        {
            if (_analogView != null)
            {
                return _analogView.ToWorld(pos) + Vector3.up * _heightOffset;
            }

            return new Vector3(pos.X, _heightOffset, pos.Y);
        }
    }
}
