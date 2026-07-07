using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public sealed class CombatJuicePresenter : MonoBehaviour, IActionPresenter, ICombatObserver, ICombatModePresenter
    {
        private const float LungeDistance = 0.42f;
        private const float LungeSeconds = 0.18f;
        private const float ProjectileSeconds = 0.24f;
        private const float ShakeSeconds = 0.18f;
        private const float PopupSeconds = 0.78f;

        private readonly AbilityFeelResolver feelResolver = new AbilityFeelResolver();
        private readonly Dictionary<string, UnitToken> tokenBuffer = new Dictionary<string, UnitToken>(StringComparer.Ordinal);

        private Action<string> logSink;
        private IReadOnlyDictionary<string, UnitToken> tokens;
        private AbilityFeelCatalog catalog;
        private Camera sceneCamera;
        private FixedIsoFollowCameraRig cameraRig;
        private TurnEngine engine;
        private GameObject pendingMarker;
        private TextMeshPro pendingText;
        private string pendingMarkerUnitId;
        private string pendingMarkerAbilityId;
        private float hitstopUntilRealtime;
        private float cameraShakeUntilRealtime;
        private float cameraShakeIntensity;

        public float PlaybackFactor { get; set; } = 1f;

        public void Initialize(
            Action<string> logSink,
            IReadOnlyDictionary<string, UnitToken> tokens,
            Camera sceneCamera,
            FixedIsoFollowCameraRig cameraRig,
            AbilityFeelCatalog catalog)
        {
            this.logSink = logSink;
            this.tokens = tokens;
            this.sceneCamera = sceneCamera;
            this.cameraRig = cameraRig;
            this.catalog = catalog ?? AbilityFeelCatalog.Empty;
        }

        public void SetMode(string mode)
        {
            if (!string.IsNullOrEmpty(mode))
            {
                logSink?.Invoke("[MODE] " + mode);
            }
        }

        public void Present(TurnPresentationEvent presentationEvent, Action completion)
        {
            logSink?.Invoke(FormatLog(presentationEvent));

            if (presentationEvent.Type == TurnPresentationEventType.Ability)
            {
                var feel = feelResolver.ResolveTurnFeel(presentationEvent, catalog);
                StartCoroutine(PlayAbilityTween(presentationEvent, feel));
            }

            completion?.Invoke();
        }

        public void OnCombatStarted(TurnEngine engine)
        {
            this.engine = engine;
            RefreshPendingTelegraph();
        }

        public void OnRoundStarted(TurnEngine engine, int roundNumber, IReadOnlyList<string> roundOrder)
        {
            this.engine = engine;
            RefreshPendingTelegraph();
        }

        public void OnCommandCommitted(TurnEngine engine, TurnCommand command)
        {
            this.engine = engine;
            RefreshPendingTelegraph();
        }

        public void OnDamageApplied(TurnEngine engine, CombatDamageEvent damageEvent)
        {
            this.engine = engine;
            var feel = feelResolver.ResolveDamageFeel(damageEvent, catalog);
            BeginHitstop(feel.HitstopMs);
            BeginCameraShake(feel.ShakeIntensity);

            if (TryGetToken(damageEvent.TargetUnitId, out var target))
            {
                StartCoroutine(ShakeToken(target, feel.ShakeIntensity));
                SpawnDamagePopup(target.transform.position + Vector3.up * 1.45f, damageEvent.Damage, feel.PopupStyle);
                if (feel.PopupStyle == DamagePopupStyle.Consume)
                {
                    SpawnConsumeBurst(target.transform.position + Vector3.up * 0.22f, feel.ShakeIntensity);
                }
            }
        }

        public void OnCombatEnded(TurnEngine engine)
        {
            this.engine = engine;
            HidePendingTelegraph();
            if (cameraRig != null)
            {
                cameraRig.SetPresentationOffset(Vector3.zero);
            }
        }

        private void Update()
        {
            RefreshPendingTelegraph();
        }

        private void LateUpdate()
        {
            if (cameraRig == null)
            {
                return;
            }

            if (Time.realtimeSinceStartup >= cameraShakeUntilRealtime || cameraShakeIntensity <= 0f)
            {
                cameraShakeIntensity = 0f;
                cameraRig.SetPresentationOffset(Vector3.zero);
                return;
            }

            var t = Time.realtimeSinceStartup * 73.17f;
            var offset = new Vector3(
                Mathf.Sin(t) * cameraShakeIntensity,
                Mathf.Sin(t * 1.37f) * cameraShakeIntensity * 0.45f,
                Mathf.Cos(t * 1.11f) * cameraShakeIntensity);
            cameraRig.SetPresentationOffset(offset);
        }

        private void OnDestroy()
        {
            if (cameraRig != null)
            {
                cameraRig.SetPresentationOffset(Vector3.zero);
            }
        }

        private IEnumerator PlayAbilityTween(TurnPresentationEvent presentationEvent, ResolvedAbilityFeel feel)
        {
            if (!TryGetToken(presentationEvent.UnitId, out var source))
            {
                yield break;
            }

            TryGetToken(presentationEvent.TargetUnitId, out var target);
            if (feel.ApproachTween == AbilityApproachTween.Projectile && target != null)
            {
                yield return PlayProjectile(source.transform.position + Vector3.up * 0.65f, target.transform.position + Vector3.up * 0.9f, feel);
                yield break;
            }

            if (feel.ApproachTween == AbilityApproachTween.Lunge && target != null)
            {
                yield return PlayLunge(source.transform, target.transform.position);
            }
        }

        private IEnumerator PlayLunge(Transform source, Vector3 targetWorld)
        {
            var origin = source.position;
            var flat = targetWorld - origin;
            flat.y = 0f;
            if (flat.sqrMagnitude <= 0.0001f)
            {
                yield break;
            }

            var lungeTo = origin + flat.normalized * LungeDistance;
            var half = LungeSeconds * 0.5f;
            yield return TweenPosition(source, origin, lungeTo, half);
            yield return TweenPosition(source, lungeTo, origin, half);
        }

        private IEnumerator TweenPosition(Transform target, Vector3 from, Vector3 to, float seconds)
        {
            var elapsed = 0f;
            while (target != null && elapsed < seconds)
            {
                elapsed += LocalDeltaTime();
                var t = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
                t = t * t * (3f - (2f * t));
                target.position = Vector3.Lerp(from, to, t);
                yield return null;
            }

            if (target != null)
            {
                target.position = to;
            }
        }

        private IEnumerator PlayProjectile(Vector3 from, Vector3 to, ResolvedAbilityFeel feel)
        {
            var lineObject = new GameObject("Ability Projectile Line");
            var line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = feel.PopupStyle == DamagePopupStyle.Consume ? 0.075f : 0.045f;
            line.endWidth = 0.01f;
            line.sharedMaterial = CreateLineMaterial(StyleColor(feel.PopupStyle));

            var elapsed = 0f;
            while (elapsed < ProjectileSeconds)
            {
                elapsed += LocalDeltaTime();
                var t = ProjectileSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / ProjectileSeconds);
                var head = Vector3.Lerp(from, to, t);
                var tail = Vector3.Lerp(from, to, Mathf.Max(0f, t - 0.22f));
                line.SetPosition(0, tail);
                line.SetPosition(1, head);
                yield return null;
            }

            Destroy(lineObject);
        }

        private IEnumerator ShakeToken(UnitToken token, float intensity)
        {
            if (token == null || intensity <= 0f)
            {
                yield break;
            }

            var origin = token.transform.position;
            var elapsed = 0f;
            while (token != null && elapsed < ShakeSeconds)
            {
                elapsed += LocalDeltaTime();
                var fade = 1f - Mathf.Clamp01(elapsed / ShakeSeconds);
                var t = Time.realtimeSinceStartup * 97.31f;
                var offset = new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t * 1.29f)) * intensity * fade;
                token.transform.position = origin + offset;
                yield return null;
            }

            if (token != null)
            {
                token.transform.position = origin;
            }
        }

        private void SpawnDamagePopup(Vector3 worldPosition, int amount, DamagePopupStyle style)
        {
            var popup = new GameObject("Damage Popup");
            popup.transform.position = worldPosition;
            popup.transform.localScale = Vector3.one * PopupScale(style);

            var text = popup.AddComponent<TextMeshPro>();
            text.text = style == DamagePopupStyle.Heal ? "+" + amount.ToString() : amount.ToString();
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.fontSize = 4f;
            text.color = StyleColor(style);

            StartCoroutine(PopupRoutine(popup.transform, text, style));
        }

        private IEnumerator PopupRoutine(Transform popup, TextMeshPro text, DamagePopupStyle style)
        {
            var start = popup.position;
            var end = start + Vector3.up * (style == DamagePopupStyle.Consume ? 1.25f : 0.9f);
            var elapsed = 0f;
            while (popup != null && elapsed < PopupSeconds)
            {
                elapsed += LocalDeltaTime();
                var t = PopupSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / PopupSeconds);
                popup.position = Vector3.Lerp(start, end, t);
                FaceCamera(popup);
                if (text != null)
                {
                    var c = text.color;
                    c.a = 1f - t;
                    text.color = c;
                }

                yield return null;
            }

            if (popup != null)
            {
                Destroy(popup.gameObject);
            }
        }

        private void SpawnConsumeBurst(Vector3 worldPosition, float intensity)
        {
            var burst = new GameObject("Consume Burst");
            burst.transform.position = worldPosition;
            var line = burst.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 28;
            line.startWidth = 0.045f + intensity * 0.05f;
            line.endWidth = line.startWidth;
            line.sharedMaterial = CreateLineMaterial(StyleColor(DamagePopupStyle.Consume));
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }

            StartCoroutine(ConsumeBurstRoutine(burst.transform, line, intensity));
        }

        private IEnumerator ConsumeBurstRoutine(Transform burst, LineRenderer line, float intensity)
        {
            var elapsed = 0f;
            const float seconds = 0.42f;
            while (burst != null && elapsed < seconds)
            {
                elapsed += LocalDeltaTime();
                var t = Mathf.Clamp01(elapsed / seconds);
                burst.localScale = Vector3.one * Mathf.Lerp(0.15f, 1.25f + intensity, t);
                if (line != null)
                {
                    var color = StyleColor(DamagePopupStyle.Consume);
                    color.a = 1f - t;
                    line.startColor = color;
                    line.endColor = color;
                }

                yield return null;
            }

            if (burst != null)
            {
                Destroy(burst.gameObject);
            }
        }

        private void RefreshPendingTelegraph()
        {
            if (engine == null || engine.CurrentTurn == null || string.IsNullOrWhiteSpace(engine.PendingAbilityId))
            {
                HidePendingTelegraph();
                return;
            }

            var unitId = engine.CurrentTurn.UnitId;
            if (!TryGetToken(unitId, out var token) || token == null || !token.gameObject.activeInHierarchy)
            {
                HidePendingTelegraph();
                return;
            }

            EnsurePendingMarker();
            if (!StringComparer.Ordinal.Equals(pendingMarkerUnitId, unitId))
            {
                pendingMarker.transform.SetParent(token.transform, false);
                pendingMarker.transform.localPosition = Vector3.up * 1.1f;
                pendingMarkerUnitId = unitId;
            }

            if (!StringComparer.Ordinal.Equals(pendingMarkerAbilityId, engine.PendingAbilityId))
            {
                pendingMarkerAbilityId = engine.PendingAbilityId;
                pendingText.text = engine.PendingAbilityId;
            }

            pendingMarker.SetActive(true);
            FaceCamera(pendingText.transform);
        }

        private void EnsurePendingMarker()
        {
            if (pendingMarker != null)
            {
                return;
            }

            pendingMarker = new GameObject("Pending Ability Telegraph");
            var lineObject = new GameObject("Ring");
            lineObject.transform.SetParent(pendingMarker.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 24;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.sharedMaterial = CreateLineMaterial(new Color(1f, 0.82f, 0.22f, 0.9f));
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f));
            }

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(pendingMarker.transform, false);
            textObject.transform.localPosition = Vector3.up * 0.15f;
            textObject.transform.localScale = Vector3.one * 0.06f;
            pendingText = textObject.AddComponent<TextMeshPro>();
            pendingText.alignment = TextAlignmentOptions.Center;
            pendingText.enableWordWrapping = false;
            pendingText.fontSize = 3.2f;
            pendingText.color = new Color(1f, 0.95f, 0.55f, 1f);
        }

        private void HidePendingTelegraph()
        {
            pendingMarkerUnitId = null;
            pendingMarkerAbilityId = null;
            if (pendingMarker != null)
            {
                pendingMarker.SetActive(false);
            }
        }

        private void BeginHitstop(int milliseconds)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            var until = Time.realtimeSinceStartup + milliseconds / 1000f;
            if (until > hitstopUntilRealtime)
            {
                hitstopUntilRealtime = until;
            }
        }

        private void BeginCameraShake(float intensity)
        {
            if (intensity <= 0f)
            {
                return;
            }

            cameraShakeIntensity = Mathf.Max(cameraShakeIntensity, intensity * 0.08f);
            cameraShakeUntilRealtime = Mathf.Max(cameraShakeUntilRealtime, Time.realtimeSinceStartup + ShakeSeconds);
        }

        private float LocalDeltaTime()
        {
            if (Time.realtimeSinceStartup < hitstopUntilRealtime)
            {
                return 0f;
            }

            return Time.unscaledDeltaTime * Mathf.Max(0.05f, PlaybackFactor);
        }

        private bool TryGetToken(string unitId, out UnitToken token)
        {
            token = null;
            if (string.IsNullOrWhiteSpace(unitId) || tokens == null)
            {
                return false;
            }

            if (tokens.TryGetValue(unitId, out token))
            {
                return token != null;
            }

            tokenBuffer.Clear();
            foreach (var pair in tokens)
            {
                tokenBuffer[pair.Key] = pair.Value;
            }

            return tokenBuffer.TryGetValue(unitId, out token) && token != null;
        }

        private void FaceCamera(Transform target)
        {
            if (target == null || sceneCamera == null)
            {
                return;
            }

            target.rotation = Quaternion.LookRotation(target.position - sceneCamera.transform.position, Vector3.up);
        }

        private static string FormatLog(TurnPresentationEvent presentationEvent)
        {
            return presentationEvent.Type switch
            {
                TurnPresentationEventType.Move => $"{presentationEvent.UnitId} moved {presentationEvent.MoveDistance:0.##}",
                TurnPresentationEventType.Ability => $"{presentationEvent.UnitId} used {presentationEvent.AbilityId} -> {presentationEvent.TargetUnitId}",
                TurnPresentationEventType.Skip => $"{presentationEvent.UnitId} skipped",
                _ => $"{presentationEvent.UnitId} acted"
            };
        }

        private static float PopupScale(DamagePopupStyle style)
        {
            return style switch
            {
                DamagePopupStyle.Crit => 0.11f,
                DamagePopupStyle.Consume => 0.13f,
                DamagePopupStyle.Heal => 0.1f,
                _ => 0.085f
            };
        }

        private static Color StyleColor(DamagePopupStyle style)
        {
            return style switch
            {
                DamagePopupStyle.Crit => new Color(1f, 0.92f, 0.22f, 1f),
                DamagePopupStyle.Consume => new Color(1f, 0.22f, 0.18f, 1f),
                DamagePopupStyle.Heal => new Color(0.35f, 1f, 0.55f, 1f),
                _ => new Color(1f, 0.96f, 0.9f, 1f)
            };
        }

        private static Material CreateLineMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.color = color;
            return material;
        }
    }
}
