using System;
using System.Collections.Generic;
using Tower.Combat;
using Tower.Core;
using Tower.Gen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tower.UI
{
    // Walkable camp hub between Boot and Loadout (T15). The greybox space is
    // generated at runtime; interactions are data-driven CampZoneDef circles
    // so new spots (recruits, board, ...) are added as data, not new input code.
    // Input: legacy Input Manager, same as IsoCameraRig (project runs in Both mode).
    public sealed class CampController : MonoBehaviour
    {
        private const string QaStateKey = "camp";
        private const float MoveSpeed = 5f;
        private const float GroundHalfExtent = 13f;
        private const float CampfireMessageSeconds = 6f;

        private readonly CampZoneRegistry zones = new CampZoneRegistry();
        private readonly Dictionary<string, Action> zoneActions = new Dictionary<string, Action>(StringComparer.Ordinal);
        private readonly List<string> qaZoneNames = new List<string>();

        private Transform player;
        private IsoCameraRig cameraRig;
        private Camera sceneCamera;
        private Text promptText;
        private Text campfireText;
        private CampZoneDef activeZone;
        private bool hasDestination;
        private float destX;
        private float destZ;
        private float campfireMessageUntil;
        private bool departing;

        private void Start()
        {
            // Ghost-UI lesson (2026-07-06): a camera must exist from the first
            // frame; the clear camera stays under the rig camera afterwards.
            RuntimeSceneUi.EnsureClearCamera();

            // v0 seam: the camp always shows the first stairway's biome. Later
            // the stairway progression feeds a different BiomeId through here.
            var theme = BiomeTheme.For(BiomeId.Forest);
            ApplyTheme(theme);
            BuildWorld(theme);
            CreateCameraRig();
            BuildHud();
            BuildZones();
            QaRuntime.RegisterStateContributor(QaStateKey, FillQaState);
        }

        private void Update()
        {
            if (player == null || departing)
            {
                return;
            }

            HandleMovement();
            UpdateActiveZone();
            HandleInteraction();

            if (campfireText != null && campfireText.text.Length > 0 && Time.time >= campfireMessageUntil)
            {
                campfireText.text = string.Empty;
            }
        }

        private void OnDestroy()
        {
            QaRuntime.UnregisterStateContributor(QaStateKey);
            foreach (var name in qaZoneNames)
            {
                QaRuntime.UnregisterButton(name);
            }

            qaZoneNames.Clear();
        }

        private void ApplyTheme(BiomeTheme theme)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ToColor(theme.AmbientColor);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = ToColor(theme.FogColor);
            RenderSettings.fogDensity = theme.FogDensity;

            var lightObject = new GameObject("Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = ToColor(theme.DirectionalLightColor);
            light.intensity = theme.DirectionalLightIntensity;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        private void BuildWorld(BiomeTheme theme)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Camp Ground";
            ground.transform.localScale = new Vector3(2.8f, 1f, 2.8f);
            Tint(ground, ToColor(theme.TileTintA));

            var fireBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fireBase.name = "Campfire";
            fireBase.transform.position = new Vector3(-4f, 0.25f, 3f);
            fireBase.transform.localScale = new Vector3(1.2f, 0.25f, 1.2f);
            Tint(fireBase, new Color(0.28f, 0.22f, 0.16f, 1f));

            var fireLightObject = new GameObject("Campfire Light");
            fireLightObject.transform.SetParent(fireBase.transform, false);
            fireLightObject.transform.localPosition = new Vector3(0f, 4f, 0f);
            var fireLight = fireLightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.62f, 0.30f, 1f);
            fireLight.intensity = 2.4f;
            fireLight.range = 9f;

            var tentColor = ToColor(theme.TileTintB);
            var tentA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tentA.name = "Tent A";
            tentA.transform.position = new Vector3(5f, 0.75f, 4f);
            tentA.transform.localScale = new Vector3(2.2f, 1.5f, 2.2f);
            Tint(tentA, tentColor);

            var tentB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tentB.name = "Tent B";
            tentB.transform.position = new Vector3(7f, 0.75f, 0.5f);
            tentB.transform.localScale = new Vector3(2f, 1.4f, 2f);
            Tint(tentB, tentColor);

            BuildGateArch();

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Regressor";
            capsule.transform.position = new Vector3(0f, 1f, -4f);
            Tint(capsule, new Color(0.2f, 0.55f, 1f, 1f));
            player = capsule.transform;
        }

        private void BuildGateArch()
        {
            var gateColor = new Color(0.55f, 0.56f, 0.60f, 1f);
            var gate = new GameObject("Depart Gate Arch");

            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.name = "Gate Pillar L";
            left.transform.SetParent(gate.transform, false);
            left.transform.position = new Vector3(-1.6f, 1.5f, 10f);
            left.transform.localScale = new Vector3(0.7f, 3f, 0.7f);
            Tint(left, gateColor);

            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.name = "Gate Pillar R";
            right.transform.SetParent(gate.transform, false);
            right.transform.position = new Vector3(1.6f, 1.5f, 10f);
            right.transform.localScale = new Vector3(0.7f, 3f, 0.7f);
            Tint(right, gateColor);

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Gate Lintel";
            lintel.transform.SetParent(gate.transform, false);
            lintel.transform.position = new Vector3(0f, 3.2f, 10f);
            lintel.transform.localScale = new Vector3(4.4f, 0.5f, 0.7f);
            Tint(lintel, gateColor);
        }

        private void CreateCameraRig()
        {
            var rigObject = new GameObject("Iso Camera Rig");
            cameraRig = rigObject.AddComponent<IsoCameraRig>();
            cameraRig.SetFollowTarget(player);
            sceneCamera = cameraRig.Camera;
        }

        private void BuildHud()
        {
            var canvas = RuntimeSceneUi.CreateCanvas("Camp Canvas");
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateHudText(
                canvas.transform,
                "Camp Title",
                "캠프 — 1층계 앞",
                26,
                new Vector2(0f, 0.92f),
                new Vector2(1f, 1f));

            // Movement hint only on the first camp entry (UX gate, 21 §0).
            if (PlayerPrefs.GetInt(TowerSceneNames.CampHintPref, 0) == 0)
            {
                PlayerPrefs.SetInt(TowerSceneNames.CampHintPref, 1);
                PlayerPrefs.Save();
                CreateHudText(
                    canvas.transform,
                    "Move Hint",
                    "이동: WASD/방향키 · 우클릭: 지점 이동 · E/좌클릭: 상호작용",
                    16,
                    new Vector2(0f, 0.86f),
                    new Vector2(1f, 0.92f));
            }

            campfireText = CreateHudText(
                canvas.transform,
                "Campfire Message",
                string.Empty,
                18,
                new Vector2(0f, 0.16f),
                new Vector2(1f, 0.24f));

            promptText = CreateHudText(
                canvas.transform,
                "Zone Prompt",
                string.Empty,
                22,
                new Vector2(0f, 0.05f),
                new Vector2(1f, 0.13f));
        }

        private void BuildZones()
        {
            // QA names: zone triggers are pressable but are not uGUI buttons,
            // so they deliberately do not carry the " Button" suffix.
            AddZone("depart-gate", "출발 게이트", 0f, 9f, 3f, "Depart Gate", ActivateDepartGate);
            AddZone("campfire", "모닥불", -4f, 3f, 2.6f, "Campfire", ActivateCampfire);
        }

        private void AddZone(string id, string label, float x, float z, float radius, string qaName, Action activate)
        {
            var created = CampZoneDef.Create(id, label, x, z, radius);
            if (created.IsFailure)
            {
                Debug.LogError("[Camp] " + created.Error);
                return;
            }

            var added = zones.Add(created.Value);
            if (added.IsFailure)
            {
                Debug.LogError("[Camp] " + added.Error);
                return;
            }

            zoneActions[id] = activate;
            qaZoneNames.Add(qaName);
            QaRuntime.RegisterButton(qaName, () => activate());
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            var position = player.position;

            if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
            {
                // Direct control cancels any pending click destination.
                hasDestination = false;
                var forward = sceneCamera.transform.forward;
                forward.y = 0f;
                var right = sceneCamera.transform.right;
                right.y = 0f;
                var move = (right.normalized * horizontal) + (forward.normalized * vertical);
                if (move.sqrMagnitude > 1f)
                {
                    move.Normalize();
                }

                position += move * MoveSpeed * Time.deltaTime;
            }
            else if (hasDestination)
            {
                var step = CampMover.StepTowards(position.x, position.z, destX, destZ, MoveSpeed, Time.deltaTime);
                position = new Vector3(step.X, position.y, step.Z);
                if (step.Arrived)
                {
                    hasDestination = false;
                }
            }

            if (Input.GetMouseButtonDown(1) && TryGetGroundPoint(out var target))
            {
                destX = Mathf.Clamp(target.x, -GroundHalfExtent, GroundHalfExtent);
                destZ = Mathf.Clamp(target.z, -GroundHalfExtent, GroundHalfExtent);
                hasDestination = true;
            }

            position.x = Mathf.Clamp(position.x, -GroundHalfExtent, GroundHalfExtent);
            position.z = Mathf.Clamp(position.z, -GroundHalfExtent, GroundHalfExtent);
            player.position = position;
        }

        private void UpdateActiveZone()
        {
            var zone = zones.FindAt(player.position.x, player.position.z);
            if (ReferenceEquals(zone, activeZone))
            {
                return;
            }

            activeZone = zone;
            if (promptText != null)
            {
                promptText.text = zone == null ? string.Empty : "[E] " + zone.Label;
            }
        }

        private void HandleInteraction()
        {
            if (activeZone == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
            {
                if (zoneActions.TryGetValue(activeZone.Id, out var action))
                {
                    action();
                }
            }
        }

        private void ActivateDepartGate()
        {
            if (departing)
            {
                return;
            }

            departing = true;
            SceneSequenceManager.Instance.LoadSceneWithSequence(TowerSceneNames.Loadout);
        }

        private void ActivateCampfire()
        {
            campfireMessageUntil = Time.time + CampfireMessageSeconds;
            if (campfireText != null)
            {
                campfireText.text = "동료들이 모닥불 곁에 있다. <color=#9E9E9E>(대화는 준비 중)</color>";
            }
        }

        private void FillQaState(QaStateSnapshot snapshot)
        {
            if (player == null)
            {
                return;
            }

            snapshot.camp = new QaCampSnapshot
            {
                x = player.position.x,
                z = player.position.z,
                zoneId = activeZone == null ? string.Empty : activeZone.Id
            };
        }

        private bool TryGetGroundPoint(out Vector3 point)
        {
            var ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 200f))
            {
                point = hit.point;
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private static Text CreateHudText(Transform parent, string name, string value, int fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            var text = RuntimeSceneUi.AddText(parent, name, value, fontSize, TextAnchor.MiddleCenter);
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        // Runtime primitives cannot keep their default material: its shader is
        // stripped from URP builds and renders magenta. TowerRuntimeMaterials
        // clones a Resources-pinned URP Lit material instead.
        private static void Tint(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = TowerRuntimeMaterials.CreateLit(target.name + " Material", color);
        }

        private static Color ToColor(BiomeColor color)
        {
            return new Color(color.R, color.G, color.B, 1f);
        }
    }
}
