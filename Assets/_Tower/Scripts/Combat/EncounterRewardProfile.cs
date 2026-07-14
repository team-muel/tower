using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    [CreateAssetMenu(fileName = "EncounterRewardProfile", menuName = "Tower/Combat/Encounter Reward Profile")]
    public sealed class EncounterRewardProfile : ScriptableObject
    {
        [SerializeField] private RewardType encounterType;
        [SerializeField, Min(1)] private int encounterAmount;
        [SerializeField] private string encounterDisplayName;
        [SerializeField] private RewardType bossType;
        [SerializeField, Min(1)] private int bossAmount;
        [SerializeField] private string bossDisplayName;

        public RewardType EncounterType => encounterType;
        public int EncounterAmount => encounterAmount;
        public string EncounterDisplayName => encounterDisplayName;
        public RewardType BossType => bossType;
        public int BossAmount => bossAmount;
        public string BossDisplayName => bossDisplayName;

        public Result Validate()
        {
            Result<EncounterReward> ordinary = EncounterReward.Create(
                "validation-encounter",
                encounterType,
                encounterAmount,
                encounterDisplayName);
            if (ordinary.IsFailure)
            {
                return Result.Failure("Encounter reward profile: " + ordinary.Error);
            }

            Result<EncounterReward> boss = EncounterReward.Create(
                "validation-boss",
                bossType,
                bossAmount,
                bossDisplayName);
            return boss.IsFailure
                ? Result.Failure("Boss reward profile: " + boss.Error)
                : Result.Success();
        }

        public Result<EncounterReward> CreateReward(RunEventSlot runEvent)
        {
            if (runEvent == null)
            {
                return Result<EncounterReward>.Failure("Run event is required for its reward.");
            }

            return runEvent.Kind == RunEventKind.Boss
                ? EncounterReward.Create(runEvent.EventId, bossType, bossAmount, bossDisplayName)
                : EncounterReward.Create(
                    runEvent.EventId,
                    encounterType,
                    encounterAmount,
                    encounterDisplayName);
        }

        public static EncounterRewardProfile CreateRuntime(
            RewardType ordinaryType,
            int ordinaryAmount,
            string ordinaryDisplayName,
            RewardType finalBossType,
            int finalBossAmount,
            string finalBossDisplayName)
        {
            var profile = CreateInstance<EncounterRewardProfile>();
            profile.encounterType = ordinaryType;
            profile.encounterAmount = ordinaryAmount;
            profile.encounterDisplayName = ordinaryDisplayName;
            profile.bossType = finalBossType;
            profile.bossAmount = finalBossAmount;
            profile.bossDisplayName = finalBossDisplayName;
            return profile;
        }
    }
}
