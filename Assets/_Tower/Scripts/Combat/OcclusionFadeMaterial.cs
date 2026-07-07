using UnityEngine;
using UnityEngine.Rendering;

namespace Tower.Combat
{
    public static class OcclusionFadeMaterial
    {
        public static void ConfigureTransparent(Material material, float alpha)
        {
            if (material == null)
            {
                return;
            }

            float clampedAlpha = Mathf.Clamp01(alpha);
            SetColorAlpha(material, "_BaseColor", clampedAlpha);
            SetColorAlpha(material, "_Color", clampedAlpha);

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        private static void SetColorAlpha(Material material, string property, float alpha)
        {
            if (!material.HasProperty(property))
            {
                return;
            }

            Color color = material.GetColor(property);
            color.a = alpha;
            material.SetColor(property, color);
        }
    }
}
