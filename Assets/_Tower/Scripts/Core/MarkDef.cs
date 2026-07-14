using UnityEngine;

namespace Tower.Core
{
    [CreateAssetMenu(menuName = "Tower/Core/Mark", fileName = "MarkDef")]
    public sealed class MarkDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, Min(0.01f)] private float durationSeconds = 1f;
        [SerializeField] private bool stackable;

        public string Id => id;
        public string DisplayName => displayName;
        public float DurationSeconds => durationSeconds;
        public bool Stackable => stackable;

        // Runtime factory mirroring AbilityDef/CharacterDef.CreateRuntime — lets
        // tests and procedural code mint marks without asset files (T49).
        public static MarkDef CreateRuntime(
            string id,
            string displayName = null,
            float durationSeconds = 1f,
            bool stackable = false)
        {
            var mark = CreateInstance<MarkDef>();
            mark.id = id;
            mark.displayName = displayName ?? id;
            mark.durationSeconds = durationSeconds;
            mark.stackable = stackable;
            return mark;
        }
    }
}
