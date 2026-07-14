using System;
using Tower.Combat;
using Tower.Core;
using UnityEditor;
using UnityEngine;

namespace Tower.EditorTools
{
    /// <summary>
    /// Binds the v0 roster to the same Blink source asset used by the playable
    /// Returner. Each profile still owns identity, accent, and movement tuning,
    /// so shared asset provenance does not turn the roster into identical data.
    /// </summary>
    public static class CompanionEntityAssetBuilder
    {
        public const string EmberProfilePath = "Assets/_Tower/Data/CompanionVisuals/CV_EmberVanguard.asset";
        public const string WardProfilePath = "Assets/_Tower/Data/CompanionVisuals/CV_WardBearer.asset";
        public const string GlassProfilePath = "Assets/_Tower/Data/CompanionVisuals/CV_GlassBreaker.asset";
        public const string SharedBodyPrefabPath = "Assets/_Tower/Prefabs/Characters/PlayableHumanoid.prefab";

        private const string ControllerPath = "Assets/_Tower/Art/Characters/Animations/PC_Locomotion.controller";
        private const string HumanPrefabPath = "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";
        private const string RuntimeLitMaterialPath = "Assets/_Tower/Resources/TowerRuntimeLit.mat";

        public static void Create()
        {
            EnsureFolder("Assets/_Tower/Prefabs", "Characters");
            EnsureFolder("Assets/_Tower/Data", "CompanionVisuals");

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanPrefabPath);
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(RuntimeLitMaterialPath);
            if (controller == null || sourcePrefab == null || bodyMaterial == null)
            {
                throw new InvalidOperationException("Shared roster visual source assets are missing.");
            }

            var bodyPrefab = CreateSharedBodyPrefab(sourcePrefab, controller, bodyMaterial);

            CreateCompanion(
                "Assets/_Tower/Data/Characters/C_EmberVanguard.asset",
                factionId: 1,
                new Color(0.82f, 0.32f, 0.16f, 1f),
                new Vector3(-1.55f, 0f, -1.55f),
                EmberProfilePath,
                controller,
                bodyPrefab,
                bodyMaterial);

            CreateCompanion(
                "Assets/_Tower/Data/Characters/C_WardBearer.asset",
                factionId: 2,
                new Color(0.20f, 0.48f, 0.72f, 1f),
                new Vector3(1.55f, 0f, -1.55f),
                WardProfilePath,
                controller,
                bodyPrefab,
                bodyMaterial);

            CreateCompanion(
                "Assets/_Tower/Data/Characters/C_GlassBreaker.asset",
                factionId: 3,
                new Color(0.72f, 0.54f, 0.22f, 1f),
                new Vector3(0f, 0f, -2.55f),
                GlassProfilePath,
                controller,
                bodyPrefab,
                bodyMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject CreateSharedBodyPrefab(
            GameObject sourcePrefab,
            RuntimeAnimatorController controller,
            Material bodyMaterial)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            instance.name = "PlayableHumanoidVisual";
            if (PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                PrefabUtility.UnpackPrefabInstance(
                    instance,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materialCount = ResolveSubMeshCount(renderer);
                var materials = new Material[materialCount];
                for (var index = 0; index < materials.Length; index++)
                {
                    materials[index] = bodyMaterial;
                }

                renderer.sharedMaterials = materials;
            }

            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException("Shared playable source has no Animator.");
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, SharedBodyPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            if (prefab == null)
            {
                throw new InvalidOperationException("Failed to create the normalized shared playable prefab.");
            }

            return prefab;
        }

        private static int ResolveSubMeshCount(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                return Mathf.Max(1, skinned.sharedMesh.subMeshCount);
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null
                ? Mathf.Max(1, filter.sharedMesh.subMeshCount)
                : 1;
        }

        private static void CreateCompanion(
            string characterPath,
            int factionId,
            Color accent,
            Vector3 formationOffset,
            string profilePath,
            RuntimeAnimatorController controller,
            GameObject bodyPrefab,
            Material bodyMaterial)
        {
            var characterDefinition = AssetDatabase.LoadAssetAtPath<CharacterDef>(characterPath);
            if (characterDefinition == null)
            {
                throw new InvalidOperationException("Companion CharacterDef is missing: " + characterPath);
            }

            MarkAsPreset(characterDefinition, factionId);
            var profile = AssetDatabase.LoadAssetAtPath<CompanionVisualProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CompanionVisualProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("characterDefinition").objectReferenceValue = characterDefinition;
            serialized.FindProperty("bodyPrefab").objectReferenceValue = bodyPrefab;
            serialized.FindProperty("bodyMaterial").objectReferenceValue = bodyMaterial;
            serialized.FindProperty("locomotionController").objectReferenceValue = controller;
            serialized.FindProperty("accentColor").colorValue = accent;
            serialized.FindProperty("formationOffset").vector3Value = formationOffset;
            serialized.FindProperty("arriveDistance").floatValue = 0.2f;
            serialized.FindProperty("moveSpeed").floatValue = 3.5f;
            serialized.FindProperty("turnSpeed").floatValue = 540f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void MarkAsPreset(CharacterDef definition, int factionId)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("isPreset").boolValue = true;
            serialized.FindProperty("factionId").intValue = factionId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
