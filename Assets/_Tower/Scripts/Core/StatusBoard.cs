using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // Per-unit mark and amplify bookkeeping with round-based expiry.
    // T3 exposes round progression as TurnEngine.RoundNumber (no event), so all
    // queries take the current round and expire lazily; OnRoundAdvanced prunes.
    public sealed class StatusBoard
    {
        // v0: the amplified status lasts one round (the round it was applied in).
        public const int AmplifyDurationRounds = 1;

        private readonly Dictionary<string, Dictionary<string, MarkInstance>> marksByUnit =
            new Dictionary<string, Dictionary<string, MarkInstance>>(StringComparer.Ordinal);

        private readonly Dictionary<string, AmplifyInstance> amplifyByUnit =
            new Dictionary<string, AmplifyInstance>(StringComparer.Ordinal);

        public Result ApplyMark(string unitId, MarkDef mark, int currentRound)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return Result.Failure("Unit id is required.");
            }

            if (mark == null)
            {
                return Result.Failure("Mark definition is required.");
            }

            if (string.IsNullOrWhiteSpace(mark.Id))
            {
                return Result.Failure("Mark id is required.");
            }

            if (mark.DurationTurns <= 0)
            {
                return Result.Failure("Mark duration must be positive.");
            }

            if (!marksByUnit.TryGetValue(unitId, out var unitMarks))
            {
                unitMarks = new Dictionary<string, MarkInstance>(StringComparer.Ordinal);
                marksByUnit.Add(unitId, unitMarks);
            }

            var stacks = 1;
            if (mark.Stackable
                && unitMarks.TryGetValue(mark.Id, out var existing)
                && currentRound < existing.ExpiresAtRound)
            {
                stacks = existing.Stacks + 1;
            }

            // Re-applying always refreshes the duration; stacking only per MarkDef.
            unitMarks[mark.Id] = new MarkInstance(mark, stacks, currentRound + mark.DurationTurns);
            return Result.Success();
        }

        public bool HasMark(string unitId, string markId, int currentRound)
        {
            return GetMarkStacks(unitId, markId, currentRound) > 0;
        }

        public int GetMarkStacks(string unitId, string markId, int currentRound)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(markId))
            {
                return 0;
            }

            if (!marksByUnit.TryGetValue(unitId, out var unitMarks) || !unitMarks.TryGetValue(markId, out var instance))
            {
                return 0;
            }

            return currentRound < instance.ExpiresAtRound ? instance.Stacks : 0;
        }

        public bool RemoveMark(string unitId, string markId)
        {
            return !string.IsNullOrEmpty(unitId)
                && !string.IsNullOrEmpty(markId)
                && marksByUnit.TryGetValue(unitId, out var unitMarks)
                && unitMarks.Remove(markId);
        }

        public Result ApplyAmplify(string unitId, float multiplier, int currentRound)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return Result.Failure("Unit id is required.");
            }

            if (multiplier <= 0f)
            {
                return Result.Failure("Amplify multiplier must be positive.");
            }

            // v0: re-applying refreshes the status; it never stacks.
            amplifyByUnit[unitId] = new AmplifyInstance(multiplier, currentRound + AmplifyDurationRounds);
            return Result.Success();
        }

        public bool IsAmplified(string unitId, int currentRound)
        {
            return !string.IsNullOrEmpty(unitId)
                && amplifyByUnit.TryGetValue(unitId, out var instance)
                && currentRound < instance.ExpiresAtRound;
        }

        public float GetAmplifyMultiplier(string unitId, int currentRound)
        {
            return IsAmplified(unitId, currentRound) ? amplifyByUnit[unitId].Multiplier : 1f;
        }

        public bool TryConsumeAmplify(string unitId, int currentRound, out float multiplier)
        {
            multiplier = 1f;
            if (string.IsNullOrEmpty(unitId) || !amplifyByUnit.TryGetValue(unitId, out var instance))
            {
                return false;
            }

            amplifyByUnit.Remove(unitId);
            if (currentRound >= instance.ExpiresAtRound)
            {
                return false;
            }

            multiplier = instance.Multiplier;
            return true;
        }

        // QA harness support: active (non-expired) mark ids, sorted for determinism.
        public IReadOnlyList<string> GetActiveMarkIds(string unitId, int currentRound)
        {
            if (string.IsNullOrEmpty(unitId) || !marksByUnit.TryGetValue(unitId, out var unitMarks))
            {
                return Array.Empty<string>();
            }

            var ids = new List<string>();
            foreach (var pair in unitMarks)
            {
                if (currentRound < pair.Value.ExpiresAtRound)
                {
                    ids.Add(pair.Key);
                }
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        public void ClearUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return;
            }

            marksByUnit.Remove(unitId);
            amplifyByUnit.Remove(unitId);
        }

        // Round-progression hook: call with TurnEngine.RoundNumber to prune expired statuses.
        public void OnRoundAdvanced(int currentRound)
        {
            foreach (var unitId in new List<string>(marksByUnit.Keys))
            {
                var unitMarks = marksByUnit[unitId];
                foreach (var markId in new List<string>(unitMarks.Keys))
                {
                    if (currentRound >= unitMarks[markId].ExpiresAtRound)
                    {
                        unitMarks.Remove(markId);
                    }
                }

                if (unitMarks.Count == 0)
                {
                    marksByUnit.Remove(unitId);
                }
            }

            foreach (var unitId in new List<string>(amplifyByUnit.Keys))
            {
                if (currentRound >= amplifyByUnit[unitId].ExpiresAtRound)
                {
                    amplifyByUnit.Remove(unitId);
                }
            }
        }

        private readonly struct MarkInstance
        {
            public MarkInstance(MarkDef definition, int stacks, int expiresAtRound)
            {
                Definition = definition;
                Stacks = stacks;
                ExpiresAtRound = expiresAtRound;
            }

            public MarkDef Definition { get; }
            public int Stacks { get; }
            public int ExpiresAtRound { get; }
        }

        private readonly struct AmplifyInstance
        {
            public AmplifyInstance(float multiplier, int expiresAtRound)
            {
                Multiplier = multiplier;
                ExpiresAtRound = expiresAtRound;
            }

            public float Multiplier { get; }
            public int ExpiresAtRound { get; }
        }
    }
}
