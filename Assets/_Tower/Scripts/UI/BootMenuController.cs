using System.Collections.Generic;
using System.IO;
using Tower.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class BootMenuController : MonoBehaviour
    {
        private readonly List<string> qaButtonNames = new List<string>();
        private Button continueButton;
        private Text saveStatus;

        private void Start()
        {
            BuildMenu();
            Refresh();
        }

        private void OnDestroy()
        {
            foreach (var name in qaButtonNames)
            {
                QaRuntime.UnregisterButton(name);
            }

            qaButtonNames.Clear();
        }

        private void BuildMenu()
        {
            RuntimeSceneUi.EnsureClearCamera();
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

            RegisterQaButton(RuntimeSceneUi.AddButton(panel, "New Expedition", StartNewExpedition));
            continueButton = RegisterQaButton(RuntimeSceneUi.AddButton(panel, "Continue", ContinueExpedition));
            RegisterQaButton(RuntimeSceneUi.AddButton(panel, "Quit", Quit));
        }

        private Button RegisterQaButton(Button button)
        {
            if (button == null)
            {
                return null;
            }

            var name = button.gameObject.name;
            qaButtonNames.Add(name);
            QaRuntime.RegisterButton(name, () => button.onClick.Invoke());
            return button;
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
            SceneManager.LoadScene(TowerSceneNames.Camp);
        }

        private void ContinueExpedition()
        {
            // T15: Continue also routes through the camp hub; the checkpoint
            // save is picked up when the expedition scene finally loads.
            PlayerPrefs.SetInt(TowerSceneNames.NewExpeditionPref, 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(TowerSceneNames.Camp);
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
