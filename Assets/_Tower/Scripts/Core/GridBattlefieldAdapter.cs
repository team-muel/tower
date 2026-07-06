using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // T20: wraps the legacy GridMap behind IBattlefield. Every rule is
    // preserved verbatim — Manhattan distance, Bresenham line of sight, BFS
    // reachability and A* path clamping — so grid-mode combat (and every
    // pre-T20 test scenario) produces exactly the same outcomes as before.
    public sealed class GridBattlefieldAdapter : IBattlefield
    {
        private const float CostEpsilon = 0.0001f;

        private static readonly GridPos[] Directions =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        private readonly GridMap map;

        public GridBattlefieldAdapter(GridMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            this.map = map;
        }

        public GridMap Map
        {
            get { return map; }
        }

        public CombatSpaceMode Mode
        {
            get { return CombatSpaceMode.Grid; }
        }

        public float Width
        {
            get { return map.Width * BattleScale.UnitsPerCell; }
        }

        public float Height
        {
            get { return map.Height * BattleScale.UnitsPerCell; }
        }

        public bool Contains(BattlePos pos)
        {
            return map.InBounds(BattleScale.ToGridPos(pos));
        }

        public float Distance(BattlePos a, BattlePos b)
        {
            return GridDistance.Manhattan(BattleScale.ToGridPos(a), BattleScale.ToGridPos(b));
        }

        public bool AreAdjacent(BattlePos a, BattlePos b)
        {
            return GridDistance.Manhattan(BattleScale.ToGridPos(a), BattleScale.ToGridPos(b)) == 1;
        }

        public bool HasLineOfSight(BattlePos from, BattlePos to)
        {
            return LineOfSight.IsClear(map, BattleScale.ToGridPos(from), BattleScale.ToGridPos(to));
        }

        public BattlePos ClampMove(string unitId, BattlePos from, BattlePos to, float moveBudget)
        {
            var start = BattleScale.ToGridPos(from);
            var goal = BattleScale.ToGridPos(to);
            var path = Pathfinder.FindPath(map, start, goal, unitId);
            if (path.Count == 0)
            {
                return BattleScale.ToBattlePos(start);
            }

            var budgetSteps = (int)Math.Floor(moveBudget + CostEpsilon);
            var steps = Math.Min(path.Count - 1, Math.Max(0, budgetSteps));
            return BattleScale.ToBattlePos(path[steps]);
        }

        public bool IsOccupied(BattlePos pos)
        {
            return map.IsOccupied(BattleScale.ToGridPos(pos));
        }

        public string GetOccupantAt(BattlePos pos)
        {
            var cell = BattleScale.ToGridPos(pos);
            return map.InBounds(cell) ? map.GetOccupant(cell) : null;
        }

        public BattlePos? FindOccupant(string unitId)
        {
            var cell = map.FindOccupant(unitId);
            return cell.HasValue ? BattleScale.ToBattlePos(cell.Value) : (BattlePos?)null;
        }

        public bool TryPlaceOccupant(string unitId, BattlePos pos)
        {
            return map.TrySetOccupant(BattleScale.ToGridPos(pos), unitId);
        }

        public bool TryMoveOccupant(string unitId, BattlePos to)
        {
            var origin = map.FindOccupant(unitId);
            if (!origin.HasValue)
            {
                return false;
            }

            return map.TryMoveOccupant(origin.Value, BattleScale.ToGridPos(to), unitId);
        }

        public bool RemoveOccupant(string unitId)
        {
            var origin = map.FindOccupant(unitId);
            return origin.HasValue && map.ClearOccupant(origin.Value, unitId);
        }

        // Breadth-first flood fill over enterable cells within the movement
        // budget — the exact reachability rule the ActionScorer used before
        // T20. Candidates come back in BFS order, starting with stay-put.
        public IReadOnlyList<BattleMoveCandidate> GetMoveCandidates(string unitId, BattlePos from, float moveBudget)
        {
            var start = BattleScale.ToGridPos(from);
            var budget = (int)Math.Floor(moveBudget + CostEpsilon);
            var candidates = new List<BattleMoveCandidate>();
            var distances = new Dictionary<GridPos, int> { [start] = 0 };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);
            candidates.Add(new BattleMoveCandidate(BattleScale.ToBattlePos(start), 0f));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var distance = distances[current];
                if (distance >= budget)
                {
                    continue;
                }

                for (var index = 0; index < Directions.Length; index++)
                {
                    var next = new GridPos(current.X + Directions[index].X, current.Y + Directions[index].Y);
                    if (distances.ContainsKey(next) || !map.CanEnter(next, unitId))
                    {
                        continue;
                    }

                    distances[next] = distance + 1;
                    queue.Enqueue(next);
                    candidates.Add(new BattleMoveCandidate(BattleScale.ToBattlePos(next), distance + 1));
                }
            }

            return candidates;
        }
    }
}
