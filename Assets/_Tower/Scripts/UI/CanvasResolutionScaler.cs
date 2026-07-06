using UnityEngine;
using UnityEngine.UI;

namespace Tower.UI
{
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class CanvasResolutionScaler : MonoBehaviour
    {
        private CanvasScaler canvasScaler;
        private int lastWidth;
        private int lastHeight;

        private void Awake()
        {
            canvasScaler = GetComponent<CanvasScaler>();
            UpdateScaler();
        }

        private void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                UpdateScaler();
            }
        }

        public void UpdateScaler()
        {
            UpdateScaler(Screen.width, Screen.height);
        }

        public void UpdateScaler(int width, int height)
        {
            if (canvasScaler == null)
            {
                canvasScaler = GetComponent<CanvasScaler>();
            }

            if (canvasScaler == null) return;

            lastWidth = width;
            lastHeight = height;

            if (lastHeight <= 0) return;

            float aspect = (float)lastWidth / lastHeight;

            if (canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                canvasScaler.matchWidthOrHeight = aspect >= 1.7f ? 1.0f : 0.0f;
            }
        }
    }
}
