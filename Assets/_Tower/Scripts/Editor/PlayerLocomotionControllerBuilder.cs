using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tower.EditorTools
{
    public static class PlayerLocomotionControllerBuilder
    {
        private const string ControllerPath = "Assets/_Tower/Art/Characters/Animations/PC_Locomotion.controller";
        private const string SpeedParameter = "Speed";

        private static readonly ClipBinding[] Clips =
        {
            new ClipBinding(
                0f,
                "Idle",
                "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/Idle.fbx"),
            new ClipBinding(
                0.5f,
                "RunForward",
                "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunForward.fbx"),
            new ClipBinding(
                1f,
                "Sprint",
                "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/Sprint.fbx")
        };

        public static void Build()
        {
            EnsureParentFolder();

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = stateMachine.AddState("Locomotion", new Vector3(240f, 100f, 0f));
            stateMachine.defaultState = locomotion;

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = SpeedParameter,
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(tree, controller);
            locomotion.motion = tree;

            foreach (ClipBinding binding in Clips)
            {
                tree.AddChild(LoadClip(binding), binding.Threshold);
            }

            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(locomotion);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[PlayerLocomotionControllerBuilder] Built {ControllerPath}");
        }

        private static AnimationClip LoadClip(ClipBinding binding)
        {
            AnimationClip clip = AssetDatabase
                .LoadAllAssetsAtPath(binding.FbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == binding.ClipName);

            if (clip == null)
            {
                string available = string.Join(
                    ", ",
                    AssetDatabase
                        .LoadAllAssetsAtPath(binding.FbxPath)
                        .OfType<AnimationClip>()
                        .Select(candidate => candidate.name));
                throw new InvalidOperationException(
                    $"Clip '{binding.ClipName}' was not found in {binding.FbxPath}. Available clips: {available}");
            }

            return clip;
        }

        private static void EnsureParentFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Tower/Art/Characters"))
            {
                AssetDatabase.CreateFolder("Assets/_Tower/Art", "Characters");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Tower/Art/Characters/Animations"))
            {
                AssetDatabase.CreateFolder("Assets/_Tower/Art/Characters", "Animations");
            }
        }

        private readonly struct ClipBinding
        {
            public ClipBinding(float threshold, string clipName, string fbxPath)
            {
                Threshold = threshold;
                ClipName = clipName;
                FbxPath = fbxPath;
            }

            public float Threshold { get; }
            public string ClipName { get; }
            public string FbxPath { get; }
        }
    }
}
