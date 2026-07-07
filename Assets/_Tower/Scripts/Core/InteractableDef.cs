using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // Data-driven definition of a world interaction anchor (33 Reference §4/§5:
    // InteractionDef / InteractableAnchor). Pure C# — no UnityEngine — so
    // eligibility and use-record logic stay unit-testable and deterministic.
    //
    // Door (Portal) anchors carry a PortalId that points at the T25 PortalDef
    // used as the display source; this def does not re-implement portal data.
    public sealed class InteractableDef
    {
        private readonly List<string> requiredTags;
        private readonly List<AnchorStateChange> stateChanges;

        private InteractableDef(
            string id,
            InteractableKind kind,
            string prompt,
            string disabledReason,
            VisibilityRule visibilityRule,
            UseRule useRule,
            string riskPreview,
            string rewardPreview,
            string portalId,
            int maxUses,
            bool recordsOnUse,
            List<string> requiredTags,
            List<AnchorStateChange> stateChanges)
        {
            Id = id;
            Kind = kind;
            Prompt = prompt;
            DisabledReason = disabledReason;
            VisibilityRule = visibilityRule;
            UseRule = useRule;
            RiskPreview = riskPreview;
            RewardPreview = rewardPreview;
            PortalId = portalId;
            MaxUses = maxUses;
            RecordsOnUse = recordsOnUse;
            this.requiredTags = requiredTags;
            this.stateChanges = stateChanges;
        }

        public string Id { get; }

        public InteractableKind Kind { get; }

        // Short affordance label shown on hover ("문을 연다", "조사한다"...).
        public string Prompt { get; }

        // Why the anchor is unavailable when its use-rule blocks it. Locking is
        // never silence — it always explains itself (33 Reference §6).
        public string DisabledReason { get; }

        public VisibilityRule VisibilityRule { get; }

        public UseRule UseRule { get; }

        public string RiskPreview { get; }

        public string RewardPreview { get; }

        // T25 PortalDef id for Portal-kind anchors; empty otherwise.
        public string PortalId { get; }

        // Uses before the anchor is spent. Negative = unlimited.
        public int MaxUses { get; }

        // Whether a use should be written to expedition/camp QA state.
        public bool RecordsOnUse { get; }

        public IReadOnlyList<string> RequiredTags => requiredTags;

        public IReadOnlyList<AnchorStateChange> StateChanges => stateChanges;

        public static Result<InteractableDef> Create(
            string id,
            InteractableKind kind,
            string prompt,
            string disabledReason = "",
            VisibilityRule visibilityRule = VisibilityRule.Always,
            UseRule useRule = UseRule.Always,
            string riskPreview = "",
            string rewardPreview = "",
            string portalId = "",
            int maxUses = 1,
            bool recordsOnUse = true,
            IEnumerable<string> requiredTags = null,
            IEnumerable<AnchorStateChange> stateChanges = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<InteractableDef>.Failure("Interactable id is required.");
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return Result<InteractableDef>.Failure("Interactable prompt is required.");
            }

            if (kind == InteractableKind.Portal && string.IsNullOrWhiteSpace(portalId))
            {
                return Result<InteractableDef>.Failure("Portal anchors require a PortalId (T25 display source).");
            }

            if (kind != InteractableKind.Portal && !string.IsNullOrWhiteSpace(portalId))
            {
                return Result<InteractableDef>.Failure("Only Portal anchors may set a PortalId.");
            }

            if (maxUses == 0)
            {
                return Result<InteractableDef>.Failure("MaxUses must be non-zero (negative = unlimited).");
            }

            var tags = new List<string>();
            if (requiredTags != null)
            {
                foreach (var tag in requiredTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag) && !tags.Contains(tag))
                    {
                        tags.Add(tag);
                    }
                }
            }

            var changes = new List<AnchorStateChange>();
            if (stateChanges != null)
            {
                foreach (var change in stateChanges)
                {
                    changes.Add(change);
                }
            }

            return Result<InteractableDef>.Success(new InteractableDef(
                id,
                kind,
                prompt,
                disabledReason ?? string.Empty,
                visibilityRule,
                useRule,
                riskPreview ?? string.Empty,
                rewardPreview ?? string.Empty,
                portalId ?? string.Empty,
                maxUses,
                recordsOnUse,
                tags,
                changes));
        }
    }

    // Declarative gate for whether an anchor is drawn at all. Kept as an enum so
    // defs stay serializable and deterministic (no captured delegates).
    public enum VisibilityRule
    {
        // Always visible.
        Always = 0,

        // Visible only to the regressor's memory sight (orb_memory).
        RegressorMemoryOnly = 1,

        // Visible only after a death has occurred this run (묘비 accrual).
        AfterDeathOnly = 2,

        // Visible only while retreating / after 대회귀.
        WhileRetreatingOnly = 3,
    }

    // Declarative gate for whether an anchor can be used right now. When it
    // blocks, the def's DisabledReason is surfaced instead of no-op silence.
    public enum UseRule
    {
        // Usable whenever visible.
        Always = 0,

        // Requires every RequiredTag present in the party.
        RequiresTags = 1,

        // Blocked while retreating (e.g. departure gate mid-대회귀).
        NotWhileRetreating = 2,

        // Blocked once a death has occurred this run.
        NotAfterDeath = 3,
    }

    // A state mutation the anchor applies on a successful use. Guarded by
    // AnchorRuntime so illegal transitions are rejected.
    public readonly struct AnchorStateChange
    {
        public AnchorStateChange(AnchorState to)
        {
            To = to;
        }

        public AnchorState To { get; }
    }
}
