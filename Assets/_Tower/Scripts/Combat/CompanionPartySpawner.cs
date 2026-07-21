using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    /// <summary>
    /// Reusable bridge from a roster of visual profiles to distinct world
    /// entities. This owns no UI and makes no ability decisions.
    /// </summary>
    public sealed class CompanionPartySpawner : MonoBehaviour
    {
        private readonly List<CompanionEntity> entities = new List<CompanionEntity>();

        private Transform leader;
        private CompanionVisualProfile[] profiles = new CompanionVisualProfile[0];
        private Transform[] enemies = new Transform[0];
        private BreadcrumbTrail trail;

        public IReadOnlyList<CompanionEntity> Entities => entities;

        // 89 rule 1: one shared distance-based breadcrumb stream per party.
        public BreadcrumbTrail Trail => trail;

        private void Update()
        {
            if (leader != null && trail != null)
            {
                trail.Record(leader.position);
            }
        }

        public void Configure(
            Transform partyLeader,
            CompanionVisualProfile[] companionProfiles,
            Transform[] enemyTransforms)
        {
            leader = partyLeader;
            profiles = companionProfiles ?? new CompanionVisualProfile[0];
            enemies = enemyTransforms ?? new Transform[0];
            trail = new BreadcrumbTrail();
            if (leader != null)
            {
                trail.Record(leader.position);
            }
        }

        public Result<IReadOnlyList<CompanionEntity>> SpawnNow()
        {
            if (leader == null)
            {
                return Result<IReadOnlyList<CompanionEntity>>.Failure("Companion party leader is required.");
            }

            if (profiles.Length == 0)
            {
                return Result<IReadOnlyList<CompanionEntity>>.Failure("At least one companion profile is required.");
            }

            var ids = new HashSet<string>();
            foreach (var profile in profiles)
            {
                if (profile == null)
                {
                    return Result<IReadOnlyList<CompanionEntity>>.Failure("Companion profile cannot be null.");
                }

                var valid = profile.Validate();
                if (valid.IsFailure)
                {
                    return Result<IReadOnlyList<CompanionEntity>>.Failure(valid.Error);
                }

                if (!ids.Add(profile.CharacterDefinition.Id))
                {
                    return Result<IReadOnlyList<CompanionEntity>>.Failure(
                        "Companion roster contains duplicate unit id: " + profile.CharacterDefinition.Id);
                }
            }

            ClearExisting();
            for (var slotIndex = 0; slotIndex < profiles.Length; slotIndex++)
            {
                var profile = profiles[slotIndex];
                var entityObject = new GameObject("Companion_" + profile.CharacterDefinition.Id);
                entityObject.transform.SetParent(transform, false);
                entityObject.transform.position = leader.TransformPoint(profile.FormationOffset);
                var entity = entityObject.AddComponent<CompanionEntity>();
                var configured = entity.Configure(profile, leader, enemies, trail, slotIndex);
                if (configured.IsFailure)
                {
                    ClearExisting();
                    return Result<IReadOnlyList<CompanionEntity>>.Failure(configured.Error);
                }

                entities.Add(entity);
            }

            return Result<IReadOnlyList<CompanionEntity>>.Success(entities.AsReadOnly());
        }

        private void ClearExisting()
        {
            foreach (var entity in entities)
            {
                if (entity != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(entity.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(entity.gameObject);
                    }
                }
            }

            entities.Clear();
        }
    }
}
