using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    [CreateAssetMenu(menuName = "Tower/Combat/Companion Visual Profile", fileName = "CompanionVisualProfile")]
    public sealed class CompanionVisualProfile : ScriptableObject
    {
        [SerializeField] private CharacterDef characterDefinition;
        [SerializeField] private GameObject bodyPrefab;
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private RuntimeAnimatorController locomotionController;
        [SerializeField] private Color accentColor = Color.white;
        [SerializeField] private Vector3 formationOffset = new Vector3(0f, 0f, -2f);
        [SerializeField, Min(0f)] private float arriveDistance = 0.2f;
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;

        public CharacterDef CharacterDefinition => characterDefinition;
        public GameObject BodyPrefab => bodyPrefab;
        public Material BodyMaterial => bodyMaterial;
        public RuntimeAnimatorController LocomotionController => locomotionController;
        public Color AccentColor => accentColor;
        public Vector3 FormationOffset => formationOffset;
        public float ArriveDistance => arriveDistance;
        public float MoveSpeed => moveSpeed;
        public float TurnSpeed => turnSpeed;

        public Result Validate()
        {
            if (characterDefinition == null)
            {
                return Result.Failure("Companion visual profile requires a character definition.");
            }

            if (characterDefinition.IsReturner)
            {
                return Result.Failure("The returner cannot be used as a companion visual profile.");
            }

            if (string.IsNullOrWhiteSpace(characterDefinition.Id))
            {
                return Result.Failure("Companion character id is required.");
            }

            if (bodyPrefab == null)
            {
                return Result.Failure("Companion visual profile requires a body prefab.");
            }

            if (!IsFinite(formationOffset.x) || !IsFinite(formationOffset.y) || !IsFinite(formationOffset.z)
                || !IsFinite(arriveDistance) || !IsFinite(moveSpeed) || !IsFinite(turnSpeed)
                || arriveDistance < 0f || moveSpeed < 0f || turnSpeed < 0f)
            {
                return Result.Failure("Companion visual tuning must be finite and non-negative.");
            }

            return Result.Success();
        }

        public static CompanionVisualProfile CreateRuntime(
            CharacterDef characterDefinition,
            GameObject bodyPrefab,
            RuntimeAnimatorController locomotionController,
            Color accentColor,
            Vector3 formationOffset,
            float arriveDistance = 0.2f,
            float moveSpeed = 3.5f,
            float turnSpeed = 540f,
            Material bodyMaterial = null)
        {
            var profile = CreateInstance<CompanionVisualProfile>();
            profile.characterDefinition = characterDefinition;
            profile.bodyPrefab = bodyPrefab;
            profile.bodyMaterial = bodyMaterial;
            profile.locomotionController = locomotionController;
            profile.accentColor = accentColor;
            profile.formationOffset = formationOffset;
            profile.arriveDistance = arriveDistance;
            profile.moveSpeed = moveSpeed;
            profile.turnSpeed = turnSpeed;
            return profile;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
