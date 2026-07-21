using UnityEngine;

namespace Tower.Core
{
    [CreateAssetMenu(menuName = "Tower/Core/Ability", fileName = "AbilityDef")]
    public sealed class AbilityDef : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private AbilityTag tag;
        [SerializeField] private MarkDef targetMark;
        [SerializeField] private int range = 1;
        [SerializeField] private int cost;
        [SerializeField] private int basePower;
        [SerializeField] private float amplificationMultiplier = 1f;
        [SerializeField] private AbilityTargetType targetType;
        // Real seconds the ability stays unavailable after a successful use.
        // Zero means no cooldown.
        [SerializeField, Min(0f)] private float cooldownSeconds;

        public string Id => id;
        public string DisplayName => displayName;
        public AbilityTag Tag => tag;
        public MarkDef TargetMark => targetMark;
        public int Range => range;
        public int Cost => cost;
        public int BasePower => basePower;
        public float AmplificationMultiplier => amplificationMultiplier;
        public AbilityTargetType TargetType => targetType;
        public float CooldownSeconds => cooldownSeconds;

        public static AbilityDef CreateRuntime(
            string id,
            AbilityTag tag,
            int basePower,
            int range,
            AbilityTargetType targetType,
            MarkDef targetMark = null,
            float amplificationMultiplier = 1f,
            string displayName = null,
            float cooldownSeconds = 0f)
        {
            var ability = CreateInstance<AbilityDef>();
            ability.id = id;
            ability.displayName = displayName ?? id;
            ability.tag = tag;
            ability.targetMark = targetMark;
            ability.range = range;
            ability.basePower = basePower;
            ability.amplificationMultiplier = amplificationMultiplier;
            ability.targetType = targetType;
            ability.cooldownSeconds = cooldownSeconds;
            return ability;
        }
    }
}
