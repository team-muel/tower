using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Tower.Gen
{
    // Core-pure spatial projection of a FloorGraph. This is the data contract
    // for render/traversal code: engine-free types, no mutable generator state.
    public sealed class FloorLayout
    {
        private const float NodeSpacing = 36f;
        private const float BaseFieldLength = 24f;
        private const float CrossAxisScale = 0.58f;
        private const float TrailShoulder = 7f;

        private FloorLayout(int seed, IReadOnlyList<NodeLayout> nodes, IReadOnlyList<RouteTrail> trails)
        {
            Seed = seed;
            Nodes = new List<NodeLayout>(nodes);
            Trails = new List<RouteTrail>(trails);
        }

        public int Seed { get; }
        public IReadOnlyList<NodeLayout> Nodes { get; }
        public IReadOnlyList<RouteTrail> Trails { get; }

        public static FloorLayout Generate(FloorGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            List<NodeLayout> nodes = new List<NodeLayout>(graph.Nodes.Count);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FloorNode node = graph.Nodes[i];
                float lateralOffset = LateralOffset(graph.Seed, node.Id);
                Vec3 position = new Vec3(lateralOffset, 0f, i * NodeSpacing);
                FieldAxis longAxis = PickLongAxis(graph.Seed, node.Id);
                FieldSize fieldSize = FieldSize.FromLongAxis(longAxis, BaseFieldLength, CrossAxisScale);
                nodes.Add(new NodeLayout(node.Id, i, position, fieldSize));
            }

            List<RouteTrail> trails = new List<RouteTrail>();
            for (int step = 0; step < graph.Nodes.Count - 1; step++)
            {
                FloorNode from = graph.Nodes[step];
                FloorNode to = graph.Nodes[step + 1];
                List<RouteEdge> stepRoutes = RoutesForStep(graph, from.Id, to.Id);
                if (stepRoutes.Count != 2)
                {
                    throw new InvalidOperationException("FloorLayout requires exactly two routes for each progression step.");
                }

                NodeLayout fromLayout = nodes[step];
                NodeLayout toLayout = nodes[step + 1];
                trails.Add(BuildTrail(step, stepRoutes[0], TrailSide.Left, fromLayout, toLayout, graph.Seed));
                trails.Add(BuildTrail(step, stepRoutes[1], TrailSide.Right, fromLayout, toLayout, graph.Seed));
            }

            return new FloorLayout(graph.Seed, nodes, trails);
        }

        public NodeLayout NodeById(int nodeId)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].NodeId == nodeId)
                {
                    return Nodes[i];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(nodeId), "Layout node was not found.");
        }

        public List<RouteTrail> TrailsForStep(int stepIndex)
        {
            List<RouteTrail> result = new List<RouteTrail>();
            for (int i = 0; i < Trails.Count; i++)
            {
                if (Trails[i].StepIndex == stepIndex)
                {
                    result.Add(Trails[i]);
                }
            }

            return result;
        }

        public string ToStableString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("seed=").Append(Seed.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Nodes.Count; i++)
            {
                NodeLayout node = Nodes[i];
                builder.Append("|node:")
                    .Append(node.NodeId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(node.StepIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(node.Position.ToStableString()).Append(',')
                    .Append(node.FieldSize.ToStableString());
            }

            for (int i = 0; i < Trails.Count; i++)
            {
                RouteTrail trail = Trails[i];
                builder.Append("|trail:")
                    .Append(trail.RouteId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(trail.StepIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(trail.Side).Append(',')
                    .Append(trail.RouteType).Append(',')
                    .Append(trail.FromNodeId.ToString(CultureInfo.InvariantCulture)).Append("->")
                    .Append(trail.ToNodeId.ToString(CultureInfo.InvariantCulture));

                for (int p = 0; p < trail.Waypoints.Count; p++)
                {
                    builder.Append(',').Append(trail.Waypoints[p].ToStableString());
                }
            }

            return builder.ToString();
        }

        private static RouteTrail BuildTrail(
            int stepIndex,
            RouteEdge route,
            TrailSide side,
            NodeLayout from,
            NodeLayout to,
            int seed)
        {
            float sideSign = side == TrailSide.Left ? -1f : 1f;
            float forkOffset = TrailShoulder + StableUnit(seed, route.Id, 3) * 2f;
            float curveOffset = sideSign * forkOffset;
            Vec3 start = from.ExitPoint;
            Vec3 end = to.EntryPoint;
            float trailSpan = end.Z - start.Z;
            Vec3 split = new Vec3(start.X + curveOffset, 0f, start.Z + trailSpan * 0.28f);
            Vec3 join = new Vec3(end.X + curveOffset, 0f, end.Z - trailSpan * 0.28f);
            Vec3[] waypoints = { start, split, join, end };
            return new RouteTrail(route.Id, stepIndex, route.FromNodeId, route.ToNodeId, route.RouteType, side, waypoints);
        }

        private static List<RouteEdge> RoutesForStep(FloorGraph graph, int fromNodeId, int toNodeId)
        {
            List<RouteEdge> result = new List<RouteEdge>();
            for (int i = 0; i < graph.Routes.Count; i++)
            {
                RouteEdge route = graph.Routes[i];
                if (route.FromNodeId == fromNodeId && route.ToNodeId == toNodeId)
                {
                    result.Add(route);
                }
            }

            return result;
        }

        private static FieldAxis PickLongAxis(int seed, int nodeId)
        {
            return (Hash(seed, nodeId, 11) & 1u) == 0u ? FieldAxis.X : FieldAxis.Z;
        }

        private static float LateralOffset(int seed, int nodeId)
        {
            return StableUnit(seed, nodeId, 29) * 8f;
        }

        private static float StableUnit(int seed, int value, int salt)
        {
            uint hash = Hash(seed, value, salt);
            float unit = (hash & 0xFFFFu) / 65535f;
            return unit * 2f - 1f;
        }

        private static uint Hash(int seed, int value, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = FnvBytes(h, (uint)seed);
                h = FnvBytes(h, (uint)value);
                h = FnvBytes(h, (uint)salt);
                return h;
            }
        }

        private static uint FnvBytes(uint h, uint value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    byte b = (byte)((value >> (i * 8)) & 0xFF);
                    h = (h ^ b) * 16777619u;
                }

                return h;
            }
        }

        public readonly struct Vec3 : IEquatable<Vec3>
        {
            public Vec3(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public float X { get; }
            public float Y { get; }
            public float Z { get; }

            public bool Equals(Vec3 other)
            {
                return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
            }

            public override bool Equals(object obj)
            {
                return obj is Vec3 other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X.GetHashCode();
                    hash = (hash * 397) ^ Y.GetHashCode();
                    hash = (hash * 397) ^ Z.GetHashCode();
                    return hash;
                }
            }

            public string ToStableString()
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "({0:0.###},{1:0.###},{2:0.###})",
                    X,
                    Y,
                    Z);
            }
        }

        public readonly struct FieldSize : IEquatable<FieldSize>
        {
            public FieldSize(float sizeX, float sizeZ, float elongationFactor, FieldAxis longAxis)
            {
                if (sizeX <= 0f) throw new ArgumentOutOfRangeException(nameof(sizeX));
                if (sizeZ <= 0f) throw new ArgumentOutOfRangeException(nameof(sizeZ));
                if (elongationFactor <= 1f) throw new ArgumentOutOfRangeException(nameof(elongationFactor));

                SizeX = sizeX;
                SizeZ = sizeZ;
                ElongationFactor = elongationFactor;
                LongAxis = longAxis;
            }

            public float SizeX { get; }
            public float SizeZ { get; }
            public float ElongationFactor { get; }
            public FieldAxis LongAxis { get; }

            public static FieldSize FromLongAxis(FieldAxis longAxis, float baseLength, float crossAxisScale)
            {
                if (baseLength <= 0f) throw new ArgumentOutOfRangeException(nameof(baseLength));
                if (crossAxisScale <= 0f || crossAxisScale >= 1f) throw new ArgumentOutOfRangeException(nameof(crossAxisScale));

                float cross = baseLength * crossAxisScale;
                float factor = baseLength / cross;
                if (longAxis == FieldAxis.X)
                {
                    return new FieldSize(baseLength, cross, factor, longAxis);
                }

                return new FieldSize(cross, baseLength, factor, longAxis);
            }

            public bool Equals(FieldSize other)
            {
                return SizeX.Equals(other.SizeX)
                    && SizeZ.Equals(other.SizeZ)
                    && ElongationFactor.Equals(other.ElongationFactor)
                    && LongAxis == other.LongAxis;
            }

            public override bool Equals(object obj)
            {
                return obj is FieldSize other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = SizeX.GetHashCode();
                    hash = (hash * 397) ^ SizeZ.GetHashCode();
                    hash = (hash * 397) ^ ElongationFactor.GetHashCode();
                    hash = (hash * 397) ^ (int)LongAxis;
                    return hash;
                }
            }

            public string ToStableString()
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "field({0:0.###},{1:0.###},factor={2:0.###},axis={3})",
                    SizeX,
                    SizeZ,
                    ElongationFactor,
                    LongAxis);
            }
        }

        public readonly struct NodeLayout : IEquatable<NodeLayout>
        {
            public NodeLayout(int nodeId, int stepIndex, Vec3 position, FieldSize fieldSize)
            {
                if (nodeId < 0) throw new ArgumentOutOfRangeException(nameof(nodeId));
                if (stepIndex < 0) throw new ArgumentOutOfRangeException(nameof(stepIndex));

                NodeId = nodeId;
                StepIndex = stepIndex;
                Position = position;
                FieldSize = fieldSize;
            }

            public int NodeId { get; }
            public int StepIndex { get; }
            public Vec3 Position { get; }
            public FieldSize FieldSize { get; }

            public Vec3 EntryPoint
            {
                get { return new Vec3(Position.X, Position.Y, Position.Z - FieldSize.SizeZ * 0.5f); }
            }

            public Vec3 ExitPoint
            {
                get { return new Vec3(Position.X, Position.Y, Position.Z + FieldSize.SizeZ * 0.5f); }
            }

            public bool Equals(NodeLayout other)
            {
                return NodeId == other.NodeId
                    && StepIndex == other.StepIndex
                    && Position.Equals(other.Position)
                    && FieldSize.Equals(other.FieldSize);
            }

            public override bool Equals(object obj)
            {
                return obj is NodeLayout other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = NodeId;
                    hash = (hash * 397) ^ StepIndex;
                    hash = (hash * 397) ^ Position.GetHashCode();
                    hash = (hash * 397) ^ FieldSize.GetHashCode();
                    return hash;
                }
            }
        }

        public sealed class RouteTrail
        {
            public RouteTrail(
                int routeId,
                int stepIndex,
                int fromNodeId,
                int toNodeId,
                RouteType routeType,
                TrailSide side,
                IReadOnlyList<Vec3> waypoints)
            {
                if (routeId < 0) throw new ArgumentOutOfRangeException(nameof(routeId));
                if (stepIndex < 0) throw new ArgumentOutOfRangeException(nameof(stepIndex));
                if (fromNodeId < 0) throw new ArgumentOutOfRangeException(nameof(fromNodeId));
                if (toNodeId < 0) throw new ArgumentOutOfRangeException(nameof(toNodeId));
                if (fromNodeId == toNodeId) throw new ArgumentException("Trail must connect two different nodes.", nameof(toNodeId));
                if (waypoints == null) throw new ArgumentNullException(nameof(waypoints));
                if (waypoints.Count < 2) throw new ArgumentException("Trail requires at least a start and end waypoint.", nameof(waypoints));

                RouteId = routeId;
                StepIndex = stepIndex;
                FromNodeId = fromNodeId;
                ToNodeId = toNodeId;
                RouteType = routeType;
                Side = side;
                Waypoints = new List<Vec3>(waypoints);
            }

            public int RouteId { get; }
            public int StepIndex { get; }
            public int FromNodeId { get; }
            public int ToNodeId { get; }
            public RouteType RouteType { get; }
            public TrailSide Side { get; }
            public IReadOnlyList<Vec3> Waypoints { get; }
        }
    }

    public enum FieldAxis
    {
        X,
        Z
    }

    public enum TrailSide
    {
        Left,
        Right
    }
}
