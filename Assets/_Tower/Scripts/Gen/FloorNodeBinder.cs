using System;
using Tower.Core;

namespace Tower.Gen
{
    // 노드 내용의 lazy 결정적 바인더. 골격(FloorGraph)은 조우를 들지 않기 때문에
    // 소비자(플레이 컨트롤러)가 조우 자리에 들어설 때 이 바인더로 조우를 만든다.
    // 조우 조합은 FloorEncounterComposer의 (graph.Seed, node.Id, node.Depth) 해시만으로
    // 결정 → 같은 씨드는 같은 내용. (전투 격자 GridMap은 T49 철거로 생성하지 않는다.)
    public static class FloorNodeBinder
    {
        public static FloorNodeContent Bind(FloorGraph graph, FloorNode node, FloorGenParams parameters)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            FloorEncounter encounter = ComposeEncounter(parameters, graph, node);
            return new FloorNodeContent(node.Id, encounter);
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
    }
}
