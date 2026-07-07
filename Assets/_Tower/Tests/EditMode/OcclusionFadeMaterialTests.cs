using NUnit.Framework;
using Tower.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tower.Tests.EditMode
{
    public sealed class OcclusionFadeMaterialTests
    {
        [Test]
        public void ConfigureTransparent_SetsAlphaAndTransparentQueue()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader, "A lit shader is required for material fade tests.");
            var material = new Material(shader);
            try
            {
                material.color = Color.white;

                OcclusionFadeMaterial.ConfigureTransparent(material, 0.25f);

                Assert.AreEqual((int)RenderQueue.Transparent, material.renderQueue);
                Assert.AreEqual(0.25f, material.color.a, 0.001f);
                if (material.HasProperty("_BaseColor"))
                {
                    Assert.AreEqual(0.25f, material.GetColor("_BaseColor").a, 0.001f);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    Assert.AreEqual(0f, material.GetFloat("_ZWrite"), 0.001f);
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Controller_FadesBlockingRendererAndRestoresWhenClear()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader, "A lit shader is required for material fade tests.");

            GameObject cameraObject = new GameObject("Fade Test Camera");
            GameObject targetObject = new GameObject("Fade Test Target");
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var original = new Material(shader);

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 5f, -5f);
                targetObject.transform.position = Vector3.zero;

                blocker.name = "Fade Test Blocker";
                blocker.transform.position = new Vector3(0f, 2.5f, -2.5f);
                blocker.transform.localScale = Vector3.one * 1.5f;
                Renderer renderer = blocker.GetComponent<Renderer>();
                original.color = Color.white;
                renderer.sharedMaterial = original;

                CameraOcclusionFadeController controller = cameraObject.AddComponent<CameraOcclusionFadeController>();
                controller.SetCamera(camera);
                controller.SetTarget(targetObject.transform);

                Physics.SyncTransforms();
                controller.RefreshOccluders();

                Assert.AreNotSame(original, renderer.sharedMaterial);
                Assert.AreEqual(0.28f, renderer.sharedMaterial.color.a, 0.001f);

                blocker.transform.position = new Vector3(5f, 5f, 5f);
                Physics.SyncTransforms();
                controller.RefreshOccluders();

                Assert.AreSame(original, renderer.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(blocker);
                Object.DestroyImmediate(original);
            }
        }
    }
}
