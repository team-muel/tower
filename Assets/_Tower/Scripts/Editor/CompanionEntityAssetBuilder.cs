using System;
using System.Linq;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.Character;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.FaceEditor;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.MaterialManagement;
using Tower.Combat;
using Tower.Core;
using UnityEditor;
using UnityEngine;

namespace Tower.EditorTools
{
    /// <summary>
    /// Deterministically bakes the three v0 companion silhouettes from the
    /// adopted ithappy modular humanoid pack. The generated prefabs only own
    /// presentation; identity remains in CharacterDef and visual profiles.
    /// </summary>
    public static class CompanionEntityAssetBuilder
    {
        public const string EmberProfilePath = "Assets/_Tower/Data/CompanionVisuals/CV_EmberVanguard.asset";
        public const string WardProfilePath = "Assets/_Tower/Data/CompanionVisuals/CV_WardBearer.asset";
        public const string GlassProfilePath = "Assets/_Tower/Data/CompanionVisuals/CV_GlassBreaker.asset";

        private const string PrefabFolder = "Assets/_Tower/Prefabs/Characters";
        private const string ProfileFolder = "Assets/_Tower/Data/CompanionVisuals";
        private const string ControllerPath = "Assets/_Tower/Art/Characters/Animations/PC_Locomotion.controller";

        public static void Create()
        {
            EnsureFolder("Assets/_Tower/Prefabs", "Characters");
            EnsureFolder("Assets/_Tower/Data", "CompanionVisuals");

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException("Companion locomotion controller is missing.");
            }

            CreateCompanion(
                "EmberVanguard",
                "Assets/_Tower/Data/Characters/C_EmberVanguard.asset",
                factionId: 1,
                new Color(0.82f, 0.32f, 0.16f, 1f),
                new Vector3(-1.55f, 0f, -1.55f),
                new[]
                {
                    new Part(GroupType.Faces, 2),
                    new Part(GroupType.Outfit, 0),
                    new Part(GroupType.Pants, 0),
                    new Part(GroupType.Shoes, 2),
                    new Part(GroupType.Hairstyle, 1),
                    new Part(GroupType.Gloves, 1),
                },
                EmberProfilePath,
                controller);

            CreateCompanion(
                "WardBearer",
                "Assets/_Tower/Data/Characters/C_WardBearer.asset",
                factionId: 2,
                new Color(0.20f, 0.48f, 0.72f, 1f),
                new Vector3(1.55f, 0f, -1.55f),
                new[]
                {
                    new Part(GroupType.Faces, 0),
                    new Part(GroupType.Outfit, 0),
                    new Part(GroupType.Outwear, 1),
                    new Part(GroupType.Pants, 1),
                    new Part(GroupType.Shoes, 0),
                    new Part(GroupType.HairstyleSingle, 0),
                    new Part(GroupType.Gloves, 0),
                },
                WardProfilePath,
                controller);

            CreateCompanion(
                "GlassBreaker",
                "Assets/_Tower/Data/Characters/C_GlassBreaker.asset",
                factionId: 3,
                new Color(0.72f, 0.54f, 0.22f, 1f),
                new Vector3(0f, 0f, -2.55f),
                new[]
                {
                    new Part(GroupType.Faces, 1),
                    new Part(GroupType.Outfit, 0),
                    new Part(GroupType.Shorts, 0),
                    new Part(GroupType.Shoes, 1),
                    new Part(GroupType.HairstyleSingle, 0),
                    new Part(GroupType.Glasses, 1),
                },
                GlassProfilePath,
                controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateCompanion(
            string name,
            string characterPath,
            int factionId,
            Color accent,
            Vector3 formationOffset,
            Part[] parts,
            string profilePath,
            RuntimeAnimatorController controller)
        {
            var characterDefinition = AssetDatabase.LoadAssetAtPath<CharacterDef>(characterPath);
            if (characterDefinition == null)
            {
                throw new InvalidOperationException("Companion CharacterDef is missing: " + characterPath);
            }

            MarkAsPreset(characterDefinition, factionId);
            var prefab = BakeModularPrefab(name, parts, controller);
            var profile = AssetDatabase.LoadAssetAtPath<CompanionVisualProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CompanionVisualProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("characterDefinition").objectReferenceValue = characterDefinition;
            serialized.FindProperty("bodyPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("locomotionController").objectReferenceValue = controller;
            serialized.FindProperty("accentColor").colorValue = accent;
            serialized.FindProperty("formationOffset").vector3Value = formationOffset;
            serialized.FindProperty("arriveDistance").floatValue = 0.2f;
            serialized.FindProperty("moveSpeed").floatValue = 3.5f;
            serialized.FindProperty("turnSpeed").floatValue = 540f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static GameObject BakeModularPrefab(
            string name,
            Part[] parts,
            RuntimeAnimatorController controller)
        {
            var customizable = new CustomizableCharacter(SlotLibraryLoader.LoadSlotLibrary());
            customizable.ToDefault();
            foreach (var part in parts)
            {
                var count = customizable.GetVariantsCountInGroup(part.Group);
                if (count <= part.Index)
                {
                    throw new InvalidOperationException(
                        $"ithappy group {part.Group} has {count} variants; requested {part.Index}.");
                }

                customizable.PickGroup(part.Group, part.Index, true);
            }

            var instance = customizable.InstantiateCharacter();
            instance.name = name + "Visual";
            var materialProvider = new MaterialProvider();
            foreach (var slot in customizable.Slots.Where(slot => slot.IsEnabled))
            {
                foreach (var mesh in slot.Meshes)
                {
                    var child = instance.transform.Cast<Transform>()
                        .First(transform => transform.name.StartsWith(mesh.Item1.ToString(), StringComparison.Ordinal));
                    var renderer = child.GetComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh.Item2;
                    renderer.sharedMaterial = materialProvider.MainColor;
                    renderer.localBounds = renderer.sharedMesh.bounds;
                }
            }

            FaceLoader.AddFaces(instance);
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var path = $"{PrefabFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
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

        private readonly struct Part
        {
            public Part(GroupType group, int index)
            {
                Group = group;
                Index = index;
            }

            public GroupType Group { get; }
            public int Index { get; }
        }
    }
}
