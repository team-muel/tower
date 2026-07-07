using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Tower.Core;

namespace Tower.Data
{
    // Immutable, id-indexed static-data catalog loaded from the six generated
    // CSVs. Load performs the SECOND validation gate (the first is the build-time
    // Sdp extractor): required-empty, primitive parse, enum membership,
    // id uniqueness, and FK integrity. ALL violations are collected and reported
    // once via DataValidationException — no silent runtime failures.
    public sealed class DataCatalog
    {
        public const string MarksSheet = "Marks";
        public const string PassivesSheet = "Passives";
        public const string AbilitiesSheet = "Abilities";
        public const string CharactersSheet = "Characters";
        public const string ItemsSheet = "Items";
        public const string DropTablesSheet = "DropTables";

        private readonly IReadOnlyDictionary<string, MarkData> _marks;
        private readonly IReadOnlyDictionary<string, PassiveData> _passives;
        private readonly IReadOnlyDictionary<string, AbilityData> _abilities;
        private readonly IReadOnlyDictionary<string, CharacterData> _characters;
        private readonly IReadOnlyDictionary<string, ItemData> _items;
        // DropTables are keyed by tableId -> ordered entries (entryId unique per table).
        private readonly IReadOnlyDictionary<string, IReadOnlyList<DropTableEntryData>> _dropTables;

        public IReadOnlyDictionary<string, MarkData> Marks => _marks;
        public IReadOnlyDictionary<string, PassiveData> Passives => _passives;
        public IReadOnlyDictionary<string, AbilityData> Abilities => _abilities;
        public IReadOnlyDictionary<string, CharacterData> Characters => _characters;
        public IReadOnlyDictionary<string, ItemData> Items => _items;
        public IReadOnlyDictionary<string, IReadOnlyList<DropTableEntryData>> DropTables => _dropTables;

        public MarkData GetMark(string id) => _marks.TryGetValue(id, out var v) ? v : null;
        public PassiveData GetPassive(string id) => _passives.TryGetValue(id, out var v) ? v : null;
        public AbilityData GetAbility(string id) => _abilities.TryGetValue(id, out var v) ? v : null;
        public CharacterData GetCharacter(string id) => _characters.TryGetValue(id, out var v) ? v : null;
        public ItemData GetItem(string id) => _items.TryGetValue(id, out var v) ? v : null;

        public IReadOnlyList<DropTableEntryData> GetDropTable(string tableId)
            => _dropTables.TryGetValue(tableId, out var v) ? v : Array.Empty<DropTableEntryData>();

        private DataCatalog(
            IReadOnlyDictionary<string, MarkData> marks,
            IReadOnlyDictionary<string, PassiveData> passives,
            IReadOnlyDictionary<string, AbilityData> abilities,
            IReadOnlyDictionary<string, CharacterData> characters,
            IReadOnlyDictionary<string, ItemData> items,
            IReadOnlyDictionary<string, IReadOnlyList<DropTableEntryData>> dropTables)
        {
            _marks = marks;
            _passives = passives;
            _abilities = abilities;
            _characters = characters;
            _items = items;
            _dropTables = dropTables;
        }

        // Loads from Unity TextAssets (CSV imported as TextAsset). Uses .text only,
        // no file IO. Sheet identity is taken from TextAsset.name.
        public static DataCatalog Load(IReadOnlyList<TextAsset> csvAssets)
        {
            if (csvAssets == null)
            {
                throw new DataValidationException("DataCatalog.Load: csvAssets is null.");
            }

            var byName = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < csvAssets.Count; i++)
            {
                var asset = csvAssets[i];
                if (asset == null)
                {
                    throw new DataValidationException(
                        "DataCatalog.Load: csvAssets[" + i + "] is null.");
                }
                byName[asset.name] = asset.text;
            }

            return Load(byName);
        }

