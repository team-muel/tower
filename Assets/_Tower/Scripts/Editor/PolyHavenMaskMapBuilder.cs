using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tower.EditorTools
{
    public static class PolyHavenMaskMapBuilder
    {
        private const string Root = "Assets/_Tower/Art/Textures";

        private static readonly MaskMapJob[] Jobs =
        {
            new MaskMapJob(
                "asphalt_01",
                $"{Root}/asphalt_01/asphalt_01_ao.jpg",
                $"{Root}/asphalt_01/asphalt_01_rough.jpg",
                $"{Root}/asphalt_01/asphalt_01_mask.png",
                "Assets/_Tower/Art/M_Asphalt_PH.mat"),
            new MaskMapJob(
                "gravel_floor_02",
                $"{Root}/gravel_floor_02/gravel_floor_02_ao.jpg",
                $"{Root}/gravel_floor_02/gravel_floor_02_rough.jpg",
                $"{Root}/gravel_floor_02/gravel_floor_02_mask.png",
                "Assets/_Tower/Art/M_Dirt_PH.mat")
        };

        public static void BuildAll()
        {
            foreach (MaskMapJob job in Jobs)
            {
                Build(job);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PolyHavenMaskMapBuilder] Built {Jobs.Length} mask maps.");
        }

        private static void Build(MaskMapJob job)
        {
            TextureImporterState aoState = MakeReadableLinear(job.AoPath);
            TextureImporterState roughState = MakeReadableLinear(job.RoughnessPath);

            try
            {
                Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(job.AoPath);
                Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(job.RoughnessPath);
                if (ao == null || roughness == null)
                {
                    throw new InvalidOperationException($"Missing source maps for {job.AssetId}.");
                }

                int width = Mathf.Min(ao.width, roughness.width);
                int height = Mathf.Min(ao.height, roughness.height);
                Texture2D mask = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    name = $"{job.AssetId}_mask"
                };

                for (int y = 0; y < height; y++)
                {
                    float v = height == 1 ? 0f : y / (float)(height - 1);
                    for (int x = 0; x < width; x++)
                    {
                        float u = width == 1 ? 0f : x / (float)(width - 1);
                        float occlusion = ao.GetPixelBilinear(u, v).grayscale;
                        float smoothness = 1f - roughness.GetPixelBilinear(u, v).grayscale;
                        mask.SetPixel(x, y, new Color(0f, occlusion, 1f, smoothness));
                    }
                }

                mask.Apply(false, false);
                File.WriteAllBytes(job.OutputPath, mask.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(mask);
                AssetDatabase.ImportAsset(job.OutputPath, ImportAssetOptions.ForceUpdate);
                ConfigureMaskImporter(job.OutputPath);
                AssignMaterial(job);
                Debug.Log($"[PolyHavenMaskMapBuilder] {job.AssetId} -> {job.OutputPath}");
            }
            finally
            {
                aoState.Restore();
                roughState.Restore();
            }
        }

        private static TextureImporterState MakeReadableLinear(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Texture importer not found: {path}");
            }

            var state = new TextureImporterState(path, importer.isReadable, importer.sRGBTexture);
            importer.isReadable = true;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            return state;
        }

        private static void ConfigureMaskImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Mask importer not found: {path}");
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        private static void AssignMaterial(MaskMapJob job)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(job.MaterialPath);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(job.OutputPath);
            if (material == null || mask == null)
            {
                throw new InvalidOperationException($"Missing material or mask for {job.AssetId}.");
            }

            material.SetTexture("_MetallicGlossMap", mask);
            material.SetTexture("_OcclusionMap", mask);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_GlossMapScale", 1f);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
        }

        private readonly struct MaskMapJob
        {
            public MaskMapJob(string assetId, string aoPath, string roughnessPath, string outputPath, string materialPath)
            {
                AssetId = assetId;
                AoPath = aoPath;
                RoughnessPath = roughnessPath;
                OutputPath = outputPath;
                MaterialPath = materialPath;
            }

            public string AssetId { get; }
            public string AoPath { get; }
            public string RoughnessPath { get; }
            public string OutputPath { get; }
            public string MaterialPath { get; }
        }

        private readonly struct TextureImporterState
        {
            public TextureImporterState(string path, bool isReadable, bool sRgbTexture)
            {
                Path = path;
                IsReadable = isReadable;
                SRgbTexture = sRgbTexture;
            }

            private string Path { get; }
            private bool IsReadable { get; }
            private bool SRgbTexture { get; }

            public void Restore()
            {
                TextureImporter importer = AssetImporter.GetAtPath(Path) as TextureImporter;
                if (importer == null)
                {
                    return;
                }

                importer.isReadable = IsReadable;
                importer.sRGBTexture = SRgbTexture;
                importer.SaveAndReimport();
            }
        }
    }
}
