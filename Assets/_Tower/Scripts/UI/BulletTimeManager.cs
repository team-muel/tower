using System;
using UnityEngine;

namespace Tower.UI
{
    public sealed class BulletTimeManager : MonoBehaviour
    {
        [Header("Bullet Time Settings")]
        [SerializeField] private float bulletTimeScale = 0.05f;
        [SerializeField] private float transitionSpeed = 10f;

        private float targetTimeScale = 1.0f;
        private float currentTimeScale = 1.0f;

        public event Action<bool> OnBulletTimeToggled;
        private bool isBulletTimeActive;

        private void Update()
        {
            HandleInput();
            SmoothTimeScale();
        }

        private void HandleInput()
        {
            bool isPressed = Input.GetKey(KeyCode.Space);

            if (isPressed != isBulletTimeActive)
            {
                isBulletTimeActive = isPressed;
                targetTimeScale = isBulletTimeActive ? bulletTimeScale : 1.0f;

                // Adjust fixedDeltaTime so physics updates stay synced with timescale
                Time.fixedDeltaTime = 0.02f * targetTimeScale;

                OnBulletTimeToggled?.Invoke(isBulletTimeActive);
            }
        }

        private void SmoothTimeScale()
        {
            currentTimeScale = Mathf.MoveTowards(currentTimeScale, targetTimeScale, transitionSpeed * Time.unscaledDeltaTime);
            Time.timeScale = currentTimeScale;
        }

        private void OnDestroy()
        {
            // Reset timescale on destroy to prevent editor stuck
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
