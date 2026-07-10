using System;
using System.IO;
using Tower.Floor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tower.EditorTools
{
    public static class FloorPreviewCapture
    {
        private const string ScenePath = "Assets/_Tower/Scenes/_FloorPreview.unity";
        private const string DefaultOutputPath = @"C:\dev\_setup\floor-preview-t32.png";

        public static void Capture()
        {
            string outputPath = ReadArgument("-floorPreviewOutput", DefaultOutputPath);
            int width = ReadIntArgument("-floorPreviewWidth", 1600);
            int height = ReadIntArgument("-floorPreviewHeight", 900);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RebuildPreviewTerrain();

            Camera camera = FindCamera();
            CaptureCamera(camera, outputPath, width, height);

            Debug.Log($"[FloorPreviewCapture] Wrote {outputPath} ({width}x{height}).");
        }

        private static void RebuildPreviewTerrain()
        {
            FloorGraphTerrainPreview preview = UnityEngine.Object.FindFirstObjectByType<FloorGraphTerrainPreview>();
            if (preview == null)
            {
                throw new InvalidOperationException("FloorGraphTerrainPreview was not found in _FloorPreview.");
            }

            preview.Rebuild();
            Transform root = preview.transform.Find(FloorGraphTerrainPreview.GeneratedRootName);
            if (root == null || root.childCount == 0)
            {
                throw new InvalidOperationException("FloorGraphTerrainPreview did not create generated terrain segments.");
            }
        }

        private static Camera FindCamera()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length > 0)
            {
                return cameras[0];
            }

            GameObject cameraObject = new GameObject("FloorPreviewCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 55f;
            camera.transform.SetPositionAndRotation(
                new Vector3(12f, 58f, -42f),
                Quaternion.Euler(55f, 0f, 0f));
            return camera;
        }

        private static void CaptureCamera(Camera camera, string outputPath, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            RenderTexture renderTexture = new RenderTexture(width, height, 24);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();

                Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static string ReadArgument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }

        private static int ReadIntArgument(string name, int fallback)
        {
            string value = ReadArgument(name, string.Empty);
            if (int.TryParse(value, out int parsed) && parsed > 0)
            {
                return parsed;
            }

            return fallback;
        }
    }
}
