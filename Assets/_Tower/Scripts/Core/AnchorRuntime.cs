using System.Collections.Generic;

namespace Tower.Core
{
    // Mutable per-anchor runtime: current lifecycle state + remaining uses.
    // Transitions are guarded per InteractableKind so illegal moves (e.g.
    // re-arming a triggered trap, unlooting a looted chest) are rejected rather
    // than silently applied. Deterministic — no randomness, no engine types.
    public sealed class AnchorRuntime
    {
        // kind -> (from -> set of legal to-states).
        private static readonly Dictionary<InteractableKind, Dictionary<AnchorState, HashSet<AnchorState>>> Transitions
            = BuildTransitions();

        private AnchorRuntime(InteractableKind kind, AnchorState state, int usesRemaining)
        {
            Kind = kind;
            State = state;
            UsesRemaining = usesRemaining;
        }

        public InteractableKind Kind { get; }

        public AnchorState State { get; private set; }

        // Negative = unlimited. Zero = spent.
        public int UsesRemaining { get; private set; }

        public bool IsSpent => UsesRemaining == 0;

        public static Result<AnchorRuntime> Create(
            InteractableKind kind,
            AnchorState initialState,
            int maxUses = 1)
        {
            if (maxUses == 0)
            {
                return Result<AnchorRuntime>.Failure("MaxUses must be non-zero (negative = unlimited).");
            }

            if (!IsLegalInitialState(kind, initialState))
            {
                return Result<AnchorRuntime>.Failure(
                    $"State '{initialState}' is not a legal start for kind '{kind}'.");
            }

            return Result<AnchorRuntime>.Success(new AnchorRuntime(kind, initialState, maxUses));
        }

        public static Result<AnchorRuntime> Restore(
            InteractableKind kind,
            AnchorState state,
            int usesRemaining)
        {
            if (!IsLegalInitialState(kind, state))
            {
                return Result<AnchorRuntime>.Failure(
                    $"State '{state}' is not a legal restore target for kind '{kind}'.");
            }

            return Result<AnchorRuntime>.Success(new AnchorRuntime(kind, state, usesRemaining));
        }

        public static AnchorRuntime CreateDefault(InteractableKind kind, int maxUses = 1)
        {
            return new AnchorRuntime(kind, DefaultState(kind), maxUses);
        }

        // The neutral starting lifecycle state for each kind.
        public static AnchorState DefaultState(InteractableKind kind)
        {
            switch (kind)
            {
                case InteractableKind.Portal:
                    return AnchorState.Unlocked;
                case InteractableKind.Chest:
                    return AnchorState.Unlooted;
                case InteractableKind.Shrine:
                    return AnchorState.Dormant;
                case InteractableKind.Grave:
                    return AnchorState.Sealed;
                case InteractableKind.Trap:
                    return AnchorState.Armed;
                case InteractableKind.Resource:
                    return AnchorState.Unlooted;
                default:
                    return AnchorState.Idle;
            }
        }

        public bool CanTransition(AnchorState to)
        {
            if (Transitions.TryGetValue(Kind, out var map)
                && map.TryGetValue(State, out var legal))
            {
                return legal.Contains(to);
            }

            return false;
        }

        // Consumes one use with no state change (Inspect-style anchors that
        // record a use but never change lifecycle state).
        public Result ConsumeUse()
        {
            if (IsSpent)
            {
                return Result.Failure("Anchor is spent (no uses remaining).");
            }

            if (UsesRemaining > 0)
            {
                UsesRemaining--;
            }

            return Result.Success();
        }

        // Applies a guarded transition and consumes one use when limited.
        public Result Transition(AnchorState to)
        {
            if (IsSpent)
            {
                return Result.Failure("Anchor is spent (no uses remaining).");
            }

            if (to == State)
            {
                return Result.Failure($"Anchor is already in state '{to}'.");
            }

            if (!CanTransition(to))
            {
                return Result.Failure(
                    $"Illegal transition {State} -> {to} for kind '{Kind}'.");
            }

            State = to;
            if (UsesRemaining > 0)
            {
                UsesRemaining--;
            }

            return Result.Success();
        }

        private static bool IsLegalInitialState(InteractableKind kind, AnchorState state)
        {
            if (state == DefaultState(kind))
            {
                return true;
            }

            // Any state that is reachable (a source or target of a transition)
            // for the kind is a legal restore target for persistence.
            if (Transitions.TryGetValue(kind, out var map))
            {
                if (map.ContainsKey(state))
                {
                    return true;
                }

                foreach (var pair in map)
                {
                    if (pair.Value.Contains(state))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Dictionary<InteractableKind, Dictionary<AnchorState, HashSet<AnchorState>>> BuildTransitions()
        {
            var t = new Dictionary<InteractableKind, Dictionary<AnchorState, HashSet<AnchorState>>>();

            // 문: locked <-> unlocked.
            t[InteractableKind.Portal] = new Dictionary<AnchorState, HashSet<AnchorState>>
            {
                { AnchorState.Locked, new HashSet<AnchorState> { AnchorState.Unlocked } },
                { AnchorState.Unlocked, new HashSet<AnchorState> { AnchorState.Locked } },
            };

            // 컨테이너: (locked -> unlocked) -> looted. One-way to looted.
            t[InteractableKind.Chest] = new Dictionary<AnchorState, HashSet<AnchorState>>
            {
                { AnchorState.Locked, new HashSet<AnchorState> { AnchorState.Unlocked } },
                { AnchorState.Unlocked, new HashSet<AnchorState> { AnchorState.Looted } },
                { AnchorState.Unlooted, new HashSet<AnchorState> { AnchorState.Looted } },
            };

            // 오브 shrine: dormant -> revealed -> open (inspected).
            t[InteractableKind.Shrine] = new Dictionary<AnchorState, HashSet<AnchorState>>
            {
                { AnchorState.Dormant, new HashSet<AnchorState> { AnchorState.Revealed } },
                { AnchorState.Revealed, new HashSet<AnchorState> { AnchorState.Open } },
            };

            // 묘비: sealed -> open (revealed / paid respects).
            t[InteractableKind.Grave] = new Dictionary<AnchorState, HashSet<AnchorState>>
            {
                { AnchorState.Sealed, new HashSet<AnchorState> { AnchorState.Open } },
            };

            // 함정: armed -> disarmed | triggered (both terminal).
            t[InteractableKind.Trap] = new Dictionary<AnchorState, HashSet<AnchorState>>
            {
                { AnchorState.Armed, new HashSet<AnchorState> { AnchorState.Disarmed, AnchorState.Triggered } },
            };

            // Resource: unlooted -> looted.
            t[InteractableKind.Resource] = new Dictionary<AnchorState, HashSet<AnchorState>>
            {
                { AnchorState.Unlooted, new HashSet<AnchorState> { AnchorState.Looted } },
            };

            // Inspect stays Idle; no state transitions (records use only).
            t[InteractableKind.Inspect] = new Dictionary<AnchorState, HashSet<AnchorState>>();

            return t;
        }
    }
}
