using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // Data-driven table of interactable anchors placed in a room or camp, each
    // paired with its runtime. Ids are unique (Ordinal) so the QA harness and
    // presenter can address anchors unambiguously (33 Reference §4 QA state).
    public sealed class InteractableRegistry
    {
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<Entry> Entries => entries;

        public Result Add(InteractableDef def)
        {
            if (def == null)
            {
                return Result.Failure("Def is required.");
            }

            return Add(def, AnchorRuntime.CreateDefault(def.Kind, def.MaxUses));
        }

        public Result Add(InteractableDef def, AnchorRuntime runtime)
        {
            if (def == null)
            {
                return Result.Failure("Def is required.");
            }

            if (runtime == null)
            {
                return Result.Failure("Runtime is required.");
            }

            if (runtime.Kind != def.Kind)
            {
                return Result.Failure("Runtime kind must match def kind.");
            }

            if (index.ContainsKey(def.Id))
            {
                return Result.Failure($"Interactable '{def.Id}' is already registered.");
            }

            index[def.Id] = entries.Count;
            entries.Add(new Entry(def, runtime));
            return Result.Success();
        }

        public Entry Find(string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && index.TryGetValue(id, out int i))
            {
                return entries[i];
            }

            return null;
        }

        // Resolves every anchor against the context — the QA "hoverable anchor
        // list" plus locked reasons.
        public IReadOnlyList<InteractionState> ResolveAll(InteractionContext context)
        {
            var states = new List<InteractionState>(entries.Count);
            foreach (var entry in entries)
            {
                states.Add(InteractionResolver.Resolve(entry.Def, entry.Runtime, context));
            }

            return states;
        }

        // The subset of anchors the player can currently hover (visible).
        public IReadOnlyList<InteractionState> HoverableAnchors(InteractionContext context)
        {
            var result = new List<InteractionState>();
            foreach (var state in ResolveAll(context))
            {
                if (state.CanHover)
                {
                    result.Add(state);
                }
            }

            return result;
        }

        // Applies a use: resolves eligibility, then transitions the runtime.
        // Returns the applied state change or a failure explaining why not.
        public Result<InteractionState> Use(string id, InteractionContext context)
        {
            var entry = Find(id);
            if (entry == null)
            {
                return Result<InteractionState>.Failure($"No anchor '{id}'.");
            }

            var state = InteractionResolver.Resolve(entry.Def, entry.Runtime, context);
            if (!state.Enabled)
            {
                string why = string.IsNullOrWhiteSpace(state.DisabledReason)
                    ? "사용 불가."
                    : state.DisabledReason;
                return Result<InteractionState>.Failure(why);
            }

            // Apply declared state changes in order; the first drives use count.
            if (entry.Def.StateChanges.Count > 0)
            {
                foreach (var change in entry.Def.StateChanges)
                {
                    var transition = entry.Runtime.Transition(change.To);
                    if (transition.IsFailure)
                    {
                        return Result<InteractionState>.Failure(transition.Error);
                    }
                }
            }
            else if (entry.Runtime.UsesRemaining > 0)
            {
                // No declared transition (e.g. Inspect): just consume a use.
                entry.ConsumeUse();
            }

            var after = InteractionResolver.Resolve(entry.Def, entry.Runtime, context);
            return Result<InteractionState>.Success(after);
        }

        public sealed class Entry
        {
            public Entry(InteractableDef def, AnchorRuntime runtime)
            {
                Def = def;
                Runtime = runtime;
            }

            public InteractableDef Def { get; }

            public AnchorRuntime Runtime { get; }

            // Consume a use without a state transition (Inspect-style anchors).
            internal void ConsumeUse()
            {
                Runtime.ConsumeUse();
            }
        }
    }
}
