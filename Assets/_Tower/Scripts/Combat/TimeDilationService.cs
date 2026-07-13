using UnityEngine;

namespace Tower.Combat
{
    /// <summary>
    /// The only writer of Unity's global time scale for the slow-mo spike.
    /// Keeping fixedDeltaTime in lockstep avoids a separate physics cadence.
    /// </summary>
    public sealed class TimeDilationService : MonoBehaviour
    {
        [SerializeField, Range(0.01f, 1f)] private float slowTimeScale = 0.25f;

        private float baseFixedDeltaTime;

        public float Current { get; private set; } = 1f;

        private void Awake()
        {
            baseFixedDeltaTime = Time.fixedDeltaTime;
            Restore();
        }

        private void OnDisable()
        {
            Restore();
        }

        public void Engage()
        {
            SetScale(slowTimeScale);
        }

        public void Restore()
        {
            SetScale(1f);
        }

        private void SetScale(float scale)
        {
            Current = Mathf.Clamp(scale, 0.01f, 1f);
            Time.timeScale = Current;
            Time.fixedDeltaTime = baseFixedDeltaTime * Current;
        }
    }
}
