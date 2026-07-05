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

        public string Id => id;
        public string DisplayName => displayName;
        public AbilityTag Tag => tag;
        public MarkDef TargetMark => targetMark;
        public int Range => range;
        public int Cost => cost;
        public int BasePower => basePower;
        public float AmplificationMultiplier => amplificationMultiplier;
        public AbilityTargetType TargetType => targetType;

        public static AbilityDef CreateRuntime(
            string id,
            AbilityTag tag,
            int basePower,
            int range,
            AbilityTargetType targetType,
            MarkDef targetMark = null,
            float amplificationMultiplier = 1f,
            string displayName = null)
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
            return ability;
        }
    }
}