        // Loads from a sheetName -> csvText map. Testable without TextAssets.
        public static DataCatalog Load(IReadOnlyDictionary<string, string> csvByName)
        {
            if (csvByName == null)
            {
                throw new DataValidationException("DataCatalog.Load: csvByName is null.");
            }

            var errors = new List<string>();

            var marks = new Dictionary<string, MarkData>(StringComparer.Ordinal);
            var passives = new Dictionary<string, PassiveData>(StringComparer.Ordinal);
            var abilities = new Dictionary<string, AbilityData>(StringComparer.Ordinal);
            var characters = new Dictionary<string, CharacterData>(StringComparer.Ordinal);
            var items = new Dictionary<string, ItemData>(StringComparer.Ordinal);
            var dropTables = new Dictionary<string, List<DropTableEntryData>>(StringComparer.Ordinal);

            ParseMarks(csvByName, marks, errors);
            ParsePassives(csvByName, passives, errors);
            ParseAbilities(csvByName, abilities, errors);
            ParseCharacters(csvByName, characters, errors);
            ParseItems(csvByName, items, errors);
            ParseDropTables(csvByName, dropTables, errors);

            // ---- FK integrity (only meaningful once rows parsed) ----
            // Abilities.targetMark in Marks.id (empty ok).
            foreach (var kv in abilities)
            {
                var ability = kv.Value;
                if (!string.IsNullOrEmpty(ability.TargetMark) && !marks.ContainsKey(ability.TargetMark))
                {
                    errors.Add(Violation(AbilitiesSheet, "id=" + ability.Id, "targetMark",
                        "references unknown Mark '" + ability.TargetMark + "'"));
                }
            }

            // Characters.passive in Passives.id (empty ok);
            // Characters.defaultAbilities each in Abilities.id.
            foreach (var kv in characters)
            {
                var ch = kv.Value;
                if (!string.IsNullOrEmpty(ch.Passive) && !passives.ContainsKey(ch.Passive))
                {
                    errors.Add(Violation(CharactersSheet, "id=" + ch.Id, "passive",
                        "references unknown Passive '" + ch.Passive + "'"));
                }

                for (int a = 0; a < ch.DefaultAbilities.Count; a++)
                {
                    var abilityId = ch.DefaultAbilities[a];
                    if (string.IsNullOrEmpty(abilityId))
                    {
                        errors.Add(Violation(CharactersSheet, "id=" + ch.Id, "defaultAbilities",
                            "contains an empty ability id at slot " + a));
                    }
                    else if (!abilities.ContainsKey(abilityId))
                    {
                        errors.Add(Violation(CharactersSheet, "id=" + ch.Id, "defaultAbilities",
                            "references unknown Ability '" + abilityId + "'"));
                    }
                }
            }

            if (errors.Count > 0)
            {
                throw new DataValidationException(BuildErrorMessage(errors));
            }

            var readonlyDropTables =
                new Dictionary<string, IReadOnlyList<DropTableEntryData>>(dropTables.Count, StringComparer.Ordinal);
            foreach (var kv in dropTables)
            {
                readonlyDropTables[kv.Key] = kv.Value.AsReadOnly();
            }

            return new DataCatalog(marks, passives, abilities, characters, items, readonlyDropTables);
        }

        // ---------------------------------------------------------------
        // Per-sheet parsers. Each collects violations rather than throwing,
        // so the final report lists ALL problems at once.
        // ---------------------------------------------------------------

