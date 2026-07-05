using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public sealed class TileHighlighter : MonoBehaviour
    {
        [SerializeField] private GridView _gridView;
        [SerializeField] private Color _hoverColor = new Color(0.95f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color _selectedColor = new Color(0.2f, 0.65f, 1f, 1f);
        [SerializeField] private Color _rangeColor = new Color(0.25f, 0.8f, 0.45f, 1f);
        [SerializeField] private Color _moveHintColor = new Color(0.18f, 0.68f, 0.45f, 1f);
        [SerializeField] private Color _targetHintColor = new Color(0.7f, 0.18f, 0.35f, 1f);

        private readonly HashSet<GridPos> _range = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _moveHints = new HashSet<GridPos>();
        private readonly HashSet<GridPos> _targetHints = new HashSet<GridPos>();
        private GridPos? _hover;
        private GridPos? _selected;

        public void Initialize(GridView gridView)
        {
            _gridView = gridView;
            RefreshAll();
        }

        public void SetHover(GridPos? pos)
        {
            GridPos? previous = _hover;
            _hover = pos;
            Refresh(previous);
            Refresh(_hover);
        }

        public void SetSelected(GridPos? pos)
        {
            GridPos? previous = _selected;
            _selected = pos;
            Refresh(previous);
            Refresh(_selected);
        }

        public void SetRange(IEnumerable<GridPos> positions)
        {
            List<GridPos> previous = new List<GridPos>(_range);
            _range.Clear();

            if (positions != null)
            {
                foreach (GridPos pos in positions)
                {
                    _range.Add(pos);
                }
            }

            for (int i = 0; i < previous.Count; i++)
            {
                Refresh(previous[i]);
            }

            foreach (GridPos pos in _range)
            {
                Refresh(pos);
            }
        }

        public void ClearAll()
        {
            GridPos? previousHover = _hover;
            GridPos? previousSelected = _selected;
            List<GridPos> previousRange = new List<GridPos>(_range);
            var previousMoveHints = new List<GridPos>(_moveHints);
            var previousTargetHints = new List<GridPos>(_targetHints);

            _hover = null;
            _selected = null;
            _range.Clear();
            _moveHints.Clear();
            _targetHints.Clear();

            Refresh(previousHover);
            Refresh(previousSelected);
            for (int i = 0; i < previousRange.Count; i++)
            {
                Refresh(previousRange[i]);
            }

            for (int i = 0; i < previousMoveHints.Count; i++)
            {
                Refresh(previousMoveHints[i]);
            }

            for (int i = 0; i < previousTargetHints.Count; i++)
            {
                Refresh(previousTargetHints[i]);
            }
        }

        public void SetMoveHints(IEnumerable<GridPos> positions)
        {
            var previous = new List<GridPos>(_moveHints);
            _moveHints.Clear();
            if (positions != null)
            {
                foreach (GridPos pos in positions)
                {
                    _moveHints.Add(pos);
                }
            }

            for (int i = 0; i < previous.Count; i++)
            {
                Refresh(previous[i]);
            }

            foreach (GridPos pos in _moveHints)
            {
                Refresh(pos);
            }
        }

        public void SetTargetHints(IEnumerable<GridPos> positions)
        {
            var previous = new List<GridPos>(_targetHints);
            _targetHints.Clear();
            if (positions != null)
            {
                foreach (GridPos pos in positions)
                {
                    _targetHints.Add(pos);
                }
            }

            for (int i = 0; i < previous.Count; i++)
            {
                Refresh(previous[i]);
            }

            foreach (GridPos pos in _targetHints)
            {
                Refresh(pos);
            }
        }

        private void RefreshAll()
        {
            Refresh(_hover);
            Refresh(_selected);
            foreach (GridPos pos in _range)
            {
                Refresh(pos);
            }
        }

        private void Refresh(GridPos? pos)
        {
            if (!pos.HasValue || _gridView == null)
            {
                return;
            }

            Refresh(pos.Value);
        }

        private void Refresh(GridPos pos)
        {
            if (_gridView == null)
            {
                return;
            }

            if (_selected.HasValue && _selected.Value == pos)
            {
                _gridView.SetTileColor(pos, _selectedColor);
            }
            else if (_hover.HasValue && _hover.Value == pos)
            {
                _gridView.SetTileColor(pos, _hoverColor);
            }
            else if (_moveHints.Contains(pos) || _targetHints.Contains(pos))
            {
                _gridView.SetTileColor(pos, _moveHints.Contains(pos) ? _moveHintColor : _targetHintColor);
            }
            else if (_range.Contains(pos))
            {
                _gridView.SetTileColor(pos, _rangeColor);
            }
            else
            {
                _gridView.ResetTileColor(pos);
            }
        }
    }
}
