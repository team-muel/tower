using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    [CreateAssetMenu(menuName = "Tower/Combat/Enemy Combat Profile", fileName = "EnemyCombatProfile")]
    public sealed class EnemyCombatProfile : ScriptableObject
    {
        [SerializeField] private string kindSlot;
        [SerializeField] private CharacterDef characterDefinition;
        [SerializeField] private PrimitiveType bodyPrimitive = PrimitiveType.Sphere;
        [SerializeField] private Color bodyColor = new Color(0.35f, 0.12f, 0.08f, 1f);
        [SerializeField] private Vector3 bodyScale = Vector3.one;
        [SerializeField, Min(0.1f)] private float healthBarHeight = 1.4f;
        [SerializeField] private bool pillbugTelegraph = true;

        public string KindSlot => kindSlot;
        public CharacterDef CharacterDefinition => characterDefinition;
        public PrimitiveType BodyPrimitive => bodyPrimitive;
        public Color BodyColor => bodyColor;
        public Vector3 BodyScale => bodyScale;
        public float HealthBarHeight => healthBarHeight;
        public bool PillbugTelegraph => pillbugTelegraph;

        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(kindSlot))
            {
                return Result.Failure("Enemy combat profile requires a kind slot.");
            }

            if (characterDefinition == null || characterDefinition.DefaultAbilities == null
                || characterDefinition.DefaultAbilities.Length < AbilityLoadout.MinSlots
                || characterDefinition.DefaultAbilities.Length > AbilityLoadout.MaxSlots)
            {
                return Result.Failure("Enemy combat profile requires a character with one to four abilities.");
            }

            if (!IsPositiveFinite(bodyScale.x) || !IsPositiveFinite(bodyScale.y)
                || !IsPositiveFinite(bodyScale.z) || !IsPositiveFinite(healthBarHeight))
            {
                return Result.Failure("Enemy combat presentation values must be finite and positive.");
            }

            return Result.Success();
        }

        public static EnemyCombatProfile CreateRuntime(
            string kindSlot,
            CharacterDef characterDefinition,
            PrimitiveType bodyPrimitive,
            Color bodyColor,
            Vector3 bodyScale,
            float healthBarHeight = 1.4f,
            bool pillbugTelegraph = true)
        {
            var profile = CreateInstance<EnemyCombatProfile>();
            profile.kindSlot = kindSlot;
            profile.characterDefinition = characterDefinition;
            profile.bodyPrimitive = bodyPrimitive;
            profile.bodyColor = bodyColor;
            profile.bodyScale = bodyScale;
            profile.healthBarHeight = healthBarHeight;
            profile.pillbugTelegraph = pillbugTelegraph;
            return profile;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
