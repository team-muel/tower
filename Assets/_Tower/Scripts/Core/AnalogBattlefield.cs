using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // T20: continuous rectangular battlefield. v0 rules:
    // - Euclidean distance; line of sight is always clear (obstacles and
    //   NavMesh integration are explicit follow-up tasks).
    // - Units are circles of radius UnitRadius; circles may not overlap and
    //   must stay fully inside the area.
    // - ClampMove walks the straight segment from 'from' toward 'to', backing
    //   off in fixed steps until the position is valid — deterministic, no RNG.
    public sealed class AnalogBattlefield : IBattlefield
    {
        public const float DefaultUnitRadius = 0.45f;

        // Melee contact: two touching circles sit 2 * UnitRadius (0.9) apart;
        // anything within this range counts as adjacent for danger scoring.
        public const float MeleeAdjacencyRange = 1.05f;

        private const float ClampStep = 0.05f;
        private const float Epsilon = 0.0001f;
        private const float MinimumMoveCost = 0.001f;

        // Analog move sampling (T-fix: kill the grid feel).
        // The rosette gives coarse omnidirectional coverage; the per-occupant
        // bearings give continuous approach/retreat lines toward real targets
        // so units glide along true vectors instead of snapping to a compass
        // lattice. 16 directions x 3 radii + 2 bearings per other occupant.
        private const int RosetteDirections = 16;
        private static readonly float[] RadiusFractions = { 1f, 0.66f, 0.33f };

        private readonly Dictionary<string, BattlePos> occupants = new Dictionary<string, BattlePos>(StringComparer.Ordinal);
        private readonly List<string> occupantOrder = new List<string>();

        public AnalogBattlefield(float width, float height, float unitRadius = DefaultUnitRadius)
        {
            if (width <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            }

            if (height <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            }

            if (unitRadius <= 0f || unitRadius * 2f > Math.Min(width, height))
            {
                throw new ArgumentOutOfRangeException(nameof(unitRadius), "Unit radius must be positive and fit the area.");
            }

            Width = width;
            Height = height;
            UnitRadius = unitRadius;
        }

        // Battle area from generated room dimensions.
        public static AnalogBattlefield FromRoom(int cellWidth, int cellHeight)
        {
            return new AnalogBattlefield(cellWidth, cellHeight);
        }

        public CombatSpaceMode Mode
        {
            get { return CombatSpaceMode.Analog; }
        }

        public float Width { get; }

        public float Height { get; }

        public float UnitRadius { get; }

        public bool Contains(BattlePos pos)
        {
            return pos.X >= 0f && pos.X <= Width && pos.Y >= 0f && pos.Y <= Height;
        }

        public float Distance(BattlePos a, BattlePos b)
        {
            var deltaX = a.X - b.X;
            var deltaY = a.Y - b.Y;
            return (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        }

        public bool AreAdjacent(BattlePos a, BattlePos b)
        {
            return Distance(a, b) <= MeleeAdjacencyRange;
        }

        public bool HasLineOfSight(BattlePos from, BattlePos to)
        {
            // v0: no obstacles on the analog battlefield.
            return true;
        }

        public BattlePos ClampMove(string unitId, BattlePos from, BattlePos to, float moveBudget)
        {
            if (moveBudget <= 0f)
            {
                return from;
            }

            var deltaX = to.X - from.X;
            var deltaY = to.Y - from.Y;
            var length = (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (length <= Epsilon)
            {
                return from;
            }

            var directionX = deltaX / length;
            var directionY = deltaY / length;
            var travel = Math.Min(length, moveBudget);
            while (travel > Epsilon)
            {
                var candidate = new BattlePos(from.X + (directionX * travel), from.Y + (directionY * travel));
                if (IsOpen(candidate, unitId))
                {
                    return candidate;
                }

                travel -= ClampStep;
            }

            return from;
        }

        public bool IsOccupied(BattlePos pos)
        {
            return GetOccupantAt(pos) != null;
        }

        // The occupant whose circle covers the point; nearest center wins,
        // insertion order breaks exact ties deterministically.
        public string GetOccupantAt(BattlePos pos)
        {
            string best = null;
            var bestDistance = float.MaxValue;
            foreach (var unitId in occupantOrder)
            {
                var distance = Distance(occupants[unitId], pos);
                if (distance <= UnitRadius + MinimumMoveCost && distance < bestDistance)
                {
                    best = unitId;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public BattlePos? FindOccupant(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }

            return occupants.TryGetValue(unitId, out var pos) ? pos : (BattlePos?)null;
        }

        public bool TryPlaceOccupant(string unitId, BattlePos pos)
        {
            if (string.IsNullOrWhiteSpace(unitId) || occupants.ContainsKey(unitId) || !IsOpen(pos, unitId))
            {
                return false;
            }

            occupants[unitId] = pos;
            occupantOrder.Add(unitId);
            return true;
        }

        public bool TryMoveOccupant(string unitId, BattlePos to)
        {
            if (string.IsNullOrEmpty(unitId) || !occupants.ContainsKey(unitId) || !IsOpen(to, unitId))
            {
                return false;
            }

            occupants[unitId] = to;
            return true;
        }

        public bool RemoveOccupant(string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || !occupants.Remove(unitId))
            {
                return false;
            }

            occupantOrder.Remove(unitId);
            return true;
        }

        // T20/analog-fix sampling contract: candidates are stay-put plus a
        // rosette (RosetteDirections directions at each RadiusFractions radius)
        // plus, for every other occupant, a straight approach and retreat
        // bearing along the true line to that occupant. Every candidate is
        // clamped by ClampMove. The set is a fixed-order pure function of the
        // battlefield state (no RNG), so AI planning stays deterministic for a
        // given state; a seeded angular jitter can be layered later without
        // touching callers.
        public IReadOnlyList<BattleMoveCandidate> GetMoveCandidates(string unitId, BattlePos from, float moveBudget)
        {
            var candidates = new List<BattleMoveCandidate>
            {
                new BattleMoveCandidate(from, 0f)
            };

            if (moveBudget <= 0f)
            {
                return candidates;
            }

            // Rosette: omnidirectional coverage at several distances.
            for (var radiusIndex = 0; radiusIndex < RadiusFractions.Length; radiusIndex++)
            {
                var radius = moveBudget * RadiusFractions[radiusIndex];
                if (radius <= Epsilon)
                {
                    continue;
                }

                for (var direction = 0; direction < RosetteDirections; direction++)
                {
                    var angle = (Math.PI * 2.0 * direction) / RosetteDirections;
                    var dirX = (float)Math.Cos(angle);
                    var dirY = (float)Math.Sin(angle);
                    AddCandidate(candidates, unitId, from,
                        new BattlePos(from.X + (dirX * radius), from.Y + (dirY * radius)), moveBudget);
                }
            }

            // Target-relative bearings: continuous approach/retreat toward every
            // other occupant so movement follows real lines, not the lattice.
            foreach (var otherId in occupantOrder)
            {
                if (StringComparer.Ordinal.Equals(otherId, unitId))
                {
                    continue;
                }

                var other = occupants[otherId];
                var toX = other.X - from.X;
                var toY = other.Y - from.Y;
                var length = (float)Math.Sqrt((toX * toX) + (toY * toY));
                if (length <= Epsilon)
                {
                    continue;
                }

                var unitX = toX / length;
                var unitY = toY / length;

                // Approach: straight at the occupant (ClampMove stops at contact).
                AddCandidate(candidates, unitId, from,
                    new BattlePos(from.X + (unitX * moveBudget), from.Y + (unitY * moveBudget)), moveBudget);

                // Retreat: directly away from the occupant.
                AddCandidate(candidates, unitId, from,
                    new BattlePos(from.X - (unitX * moveBudget), from.Y - (unitY * moveBudget)), moveBudget);
            }

            return candidates;
        }

        private void AddCandidate(List<BattleMoveCandidate> candidates, string unitId, BattlePos from, BattlePos target, float moveBudget)
        {
            var destination = ClampMove(unitId, from, target, moveBudget);
            var cost = Distance(from, destination);
            if (cost > MinimumMoveCost)
            {
                candidates.Add(new BattleMoveCandidate(destination, cost));
            }
        }

        private bool IsOpen(BattlePos pos, string movingUnitId)
        {
            if (pos.X < UnitRadius || pos.X > Width - UnitRadius
                || pos.Y < UnitRadius || pos.Y > Height - UnitRadius)
            {
                return false;
            }

            foreach (var unitId in occupantOrder)
            {
                if (StringComparer.Ordinal.Equals(unitId, movingUnitId))
                {
                    continue;
                }

                if (Distance(occupants[unitId], pos) < (UnitRadius * 2f) - Epsilon)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
