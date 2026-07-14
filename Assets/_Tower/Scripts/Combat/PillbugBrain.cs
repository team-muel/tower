using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public readonly struct PillbugTuning
    {
        public PillbugTuning(
            float awarenessRadius,
            float windupTriggerDistance,
            float dashRange,
            TelegraphDurations durations,
            float contactDistance,
            float flashSeconds,
            float ringMinRadius,
            float ringMaxRadius,
            float ringWidth,
            int ringSegments,
            Color cleanColor,
            Color insufficientColor,
            Color missedColor,
            Color normalColor,
            Color flashColor)
        {
            AwarenessRadius = awarenessRadius;
            WindupTriggerDistance = windupTriggerDistance;
            DashRange = dashRange;
            Durations = durations;
            ContactDistance = contactDistance;
            FlashSeconds = flashSeconds;
            RingMinRadius = ringMinRadius;
            RingMaxRadius = ringMaxRadius;
            RingWidth = ringWidth;
            RingSegments = ringSegments;
            CleanColor = cleanColor;
            InsufficientColor = insufficientColor;
            MissedColor = missedColor;
            NormalColor = normalColor;
            FlashColor = flashColor;
        }

        public float AwarenessRadius { get; }
        public float WindupTriggerDistance { get; }
        public float DashRange { get; }
        public TelegraphDurations Durations { get; }
        public float ContactDistance { get; }
        public float FlashSeconds { get; }
        public float RingMinRadius { get; }
        public float RingMaxRadius { get; }
        public float RingWidth { get; }
        public int RingSegments { get; }
        public Color CleanColor { get; }
        public Color InsufficientColor { get; }
        public Color MissedColor { get; }
        public Color NormalColor { get; }
        public Color FlashColor { get; }
    }

    /// <summary>
    /// Visual-only, deterministic pillbug preview. It deliberately has no HP,
    /// damage, knockback, or other counter result.
    /// </summary>
    public sealed class PillbugBrain : MonoBehaviour
    {
        private Transform player;
        private Transform[] companions = new Transform[0];
        private PillbugTuning tuning;
        private TelegraphState telegraph;
        private Renderer bodyRenderer;
        private MaterialPropertyBlock materialProperties;
        private LineRenderer windupRing;
        private Vector3 dashOrigin;
        private Vector3 dashDestination;
        private Transform dashTarget;
        private bool didFlashContact;
        private float flashEndsAt;
        private bool engagementEnabled = true;
        private bool motionEnabled = true;

        public TelegraphState Telegraph => telegraph;
        public CounterCoverageResult CoverageResult { get; private set; } = CounterCoverageResult.Missed;
        public bool EngagementEnabled => engagementEnabled;
        public bool MotionEnabled => motionEnabled;

        public void Configure(Transform player, Transform companion, PillbugTuning tuning)
        {
            Configure(
                player,
                companion == null ? new Transform[0] : new[] { companion },
                tuning);
        }

        public void Configure(
            Transform player,
            IReadOnlyList<Transform> companionTargets,
            PillbugTuning tuning)
        {
            this.player = player;
            companions = companionTargets == null
                ? new Transform[0]
                : CopyTargets(companionTargets);
            this.tuning = tuning;
            telegraph = new TelegraphState(tuning.Durations);
            bodyRenderer = GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMaterial = TowerRuntimeMaterials.CreateLit(
                    "Pillbug Body Material",
                    tuning.NormalColor);
            }

            materialProperties = new MaterialPropertyBlock();
            EnsureWindupRing();
            SetBodyColor(tuning.NormalColor);
        }

        public void SetCoverageResult(CounterCoverageResult result)
        {
            CoverageResult = result;
        }

        public void SetEngagementEnabled(bool value)
        {
            engagementEnabled = value;
            if (!value && windupRing != null)
            {
                windupRing.enabled = false;
            }
        }

        public void SetMotionEnabled(bool value)
        {
            motionEnabled = value;
        }

        private void Update()
        {
            if (telegraph == null || !engagementEnabled)
            {
                return;
            }

            var previousPhase = telegraph.Phase;
            telegraph.Advance(Time.deltaTime);
            if (previousPhase != TelegraphPhase.Commit && telegraph.Phase == TelegraphPhase.Commit)
            {
                BeginCommit();
            }

            if (telegraph.Phase == TelegraphPhase.Commit)
            {
                AdvanceCommit();
            }

            if (telegraph.Phase == TelegraphPhase.Idle)
            {
                var target = FindNearestTarget();
                if (target != null && PlanarDistance(transform.position, target.position) <= tuning.WindupTriggerDistance)
                {
                    telegraph.TryBeginWindup();
                }
            }

            UpdateWindupRing();
            SetBodyColor(Time.time < flashEndsAt ? tuning.FlashColor : tuning.NormalColor);
        }

        private void BeginCommit()
        {
            dashTarget = FindNearestTarget();
            dashOrigin = transform.position;
            dashDestination = dashOrigin;
            didFlashContact = false;
            if (dashTarget == null)
            {
                return;
            }

            var toTarget = dashTarget.position - dashOrigin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            dashDestination = motionEnabled
                ? dashOrigin + (toTarget.normalized * Mathf.Min(tuning.DashRange, toTarget.magnitude))
                : dashOrigin;
            transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        }

        private void AdvanceCommit()
        {
            var progress = Mathf.Clamp01(telegraph.PhaseElapsed / tuning.Durations.CommitSeconds);
            transform.position = Vector3.Lerp(dashOrigin, dashDestination, progress);
            if (!didFlashContact && dashTarget != null && PlanarDistance(transform.position, dashTarget.position) <= tuning.ContactDistance)
            {
                didFlashContact = true;
                flashEndsAt = Time.time + tuning.FlashSeconds;
            }
        }

        private Transform FindNearestTarget()
        {
            Transform best = player;
            var bestDistance = best == null ? float.PositiveInfinity : PlanarDistance(transform.position, best.position);
            foreach (var companion in companions)
            {
                if (companion == null)
                {
                    continue;
                }

                var companionDistance = PlanarDistance(transform.position, companion.position);
                if (companionDistance < bestDistance)
                {
                    best = companion;
                    bestDistance = companionDistance;
                }
            }

            return bestDistance <= tuning.AwarenessRadius ? best : null;
        }

        private static Transform[] CopyTargets(IReadOnlyList<Transform> targets)
        {
            var copy = new Transform[targets.Count];
            for (var index = 0; index < targets.Count; index++)
            {
                copy[index] = targets[index];
            }

            return copy;
        }

        private void EnsureWindupRing()
        {
            if (windupRing != null)
            {
                return;
            }

            var ringObject = new GameObject("WindupRing");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = Vector3.up * 0.02f;
            windupRing = ringObject.AddComponent<LineRenderer>();
            windupRing.loop = true;
            windupRing.useWorldSpace = false;
            windupRing.widthMultiplier = tuning.RingWidth;
            windupRing.positionCount = Mathf.Max(3, tuning.RingSegments);
            windupRing.material = TowerRuntimeMaterials.CreateLit(
                "Pillbug Windup Ring Material",
                Color.white);
            for (var index = 0; index < windupRing.positionCount; index++)
            {
                var angle = (Mathf.PI * 2f * index) / windupRing.positionCount;
                windupRing.SetPosition(index, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
        }

        private void UpdateWindupRing()
        {
            if (windupRing == null)
            {
                return;
            }

            var isWindup = telegraph.Phase == TelegraphPhase.Windup;
            var shouldShowResult = telegraph.Phase == TelegraphPhase.Commit || telegraph.Phase == TelegraphPhase.Recover;
            windupRing.enabled = isWindup || shouldShowResult;
            if (!windupRing.enabled)
            {
                return;
            }

            var progress = isWindup
                ? Mathf.Clamp01(telegraph.PhaseElapsed / tuning.Durations.WindupSeconds)
                : 1f;
            var radius = Mathf.Lerp(tuning.RingMinRadius, tuning.RingMaxRadius, progress);
            windupRing.transform.localScale = new Vector3(radius, 1f, radius);
            windupRing.startColor = CoverageColor();
            windupRing.endColor = CoverageColor();
        }

        private Color CoverageColor()
        {
            switch (CoverageResult)
            {
                case CounterCoverageResult.Clean:
                    return tuning.CleanColor;
                case CounterCoverageResult.InsufficientCoverage:
                    return tuning.InsufficientColor;
                default:
                    return tuning.MissedColor;
            }
        }

        private void SetBodyColor(Color color)
        {
            if (bodyRenderer == null)
            {
                return;
            }

            bodyRenderer.GetPropertyBlock(materialProperties);
            materialProperties.SetColor("_BaseColor", color);
            materialProperties.SetColor("_Color", color);
            bodyRenderer.SetPropertyBlock(materialProperties);
        }

        private static float PlanarDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
