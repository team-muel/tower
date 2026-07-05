using System.Collections.Generic;

namespace Tower.Core
{
    public static class Pathfinder
    {
        private static readonly GridPos[] Directions =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        public static IReadOnlyList<GridPos> FindPath(GridMap map, GridPos start, GridPos goal)
        {
            return FindPath(map, start, goal, null);
        }

        public static IReadOnlyList<GridPos> FindPath(GridMap map, GridPos start, GridPos goal, string movingOccupantId)
        {
            if (map == null || !map.IsPassable(start) || !map.CanEnter(goal, movingOccupantId))
            {
                return new GridPos[0];
            }

            if (start == goal)
            {
                return new[] { start };
            }

            List<GridPos> openSet = new List<GridPos> { start };
            HashSet<GridPos> closedSet = new HashSet<GridPos>();
            Dictionary<GridPos, GridPos> cameFrom = new Dictionary<GridPos, GridPos>();
            Dictionary<GridPos, int> gScore = new Dictionary<GridPos, int>();
            Dictionary<GridPos, int> fScore = new Dictionary<GridPos, int>();

            gScore[start] = 0;
            fScore[start] = GridDistance.Manhattan(start, goal);

            while (openSet.Count > 0)
            {
                GridPos current = TakeLowestScore(openSet, fScore);
                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }

                closedSet.Add(current);

                for (int i = 0; i < Directions.Length; i++)
                {
                    GridPos neighbor = new GridPos(current.X + Directions[i].X, current.Y + Directions[i].Y);
                    if (closedSet.Contains(neighbor) || !map.CanEnter(neighbor, movingOccupantId))
                    {
                        continue;
                    }

                    int tentativeGScore = gScore[current] + 1;
                    int knownGScore;
                    bool hasKnownGScore = gScore.TryGetValue(neighbor, out knownGScore);
                    if (hasKnownGScore && tentativeGScore >= knownGScore)
                    {
                        continue;
                    }

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + GridDistance.Manhattan(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }

            return new GridPos[0];
        }

        private static GridPos TakeLowestScore(List<GridPos> openSet, Dictionary<GridPos, int> fScore)
        {
            int bestIndex = 0;
            int bestScore = GetScore(fScore, openSet[0]);

            for (int i = 1; i < openSet.Count; i++)
            {
                int score = GetScore(fScore, openSet[i]);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            GridPos best = openSet[bestIndex];
            openSet.RemoveAt(bestIndex);
            return best;
        }

        private static int GetScore(Dictionary<GridPos, int> scores, GridPos pos)
        {
            int score;
            return scores.TryGetValue(pos, out score) ? score : int.MaxValue;
        }

        private static IReadOnlyList<GridPos> ReconstructPath(Dictionary<GridPos, GridPos> cameFrom, GridPos current)
        {
            List<GridPos> path = new List<GridPos> { current };

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
