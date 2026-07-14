using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // T59: the first stair-step's parasite (공벌레) gets a real segmented body
    // built from primitives — zero asset procurement, deterministic hierarchy.
    // Shell plates arch over a low abdomen; a head with antennae marks the
    // facing so the windup->commit telegraph reads at a glance.
    public static class PillbugBodyBuilder
    {
        public const int ShellSegments = 4;
        public const int LegPairs = 3;

        public static GameObject Build(string name, Color bodyColor)
        {
            var root = new GameObject(name);

            Color shellColor = bodyColor * 0.72f;
            shellColor.a = 1f;
            Color bellyColor = Color.Lerp(bodyColor, Color.white, 0.25f);
            Material shellMaterial = TowerRuntimeMaterials.CreateLit(name + " Shell", shellColor);
            Material bodyMaterial = TowerRuntimeMaterials.CreateLit(name + " Body", bodyColor);
            Material bellyMaterial = TowerRuntimeMaterials.CreateLit(name + " Belly", bellyColor);

            // Abdomen: low flattened capsule the shell plates sit on.
            AddPart(root, PrimitiveType.Sphere, "Abdomen",
                new Vector3(0f, 0.28f, -0.05f), new Vector3(0.72f, 0.4f, 1.05f), bellyMaterial);

            // Arched shell plates, tallest in the middle, sloping to the rear.
            for (int index = 0; index < ShellSegments; index++)
            {
                float t = index / (float)(ShellSegments - 1);
                float z = 0.32f - (t * 0.82f);
                float arch = 0.5f + (0.12f * Mathf.Sin(t * Mathf.PI));
                float width = 0.86f - (t * 0.18f);
                AddPart(root, PrimitiveType.Sphere, $"Shell_{index:00}",
                    new Vector3(0f, 0.34f + (0.06f * Mathf.Sin(t * Mathf.PI)), z),
                    new Vector3(width, arch, 0.46f), shellMaterial);
            }

            // Head with two antennae; forward = +Z so telegraphs read facing.
            AddPart(root, PrimitiveType.Sphere, "Head",
                new Vector3(0f, 0.3f, 0.58f), new Vector3(0.42f, 0.34f, 0.4f), bodyMaterial);
            AddAntenna(root, "Antenna_L", new Vector3(-0.12f, 0.48f, 0.72f), -18f, bodyMaterial);
            AddAntenna(root, "Antenna_R", new Vector3(0.12f, 0.48f, 0.72f), 18f, bodyMaterial);

            // Stub legs, alternating along the abdomen.
            for (int pair = 0; pair < LegPairs; pair++)
            {
                float z = 0.28f - (pair * 0.34f);
                AddLeg(root, $"Leg_L{pair}", new Vector3(-0.4f, 0.12f, z), bodyMaterial);
                AddLeg(root, $"Leg_R{pair}", new Vector3(0.4f, 0.12f, z), bodyMaterial);
            }

            // One root collider replaces the per-primitive colliders.
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.center = new Vector3(0f, 0.34f, 0f);
            collider.radius = 0.55f;
            return root;
        }

        private static void AddPart(
            GameObject root,
            PrimitiveType primitive,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            StripCollider(part);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void AddAntenna(GameObject root, string partName, Vector3 localPosition, float rollDegrees, Material material)
        {
            GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antenna.name = partName;
            StripCollider(antenna);
            antenna.transform.SetParent(root.transform, false);
            antenna.transform.localPosition = localPosition;
            antenna.transform.localScale = new Vector3(0.04f, 0.16f, 0.04f);
            antenna.transform.localRotation = Quaternion.Euler(35f, 0f, rollDegrees);
            Renderer renderer = antenna.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void AddLeg(GameObject root, string partName, Vector3 localPosition, Material material)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = partName;
            StripCollider(leg);
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = localPosition;
            leg.transform.localScale = new Vector3(0.06f, 0.12f, 0.06f);
            leg.transform.localRotation = Quaternion.Euler(0f, 0f, localPosition.x < 0f ? 24f : -24f);
            Renderer renderer = leg.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void StripCollider(GameObject part)
        {
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(collider);
                }
                else
                {
                    Object.DestroyImmediate(collider);
                }
            }
        }
    }
}
