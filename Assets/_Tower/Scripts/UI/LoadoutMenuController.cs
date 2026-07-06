using System.Collections.Generic;
using System.Linq;
using Tower.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class LoadoutMenuController : MonoBehaviour
    {
        private readonly Dictionary<string, Text> speedLines = new Dictionary<string, Text>();
        private readonly List<string> qaButtonNames = new List<string>();
        private TowerSliceContent content;

        private void Start()
        {
            content = TowerSliceContent.Create();
            BuildLoadout();
        }

        private void OnDestroy()
        {
            foreach (var name in qaButtonNames)
            {
                QaRuntime.UnregisterButton(name);
            }

            qaButtonNames.Clear();
        }

        private void BuildLoadout()
        {
            var canvas = RuntimeSceneUi.CreateCanvas("Loadout Canvas");
            var panel = RuntimeSceneUi.CreatePanel(
                canvas.transform,
                "Loadout",
                new Vector2(0.18f, 0.08f),
                new Vector2(0.82f, 0.92f),
                Vector2.zero,
                Vector2.zero);

            RuntimeSceneUi.AddText(panel, "Title", "Loadout", 30, TextAnchor.MiddleCenter);
            RuntimeSceneUi.AddText(panel, "Summary", "Party: regressor plus three companions. Tune speed, then depart.", 16, TextAnchor.MiddleCenter);

            foreach (var id in TowerSliceContent.PartyIds)
            {
                AddMemberRow(panel, id);
            }

            RegisterQaButton(RuntimeSceneUi.AddButton(panel, "Start Expedition", StartExpedition));
            RegisterQaButton(RuntimeSceneUi.AddButton(panel, "Back", () => SceneManager.LoadScene(TowerSceneNames.Boot)));
            Refresh();
        }

        private void AddMemberRow(Transform parent, string characterId)
        {
            var definition = content.Characters[characterId];
            var row = RuntimeSceneUi.CreatePanel(
                parent,
                characterId + " Row",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            var abilities = string.Join(", ", definition.DefaultAbilities.Select(ability => ability.DisplayName));
            speedLines[characterId] = RuntimeSceneUi.AddText(
                row,
                characterId + " Speed",
                string.Empty,
                16,
                TextAnchor.MiddleLeft);
            RuntimeSceneUi.AddText(row, characterId + " Abilities", "Slots: " + abilities, 14, TextAnchor.MiddleLeft);

            var controls = new GameObject(characterId + " Speed Controls");
            controls.transform.SetParent(row, false);
            var layout = controls.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;

            // Speed rows repeat per character; give the buttons unique GameObject
            // names so the QA registry (keyed by GameObject name) stays unambiguous.
            var minus = RuntimeSceneUi.AddButton(controls.transform, "- Speed", () => AdjustSpeed(characterId, -1));
            minus.gameObject.name = characterId + " - Speed Button";
            RegisterQaButton(minus);
            var plus = RuntimeSceneUi.AddButton(controls.transform, "+ Speed", () => AdjustSpeed(characterId, 1));
            plus.gameObject.name = characterId + " + Speed Button";
            RegisterQaButton(plus);
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

        private void AdjustSpeed(string characterId, int delta)
        {
            TowerSliceContent.SetSpeedModifier(characterId, TowerSliceContent.GetSpeedModifier(characterId) + delta);
            Refresh();
        }

        private void Refresh()
        {
            foreach (var pair in speedLines)
            {
                var definition = content.Characters[pair.Key];
                var modifier = TowerSliceContent.GetSpeedModifier(pair.Key);
                pair.Value.text = $"{definition.DisplayName} | HP {definition.MaxHp} | Speed {definition.Speed + modifier} ({modifier:+#;-#;0})";
            }
        }

        private static void StartExpedition()
        {
            PlayerPrefs.SetInt(TowerSceneNames.NewExpeditionPref, 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene(TowerSceneNames.Expedition);
        }
    }
}
