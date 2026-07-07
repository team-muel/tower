namespace Tower.Core
{
    // A short cognition marker attached to an orb-bearing anchor (33 Reference
    // §4/§5: OrbCueDef). Not dialogue — a terse tell gated by a skill tag,
    // disposition, or regressor memory. Pure C# so condition matching is
    // deterministic and unit-testable.
    public sealed class OrbCueDef
    {
        private OrbCueDef(
            string id,
            OrbCueKind cueKind,
            string requiredSkillTag,
            DispositionType? requiredDisposition,
            bool requiresRegressorMemory,
            string markerText)
        {
            Id = id;
            CueKind = cueKind;
            RequiredSkillTag = requiredSkillTag;
            RequiredDisposition = requiredDisposition;
            RequiresRegressorMemory = requiresRegressorMemory;
            MarkerText = markerText;
        }

        public string Id { get; }

        public OrbCueKind CueKind { get; }

        // Empty = no skill gate.
        public string RequiredSkillTag { get; }

        // The cue only surfaces for a party with this disposition; null = any.
        public DispositionType? RequiredDisposition { get; }

        // When true, only the regressor's memory sight surfaces this cue.
        public bool RequiresRegressorMemory { get; }

        // The short marker text shown when the cue matches.
        public string MarkerText { get; }

        // Which orb filter bucket this cue belongs to (기본 always includes it).
        public OrbFilter Filter
        {
            get
            {
                switch (CueKind)
                {
                    case OrbCueKind.Memory:
                    case OrbCueKind.Companion:
                        return OrbFilter.Cognition;
                    case OrbCueKind.Hazard:
                        return OrbFilter.Hazard;
                    case OrbCueKind.Loot:
                        return OrbFilter.Loot;
                    default:
                        return OrbFilter.All;
                }
            }
        }

        public static Result<OrbCueDef> Create(
            string id,
            OrbCueKind cueKind,
            string markerText,
            string requiredSkillTag = "",
            DispositionType? requiredDisposition = null,
            bool requiresRegressorMemory = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<OrbCueDef>.Failure("Orb cue id is required.");
            }

            if (string.IsNullOrWhiteSpace(markerText))
            {
                return Result<OrbCueDef>.Failure("Orb cue marker text is required.");
            }

            return Result<OrbCueDef>.Success(new OrbCueDef(
                id,
                cueKind,
                requiredSkillTag ?? string.Empty,
                requiredDisposition,
                requiresRegressorMemory,
                markerText));
        }

        // Matches when every configured gate is satisfied by the context.
        public bool Matches(OrbCueContext context)
        {
            if (context == null)
            {
                return false;
            }

            if (RequiresRegressorMemory && !context.RegressorMemorySight)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(RequiredSkillTag) && !context.HasSkillTag(RequiredSkillTag))
            {
                return false;
            }

            if (RequiredDisposition.HasValue
                && context.Disposition != RequiredDisposition.Value)
            {
                return false;
            }

            return true;
        }

        // Whether this cue passes a given orb filter (기본 shows all).
        public bool PassesFilter(OrbFilter filter)
        {
            return filter == OrbFilter.All || filter == Filter;
        }
    }
}
