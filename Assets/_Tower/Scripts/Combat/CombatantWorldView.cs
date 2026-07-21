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
        private Transform fill;

        public string UnitId { get; private set; }
        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public float FillRatio { get; private set; }
        public bool IsAlive { get; private set; }

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

            if (fill != null)
            {
                fill.localScale = new Vector3(FillRatio, 1f, 1f);
                fill.localPosition = new Vector3(-0.5f * (1f - FillRatio), 0f, -0.04f);
            }

            if (barRoot != null)
            {
                barRoot.SetActive(IsAlive);
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
