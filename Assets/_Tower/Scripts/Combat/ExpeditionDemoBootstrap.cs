using System;
using System.Collections.Generic;
using System.IO;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // T8 demo wiring, code-bootstrap only (no scene edits): builds a demo
    // party and enemy factory, points the SaveRepository at
    // persistentDataPath, and plays the whole expedition through
    // ExpeditionRunner, logging every advance/retreat.
    public sealed class ExpeditionDemoBootstrap : MonoBehaviour
    {
        private const int SafetyIterations = 32;

        [SerializeField] private int _baseSeed = 20260705;
        [SerializeField] private string _saveFileName = "tower-expedition-save.json";

        private void Start()
        {
            var savePath = Path.Combine(Application.persistentDataPath, _saveFileName);
            var repository = SaveRepository.Create(savePath);
            if (repository.IsFailure)
            {
                Debug.LogError("[Expedition] " + repository.Error);
                return;
            }

            var roster = new List<ExpeditionMember>
            {
                CreateMember("regressor", isReturner: true, maxHp: 24, attack: 4, speed: 6),
                CreateMember("ally-a", isReturner: false, maxHp: 18, attack: 3, speed: 5),
                CreateMember("ally-b", isReturner: false, maxHp: 16, attack: 3, speed: 4)
            };

            var state = ExpeditionState.CreateNew(roster);
            if (state.IsFailure)
            {
                Debug.LogError("[Expedition] " + state.Error);
                return;
            }

            var runner = ExpeditionRunner.Create(state.Value, repository.Value, new DemoEnemyFactory(), _baseSeed);
            if (runner.IsFailure)
            {
                Debug.LogError("[Expedition] " + runner.Error);
                return;
            }

            Debug.Log($"[Expedition] Start — save file: {savePath}");
            for (var iteration = 0; iteration < SafetyIterations; iteration++)
            {
                if (runner.Value.State.IsComplete)
                {
                    Debug.Log("[Expedition] Stairway conquered — expedition complete.");
                    break;
                }

                var floor = runner.Value.State.FloorIndex;
                var stairway = runner.Value.State.StairwayIndex;
                var progress = runner.Value.PlayCurrentFloor();
                if (progress.IsFailure)
                {
                    Debug.LogError($"[Expedition] Floor {stairway}-{floor} failed: {progress.Error}");
                    return;
                }

                Log(stairway, floor, progress.Value);
            }
        }

        private static void Log(int stairway, int floor, ExpeditionProgress progress)
        {
            var state = progress.State;
            var message = $"[Expedition] {stairway}-{floor} → {progress.Outcome}"
                + $" | now {state.StairwayIndex}-{state.FloorIndex}"
                + $" | roster {state.Roster.Count}"
                + $" | retreats {state.RetreatCount}";

            if (progress.ConfirmedDeadIds.Count > 0)
            {
                message += " | fallen: " + string.Join(", ", progress.ConfirmedDeadIds);
            }

            if (progress.RevivedIds.Count > 0)
            {
                message += " | revived: " + string.Join(", ", progress.RevivedIds);
            }

            if (progress.NewlyMissingIds.Count > 0)
            {
                message += " | missing: " + string.Join(", ", progress.NewlyMissingIds);
            }

            Debug.Log(message);
        }

        private static ExpeditionMember CreateMember(string unitId, bool isReturner, int maxHp, int attack, int speed)
        {
            var definition = CreateCharacter(unitId, maxHp, attack, defense: 1, speed: speed, isReturner: isReturner);
            var state = CharacterState.Create(definition, slotCount: 1);
            Debug.Assert(state.IsSuccess, state.Error);
            var member = ExpeditionMember.Create(unitId, state.Value);
            Debug.Assert(member.IsSuccess, member.Error);
            return member.Value;
        }

        private static CharacterDef CreateCharacter(string id, int maxHp, int attack, int defense, int speed, bool isReturner)
        {
            var strike = ScriptableObject.CreateInstance<AbilityDef>();
            SetField(strike, "id", id + "-strike");
            SetField(strike, "displayName", id + " Strike");
            SetField(strike, "tag", AbilityTag.Apply);
            SetField(strike, "range", 1);
            SetField(strike, "basePower", 3);
            SetField(strike, "targetType", AbilityTargetType.Enemy);

            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            SetField(definition, "id", id);
            SetField(definition, "displayName", id);
            SetField(definition, "maxHp", maxHp);
            SetField(definition, "attack", attack);
            SetField(definition, "defense", defense);
            SetField(definition, "speed", speed);
            SetField(definition, "isReturner", isReturner);
            SetField(definition, "defaultAbilities", new[] { strike });
            return definition;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Debug.Assert(field != null, name);
            field.SetValue(target, value);
        }

        // Demo enemy data: weak fodder per kind slot, a tougher boss. Real
        // content will replace this with ScriptableObject-driven data.
        private sealed class DemoEnemyFactory : IExpeditionEnemyFactory
        {
            private readonly Dictionary<string, CharacterDef> definitions =
                new Dictionary<string, CharacterDef>(StringComparer.Ordinal);

            public Result<CharacterState> Create(string kindSlot, int stairwayIndex, int floorIndex)
            {
                if (string.IsNullOrWhiteSpace(kindSlot))
                {
                    return Result<CharacterState>.Failure("Enemy kind slot is required.");
                }

                if (!definitions.TryGetValue(kindSlot, out var definition))
                {
                    var isBoss = StringComparer.Ordinal.Equals(kindSlot, "boss");
                    definition = CreateCharacter(
                        "demo-" + kindSlot,
                        maxHp: isBoss ? 12 : 4,
                        attack: isBoss ? 3 : 1,
                        defense: 0,
                        speed: isBoss ? 4 : 3,
                        isReturner: false);
                    definitions[kindSlot] = definition;
                }

                return CharacterState.Create(definition, slotCount: 1);
            }
        }
    }
}
