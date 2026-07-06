using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // Explicit QA exposure registry: only registered buttons can be pressed and
    // only registered contributors feed the state snapshot. No reflection scans.
    public sealed class QaRegistry
    {
        private readonly Dictionary<string, Action> buttons =
            new Dictionary<string, Action>(StringComparer.Ordinal);

        private readonly Dictionary<string, Action<QaStateSnapshot>> stateContributors =
            new Dictionary<string, Action<QaStateSnapshot>>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> ButtonNames => buttons.Keys;

        public Result RegisterButton(string name, Action onPress)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Failure("Button name is required.");
            }

            if (onPress == null)
            {
                return Result.Failure("Button handler is required.");
            }

            if (buttons.ContainsKey(name))
            {
                return Result.Failure($"Button '{name}' is already registered.");
            }

            buttons.Add(name, onPress);
            return Result.Success();
        }

        public Result UnregisterButton(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && buttons.Remove(name)
                ? Result.Success()
                : Result.Failure($"Button '{name}' is not registered.");
        }

        public Result Press(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Failure("Button name is required.");
            }

            if (!buttons.TryGetValue(name, out var onPress))
            {
                return Result.Failure($"Button '{name}' is not registered.");
            }

            try
            {
                onPress();
                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Button '{name}' handler threw: {exception.Message}");
            }
        }

        public Result RegisterStateContributor(string key, Action<QaStateSnapshot> contributor)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Result.Failure("State contributor key is required.");
            }

            if (contributor == null)
            {
                return Result.Failure("State contributor is required.");
            }

            if (stateContributors.ContainsKey(key))
            {
                return Result.Failure($"State contributor '{key}' is already registered.");
            }

            stateContributors.Add(key, contributor);
            return Result.Success();
        }

        public Result UnregisterStateContributor(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && stateContributors.Remove(key)
                ? Result.Success()
                : Result.Failure($"State contributor '{key}' is not registered.");
        }

        public QaStateSnapshot BuildState(string sceneName)
        {
            var snapshot = new QaStateSnapshot { sceneName = sceneName ?? string.Empty };
            var keys = new List<string>(stateContributors.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                stateContributors[key](snapshot);
            }

            return snapshot;
        }
    }
}