        private static void ParseMarks(
            IReadOnlyDictionary<string, string> csv,
            Dictionary<string, MarkData> outMap,
            List<string> errors)
        {
            var table = GetTable(csv, MarksSheet, errors);
            if (table == null) return;

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                string rowRef = RowRef(r);
                string id = Required(MarksSheet, rowRef, "id", row, errors);
                string displayName = Required(MarksSheet, rowRef, "displayName", row, errors);
                int duration = ParseInt(MarksSheet, rowRef, "durationTurns", row, errors);
                bool stackable = ParseBool(MarksSheet, rowRef, "stackable", row, errors);

                if (id == null) continue;
                if (outMap.ContainsKey(id))
                {
                    errors.Add(Violation(MarksSheet, rowRef, "id", "duplicate id '" + id + "'"));
                    continue;
                }
                outMap[id] = new MarkData(id, displayName ?? string.Empty, duration, stackable);
            }
        }

        private static void ParsePassives(
            IReadOnlyDictionary<string, string> csv,
            Dictionary<string, PassiveData> outMap,
            List<string> errors)
        {
            var table = GetTable(csv, PassivesSheet, errors);
            if (table == null) return;

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                string rowRef = RowRef(r);
                string id = Required(PassivesSheet, rowRef, "id", row, errors);
                string displayName = Required(PassivesSheet, rowRef, "displayName", row, errors);
                string hook = Required(PassivesSheet, rowRef, "effectHookKey", row, errors);

                if (id == null) continue;
                if (outMap.ContainsKey(id))
                {
                    errors.Add(Violation(PassivesSheet, rowRef, "id", "duplicate id '" + id + "'"));
                    continue;
                }
                outMap[id] = new PassiveData(id, displayName ?? string.Empty, hook ?? string.Empty);
            }
        }

        private static void ParseAbilities(
            IReadOnlyDictionary<string, string> csv,
            Dictionary<string, AbilityData> outMap,
            List<string> errors)
        {
            var table = GetTable(csv, AbilitiesSheet, errors);
            if (table == null) return;

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                string rowRef = RowRef(r);
                string id = Required(AbilitiesSheet, rowRef, "id", row, errors);
                string displayName = Required(AbilitiesSheet, rowRef, "displayName", row, errors);
                AbilityTag tag = ParseEnum<AbilityTag>(AbilitiesSheet, rowRef, "tag", row, errors);
                string targetMark = Optional(row, "targetMark"); // FK checked later; empty ok
                int range = ParseInt(AbilitiesSheet, rowRef, "range", row, errors);
                int cost = ParseInt(AbilitiesSheet, rowRef, "cost", row, errors);
                int basePower = ParseInt(AbilitiesSheet, rowRef, "basePower", row, errors);
                float amp = ParseFloat(AbilitiesSheet, rowRef, "amplificationMultiplier", row, errors);
                AbilityTargetType targetType =
                    ParseEnum<AbilityTargetType>(AbilitiesSheet, rowRef, "targetType", row, errors);
                int cooldown = ParseInt(AbilitiesSheet, rowRef, "cooldownRounds", row, errors);

                if (id == null) continue;
                if (outMap.ContainsKey(id))
                {
                    errors.Add(Violation(AbilitiesSheet, rowRef, "id", "duplicate id '" + id + "'"));
                    continue;
                }
                outMap[id] = new AbilityData(
                    id, displayName ?? string.Empty, tag, targetMark, range, cost,
                    basePower, amp, targetType, cooldown);
            }
        }

        private static void ParseCharacters(
            IReadOnlyDictionary<string, string> csv,
            Dictionary<string, CharacterData> outMap,
            List<string> errors)
        {
            var table = GetTable(csv, CharactersSheet, errors);
            if (table == null) return;

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                string rowRef = RowRef(r);
                string id = Required(CharactersSheet, rowRef, "id", row, errors);
                string displayName = Required(CharactersSheet, rowRef, "displayName", row, errors);
                int maxHp = ParseInt(CharactersSheet, rowRef, "maxHp", row, errors);
                int attack = ParseInt(CharactersSheet, rowRef, "attack", row, errors);
                int defense = ParseInt(CharactersSheet, rowRef, "defense", row, errors);
                int speed = ParseInt(CharactersSheet, rowRef, "speed", row, errors);
                DispositionType disposition =
                    ParseEnum<DispositionType>(CharactersSheet, rowRef, "disposition", row, errors);
                string passive = Optional(row, "passive"); // FK checked later; empty ok
                string abilitiesRaw = Optional(row, "defaultAbilities");
                bool isReturner = ParseBool(CharactersSheet, rowRef, "isReturner", row, errors);
                bool chainLocked = ParseBool(CharactersSheet, rowRef, "chainLocked", row, errors);
                bool isPreset = ParseBool(CharactersSheet, rowRef, "isPreset", row, errors);
                int factionId = ParseInt(CharactersSheet, rowRef, "factionId", row, errors);

                var defaultAbilities = SplitList(abilitiesRaw);

                if (id == null) continue;
                if (outMap.ContainsKey(id))
                {
                    errors.Add(Violation(CharactersSheet, rowRef, "id", "duplicate id '" + id + "'"));
                    continue;
                }
                outMap[id] = new CharacterData(
                    id, displayName ?? string.Empty, maxHp, attack, defense, speed, disposition,
                    passive, defaultAbilities, isReturner, chainLocked, isPreset, factionId);
            }
        }

        private static void ParseItems(
            IReadOnlyDictionary<string, string> csv,
            Dictionary<string, ItemData> outMap,
            List<string> errors)
        {
            var table = GetTable(csv, ItemsSheet, errors);
            if (table == null) return;

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                string rowRef = RowRef(r);
                string id = Required(ItemsSheet, rowRef, "id", row, errors);
                string displayName = Required(ItemsSheet, rowRef, "displayName", row, errors);
                ResourceScope scope =
                    ParseEnum<ResourceScope>(ItemsSheet, rowRef, "resourceScope", row, errors);
                int power = ParseInt(ItemsSheet, rowRef, "power", row, errors);
                int stackMax = ParseInt(ItemsSheet, rowRef, "stackMax", row, errors);
                string description = Optional(row, "description");

                if (id == null) continue;
                if (outMap.ContainsKey(id))
                {
                    errors.Add(Violation(ItemsSheet, rowRef, "id", "duplicate id '" + id + "'"));
                    continue;
                }
                outMap[id] = new ItemData(
                    id, displayName ?? string.Empty, scope, power, stackMax, description ?? string.Empty);
            }
        }

        private static void ParseDropTables(
            IReadOnlyDictionary<string, string> csv,
            Dictionary<string, List<DropTableEntryData>> outMap,
            List<string> errors)
        {
            var table = GetTable(csv, DropTablesSheet, errors);
            if (table == null) return;

            // Uniqueness key = tableId + '|' + entryId.
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                string rowRef = RowRef(r);
                string tableId = Required(DropTablesSheet, rowRef, "tableId", row, errors);
                string entryId = Required(DropTablesSheet, rowRef, "entryId", row, errors);
                int weight = ParseInt(DropTablesSheet, rowRef, "weight", row, errors);
                RewardType rewardType =
                    ParseEnum<RewardType>(DropTablesSheet, rowRef, "rewardType", row, errors);
                string refId = Optional(row, "refId");
                int minDepth = ParseInt(DropTablesSheet, rowRef, "minDepth", row, errors);
                // maxDepth may be blank (open-ended) -> int.MaxValue.
                int maxDepth = ParseIntOrDefault(
                    DropTablesSheet, rowRef, "maxDepth", row, errors, int.MaxValue);

                if (tableId == null || entryId == null) continue;

                string key = tableId + "|" + entryId;
                if (!seen.Add(key))
                {
                    errors.Add(Violation(DropTablesSheet, rowRef, "entryId",
                        "duplicate entryId '" + entryId + "' within table '" + tableId + "'"));
                    continue;
                }

                if (!outMap.TryGetValue(tableId, out var list))
                {
                    list = new List<DropTableEntryData>();
                    outMap[tableId] = list;
                }
                list.Add(new DropTableEntryData(
                    tableId, entryId, weight, rewardType, refId ?? string.Empty, minDepth, maxDepth));
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static CsvTable GetTable(
            IReadOnlyDictionary<string, string> csv, string sheet, List<string> errors)
        {
            if (!csv.TryGetValue(sheet, out var text))
            {
                errors.Add("Sheet '" + sheet + "': missing CSV (no TextAsset named '" + sheet + "').");
                return null;
            }

            try
            {
                return CsvTable.Parse(sheet, text);
            }
            catch (DataValidationException ex)
            {
                errors.Add(ex.Message);
                return null;
            }
        }

        private static string RowRef(int zeroBasedDataRow)
        {
            // +2: 1 for the header row, 1 to make it 1-based file line number.
            return "row " + (zeroBasedDataRow + 2);
        }

        private static string Required(
            string sheet, string rowRef, string col,
            IReadOnlyDictionary<string, string> row, List<string> errors)
        {
            if (!row.TryGetValue(col, out var raw))
            {
                errors.Add(Violation(sheet, rowRef, col, "missing column"));
                return null;
            }
            var value = raw == null ? string.Empty : raw.Trim();
            if (value.Length == 0)
            {
                errors.Add(Violation(sheet, rowRef, col, "required value is empty"));
                return null;
            }
            return value;
        }

        private static string Optional(IReadOnlyDictionary<string, string> row, string col)
        {
            if (!row.TryGetValue(col, out var raw) || raw == null) return string.Empty;
            return raw.Trim();
        }

        private static int ParseInt(
            string sheet, string rowRef, string col,
            IReadOnlyDictionary<string, string> row, List<string> errors)
        {
            var raw = Optional(row, col);
            if (raw.Length == 0)
            {
                errors.Add(Violation(sheet, rowRef, col, "required int is empty"));
                return 0;
            }
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                errors.Add(Violation(sheet, rowRef, col, "'" + raw + "' is not a valid int"));
                return 0;
            }
            return v;
        }

        private static int ParseIntOrDefault(
            string sheet, string rowRef, string col,
            IReadOnlyDictionary<string, string> row, List<string> errors, int fallback)
        {
            var raw = Optional(row, col);
            if (raw.Length == 0) return fallback;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                errors.Add(Violation(sheet, rowRef, col, "'" + raw + "' is not a valid int"));
                return fallback;
            }
            return v;
        }

        private static float ParseFloat(
            string sheet, string rowRef, string col,
            IReadOnlyDictionary<string, string> row, List<string> errors)
        {
            var raw = Optional(row, col);
            if (raw.Length == 0)
            {
                errors.Add(Violation(sheet, rowRef, col, "required float is empty"));
                return 0f;
            }
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                errors.Add(Violation(sheet, rowRef, col, "'" + raw + "' is not a valid float"));
                return 0f;
            }
            return v;
        }

        private static bool ParseBool(
            string sheet, string rowRef, string col,
            IReadOnlyDictionary<string, string> row, List<string> errors)
        {
            var raw = Optional(row, col);
            if (raw.Length == 0)
            {
                errors.Add(Violation(sheet, rowRef, col, "required bool is empty"));
                return false;
            }
            if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) return false;
            errors.Add(Violation(sheet, rowRef, col, "'" + raw + "' is not a valid bool (true/false)"));
            return false;
        }

        private static T ParseEnum<T>(
            string sheet, string rowRef, string col,
            IReadOnlyDictionary<string, string> row, List<string> errors) where T : struct
        {
            var raw = Optional(row, col);
            if (raw.Length == 0)
            {
                errors.Add(Violation(sheet, rowRef, col, "required enum is empty"));
                return default;
            }
            // Reject numeric strings and unknown names; enforce exact name membership.
            if (Enum.TryParse<T>(raw, false, out var parsed) && Enum.IsDefined(typeof(T), parsed)
                && !IsNumeric(raw))
            {
                return parsed;
            }
            errors.Add(Violation(sheet, rowRef, col,
                "'" + raw + "' is not a valid " + typeof(T).Name +
                " (expected one of: " + string.Join(", ", Enum.GetNames(typeof(T))) + ")"));
            return default;
        }

        private static bool IsNumeric(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '-' && i == 0) continue;
                if (c < '0' || c > '9') return false;
            }
            return s.Length > 0;
        }

        private static IReadOnlyList<string> SplitList(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Array.Empty<string>();
            var parts = raw.Split(';');
            var list = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                var trimmed = parts[i].Trim();
                if (trimmed.Length > 0) list.Add(trimmed);
            }
            return list;
        }

        private static string Violation(string sheet, string rowRef, string col, string detail)
        {
            return "Sheet '" + sheet + "' " + rowRef + " column '" + col + "': " + detail + ".";
        }

        private static string BuildErrorMessage(List<string> errors)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("DataCatalog load failed with ");
            sb.Append(errors.Count);
            sb.Append(errors.Count == 1 ? " violation:" : " violations:");
            for (int i = 0; i < errors.Count; i++)
            {
                sb.Append("\n  - ");
                sb.Append(errors[i]);
            }
            return sb.ToString();
        }
    }
}
