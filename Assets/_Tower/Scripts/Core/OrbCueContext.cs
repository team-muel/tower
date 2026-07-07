using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // The perception facts an orb cue matches against: the party's skill tags,
    // its disposition, and whether the regressor's memory sight is active.
    // Pure snapshot so cue matching is deterministic.
    public sealed class OrbCueContext
    {
        private readonly HashSet<string> skillTags;

        public OrbCueContext(
            DispositionType disposition,
            bool regressorMemorySight,
            IEnumerable<string> skillTags = null)
        {
            Disposition = disposition;
            RegressorMemorySight = regressorMemorySight;
            this.skillTags = new HashSet<string>(StringComparer.Ordinal);
            if (skillTags != null)
            {
                foreach (var tag in skillTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        this.skillTags.Add(tag);
                    }
                }
            }
        }

        public DispositionType Disposition { get; }

        public bool RegressorMemorySight { get; }

        public IReadOnlyCollection<string> SkillTags => skillTags;

        public bool HasSkillTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && skillTags.Contains(tag);
        }
    }
}
