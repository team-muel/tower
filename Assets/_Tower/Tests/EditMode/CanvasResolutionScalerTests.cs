using NUnit.Framework;
using Tower.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tower.Tests.EditMode
{
    [TestFixture]
    public sealed class CanvasResolutionScalerTests
    {
        private GameObject go;
        private CanvasScaler scaler;
        private CanvasResolutionScaler resScaler;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Test Canvas");
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            resScaler = go.AddComponent<CanvasResolutionScaler>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void UpdateScaler_WhenWideAspect_MatchesHeight()
        {
            resScaler.UpdateScaler(2560, 1080);
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(1.0f));
        }

        [Test]
        public void UpdateScaler_WhenTallAspect_MatchesWidth()
        {
            resScaler.UpdateScaler(1024, 768);
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.0f));
        }
    }
}
