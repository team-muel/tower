using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public readonly struct CombatPresentationDamageEvent
    {
        public CombatPresentationDamageEvent(CombatDamageEvent damageEvent)
        {
            SourceUnitId = damageEvent.SourceUnitId ?? string.Empty;
            TargetUnitId = damageEvent.TargetUnitId ?? string.Empty;
            AbilityId = damageEvent.AbilityId ?? string.Empty;
            Damage = damageEvent.Damage;
            TargetDefeated = damageEvent.TargetDefeated;
        }

        public string SourceUnitId { get; }
        public string TargetUnitId { get; }
        public string AbilityId { get; }
        public int Damage { get; }
        public bool TargetDefeated { get; }
    }

    // Keeps Core simulation and Unity presentation separate. The buffer is
    // deterministic and can be drained after each fixed combat step.
    public sealed class CombatPresentationEventBuffer
    {
        private readonly List<CombatPresentationDamageEvent> damageEvents =
            new List<CombatPresentationDamageEvent>();

        public int PendingDamageCount => damageEvents.Count;

        public void RecordDamage(CombatDamageEvent damageEvent)
        {
            if (damageEvent.Damage <= 0)
            {
                return;
            }

            damageEvents.Add(new CombatPresentationDamageEvent(damageEvent));
        }

        public IReadOnlyList<CombatPresentationDamageEvent> DrainDamageEvents()
        {
            if (damageEvents.Count == 0)
            {
                return new CombatPresentationDamageEvent[0];
            }

            var drained = new List<CombatPresentationDamageEvent>(damageEvents);
            damageEvents.Clear();
            return drained;
        }

        public void Clear()
        {
            damageEvents.Clear();
        }
    }

    // T70: forwards Core observer callbacks to both combat metrics and the
    // presentation queue. The wrapper changes no simulation result.
    public sealed class CombatPresentationObserver : ICombatObserver
    {
        public CombatPresentationObserver(CombatMetrics metrics = null)
        {
            Metrics = metrics ?? new CombatMetrics();
            Events = new CombatPresentationEventBuffer();
        }

        public CombatMetrics Metrics { get; }
        public CombatPresentationEventBuffer Events { get; }

        public void OnCombatStarted(CombatState state)
        {
            Metrics.OnCombatStarted(state);
        }

        public void OnAbilityResolved(CombatState state, UseAbilityCommand command)
        {
            if (command != null)
            {
                Metrics.OnAbilityResolved(state, command);
            }
        }

        public void OnDamageApplied(CombatState state, CombatDamageEvent damageEvent)
        {
            Metrics.OnDamageApplied(state, damageEvent);
            Events.RecordDamage(damageEvent);
        }

        public void OnCombatEnded(CombatState state)
        {
            Metrics.OnCombatEnded(state);
        }
    }

    // Provisional world-space hit feedback. It uses local unscaled animation
    // only: no global timeScale write, no Core mutation, and no external tween
    // package. The final art pass can replace the TextMesh/pulse primitives
    // without changing the observer contract.
    public sealed class CombatFeedbackPresenter : MonoBehaviour
    {
        private const float BasePopupLifetime = 0.5f;
        private const float PopupRise = 0.62f;
        private const float PulseLifetime = 0.14f;

        private readonly Dictionary<string, Transform> bodies =
            new Dictionary<string, Transform>();
        private readonly Dictionary<string, Pulse> pulses =
            new Dictionary<string, Pulse>();
        private readonly List<Popup> popups = new List<Popup>();
        private readonly AbilityFeelResolver feelResolver = new AbilityFeelResolver();

        private AbilityFeelCatalog feelCatalog = AbilityFeelCatalog.Empty;

        public int ActivePopupCount => popups.Count;

        public void Configure(
            IReadOnlyDictionary<string, Transform> targetBodies,
            IEnumerable<CombatantRef> combatants)
        {
            bodies.Clear();
            if (targetBodies != null)
            {
                foreach (KeyValuePair<string, Transform> entry in targetBodies)
                {
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Value != null)
                    {
                        bodies[entry.Key] = entry.Value;
                    }
                }
            }

            feelCatalog = AbilityFeelCatalog.FromCombatants(combatants);
        }

        public void Present(IReadOnlyList<CombatPresentationDamageEvent> damageEvents)
        {
            if (damageEvents == null)
            {
                return;
            }

            for (int index = 0; index < damageEvents.Count; index++)
            {
                CombatPresentationDamageEvent damageEvent = damageEvents[index];
                if (!bodies.TryGetValue(damageEvent.TargetUnitId, out Transform target)
                    || target == null)
                {
                    continue;
                }

                CombatDamageEvent coreEvent = new CombatDamageEvent(
                    damageEvent.SourceUnitId,
                    damageEvent.TargetUnitId,
                    damageEvent.AbilityId,
                    damageEvent.Damage,
                    damageEvent.TargetDefeated);
                ResolvedAbilityFeel feel = feelResolver.ResolveDamageFeel(coreEvent, feelCatalog);
                CreatePopup(target, damageEvent.Damage, feel);
                PulseBody(damageEvent.TargetUnitId, target, feel.ShakeIntensity);
            }
        }

        public void Clear()
        {
            for (int index = popups.Count - 1; index >= 0; index--)
            {
                DestroyPresentationObject(popups[index].Object);
            }

            popups.Clear();
            foreach (KeyValuePair<string, Pulse> entry in pulses)
            {
                if (entry.Value.Body != null)
                {
                    entry.Value.Body.localScale = entry.Value.BaseScale;
                }
            }

            pulses.Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Update()
        {
            float deltaSeconds = Time.unscaledDeltaTime;
            UpdatePopups(deltaSeconds);
            UpdatePulses(deltaSeconds);
        }

        private void CreatePopup(Transform target, int damage, ResolvedAbilityFeel feel)
        {
            GameObject popupObject = new GameObject("DamagePopup");
            popupObject.transform.SetParent(transform, true);
            popupObject.transform.position = target.position + (Vector3.up * 1.8f);

            TextMesh text = popupObject.AddComponent<TextMesh>();
            text.text = "-" + damage;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = feel.PopupStyle == DamagePopupStyle.Normal ? 34 : 42;
            text.characterSize = feel.PopupStyle == DamagePopupStyle.Normal ? 0.075f : 0.095f;
            text.fontStyle = feel.PopupStyle == DamagePopupStyle.Crit
                ? FontStyle.Bold
                : FontStyle.Normal;
            text.color = PopupColor(feel.PopupStyle);

            float lifetime = BasePopupLifetime + Mathf.Clamp(feel.HitstopMs / 1000f, 0.05f, 0.2f);
            float rise = feel.ApproachTween == AbilityApproachTween.Projectile
                ? PopupRise * 1.25f
                : PopupRise;
            popups.Add(new Popup(popupObject, text, lifetime, rise));
        }

        private void PulseBody(string unitId, Transform body, float shakeIntensity)
        {
            if (!pulses.TryGetValue(unitId, out Pulse pulse) || pulse.Body == null)
            {
                pulse = new Pulse(body, body.localScale);
                pulses[unitId] = pulse;
            }

            pulse.Remaining = PulseLifetime;
            pulse.Amplitude = Mathf.Max(
                pulse.Amplitude,
                Mathf.Clamp01(shakeIntensity) * 0.18f);
        }

        private void UpdatePopups(float deltaSeconds)
        {
            Camera camera = Camera.main;
            for (int index = popups.Count - 1; index >= 0; index--)
            {
                Popup popup = popups[index];
                popup.Remaining -= Mathf.Max(0f, deltaSeconds);
                float progress = popup.Lifetime <= 0f
                    ? 1f
                    : Mathf.Clamp01(1f - (popup.Remaining / popup.Lifetime));
                popup.Object.transform.position = popup.Origin + (Vector3.up * (popup.Rise * progress));
                if (camera != null)
                {
                    popup.Object.transform.rotation = Quaternion.LookRotation(
                        camera.transform.position - popup.Object.transform.position,
                        Vector3.up);
                }

                Color color = popup.BaseColor;
                color.a = 1f - progress;
                popup.Text.color = color;
                if (popup.Remaining > 0f)
                {
                    continue;
                }

                DestroyPresentationObject(popup.Object);
                popups.RemoveAt(index);
            }
        }

        private void UpdatePulses(float deltaSeconds)
        {
            foreach (KeyValuePair<string, Pulse> entry in new List<KeyValuePair<string, Pulse>>(pulses))
            {
                Pulse pulse = entry.Value;
                if (pulse.Body == null)
                {
                    pulses.Remove(entry.Key);
                    continue;
                }

                pulse.Remaining -= Mathf.Max(0f, deltaSeconds);
                if (pulse.Remaining <= 0f)
                {
                    pulse.Body.localScale = pulse.BaseScale;
                    pulses.Remove(entry.Key);
                    continue;
                }

                float progress = 1f - (pulse.Remaining / PulseLifetime);
                float envelope = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
                pulse.Body.localScale = pulse.BaseScale * (1f + (pulse.Amplitude * envelope));
            }
        }

        private static Color PopupColor(DamagePopupStyle style)
        {
            switch (style)
            {
                case DamagePopupStyle.Crit: return new Color(1f, 0.3f, 0.16f, 1f);
                case DamagePopupStyle.Consume: return new Color(1f, 0.75f, 0.18f, 1f);
                case DamagePopupStyle.Heal: return new Color(0.35f, 1f, 0.5f, 1f);
                default: return Color.white;
            }
        }

        private static void DestroyPresentationObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class Popup
        {
            public Popup(GameObject popupObject, TextMesh text, float lifetime, float rise)
            {
                Object = popupObject;
                Text = text;
                Origin = popupObject.transform.position;
                Lifetime = lifetime;
                Remaining = lifetime;
                Rise = rise;
                BaseColor = text.color;
            }

            public GameObject Object { get; }
            public TextMesh Text { get; }
            public Vector3 Origin { get; }
            public float Lifetime { get; }
            public float Remaining { get; set; }
            public float Rise { get; }
            public Color BaseColor { get; }
        }

        private sealed class Pulse
        {
            public Pulse(Transform body, Vector3 baseScale)
            {
                Body = body;
                BaseScale = baseScale;
            }

            public Transform Body { get; }
            public Vector3 BaseScale { get; }
            public float Remaining { get; set; }
            public float Amplitude { get; set; }
        }
    }
}
