using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // The world facts an interaction resolves against (33 Reference §6 UI
    // behavior): who is present, how deep, whether we are retreating, whether a
    // death has occurred this run, and which biome. Pure snapshot so eligibility
    // is deterministic for a given input.
    public sealed class InteractionContext
    {
        private readonly HashSet<string> partyTags;

        public InteractionContext(
            int floor,
            bool retreating,
            bool deathThisRun,
            string biomeId,
            IEnumerable<string> partyTags = null)
        {
            Floor = floor;
            Retreating = retreating;
            DeathThisRun = deathThisRun;
            BiomeId = biomeId ?? string.Empty;
            this.partyTags = new HashSet<string>(StringComparer.Ordinal);
            if (partyTags != null)
            {
                foreach (var tag in partyTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        this.partyTags.Add(tag);
                    }
                }
            }
        }

        public int Floor { get; }

        public bool Retreating { get; }

        public bool DeathThisRun { get; }

        public string BiomeId { get; }

        public IReadOnlyCollection<string> PartyTags => partyTags;

        public bool HasTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && partyTags.Contains(tag);
        }

        // True when every required tag is present in the party (empty = always).
        public bool HasAllTags(IReadOnlyList<string> required)
        {
            if (required == null || required.Count == 0)
            {
                return true;
            }

            foreach (var tag in required)
            {
                if (!partyTags.Contains(tag))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
