using System;

namespace Tower.Core
{
    // Process-wide QA gate. Disabled (null registry) unless the harness
    // bootstrap enables it, so registration call sites are safe no-ops in
    // normal (non -qaPort) runs.
    public static class QaRuntime
    {
        public static QaRegistry Registry { get; private set; }

        public static bool IsEnabled => Registry != null;

        public static void Enable(QaRegistry registry)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public static void Disable()
        {
            Registry = null;
        }

        public static void RegisterButton(string name, Action onPress)
        {
            Registry?.RegisterButton(name, onPress);
        }

        public static void UnregisterButton(string name)
        {
            Registry?.UnregisterButton(name);
        }

        public static void RegisterStateContributor(string key, Action<QaStateSnapshot> contributor)
        {
            Registry?.RegisterStateContributor(key, contributor);
        }

        public static void UnregisterStateContributor(string key)
        {
            Registry?.UnregisterStateContributor(key);
        }
    }
}
