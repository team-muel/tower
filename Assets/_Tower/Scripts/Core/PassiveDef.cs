using UnityEngine;

namespace Tower.Core
{
    [CreateAssetMenu(menuName = "Tower/Core/Passive", fileName = "PassiveDef")]
    public sealed class PassiveDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string effectHookKey;

        public string Id => id;
        public string DisplayName => displayName;
        public string EffectHookKey => effectHookKey;
    }
}
