using UnityEngine;

namespace Tower.Core
{
    // Base material for runtime-created primitives. Lives in Resources so URP
    // Lit survives build-time shader stripping (code-generated scenes reference
    // no materials of their own; a bare Shader.Find is null in the player).
    public static class TowerRuntimeMaterials
    {
        public const string RuntimeLitName = "TowerRuntimeLit";

        private static Material _baseLit;

        public static Material CreateLit(string name, Color color)
        {
            if (_baseLit == null)
            {
                _baseLit = Resources.Load<Material>(RuntimeLitName);
            }

            var material = _baseLit != null
                ? new Material(_baseLit)
                : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            return material;
        }
    }
}
