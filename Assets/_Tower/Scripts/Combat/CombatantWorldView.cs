using Tower.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Tower.Combat
{
    // Minimal world-space combat projection. Core remains authoritative; this
    // component only maps HP/alive state to a body and a small health bar.
    public sealed class CombatantWorldView : MonoBehaviour
    {
        private const float BarWidth = 1.35f;
        private const float BarHeight = 0.12f;
        private const float BarDepth = 0.06f;

        private Transform body;
        private Renderer[] bodyRenderers = new Renderer[0];
        private Collider[] bodyColliders = new Collider[0];
        private GameObject barRoot;
        private GameObject labelsRoot;
        private Transform fill;
        private TextMesh dispositionText;
        private TextMesh intentText;

        public string UnitId { get; private set; }
        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public float FillRatio { get; private set; }
        public bool IsAlive { get; private set; }
        public string DispositionLabel { get; private set; } = string.Empty;
        public string IntentLabel { get; private set; } = string.Empty;

        public Result Configure(string unitId, Transform bodyTransform, float barHeight)
        {
            if (string.IsNullOrWhiteSpace(unitId) || bodyTransform == null)
            {
                return Result.Failure("Combatant view requires a unit id and body.");
            }

            if (barHeight <= 0f || float.IsNaN(barHeight) || float.IsInfinity(barHeight))
            {
                return Result.Failure("Combatant health-bar height must be finite and positive.");
            }

            UnitId = unitId;
            body = bodyTransform;
            Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
            var visibleRenderers = new List<Renderer>(renderers.Length);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].enabled) visibleRenderers.Add(renderers[index]);
            }

            bodyRenderers = visibleRenderers.ToArray();
            bodyColliders = body.GetComponentsInChildren<Collider>(true);
            CreateBar(barHeight);
            CreateLabels(barHeight);
            return Result.Success();
        }

        public Result Refresh(CharacterState state)
        {
            if (state == null)
            {
                return Result.Failure("Combatant state is required.");
            }

            CurrentHp = state.CurrentHp;
            MaxHp = state.Definition.MaxHp;
            FillRatio = MaxHp <= 0 ? 0f : Mathf.Clamp01((float)CurrentHp / MaxHp);
            IsAlive = CurrentHp > 0;
            DispositionLabel = state.Definition.Disposition.ToString();
            if (dispositionText != null)
            {
                dispositionText.text = DispositionLabel;
            }

            if (fill != null)
            {
                fill.localScale = new Vector3(FillRatio, 1f, 1f);
                fill.localPosition = new Vector3(-0.5f * (1f - FillRatio), 0f, -0.04f);
            }

            if (barRoot != null)
            {
                barRoot.SetActive(IsAlive);
            }

            if (labelsRoot != null)
            {
                labelsRoot.SetActive(IsAlive);
            }

            for (int index = 0; index < bodyRenderers.Length; index++)
            {
                if (bodyRenderers[index] != null) bodyRenderers[index].enabled = IsAlive;
            }

            for (int index = 0; index < bodyColliders.Length; index++)
            {
                if (bodyColliders[index] != null) bodyColliders[index].enabled = IsAlive;
            }

            return Result.Success();
        }

        public void SetIntent(AutonomousCombatIntent intent)
        {
            if (intent == null)
            {
                ClearIntent();
                return;
            }

            string action = intent.Plan.Kind == AiPlanKind.Ability
                ? intent.Plan.AbilityId
                : intent.Plan.Kind.ToString();
            string target = string.IsNullOrEmpty(intent.Plan.TargetUnitId)
                ? string.Empty
                : " -> " + intent.Plan.TargetUnitId;
            IntentLabel = (intent.IsPreciseOrder ? "! " : "… ")
                + CommandStanceRules.DisplayName(intent.Stance)
                + ": " + action + target;
            if (intentText != null)
            {
                intentText.text = IntentLabel;
                intentText.color = intent.IsPreciseOrder
                    ? new Color(1f, 0.78f, 0.25f)
                    : new Color(0.75f, 0.9f, 1f);
            }
        }

        public void ClearIntent()
        {
            IntentLabel = string.Empty;
            if (intentText != null)
            {
                intentText.text = string.Empty;
            }
        }

        private void CreateBar(float height)
        {
            barRoot = new GameObject("HealthBar");
            barRoot.transform.SetParent(transform, false);
            barRoot.transform.localPosition = Vector3.up * height;

            GameObject background = CreateBarPart(
                "Background",
                new Color(0.08f, 0.08f, 0.08f, 0.95f));
            background.transform.SetParent(barRoot.transform, false);

            GameObject fillObject = CreateBarPart(
                "Fill",
                new Color(0.15f, 0.85f, 0.28f, 1f));
            fillObject.transform.SetParent(barRoot.transform, false);
            fillObject.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            fill = fillObject.transform;
        }

        private void CreateLabels(float height)
        {
            labelsRoot = new GameObject("CombatIntentLabels");
            labelsRoot.transform.SetParent(transform, false);
            labelsRoot.transform.localPosition = Vector3.up * (height + 0.24f);

            dispositionText = CreateLabel(
                "Disposition",
                labelsRoot.transform,
                Vector3.zero,
                new Color(1f, 0.62f, 0.62f));
            intentText = CreateLabel(
                "Intent",
                labelsRoot.transform,
                Vector3.up * 0.16f,
                Color.white);
        }

        private static TextMesh CreateLabel(
            string labelName,
            Transform parent,
            Vector3 localPosition,
            Color color)
        {
            GameObject labelObject = new GameObject(labelName);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.06f;
            text.fontSize = 32;
            text.color = color;
            return text;
        }

        private static GameObject CreateBarPart(string partName, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.localScale = new Vector3(BarWidth, BarHeight, BarDepth);
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = TowerRuntimeMaterials.CreateLit(partName + " Material", color);
            }

            return part;
        }
    }
}
