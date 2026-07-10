using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tower.Core
{
    [CreateAssetMenu(fileName = "ScreenSettingsAsset", menuName = "Tower/ScreenSettingsAsset")]
    public sealed class ScreenSettingsAsset : ScriptableObject
    {
        public int targetWidth = 1600;
        public int targetHeight = 900;
        public FullScreenMode screenMode = FullScreenMode.Windowed;
        public float renderScale = 1.0f;

        public void ApplySettings()
        {
            Screen.SetResolution(targetWidth, targetHeight, screenMode);

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
            {
                urpAsset.renderScale = Mathf.Clamp(renderScale, 0.5f, 2.0f);
            }
        }

        public void SaveToPlayerPrefs()
        {
            PlayerPrefs.SetInt("tower.screen.width", targetWidth);
            PlayerPrefs.SetInt("tower.screen.height", targetHeight);
            PlayerPrefs.SetInt("tower.screen.mode", (int)screenMode);
            PlayerPrefs.SetFloat("tower.screen.renderscale", renderScale);
            PlayerPrefs.Save();
        }

        public void LoadFromPlayerPrefs()
        {
            targetWidth = PlayerPrefs.GetInt("tower.screen.width", 1600);
            targetHeight = PlayerPrefs.GetInt("tower.screen.height", 900);
            screenMode = (FullScreenMode)PlayerPrefs.GetInt("tower.screen.mode", (int)FullScreenMode.Windowed);
            renderScale = PlayerPrefs.GetFloat("tower.screen.renderscale", 1.0f);
        }
    }
}
