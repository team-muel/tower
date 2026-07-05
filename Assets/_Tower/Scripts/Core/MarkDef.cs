using UnityEngine;

namespace Tower.Core
{
    [CreateAssetMenu(menuName = "Tower/Core/Mark", fileName = "MarkDef")]
    public sealed class MarkDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int durationTurns = 1;
        [SerializeField] private bool stackable;

        public string Id => id;
        public string DisplayName => displayName;
        public int DurationTurns => durationTurns;
        public bool Stackable => stackable;
    }
}
