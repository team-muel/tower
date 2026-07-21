using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // Per-unit mark and amplify bookkeeping with elapsed-time expiry.
    public sealed class StatusBoard
    {
        public const float AmplifyDurationSeconds = 1f;

        private readonly Dictionary<string, Dictionary<string, MarkInstance>> marksByUnit =
            new Dictionary<string, Dictionary<string, MarkInstance>>(StringComparer.Ordinal);

        private readonly Dictionary<string, AmplifyInstance> amplifyByUnit =
            new Dictionary<string, AmplifyInstance>(StringComparer.Ordinal);

        public Result ApplyMark(string unitId, MarkDef mark, float elapsedSeconds)
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

            if (mark.DurationSeconds <= 0f || float.IsNaN(mark.DurationSeconds)
                || float.IsInfinity(mark.DurationSeconds))
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
                && elapsedSeconds < existing.ExpiresAtSeconds)
            {
                stacks = existing.Stacks + 1;
            }

            // Re-applying always refreshes the duration; stacking only per MarkDef.
            unitMarks[mark.Id] = new MarkInstance(mark, stacks, elapsedSeconds + mark.DurationSeconds);
            return Result.Success();
        }

        public bool HasMark(string unitId, string markId, float elapsedSeconds)
        {
            return GetMarkStacks(unitId, markId, elapsedSeconds) > 0;
        }

        public int GetMarkStacks(string unitId, string markId, float elapsedSeconds)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(markId))
            {
                return 0;
            }

            if (!marksByUnit.TryGetValue(unitId, out var unitMarks) || !unitMarks.TryGetValue(markId, out var instance))
            {
                return 0;
            }

            return elapsedSeconds < instance.ExpiresAtSeconds ? instance.Stacks : 0;
        }

        public bool RemoveMark(string unitId, string markId)
        {
            return !string.IsNullOrEmpty(unitId)
                && !string.IsNullOrEmpty(markId)
                && marksByUnit.TryGetValue(unitId, out var unitMarks)
                && unitMarks.Remove(markId);
        }

        public Result ApplyAmplify(string unitId, float multiplier, float elapsedSeconds)
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
            amplifyByUnit[unitId] = new AmplifyInstance(multiplier, elapsedSeconds + AmplifyDurationSeconds);
            return Result.Success();
        }

        public bool IsAmplified(string unitId, float elapsedSeconds)
        {
            return !string.IsNullOrEmpty(unitId)
                && amplifyByUnit.TryGetValue(unitId, out var instance)
                && elapsedSeconds < instance.ExpiresAtSeconds;
        }

        public float GetAmplifyMultiplier(string unitId, float elapsedSeconds)
        {
            return IsAmplified(unitId, elapsedSeconds) ? amplifyByUnit[unitId].Multiplier : 1f;
        }

        public bool TryConsumeAmplify(string unitId, float elapsedSeconds, out float multiplier)
        {
            multiplier = 1f;
            if (string.IsNullOrEmpty(unitId) || !amplifyByUnit.TryGetValue(unitId, out var instance))
            {
                return false;
            }

            amplifyByUnit.Remove(unitId);
            if (elapsedSeconds >= instance.ExpiresAtSeconds)
            {
                return false;
            }

            multiplier = instance.Multiplier;
            return true;
        }

        // QA harness support: active (non-expired) mark ids, sorted for determinism.
        public IReadOnlyList<string> GetActiveMarkIds(string unitId, float elapsedSeconds)
        {
            if (string.IsNullOrEmpty(unitId) || !marksByUnit.TryGetValue(unitId, out var unitMarks))
            {
                return Array.Empty<string>();
            }

            var ids = new List<string>();
            foreach (var pair in unitMarks)
            {
                if (elapsedSeconds < pair.Value.ExpiresAtSeconds)
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

        public void PruneExpired(float elapsedSeconds)
        {
            foreach (var unitId in new List<string>(marksByUnit.Keys))
            {
                var unitMarks = marksByUnit[unitId];
                foreach (var markId in new List<string>(unitMarks.Keys))
                {
                    if (elapsedSeconds >= unitMarks[markId].ExpiresAtSeconds)
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
                if (elapsedSeconds >= amplifyByUnit[unitId].ExpiresAtSeconds)
                {
                    amplifyByUnit.Remove(unitId);
                }
            }
        }

        private readonly struct MarkInstance
        {
            public MarkInstance(MarkDef definition, int stacks, float expiresAtSeconds)
            {
                Definition = definition;
                Stacks = stacks;
                ExpiresAtSeconds = expiresAtSeconds;
            }

            public MarkDef Definition { get; }
            public int Stacks { get; }
            public float ExpiresAtSeconds { get; }
        }

        private readonly struct AmplifyInstance
        {
            public AmplifyInstance(float multiplier, float expiresAtSeconds)
            {
                Multiplier = multiplier;
                ExpiresAtSeconds = expiresAtSeconds;
            }

            public float Multiplier { get; }
            public float ExpiresAtSeconds { get; }
        }
    }
}
