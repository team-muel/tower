using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // T64: the CombatSpike bullet-time (owner-approved feel, 2026-07-14)
    // ported into the run loop. Left Shift engages the T48 revolver-style
    // charge (`SlowMoResource`) through the same `TimeDilationService`.
    // Deliberately decoupled from the spike's single-pillbug CounterWindow:
    // counters stay owner-frozen; this is dilation + gauge only.
    public sealed class RunSlowMo : MonoBehaviour
    {
        [Header("Provisional resource tuning (T48 values)")]
        [SerializeField, Min(0.01f)] private float fullDrainSeconds = 2.5f;
        [SerializeField, Min(0.01f)] private float fullRechargeSeconds = 8f;
        [SerializeField, Range(0f, 1f)] private float minEngageCharge = 0.3f;

        private TimeDilationService timeDilation;
        private SlowMoResource resource;
        private bool engaged;

        public float Charge => resource == null ? 1f : resource.Charge;
        public bool IsEngaged => engaged;
        public bool CanIssuePreciseOrders => resource != null
            && resource.CanEngage
            && (engaged || Input.GetKey(KeyCode.LeftShift));

        private void Awake()
        {
            timeDilation = GetComponent<TimeDilationService>();
            if (timeDilation == null)
            {
                timeDilation = gameObject.AddComponent<TimeDilationService>();
            }

            resource = new SlowMoResource(1f, fullDrainSeconds, fullRechargeSeconds, minEngageCharge);
        }

        private void OnDisable()
        {
            Release();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) && resource.CanEngage)
            {
                engaged = true;
                timeDilation.Engage();
            }

            if (Input.GetKeyUp(KeyCode.LeftShift))
            {
                Release();
            }

            if (engaged)
            {
                resource.Drain(Time.unscaledDeltaTime);
                if (resource.IsDepleted)
                {
                    Release();
                }
            }
            else
            {
                resource.Recharge(Time.unscaledDeltaTime);
            }
        }

        private void Release()
        {
            if (engaged && timeDilation != null)
            {
                timeDilation.Restore();
            }

            engaged = false;
        }
    }
}
