using System;
using Tower.Core;

namespace Tower.Gen
{
    // 노드 내용의 lazy 결정적 바인더. 골격(FloorGraph)에서 격자·조우를 제거했기 때문에
    // 소비자(전투 하니스/플레이 컨트롤러)가 조우 자리에 들어설 때 이 바인더로 전투 격자와
    // 조우를 만든다. (graph.Seed, node.Id)만으로 결정 → 같은 씨드는 같은 내용.
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
            GridMap battlefield = new GridMap(width, height);

            FloorEncounter encounter = ComposeEncounter(parameters, graph, node);
            return new FloorNodeContent(node.Id, battlefield, encounter);
        }

        private static FloorEncounter ComposeEncounter(FloorGenParams parameters, FloorGraph graph, FloorNode node)
        {
            EncounterBudget budget = parameters.EncounterBudgetTable.Resolve(
                parameters.BiomeId.ToString(),
                node.Kind.ToString());

            return FloorEncounterComposer.Compose(
                budget,
                node.Kind,
                graph.Seed,
                node.Id,
                node.Depth,
                parameters.BiomeId,
                parameters.EnemyKindSlots,
                parameters.BossKindSlot,
                parameters.EliteKindSlot);
        }

        private static int NextInclusive(Random random, int min, int max)
        {
            return random.Next(min, max + 1);
        }

        // FNV-1a 스타일 결정적 믹스(PortalAssigner/FloorEncounterComposer와 동일 계열).
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
