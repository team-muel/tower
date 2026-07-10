using System;
namespace Tower.Gen
{
    // 노드 내용의 lazy 결정적 바인더. T49 이후 조우 실행과 전투 격자는 제거되어,
    // 현재는 노드별 공간 치수만 결정한다.
    public static class FloorNodeBinder
    {
        public static FloorNodeContent Bind(FloorGraph graph, FloorNode node, FloorGenParams parameters)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            Random random = new Random(unchecked((int)Hash(graph.Seed, node.Id, node.Depth)));
            int width = NextInclusive(random, parameters.RoomSizeRange.Min, parameters.RoomSizeRange.Max);
            int height = NextInclusive(random, parameters.RoomSizeRange.Min, parameters.RoomSizeRange.Max);
            return new FloorNodeContent(node.Id, width, height);
        }

        private static int NextInclusive(Random random, int min, int max)
        {
            return random.Next(min, max + 1);
        }

        // FNV-1a 스타일 결정적 믹스(PortalAssigner와 동일 계열).
        private static uint Hash(int seed, int nodeId, int depth)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)seed) * 16777619u;
                hash = (hash ^ (uint)nodeId) * 16777619u;
                hash = (hash ^ (uint)depth) * 16777619u;
                return hash;
            }
        }
    }
}
