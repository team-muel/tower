using System;
using System.Collections.Generic;
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

            FloorEncounter encounter = ComposeEncounter(parameters, node, width, height);
            return new FloorNodeContent(node.Id, battlefield, encounter);
        }

        private static FloorEncounter ComposeEncounter(FloorGenParams parameters, FloorNode node, int width, int height)
        {
            if (node.IsEntrance || node.Kind == RoomKind.Entrance || node.Kind == RoomKind.Camp)
            {
                return FloorEncounter.None();
            }

            if (node.IsBossRoom || node.Kind == RoomKind.Boss)
            {
                return new FloorEncounter(true, 1, new[] { new FloorEnemySlot(0, parameters.BossKindSlot) });
            }

            int sizeBonus = Math.Max(0, ((width * height) - 64) / 64);
            int enemyCount = Clamp(1 + node.Depth + sizeBonus, 1, 5);
            List<FloorEnemySlot> slots = new List<FloorEnemySlot>();
            for (int i = 0; i < enemyCount; i++)
            {
                int slotIndex = (node.Id + node.Depth + width + height + i) % parameters.EnemyKindSlots.Count;
                slots.Add(new FloorEnemySlot(i, parameters.EnemyKindSlots[slotIndex]));
            }

            return new FloorEncounter(false, enemyCount, slots);
        }

        private static int NextInclusive(Random random, int min, int max)
        {
            return random.Next(min, max + 1);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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
