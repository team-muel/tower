using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    /// <summary>
    /// Sole source of Left Shift (bullet-time) input and press/hold timestamps for counter
    /// measurement. Timestamps deliberately use scaled Time.time.
    /// </summary>
    public sealed class SlowMoInput : MonoBehaviour
    {
        [SerializeField] private TimeDilationService timeDilation;
        [SerializeField] private PillbugBrain pillbug;
        [Header("Provisional resource tuning")]
        [SerializeField, Min(0.01f)] private float fullDrainSeconds = 2.5f;
        [SerializeField, Min(0.01f)] private float fullRechargeSeconds = 8f;
        [SerializeField, Range(0f, 1f)] private float minEngageCharge = 0.3f;
        [Header("Provisional counter tuning")]
        [SerializeField, Range(0f, 1f)] private float earlyBoundary = 0.33f;
        [SerializeField, Range(0f, 1f)] private float cleanBoundary = 0.78f;
        [SerializeField, Range(0f, 1f)] private float coverageThreshold = 0.5f;

        private SlowMoResource resource;
        private CounterWindow counterWindow;
        private bool slowMotionEngaged;
        private bool holdingSpace;
        private float? holdStartedAt;
        private float? holdEndedAt;
        private float observedCommitStart = -1f;

        public float Charge => resource == null ? 0f : resource.Charge;
        public CounterInstantResult LastInstantResult { get; private set; } = CounterInstantResult.Missed;
        public CounterCoverageResult LastCoverageResult { get; private set; } = CounterCoverageResult.Missed;

        public void SetPillbug(PillbugBrain pillbug)
        {
            this.pillbug = pillbug;
        }

        private void Awake()
        {
            if (timeDilation == null)
            {
                timeDilation = GetComponent<TimeDilationService>();
            }

            resource = new SlowMoResource(1f, fullDrainSeconds, fullRechargeSeconds, minEngageCharge);
            counterWindow = new CounterWindow(earlyBoundary, cleanBoundary, coverageThreshold);
        }

        private void OnDisable()
        {
            if (slowMotionEngaged && timeDilation != null)
            {
                timeDilation.Restore();
            }

            slowMotionEngaged = false;
        }

        private void Update()
        {
            HandleSpaceInput();
            UpdateResource();
            ObserveCommitCoverage();
        }

        private void HandleSpaceInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                holdingSpace = true;
                holdStartedAt = Time.time;
                holdEndedAt = null;
                LastInstantResult = counterWindow.ClassifyInstant(holdStartedAt, pillbug == null ? null : pillbug.Telegraph);
                Debug.Log("Slow-mo counter instant: " + LastInstantResult, this);

                if (resource.CanEngage && timeDilation != null)
                {
                    slowMotionEngaged = true;
                    timeDilation.Engage();
                }
            }

            if (Input.GetKeyUp(KeyCode.LeftShift))
            {
                holdingSpace = false;
                holdEndedAt = Time.time;
                if (slowMotionEngaged && timeDilation != null)
                {
                    timeDilation.Restore();
                }

                slowMotionEngaged = false;
            }
        }

        private void UpdateResource()
        {
            if (slowMotionEngaged)
            {
                resource.Drain(Time.unscaledDeltaTime);
                if (resource.IsDepleted)
                {
                    slowMotionEngaged = false;
                    if (timeDilation != null)
                    {
                        timeDilation.Restore();
                    }
                }

                return;
            }

            resource.Recharge(Time.unscaledDeltaTime);
        }

        private void ObserveCommitCoverage()
        {
            if (pillbug == null || pillbug.Telegraph == null)
            {
                return;
            }

            var commitStart = pillbug.Telegraph.CommitStartedAt;
            if (commitStart < 0f || Mathf.Approximately(commitStart, observedCommitStart))
            {
                return;
            }

            observedCommitStart = commitStart;
            var holdEnd = holdingSpace ? (float?)Time.time : holdEndedAt;
            LastCoverageResult = counterWindow.ClassifyCoverage(
                holdStartedAt,
                holdEnd,
                pillbug.Telegraph.WindupStartedAt,
                commitStart);
            pillbug.SetCoverageResult(LastCoverageResult);
        }
    }
}
