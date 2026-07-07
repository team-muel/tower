using System.Text;

namespace Tower.Core
{
    // Pure interaction-state engine (33 Reference §2 interaction engine).
    // Given a def, its runtime, and a world context, decides visible / enabled /
    // disabledReason / preview. Locking always explains itself — a disabled
    // anchor is still visible with a reason, never silent (§6).
    public static class InteractionResolver
    {
        public static InteractionState Resolve(
            InteractableDef def,
            AnchorRuntime runtime,
            InteractionContext context)
        {
            if (def == null)
            {
                return new InteractionState("", false, false, "No definition.", "", 0, false);
            }

            int uses = runtime != null ? runtime.UsesRemaining : def.MaxUses;
            string preview = BuildPreview(def);

            bool visible = IsVisible(def, context);
            if (!visible)
            {
                return new InteractionState(def.Id, false, false, "", preview, uses, def.RecordsOnUse);
            }

            // Spent anchors stay visible (so the player sees the outcome) but
            // are disabled.
            if (runtime != null && runtime.IsSpent)
            {
                return new InteractionState(
                    def.Id, true, false, Reason(def, "이미 사용됨."), preview, uses, def.RecordsOnUse);
            }

            string block = BlockReason(def, context);
            if (block != null)
            {
                return new InteractionState(def.Id, true, false, block, preview, uses, def.RecordsOnUse);
            }

            return new InteractionState(def.Id, true, true, "", preview, uses, def.RecordsOnUse);
        }

        private static bool IsVisible(InteractableDef def, InteractionContext context)
        {
            switch (def.VisibilityRule)
            {
                case VisibilityRule.RegressorMemoryOnly:
                    // Regressor memory sight is modelled as the "regressor" party
                    // tag being present.
                    return context != null && context.HasTag("regressor");
                case VisibilityRule.AfterDeathOnly:
                    return context != null && context.DeathThisRun;
                case VisibilityRule.WhileRetreatingOnly:
                    return context != null && context.Retreating;
                case VisibilityRule.Always:
                default:
                    return true;
            }
        }

        // Returns a disabled reason, or null when the anchor is usable.
        private static string BlockReason(InteractableDef def, InteractionContext context)
        {
            switch (def.UseRule)
            {
                case UseRule.RequiresTags:
                    if (context == null || !context.HasAllTags(def.RequiredTags))
                    {
                        return Reason(def, "조건 미충족.");
                    }

                    return null;
                case UseRule.NotWhileRetreating:
                    if (context != null && context.Retreating)
                    {
                        return Reason(def, "후퇴 중에는 불가.");
                    }

                    return null;
                case UseRule.NotAfterDeath:
                    if (context != null && context.DeathThisRun)
                    {
                        return Reason(def, "사망 이후 불가.");
                    }

                    return null;
                case UseRule.Always:
                default:
                    return null;
            }
        }

        // Prefer the def's authored reason; fall back to a rule default.
        private static string Reason(InteractableDef def, string fallback)
        {
            return string.IsNullOrWhiteSpace(def.DisabledReason) ? fallback : def.DisabledReason;
        }

        private static string BuildPreview(InteractableDef def)
        {
            bool hasRisk = !string.IsNullOrWhiteSpace(def.RiskPreview);
            bool hasReward = !string.IsNullOrWhiteSpace(def.RewardPreview);
            if (!hasRisk && !hasReward)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            if (hasRisk)
            {
                sb.Append("위험: ").Append(def.RiskPreview);
            }

            if (hasReward)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" | ");
                }

                sb.Append("보상: ").Append(def.RewardPreview);
            }

            return sb.ToString();
        }
    }
}
