using System.Collections.Generic;
using Tower.Core;
using Tower.Gen;
using UnityEngine;

namespace Tower.Floor
{
    // T41 (M4): deterministic placement of T29 interaction anchors into a node's
    // forest segment. Pure, engine-agnostic-of-MonoBehaviour (uses Vector3/Mathf
    // only, like ForestContentPlanner) so it is unit-testable. Given
    // (seed, node, field, clearing) it maps the node's role -> a set of
    // InteractableDefs (built via the Core factory) and places them in the
    // tree-free clearing so they never overlap scattered props.
    public static class NodeAnchorPlanner
    {
        public static NodeAnchorPlan Build(int seed, FloorNode node, FloorFieldRect field, ForestClearing clearing)
        {
            DeterministicRng rng = DeterministicRng.ForSalt(seed, unchecked(node.Id ^ 0x5EED));
            List<InteractableDef> defs = new List<InteractableDef>();
            AppendRoleDefs(ref rng, node, defs);
            IReadOnlyList<PlacedAnchor> placed = PlaceAnchors(ref rng, field, clearing, defs);
            return new NodeAnchorPlan(placed);
        }

        // Role -> anchor kinds (v0, per 77 §1). Entrance = orient; Camp = 오브 +
        // 묘비(after-death); Boss = 보상 상자; Exit = 회복 자원; Normal = seeded mix.
        private static void AppendRoleDefs(ref DeterministicRng rng, FloorNode node, List<InteractableDef> defs)
        {
            if (node.IsEntrance || node.Kind == RoomKind.Entrance)
            {
                Add(defs, node, InteractableKind.Inspect, "숲의 초입을 살핀다", maxUses: -1, reward: "지형 파악");
                if (rng.NextFloat() < 0.5f)
                {
                    Add(defs, node, InteractableKind.Resource, "약초를 캔다", reward: "회복");
                }

                return;
            }

            if (node.Kind == RoomKind.Camp)
            {
                Add(defs, node, InteractableKind.Shrine, "오브를 조사한다", reward: "기억의 단서");
                Add(defs, node, InteractableKind.Grave, "묘비 앞에 선다",
                    visibility: VisibilityRule.AfterDeathOnly, disabled: "아직 기려야 할 이가 없다");
                return;
            }

            if (node.IsBossRoom || node.Kind == RoomKind.Boss)
            {
                Add(defs, node, InteractableKind.Chest, "보스의 유물을 연다", reward: "희귀 보상");
                return;
            }

            if (node.IsExit || node.Kind == RoomKind.Exit)
            {
                Add(defs, node, InteractableKind.Resource, "샘물을 마신다", reward: "회복");
                return;
            }

            // Normal: 1-2 anchors from {Chest, Resource, Trap}, seeded.
            int count = rng.RangeInt(1, 3);
            for (int i = 0; i < count; i++)
            {
                switch (rng.RangeInt(0, 3))
                {
                    case 0:
                        Add(defs, node, InteractableKind.Chest, "상자를 연다", reward: "전리품");
                        break;
                    case 1:
                        Add(defs, node, InteractableKind.Resource, "흔적을 줍는다", reward: "재화");
                        break;
                    default:
                        Add(defs, node, InteractableKind.Trap, "함정을 해체한다", risk: "밟으면 피해");
                        break;
                }
            }
        }

        private static void Add(List<InteractableDef> defs, FloorNode node, InteractableKind kind, string prompt,
            int maxUses = 1, string reward = "", string risk = "",
            VisibilityRule visibility = VisibilityRule.Always, string disabled = "")
        {
            string id = $"n{node.Id:00}_{kind}_{defs.Count}";
            Result<InteractableDef> r = InteractableDef.Create(
                id,
                kind,
                prompt,
                disabledReason: disabled,
                visibilityRule: visibility,
                riskPreview: risk,
                rewardPreview: reward,
                maxUses: maxUses);
            if (r.IsSuccess)
            {
                defs.Add(r.Value);
            }
        }

        // Featured anchor sits at the clearing centre; extras ring it inside the
        // clearing radius. Every position is clamped into the field with a margin
        // so no anchor escapes the walkable rect (unit-tested invariant).
        private static IReadOnlyList<PlacedAnchor> PlaceAnchors(ref DeterministicRng rng, FloorFieldRect field,
            ForestClearing clearing, List<InteractableDef> defs)
        {
            List<PlacedAnchor> placed = new List<PlacedAnchor>(defs.Count);
            float baseY = field.Center.y;
            const float margin = 1.2f;
            for (int i = 0; i < defs.Count; i++)
            {
                Vector3 pos;
                if (i == 0)
                {
                    pos = new Vector3(clearing.Center.x, baseY, clearing.Center.z);
                }
                else
                {
                    float ang = rng.Range(0f, Mathf.PI * 2f);
                    float rad = clearing.Radius * rng.Range(0.35f, 0.85f);
                    pos = new Vector3(
                        clearing.Center.x + Mathf.Cos(ang) * rad,
                        baseY,
                        clearing.Center.z + Mathf.Sin(ang) * rad);
                }

                pos.x = Mathf.Clamp(pos.x, field.MinX + margin, field.MaxX - margin);
                pos.z = Mathf.Clamp(pos.z, field.MinZ + margin, field.MaxZ - margin);
                placed.Add(new PlacedAnchor(defs[i], pos));
            }

            return placed;
        }
    }
}
