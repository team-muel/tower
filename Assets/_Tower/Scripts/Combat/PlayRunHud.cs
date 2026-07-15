using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public struct HudAbilitySlot
    {
        public string Label;
        public float CooldownFraction; // 0 = ready, 1 = just used
        public bool Ready;
    }

    public sealed class PlayRunHudModel
    {
        public string RunLine = string.Empty;
        public string RewardLine = string.Empty;
        public bool CombatVisible;
        public string PlayerHpLine = string.Empty;
        public float PlayerHpFraction;
        public float SlowMoCharge = -1f; // negative = gauge hidden
        public List<HudAbilitySlot> Slots = new List<HudAbilitySlot>();
    }

    // T60: composes the play HUD from live Core state. Pure and EditMode-
    // testable; the MonoBehaviour below only renders the model. Command and
    // counter UI stay owner-frozen and are deliberately absent.
    public static class PlayRunHudComposer
    {
        public static PlayRunHudModel Compose(
            RunLifecycle run,
            CombatantRef player,
            MetaProgress meta = null,
            float slowMoCharge = -1f)
        {
            var model = new PlayRunHudModel { SlowMoCharge = slowMoCharge };
            if (run != null)
            {
                model.RunLine = run.IsConquered
                    ? $"CONQUERED · Events {run.Progress.CompletedCount}/{run.Progress.Plan.Slots.Count}"
                    : $"Floor {run.FloorNumber}/{RunEventPlan.FloorCount} · "
                        + $"Events {run.Progress.CompletedCount}/{run.Progress.Plan.Slots.Count} · "
                        + $"Retreats {run.RetreatCount}";
                model.RewardLine =
                    $"Resource x{run.Rewards.AmountOf(RewardType.Resource)} · "
                    + $"Ability x{run.Rewards.AmountOf(RewardType.Ability)}";
            }

            if (meta != null)
            {
                model.RewardLine = string.IsNullOrEmpty(model.RewardLine)
                    ? $"Platinum x{meta.Platinum}"
                    : model.RewardLine + $" · Platinum x{meta.Platinum}";
            }

            if (player == null || player.State == null || player.State.Definition == null)
            {
                return model;
            }

            model.CombatVisible = true;
            int maxHp = Mathf.Max(1, player.State.Definition.MaxHp);
            model.PlayerHpLine = $"{player.State.Definition.DisplayName}  {player.State.CurrentHp}/{maxHp}";
            model.PlayerHpFraction = Mathf.Clamp01(player.State.CurrentHp / (float)maxHp);

            AbilityLoadout loadout = player.State.Loadout;
            if (loadout != null)
            {
                foreach (AbilityDef ability in loadout.Abilities)
                {
                    if (ability == null)
                    {
                        continue;
                    }

                    float remaining = player.State.RemainingCooldownSeconds(ability.Id);
                    float total = Mathf.Max(0.0001f, ability.CooldownSeconds);
                    model.Slots.Add(new HudAbilitySlot
                    {
                        Label = ability.DisplayName,
                        CooldownFraction = ability.CooldownSeconds <= 0f
                            ? 0f
                            : Mathf.Clamp01(remaining / total),
                        Ready = remaining <= 0f
                    });
                }
            }

            return model;
        }
    }

    // Provisional IMGUI presentation (same lane as EncounterResultPresenter);
    // final styling belongs to the art/미감 pass.
    public sealed class PlayRunHud : MonoBehaviour
    {
        private System.Func<PlayRunHudModel> source;
        private GUIStyle lineStyle;
        private GUIStyle slotStyle;

        public void Configure(System.Func<PlayRunHudModel> modelSource)
        {
            source = modelSource;
        }

        private void OnGUI()
        {
            if (source == null)
            {
                return;
            }

            PlayRunHudModel model = source();
            if (model == null)
            {
                return;
            }

            EnsureStyles();
            if (!string.IsNullOrEmpty(model.RunLine))
            {
                DrawPanel(new Rect(14f, 12f, 430f, 30f), model.RunLine, TextAnchor.MiddleLeft);
            }

            if (model.SlowMoCharge >= 0f)
            {
                // T64 revolver gauge: thin amber bar under the run panel.
                GUI.color = new Color(0.13f, 0.13f, 0.13f, 0.85f);
                GUI.DrawTexture(new Rect(14f, 46f, 430f, 6f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.78f, 0.3f, 0.95f);
                GUI.DrawTexture(
                    new Rect(14f, 46f, 430f * Mathf.Clamp01(model.SlowMoCharge), 6f),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (!string.IsNullOrEmpty(model.RewardLine))
            {
                DrawPanel(new Rect(Screen.width - 320f, 12f, 306f, 30f), model.RewardLine, TextAnchor.MiddleRight);
            }

            if (!model.CombatVisible)
            {
                return;
            }

            const float barWidth = 340f;
            float barX = (Screen.width - barWidth) * 0.5f;
            float barY = Screen.height - 96f;
            GUI.Box(new Rect(barX - 8f, barY - 8f, barWidth + 16f, 78f), GUIContent.none);
            GUI.color = new Color(0.16f, 0.16f, 0.16f, 0.9f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, 16f), Texture2D.whiteTexture);
            GUI.color = new Color(0.86f, 0.28f, 0.24f, 0.95f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth * model.PlayerHpFraction, 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY - 2f, barWidth, 20f), model.PlayerHpLine, lineStyle);

            float slotWidth = 82f;
            float slotsX = barX;
            float slotsY = barY + 24f;
            for (int index = 0; index < model.Slots.Count; index++)
            {
                HudAbilitySlot slot = model.Slots[index];
                Rect slotRect = new Rect(slotsX + (index * (slotWidth + 6f)), slotsY, slotWidth, 38f);
                GUI.color = slot.Ready
                    ? new Color(0.22f, 0.34f, 0.22f, 0.95f)
                    : new Color(0.24f, 0.24f, 0.24f, 0.95f);
                GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                if (!slot.Ready)
                {
                    GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.75f);
                    GUI.DrawTexture(
                        new Rect(slotRect.x, slotRect.y, slotRect.width, slotRect.height * slot.CooldownFraction),
                        Texture2D.whiteTexture);
                }

                GUI.color = Color.white;
                GUI.Label(slotRect, slot.Label, slotStyle);
            }
        }

        private void DrawPanel(Rect rect, string text, TextAnchor anchor)
        {
            GUI.Box(rect, GUIContent.none);
            var style = new GUIStyle(lineStyle) { alignment = anchor, padding = new RectOffset(8, 8, 0, 0) };
            GUI.Label(rect, text, style);
        }

        private void EnsureStyles()
        {
            if (lineStyle != null)
            {
                return;
            }

            lineStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            slotStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }
    }
}
