using System;
using System.Linq;
using Tower.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tower.EditorTools
{
    /// <summary>
    /// Rebuilds the isolated T48 preview scene without serializing its spawned
    /// enemy/companion runtime output. Invoke from batchmode with Create.
    /// </summary>
    public static class CombatSpikeSceneBuilder
    {
        private const string ScenePath = "Assets/_Tower/Scenes/_CombatSpike.unity";
        private const string HumanPrefabPath = "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";
        private const string LocomotionControllerPath = "Assets/_Tower/Art/Characters/Animations/PC_Locomotion.controller";
        private const string IdleFbxPath = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/Idle.fbx";
        private const string RunForwardFbxPath = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunForward.fbx";
        private const string SprintFbxPath = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/Sprint.fbx";

        public static void Create()
        {
            VerifyAnimationClip(IdleFbxPath, "Idle");
            VerifyAnimationClip(RunForwardFbxPath, "RunForward");
            VerifyAnimationClip(SprintFbxPath, "Sprint");

            var humanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanPrefabPath);
            var locomotionController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LocomotionControllerPath);
            if (humanPrefab == null || locomotionController == null)
            {
                throw new InvalidOperationException("Combat spike character source assets are missing.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateGround();
            CreateLighting();
            var player = CreatePlayer(scene, humanPrefab, locomotionController);
            CreateCamera(player.transform);
            CreateEncounterHost(player.transform, humanPrefab, locomotionController);
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            AssetDatabase.SaveAssets();
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static GameObject CreatePlayer(Scene scene, GameObject humanPrefab, RuntimeAnimatorController locomotionController)
        {
            var player = new GameObject("Player");
            SceneManager.MoveGameObjectToScene(player, scene);
            player.transform.position = Vector3.zero;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(humanPrefab, scene);
            visual.name = "PlayerVisual";
            visual.transform.SetParent(player.transform, false);
            var animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException("HumanMale_Character_FREE has no Animator.");
            }

            animator.runtimeAnimatorController = locomotionController;
            var playerControllerType = Type.GetType("PlayerIsoController, Assembly-CSharp");
            if (playerControllerType == null)
            {
                throw new InvalidOperationException("PlayerIsoController is not available in Assembly-CSharp.");
            }

            var playerController = player.AddComponent(playerControllerType);
            var controllerProperties = new SerializedObject(playerController);
            controllerProperties.FindProperty("moveSpeed").floatValue = 4.5f;
            controllerProperties.FindProperty("turnSpeed").floatValue = 12f;
            controllerProperties.FindProperty("arriveDistance").floatValue = 0.2f;
            controllerProperties.FindProperty("groundSnapUp").floatValue = 3f;
            controllerProperties.FindProperty("groundSnapDown").floatValue = 8f;
            controllerProperties.FindProperty("animator").objectReferenceValue = animator;
            controllerProperties.FindProperty("speedParameter").stringValue = "Speed";
            controllerProperties.ApplyModifiedPropertiesWithoutUndo();
            var body = player.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            return player;
        }

        private static void CreateCamera(Transform player)
        {
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            var rig = cameraObject.AddComponent<FixedIsoFollowCameraRig>();
            rig.SetFocusTarget(player);
        }

        private static void CreateEncounterHost(
            Transform player,
            GameObject humanPrefab,
            RuntimeAnimatorController locomotionController)
        {
            var hostObject = new GameObject("CombatEncounterHost");
            var timeDilation = hostObject.AddComponent<TimeDilationService>();
            var slowMoInput = hostObject.AddComponent<SlowMoInput>();
            var host = hostObject.AddComponent<CombatEncounterHost>();

            var hostProperties = new SerializedObject(host);
            hostProperties.FindProperty("player").objectReferenceValue = player;
            hostProperties.FindProperty("companionPrefab").objectReferenceValue = humanPrefab;
            hostProperties.FindProperty("companionLocomotionController").objectReferenceValue = locomotionController;
            hostProperties.FindProperty("pillbugSpawnDistance").floatValue = 8f;
            hostProperties.FindProperty("companionSpawnDistance").floatValue = 2f;
            hostProperties.FindProperty("awarenessRadius").floatValue = 12f;
            hostProperties.FindProperty("windupTriggerDistance").floatValue = 2.5f;
            hostProperties.FindProperty("dashRange").floatValue = 3f;
            hostProperties.FindProperty("windupSeconds").floatValue = 0.9f;
            hostProperties.FindProperty("commitSeconds").floatValue = 0.25f;
            hostProperties.FindProperty("recoverSeconds").floatValue = 0.6f;
            hostProperties.FindProperty("companionLeashDistance").floatValue = 4f;
            hostProperties.ApplyModifiedPropertiesWithoutUndo();

            var inputProperties = new SerializedObject(slowMoInput);
            inputProperties.FindProperty("timeDilation").objectReferenceValue = timeDilation;
            inputProperties.FindProperty("fullDrainSeconds").floatValue = 2.5f;
            inputProperties.FindProperty("fullRechargeSeconds").floatValue = 8f;
            inputProperties.FindProperty("minEngageCharge").floatValue = 0.3f;
            inputProperties.FindProperty("earlyBoundary").floatValue = 0.33f;
            inputProperties.FindProperty("cleanBoundary").floatValue = 0.78f;
            inputProperties.FindProperty("coverageThreshold").floatValue = 0.5f;
            inputProperties.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void VerifyAnimationClip(string assetPath, string expectedName)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => string.Equals(candidate.name, expectedName, StringComparison.Ordinal));
            if (clip == null)
            {
                throw new InvalidOperationException("Expected animation clip was not found: " + expectedName);
            }
        }
    }
}
