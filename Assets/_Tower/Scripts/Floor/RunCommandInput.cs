using System;
using Tower.Combat;
using Tower.Core;
using UnityEngine;

namespace Tower.Floor
{
    /// <summary>
    /// Minimal package-β command surface for the generated run.
    ///
    /// Z/X/C/V choose the player's corresponding loadout slot and issue a
    /// precise ability order. F1-F3 select a companion, 1/2/3 set
    /// Assault/Guard/Focus, and Q issues that companion's first ready ability
    /// against the nearest enemy. Precise orders are accepted only while Left
    /// Shift bullet-time is the active command window. The input layer is
    /// intentionally thin; all validation lives in Core or
    /// GeneratedFloorEncounterHost.
    /// </summary>
    public sealed class RunCommandInput : MonoBehaviour
    {
        private static readonly KeyCode[] PlayerAbilityKeys =
        {
            KeyCode.Z,
            KeyCode.X,
            KeyCode.C,
            KeyCode.V
        };

        private ForestFloorRenderer floor;
        private int selectedIndex;
        private string feedback = string.Empty;
        private float feedbackUntil;

        public void Configure(ForestFloorRenderer target)
        {
            floor = target;
        }

        private void Update()
        {
            GeneratedFloorEncounterHost encounter = floor == null ? null : floor.ActiveEncounter;
            if (encounter == null || encounter.IsResolved || encounter.IsPlayerDefeated
                || encounter.CombatState == null)
            {
                return;
            }

            HandlePlayerAbilityInput(encounter);
            if (encounter.Companions == null || encounter.Companions.Count == 0)
            {
                return;
            }

            HandleSelection(encounter);
            CompanionEntity selected = SelectedCompanion(encounter);
            if (selected == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ShowResult(encounter.SetCommandStance(selected.UnitId, CommandStance.Assault));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ShowResult(encounter.SetCommandStance(selected.UnitId, CommandStance.Guard));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ShowResult(encounter.SetFocusStanceOnNearestEnemy(selected.UnitId));
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                ShowResult(encounter.IssueBestPreciseOrder(
                    selected.UnitId,
                    floor.IsSlowMoCommandWindow));
            }
        }

        public static int AbilitySlotForKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Z: return 0;
                case KeyCode.X: return 1;
                case KeyCode.C: return 2;
                case KeyCode.V: return 3;
                default: return -1;
            }
        }

        private void HandlePlayerAbilityInput(GeneratedFloorEncounterHost encounter)
        {
            for (int index = 0; index < PlayerAbilityKeys.Length; index++)
            {
                KeyCode key = PlayerAbilityKeys[index];
                if (!Input.GetKeyDown(key))
                {
                    continue;
                }

                ShowResult(encounter.IssuePlayerPreciseOrder(
                    AbilitySlotForKey(key),
                    floor.IsSlowMoCommandWindow));
                return;
            }
        }

        private void HandleSelection(GeneratedFloorEncounterHost encounter)
        {
            if (Input.GetKeyDown(KeyCode.F1)) selectedIndex = 0;
            if (Input.GetKeyDown(KeyCode.F2)) selectedIndex = 1;
            if (Input.GetKeyDown(KeyCode.F3)) selectedIndex = 2;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, encounter.Companions.Count - 1);
        }

        private CompanionEntity SelectedCompanion(GeneratedFloorEncounterHost encounter)
        {
            if (encounter.Companions == null || encounter.Companions.Count == 0)
            {
                return null;
            }

            return encounter.Companions[Mathf.Clamp(selectedIndex, 0, encounter.Companions.Count - 1)];
        }

        private void ShowResult(Result result)
        {
            feedback = result.IsSuccess ? "Command accepted" : result.Error;
            feedbackUntil = Time.unscaledTime + 1.6f;
        }

        private void OnGUI()
        {
            if (floor == null || floor.ActiveEncounter == null || floor.ActiveEncounter.IsResolved)
            {
                return;
            }

            GeneratedFloorEncounterHost encounter = floor.ActiveEncounter;
            int companionCount = encounter.Companions == null ? 0 : encounter.Companions.Count;
            if (encounter.CombatState == null)
            {
                return;
            }

            float width = 390f;
            CommandTelemetrySnapshot telemetry = encounter.CommandTelemetry;
            float height = 124f + (companionCount * 22f);
            Rect panel = new Rect(14f, Screen.height - height - 14f, width, height);
            GUI.Box(panel, GUIContent.none);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(panel.x + 8f, panel.y + 5f, width - 16f, 20f),
                "Z/X/C/V player precise (Left Shift)",
                style);
            GUI.Label(
                new Rect(panel.x + 8f, panel.y + 25f, width - 16f, 20f),
                "F1-F3 select · 1 Assault · 2 Guard · 3 Focus · Q precise",
                style);
            GUI.Label(
                new Rect(panel.x + 8f, panel.y + 45f, width - 16f, 20f),
                "Commands  stance:" + telemetry.StanceCommands
                    + "  precise:" + telemetry.PreciseOrdersIssued
                    + "  used:" + telemetry.PreciseOrdersConsumed,
                style);
            GUI.Label(
                new Rect(panel.x + 8f, panel.y + 65f, width - 16f, 20f),
                "replaced:" + telemetry.PreciseOrdersReplaced
                    + "  fallback:" + telemetry.PreciseOrderFallbacks
                    + "  expired:" + telemetry.PreciseOrdersExpired,
                style);

            for (int index = 0; index < companionCount; index++)
            {
                CompanionEntity companion = encounter.Companions[index];
                if (companion == null) continue;
                CommandStanceAssignment assignment = encounter.CommandBoard.GetAssignment(
                    companion.UnitId,
                    companion.Disposition);
                string marker = index == selectedIndex ? "> " : "  ";
                string target = string.IsNullOrEmpty(assignment.FocusTargetId)
                    ? string.Empty
                    : " -> " + assignment.FocusTargetId;
                GUI.Label(
                    new Rect(panel.x + 8f, panel.y + 86f + (index * 22f), width - 16f, 20f),
                    marker + companion.DisplayName + "  ["
                        + CommandStanceRules.DisplayName(assignment.Stance) + "]" + target,
                    style);
            }

            if (Time.unscaledTime < feedbackUntil && !string.IsNullOrEmpty(feedback))
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(panel.x + 8f, panel.y + height - 20f, width - 16f, 20f), feedback, style);
                GUI.color = Color.white;
            }
        }
    }
}
