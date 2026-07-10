using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    [TestFixture]
    public sealed class ScreenSettingsTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [Test]
        public void ScreenSettings_SaveAndLoad_RoundTrips()
        {
            var settings = ScriptableObject.CreateInstance<ScreenSettingsAsset>();
            settings.targetWidth = 1920;
            settings.targetHeight = 1080;
            settings.screenMode = FullScreenMode.FullScreenWindow;
            settings.renderScale = 1.5f;

            settings.SaveToPlayerPrefs();

            var loaded = ScriptableObject.CreateInstance<ScreenSettingsAsset>();
            loaded.LoadFromPlayerPrefs();

            Assert.That(loaded.targetWidth, Is.EqualTo(1920));
            Assert.That(loaded.targetHeight, Is.EqualTo(1080));
            Assert.That(loaded.screenMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(loaded.renderScale, Is.EqualTo(1.5f));
        }
    }
}
