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
            new EditorBuildSettingsScene("Assets/_Tower/Scenes/Loadout.unity", true),
            new EditorBuildSettingsScene("Assets/_Tower/Scenes/Expedition.unity", true)
        };

        public static void WireScenes()
        {
            SaveControllerScene("Assets/_Tower/Scenes/Boot.unity", "Boot Menu", typeof(BootMenuController));
            SaveControllerScene("Assets/_Tower/Scenes/Loadout.unity", "Loadout Menu", typeof(LoadoutMenuController));
            SaveControllerScene("Assets/_Tower/Scenes/Expedition.unity", "Playable Expedition", typeof(PlayableExpeditionController));
            EditorBuildSettings.scenes = BuildScenes;
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayableSceneWiring] Boot, Loadout, and Expedition scenes registered.");
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
