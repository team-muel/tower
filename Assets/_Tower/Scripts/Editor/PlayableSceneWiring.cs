using Tower.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tower.EditorTools
{
    public static class PlayableSceneWiring
    {
        private static readonly EditorBuildSettingsScene[] BuildScenes =
        {
            new EditorBuildSettingsScene("Assets/_Tower/Scenes/Boot.unity", true),
            new EditorBuildSettingsScene("Assets/_Tower/Scenes/Camp.unity", true),
            new EditorBuildSettingsScene("Assets/_Tower/Scenes/Loadout.unity", true),
            new EditorBuildSettingsScene("Assets/_Tower/Scenes/Expedition.unity", true)
        };

        public static void WireScenes()
        {
            EnsureRuntimeLitMaterial();
            SaveControllerScene("Assets/_Tower/Scenes/Boot.unity", "Boot Menu", typeof(BootMenuController));
            SaveControllerScene("Assets/_Tower/Scenes/Camp.unity", "Camp Hub", typeof(CampController));
            SaveControllerScene("Assets/_Tower/Scenes/Loadout.unity", "Loadout Menu", typeof(LoadoutMenuController));
            SaveControllerScene("Assets/_Tower/Scenes/Expedition.unity", "Playable Expedition", typeof(PlayableExpeditionController));
            EditorBuildSettings.scenes = BuildScenes;
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayableSceneWiring] Boot, Camp, Loadout, and Expedition scenes registered.");
        }

        // Scenes are code-generated, so no built asset references URP Lit and
        // the player strips it; runtime Shader.Find then returns null (T15
        // camp greybox rendered magenta). A Resources material pins the shader
        // into every build for all runtime-created primitives.
        private static void EnsureRuntimeLitMaterial()
        {
            const string folder = "Assets/_Tower/Resources";
            var path = folder + "/" + TowerRuntimeMaterials.RuntimeLitName + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/_Tower", "Resources");
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new System.InvalidOperationException("URP Lit shader not found in editor.");
            }

            AssetDatabase.CreateAsset(new Material(shader), path);
            Debug.Log("[PlayableSceneWiring] Created " + path);
        }

        private static void SaveControllerScene(string path, string rootName, System.Type controllerType)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(rootName);
            root.AddComponent(controllerType);
            SceneManager.MoveGameObjectToScene(root, scene);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
