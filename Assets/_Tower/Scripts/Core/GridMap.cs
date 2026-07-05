using System;
using System.Collections.Generic;

namespace Tower.Core
{
    public sealed class GridMap
    {
        private readonly Cell[] _cells;

        public GridMap(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            }

            Width = width;
            Height = height;
            _cells = new Cell[width * height];

            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i].IsPassable = true;
            }
        }

        public int Width { get; }

        public int Height { get; }

        public IEnumerable<GridPos> Positions
        {
            get
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        yield return new GridPos(x, y);
                    }
                }
            }
        }

        public bool InBounds(GridPos pos)
        {
            return pos.X >= 0 && pos.Y >= 0 && pos.X < Width && pos.Y < Height;
        }

        public bool IsPassable(GridPos pos)
        {
            return InBounds(pos) && _cells[ToIndex(pos)].IsPassable;
        }

        public bool IsBlocked(GridPos pos)
        {
            return !IsPassable(pos);
        }

        public void SetPassable(GridPos pos, bool passable)
        {
            EnsureInBounds(pos);
            Cell cell = _cells[ToIndex(pos)];
            cell.IsPassable = passable;
            if (!passable)
            {
                cell.OccupantId = null;
            }

            _cells[ToIndex(pos)] = cell;
        }

        public void SetBlocked(GridPos pos, bool blocked)
        {
            SetPassable(pos, !blocked);
        }

        public string GetOccupant(GridPos pos)
        {
            EnsureInBounds(pos);
            return _cells[ToIndex(pos)].OccupantId;
        }

        public bool IsOccupied(GridPos pos)
        {
            return InBounds(pos) && !string.IsNullOrEmpty(_cells[ToIndex(pos)].OccupantId);
        }

        public bool CanEnter(GridPos pos)
        {
            return CanEnter(pos, null);
        }

        public bool CanEnter(GridPos pos, string movingOccupantId)
        {
            if (!InBounds(pos))
            {
                return false;
            }

            Cell cell = _cells[ToIndex(pos)];
            if (!cell.IsPassable)
            {
                return false;
            }

            return string.IsNullOrEmpty(cell.OccupantId) || cell.OccupantId == movingOccupantId;
        }

        public bool TrySetOccupant(GridPos pos, string occupantId)
        {
            if (string.IsNullOrWhiteSpace(occupantId) || !CanEnter(pos, occupantId))
            {
                return false;
            }

            Cell cell = _cells[ToIndex(pos)];
            cell.OccupantId = occupantId;
            _cells[ToIndex(pos)] = cell;
            return true;
        }

        public bool ClearOccupant(GridPos pos)
        {
            if (!InBounds(pos) || string.IsNullOrEmpty(_cells[ToIndex(pos)].OccupantId))
            {
                return false;
            }

            Cell cell = _cells[ToIndex(pos)];
            cell.OccupantId = null;
            _cells[ToIndex(pos)] = cell;
            return true;
        }

        public bool ClearOccupant(GridPos pos, string occupantId)
        {
            if (string.IsNullOrEmpty(occupantId) || !InBounds(pos) || _cells[ToIndex(pos)].OccupantId != occupantId)
            {
                return false;
            }

            Cell cell = _cells[ToIndex(pos)];
            cell.OccupantId = null;
            _cells[ToIndex(pos)] = cell;
            return true;
        }

        public bool TryMoveOccupant(GridPos from, GridPos to, string occupantId)
        {
            if (string.IsNullOrEmpty(occupantId) || !InBounds(from) || _cells[ToIndex(from)].OccupantId != occupantId)
            {
                return false;
            }

            if (!CanEnter(to, occupantId))
            {
                return false;
            }

            ClearOccupant(from, occupantId);
            return TrySetOccupant(to, occupantId);
        }

        public GridPos? FindOccupant(string occupantId)
        {
            if (string.IsNullOrEmpty(occupantId))
            {
                return null;
            }

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].OccupantId == occupantId)
                {
                    return new GridPos(i % Width, i / Width);
                }
            }

            return null;
        }

        private void EnsureInBounds(GridPos pos)
        {
            if (!InBounds(pos))
            {
                throw new ArgumentOutOfRangeException(nameof(pos), string.Format("Grid position {0} is outside {1}x{2}.", pos, Width, Height));
            }
        }

        private int ToIndex(GridPos pos)
        {
            return pos.Y * Width + pos.X;
        }

        private struct Cell
        {
            public bool IsPassable;
            public string OccupantId;
        }
    }
}
