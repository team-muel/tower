using System;
using UnityEditor;
using UnityEngine;

namespace Tower.Core.Editor
{
    public static class T1SliceContentGenerator
    {
        private const string DataRoot = "Assets/_Tower/Data";
        private const string MarksRoot = DataRoot + "/Marks";
        private const string AbilitiesRoot = DataRoot + "/Abilities";
        private const string PassivesRoot = DataRoot + "/Passives";
        private const string CharactersRoot = DataRoot + "/Characters";

        public static void Generate()
        {
            EnsureFolder("Assets/_Tower", "Data");
            EnsureFolder(DataRoot, "Marks");
            EnsureFolder(DataRoot, "Abilities");
            EnsureFolder(DataRoot, "Passives");
            EnsureFolder(DataRoot, "Characters");

            var frost = CreateMark("M_Frost", "Frost", 2, true, MarksRoot + "/M_Frost.asset");
            var burn = CreateMark("M_Burn", "Burn", 3, true, MarksRoot + "/M_Burn.asset");

            var reckless = CreatePassive("P_Reckless", "Reckless", "passive.reckless", PassivesRoot + "/P_Reckless.asset");
            var guardian = CreatePassive("P_Guardian", "Guardian", "passive.guardian", PassivesRoot + "/P_Guardian.asset");
            var tempo = CreatePassive("P_Tempo", "Tempo", "passive.tempo", PassivesRoot + "/P_Tempo.asset");

            var frostBolt = CreateAbility("A_FrostBolt", "Frost Bolt", AbilityTag.Apply, frost, 5, 1, 6, 1f, AbilityTargetType.Enemy, AbilitiesRoot + "/A_FrostBolt.asset");
            var burningBrand = CreateAbility("A_BurningBrand", "Burning Brand", AbilityTag.Apply, burn, 4, 1, 5, 1f, AbilityTargetType.Enemy, AbilitiesRoot + "/A_BurningBrand.asset");
            var chillTrap = CreateAbility("A_ChillTrap", "Chill Trap", AbilityTag.Apply, frost, 3, 2, 4, 1f, AbilityTargetType.Cell, AbilitiesRoot + "/A_ChillTrap.asset");
            var shatterFrost = CreateAbility("A_ShatterFrost", "Shatter Frost", AbilityTag.Consume, frost, 4, 2, 12, 1f, AbilityTargetType.Enemy, AbilitiesRoot + "/A_ShatterFrost.asset");
            var igniteAsh = CreateAbility("A_IgniteAsh", "Ignite Ash", AbilityTag.Consume, burn, 4, 2, 11, 1f, AbilityTargetType.Enemy, AbilitiesRoot + "/A_IgniteAsh.asset");
            var thermalBreak = CreateAbility("A_ThermalBreak", "Thermal Break", AbilityTag.Consume, burn, 1, 2, 13, 1f, AbilityTargetType.Enemy, AbilitiesRoot + "/A_ThermalBreak.asset");
            var focusStrike = CreateAbility("A_FocusStrike", "Focus Strike", AbilityTag.Amplify, null, 1, 1, 8, 1.5f, AbilityTargetType.Enemy, AbilitiesRoot + "/A_FocusStrike.asset");
            var guardedSurge = CreateAbility("A_GuardedSurge", "Guarded Surge", AbilityTag.Amplify, null, 2, 1, 3, 1.35f, AbilityTargetType.Ally, AbilitiesRoot + "/A_GuardedSurge.asset");
            var quickSlash = CreateAbility("A_QuickSlash", "Quick Slash", AbilityTag.None, null, 1, 0, 7, 1f, AbilityTargetType.Enemy, AbilitiesRoot + "/A_QuickSlash.asset");
            var holdLine = CreateAbility("A_HoldLine", "Hold Line", AbilityTag.None, null, 1, 0, 2, 1f, AbilityTargetType.Ally, AbilitiesRoot + "/A_HoldLine.asset");

            CreateCharacter("C_Returner", "Returner", 34, 8, 5, 11, DispositionType.Protective, tempo, true, new[] { quickSlash, frostBolt }, CharactersRoot + "/C_Returner.asset");
            CreateCharacter("C_EmberVanguard", "Ember Vanguard", 38, 10, 6, 8, DispositionType.Aggressive, reckless, false, new[] { burningBrand, thermalBreak }, CharactersRoot + "/C_EmberVanguard.asset");
            CreateCharacter("C_GlassBreaker", "Glass Breaker", 28, 12, 3, 12, DispositionType.Aggressive, tempo, false, new[] { focusStrike, shatterFrost }, CharactersRoot + "/C_GlassBreaker.asset");
            CreateCharacter("C_WardBearer", "Ward Bearer", 44, 6, 9, 7, DispositionType.Protective, guardian, false, new[] { holdLine, guardedSurge }, CharactersRoot + "/C_WardBearer.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static MarkDef CreateMark(string id, string displayName, int durationTurns, bool stackable, string path)
        {
            var mark = CreateAsset<MarkDef>(path);
            SetString(mark, "id", id);
            SetString(mark, "displayName", displayName);
            SetInt(mark, "durationTurns", durationTurns);
            SetBool(mark, "stackable", stackable);
            return mark;
        }

        private static AbilityDef CreateAbility(string id, string displayName, AbilityTag tag, MarkDef targetMark, int range, int cost, int basePower, float amplificationMultiplier, AbilityTargetType targetType, string path)
        {
            var ability = CreateAsset<AbilityDef>(path);
            SetString(ability, "id", id);
            SetString(ability, "displayName", displayName);
            SetEnum(ability, "tag", tag);
            SetObject(ability, "targetMark", targetMark);
            SetInt(ability, "range", range);
            SetInt(ability, "cost", cost);
            SetInt(ability, "basePower", basePower);
            SetFloat(ability, "amplificationMultiplier", amplificationMultiplier);
            SetEnum(ability, "targetType", targetType);
            return ability;
        }

        private static PassiveDef CreatePassive(string id, string displayName, string effectHookKey, string path)
        {
            var passive = CreateAsset<PassiveDef>(path);
            SetString(passive, "id", id);
            SetString(passive, "displayName", displayName);
            SetString(passive, "effectHookKey", effectHookKey);
            return passive;
        }

        private static CharacterDef CreateCharacter(string id, string displayName, int maxHp, int attack, int defense, int speed, DispositionType disposition, PassiveDef passive, bool isReturner, AbilityDef[] defaultAbilities, string path)
        {
            var character = CreateAsset<CharacterDef>(path);
            SetString(character, "id", id);
            SetString(character, "displayName", displayName);
            SetInt(character, "maxHp", maxHp);
            SetInt(character, "attack", attack);
            SetInt(character, "defense", defense);
            SetInt(character, "speed", speed);
            SetEnum(character, "disposition", disposition);
            SetObject(character, "passive", passive);
            SetObjectArray(character, "defaultAbilities", defaultAbilities);
            SetBool(character, "isReturner", isReturner);
            return character;
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            AssetDatabase.DeleteAsset(path);
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            Edit(target, propertyName, property => property.stringValue = value);
        }

        private static void SetInt(UnityEngine.Object target, string propertyName, int value)
        {
            Edit(target, propertyName, property => property.intValue = value);
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            Edit(target, propertyName, property => property.floatValue = value);
        }

        private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
        {
            Edit(target, propertyName, property => property.boolValue = value);
        }

        private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            Edit(target, propertyName, property => property.objectReferenceValue = value);
        }

        private static void SetEnum<T>(UnityEngine.Object target, string propertyName, T value) where T : Enum
        {
            Edit(target, propertyName, property => property.enumValueIndex = Convert.ToInt32(value));
        }

        private static void SetObjectArray(UnityEngine.Object target, string propertyName, UnityEngine.Object[] values)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Edit(UnityEngine.Object target, string propertyName, Action<SerializedProperty> edit)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            edit(property);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
