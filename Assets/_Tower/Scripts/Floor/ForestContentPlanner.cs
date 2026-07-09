using System.Collections.Generic;
using UnityEngine;

namespace Tower.Floor
{
    // Pure, engine-agnostic (no MonoBehaviour, no engine RNG) deterministic
    // generator of a node's forest content from (seed, nodeId, field). Identical
    // inputs always yield an identical ForestContentPlan; different node ids or seeds
    // diverge. The renderer turns this plan into GameObjects and grounds it onto the
    // terrain. Factored out of the renderer so it is unit-testable.
    public sealed class ForestContentPlanner
    {
        public struct Settings
        {
            public float Margin;          // keep props off the field edge
            public float PathHalfWidth;   // clear corridor around the winding path
            public float TreeDensity;     // trees per 100 sq units
            public float RockDensity;     // rocks per 100 sq units
            public int PathWaypoints;

            public static Settings Default => new Settings
            {
                Margin = 1.5f,
                PathHalfWidth = 1.8f,
                TreeDensity = 5.5f,
                RockDensity = 1.6f,
                PathWaypoints = 6
            };
        }

        public static ForestContentPlan Build(int seed, int nodeId, FloorFieldRect field)
        {
            return Build(seed, nodeId, field, Settings.Default);
        }

        public static ForestContentPlan Build(int seed, int nodeId, FloorFieldRect field, Settings settings)
        {
            DeterministicRng rng = DeterministicRng.ForSalt(seed, nodeId);
            float y = field.Center.y;

            // Winding path down the travel axis (entry -> exit), with a seeded lateral sway.
            int wpCount = Mathf.Max(2, settings.PathWaypoints);
            float sway = field.CrossWidth * rng.Range(0.12f, 0.28f);
            float phase = rng.Range(0f, Mathf.PI * 2f);
            List<Vector3> path = new List<Vector3>(wpCount);
            for (int i = 0; i < wpCount; i++)
            {
                float t = i / (float)(wpCount - 1);
                float z = Mathf.Lerp(field.MinZ + settings.Margin, field.MaxZ - settings.Margin, t);
                float x = field.Center.x + sway * Mathf.Sin(phase + t * Mathf.PI * 2f);
                path.Add(new Vector3(x, y, z));
            }

            // A tree-free clearing biased toward one side of the field.
            float clearRadius = field.CrossWidth * rng.Range(0.18f, 0.30f);
            float clearX = field.Center.x + rng.Range(-1f, 1f) * (field.CrossWidth * 0.25f);
            float clearZ = Mathf.Lerp(field.MinZ + clearRadius, field.MaxZ - clearRadius, rng.NextFloat());
            ForestClearing clearing = new ForestClearing(new Vector3(clearX, y, clearZ), clearRadius);

            float area = field.CrossWidth * field.TravelLength;
            int treeTarget = Mathf.RoundToInt(area / 100f * settings.TreeDensity);
            int rockTarget = Mathf.RoundToInt(area / 100f * settings.RockDensity);

            List<ForestProp> trees = ScatterTrees(ref rng, field, settings, clearing, path, treeTarget, y);
            List<ForestProp> rocks = ScatterRocks(ref rng, field, settings, clearing, path, rockTarget, y);
            return new ForestContentPlan(trees, rocks, clearing, path);
        }

        private static List<ForestProp> ScatterTrees(ref DeterministicRng rng, FloorFieldRect field,
            Settings settings, ForestClearing clearing, List<Vector3> path, int target, float y)
        {
            List<ForestProp> trees = new List<ForestProp>(target);
            int attempts = target * 6;
            for (int a = 0; a < attempts && trees.Count < target; a++)
            {
                float x = rng.Range(field.MinX + settings.Margin, field.MaxX - settings.Margin);
                float z = rng.Range(field.MinZ + settings.Margin, field.MaxZ - settings.Margin);
                if (clearing.Contains(x, z)) continue;
                if (NearPath(path, x, z, settings.PathHalfWidth)) continue;

                float height = rng.Range(3.2f, 6.5f);
                float radius = rng.Range(0.22f, 0.42f);
                int canopy = rng.RangeInt(1, 4);
                float yaw = rng.Range(0f, 360f);
                trees.Add(new ForestProp(ForestPropKind.Tree, new Vector3(x, y, z), radius, height, canopy, yaw));
            }

            return trees;
        }

        private static List<ForestProp> ScatterRocks(ref DeterministicRng rng, FloorFieldRect field,
            Settings settings, ForestClearing clearing, List<Vector3> path, int target, float y)
        {
            List<ForestProp> rocks = new List<ForestProp>(target);
            int attempts = target * 6;
            for (int a = 0; a < attempts && rocks.Count < target; a++)
            {
                float x = rng.Range(field.MinX + settings.Margin, field.MaxX - settings.Margin);
                float z = rng.Range(field.MinZ + settings.Margin, field.MaxZ - settings.Margin);
                // Rocks may sit at path edges but not on the walking line or in the clearing centre.
                if (NearPath(path, x, z, settings.PathHalfWidth * 0.5f)) continue;
                if (clearing.Contains(x, z) && rng.NextFloat() > 0.35f) continue;

                float radius = rng.Range(0.35f, 0.9f);
                float height = radius * rng.Range(0.5f, 0.9f);
                float yaw = rng.Range(0f, 360f);
                rocks.Add(new ForestProp(ForestPropKind.Rock, new Vector3(x, y, z), radius, height, 0, yaw));
            }

            return rocks;
        }

        private static bool NearPath(List<Vector3> path, float x, float z, float halfWidth)
        {
            float sq = halfWidth * halfWidth;
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (DistanceSqToSegment(path[i], path[i + 1], x, z) <= sq) return true;
            }

            return false;
        }

        private static float DistanceSqToSegment(Vector3 a, Vector3 b, float px, float pz)
        {
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float apx = px - a.x;
            float apz = pz - a.z;
            float denom = abx * abx + abz * abz;
            float t = denom > 0f ? (apx * abx + apz * abz) / denom : 0f;
            t = Mathf.Clamp01(t);
            float cx = a.x + abx * t;
            float cz = a.z + abz * t;
            float dx = px - cx;
            float dz = pz - cz;
            return dx * dx + dz * dz;
        }
    }
}
