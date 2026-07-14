using System;
using System.Collections.Generic;
using Tower.Core;
using Tower.Gen;
using UnityEngine;

namespace Tower.Combat
{
    // T54 bridge from a generated FloorEncounter to visible real-time enemies.
    // R is an explicit temporary QA resolution path until T56 owns HP/death.
    public sealed class GeneratedFloorEncounterHost : MonoBehaviour
    {
        private readonly List<GameObject> enemies = new List<GameObject>();
        private readonly List<PillbugBrain> brains = new List<PillbugBrain>();
        private EncounterEngagementController engagement;
        private Action<string> resolved;
        private string eventId;
        private bool secondaryBrainsEnabled;

        public int EnemyCount => enemies.Count;
        public bool IsCombatActive => engagement != null && engagement.IsCombatActive;
        public bool IsResolved { get; private set; }
        public IReadOnlyList<GameObject> Enemies => enemies;

        public Result Configure(
            Transform player,
            Behaviour playerMovement,
            FloorEncounter encounter,
            RunEventSlot runEvent,
            Vector3 spawnCenter,
            Action<string> onResolved,
            float triggerRadius = 7f,
            float introHoldSeconds = 0.45f)
        {
            if (player == null || playerMovement == null || encounter == null || runEvent == null)
            {
                return Result.Failure("Generated encounter requires player, movement, encounter, and run event.");
            }

            if (!encounter.HasEncounter)
            {
                return Result.Failure("Generated encounter cannot present an empty composition.");
            }

            if ((runEvent.Kind == RunEventKind.Boss) != encounter.IsBoss)
            {
                return Result.Failure("Run event kind must match the generated encounter composition.");
            }

            eventId = runEvent.EventId;
            resolved = onResolved;
            for (int index = 0; index < encounter.EnemySlots.Count; index++)
            {
                FloorEnemySlot slot = encounter.EnemySlots[index];
                Vector3 offset = FormationOffset(index, encounter.EnemySlots.Count);
                GameObject enemy = GameObject.CreatePrimitive(encounter.IsBoss
                    ? PrimitiveType.Capsule
                    : PrimitiveType.Sphere);
                enemy.name = $"GeneratedEnemy_{slot.KindSlot}_{slot.Index:00}";
                enemy.transform.SetParent(transform, true);
                enemy.transform.position = spawnCenter + offset + (Vector3.up * (encounter.IsBoss ? 1f : 0.5f));
                if (encounter.IsBoss)
                {
                    enemy.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                }

                PillbugBrain brain = enemy.AddComponent<PillbugBrain>();
                brain.Configure(player, (Transform)null, DefaultTuning(encounter.IsBoss));
                brain.SetEngagementEnabled(false);
                enemies.Add(enemy);
                brains.Add(brain);
            }

            engagement = gameObject.AddComponent<EncounterEngagementController>();
            Result configured = engagement.Configure(
                player,
                enemies[0].transform,
                playerMovement,
                brains[0],
                triggerRadius,
                introHoldSeconds);
            if (configured.IsFailure)
            {
                CleanupEnemies();
                return configured;
            }

            Debug.Log(
                $"[GeneratedEncounter] Spawned event={eventId} floor={runEvent.FloorNumber} "
                + $"kind={runEvent.Kind} enemies={encounter.EnemyCount}.",
                this);
            return Result.Success();
        }

        public void Tick(float realDeltaSeconds)
        {
            if (engagement == null || IsResolved)
            {
                return;
            }

            engagement.Tick(realDeltaSeconds);
            EnableSecondaryBrainsIfActive();
        }

        public Result ResolveEncounter()
        {
            if (engagement == null)
            {
                return Result.Failure("Generated encounter is not configured.");
            }

            if (IsResolved)
            {
                return Result.Success();
            }

            if (!engagement.IsCombatActive)
            {
                return Result.Failure("Generated encounter cannot resolve before real-time combat starts.");
            }

            Result result = engagement.ResolveEncounter();
            if (result.IsFailure)
            {
                return result;
            }

            IsResolved = true;
            for (int index = 0; index < brains.Count; index++)
            {
                if (brains[index] != null)
                {
                    brains[index].SetEngagementEnabled(false);
                }
            }

            resolved?.Invoke(eventId);
            Debug.Log($"[GeneratedEncounter] Resolved event={eventId}; traversal unlocked.", this);
            CleanupEnemies();
            return Result.Success();
        }

        private void Update()
        {
            EnableSecondaryBrainsIfActive();
            if (!IsResolved && IsCombatActive && Input.GetKeyDown(KeyCode.R))
            {
                Result result = ResolveEncounter();
                if (result.IsFailure)
                {
                    Debug.LogError(result.Error, this);
                }
            }
        }

        private void EnableSecondaryBrainsIfActive()
        {
            if (secondaryBrainsEnabled || !IsCombatActive)
            {
                return;
            }

            secondaryBrainsEnabled = true;
            for (int index = 1; index < brains.Count; index++)
            {
                brains[index].SetEngagementEnabled(true);
            }
        }

        private void OnDestroy()
        {
            CleanupEnemies();
        }

        private void CleanupEnemies()
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                GameObject enemy = enemies[index];
                if (enemy == null)
                {
                    continue;
                }

                if (Application.isPlaying) Destroy(enemy); else DestroyImmediate(enemy);
            }

            enemies.Clear();
            brains.Clear();
        }

        private static Vector3 FormationOffset(int index, int count)
        {
            if (count <= 1)
            {
                return Vector3.zero;
            }

            float angle = (Mathf.PI * 2f * index) / count;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.5f;
        }

        private static PillbugTuning DefaultTuning(bool boss)
        {
            return new PillbugTuning(
                12f,
                boss ? 3.5f : 2.5f,
                boss ? 4f : 3f,
                new TelegraphDurations(0.9f, 0.25f, 0.6f),
                0.7f,
                0.12f,
                0.4f,
                boss ? 1.5f : 1.1f,
                0.08f,
                32,
                new Color(0.15f, 1f, 0.35f, 1f),
                new Color(1f, 0.75f, 0.1f, 1f),
                new Color(1f, 0.2f, 0.2f, 1f),
                boss ? new Color(0.55f, 0.08f, 0.06f, 1f) : new Color(0.35f, 0.12f, 0.08f, 1f),
                Color.white);
        }
    }
}
