using System.IO;
using Tower.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class BootMenuController : MonoBehaviour
    {
        private Button continueButton;
        private Text saveStatus;

        private void Start()
        {
            BuildMenu();
            Refresh();
        }

        private void BuildMenu()
        {
            var canvas = RuntimeSceneUi.CreateCanvas("Boot Canvas");
            var panel = RuntimeSceneUi.CreatePanel(
                canvas.transform,
                "Main Menu",
                new Vector2(0.32f, 0.18f),
                new Vector2(0.68f, 0.82f),
                Vector2.zero,
                Vector2.zero);

            RuntimeSceneUi.AddText(panel, "Title", "Tower (working title)", 34, TextAnchor.MiddleCenter);
            RuntimeSceneUi.AddText(panel, "Subtitle", "Grid tactics vertical slice", 18, TextAnchor.MiddleCenter);
            saveStatus = RuntimeSceneUi.AddText(panel, "Save Status", string.Empty, 15, TextAnchor.MiddleCenter);

            RuntimeSceneUi.AddButton(panel, "New Expedition", StartNewExpedition);
            continueButton = RuntimeSceneUi.AddButton(panel, "Continue", ContinueExpedition);
            RuntimeSceneUi.AddButton(panel, "Quit", Quit);
        }

        private void Refresh()
        {
            var repository = CreateRepository();
            var hasSave = repository != null && repository.HasSave;
            if (continueButton != null)
            {
                continueButton.interactable = hasSave;
            }

            if (saveStatus != null)
            {
                saveStatus.text = hasSave ? "Checkpoint save found." : "No checkpoint save yet.";
            }
        }

        private void StartNewExpedition()
        {
            var repository = CreateRepository();
            repository?.Delete();
            PlayerPrefs.SetInt(TowerSceneNames.NewExpeditionPref, 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene(TowerSceneNames.Loadout);
        }

        private void ContinueExpedition()
        {
            PlayerPrefs.SetInt(TowerSceneNames.NewExpeditionPref, 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(TowerSceneNames.Expedition);
        }

        private static SaveRepository CreateRepository()
        {
            var path = Path.Combine(Application.persistentDataPath, TowerSceneNames.SaveFileName);
            var repository = SaveRepository.Create(path);
            return repository.IsSuccess ? repository.Value : null;
        }

        private static void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
