using System.Collections.Generic;
using System.Globalization;

namespace Tower.Core
{
    // T14: pure text builders for the loadout screen's ability badges,
    // tooltips and member stat lines. Callers feed AbilityDef/CharacterDef
    // field values in; nothing here is per-ability — styling is keyed by
    // AbilityTag only and extends by adding a map row (no switch growth).
    public static class AbilityDisplayText
    {
        public const string FallbackColorHex = "#B0B0B0";

        private readonly struct TagStyle
        {
            public TagStyle(string label, string colorHex, string explanation)
            {
                Label = label;
                ColorHex = colorHex;
                Explanation = explanation;
            }

            public string Label { get; }
            public string ColorHex { get; }
            public string Explanation { get; }
        }

        private static readonly Dictionary<AbilityTag, TagStyle> Styles = new Dictionary<AbilityTag, TagStyle>
        {
            { AbilityTag.Apply, new TagStyle("부여", "#4FC3F7", "대상에게 표식을 부여한다.") },
            { AbilityTag.Consume, new TagStyle("소모", "#EF5350", "표식을 소모해 강화된 피해를 입힌다.") },
            { AbilityTag.Amplify, new TagStyle("증폭", "#FFC107", "아군 표식의 효과를 증폭한다.") }
        };

        public static string TagLabel(AbilityTag tag)
        {
            return Styles.TryGetValue(tag, out var style) ? style.Label : tag.ToString();
        }

        public static string TagColorHex(AbilityTag tag)
        {
            return Styles.TryGetValue(tag, out var style) ? style.ColorHex : FallbackColorHex;
        }

        public static string TagExplanation(AbilityTag tag)
        {
            return Styles.TryGetValue(tag, out var style) ? style.Explanation : string.Empty;
        }

        // uGUI rich-text badge, e.g. <color=#4FC3F7>[부여]</color>.
        public static string BuildBadge(AbilityTag tag)
        {
            return "<color=" + TagColorHex(tag) + ">[" + TagLabel(tag) + "]</color>";
        }

        public static string BuildAbilityLine(string displayName, AbilityTag tag)
        {
            return displayName + " " + BuildBadge(tag);
        }

        // Three lines: name+tag / power+range (+amplification when relevant) /
        // one-line tag explanation.
        public static string BuildTooltip(string displayName, AbilityTag tag, int basePower, int range, float amplificationMultiplier = 1f)
        {
            var stats = string.Format(CultureInfo.InvariantCulture, "위력 {0} · 사거리 {1}", basePower, range);
            if (System.Math.Abs(amplificationMultiplier - 1f) > 0.0001f)
            {
                stats += string.Format(CultureInfo.InvariantCulture, " · 증폭 x{0:0.##}", amplificationMultiplier);
            }

            return displayName + " [" + TagLabel(tag) + "]\n" + stats + "\n" + TagExplanation(tag);
        }

        public static string BuildMemberStatsLine(string displayName, int maxHp, int baseSpeed, int speedModifier)
        {
            var sign = speedModifier.ToString("+#;-#;0", CultureInfo.InvariantCulture);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} | HP {1} | 속도 {2} ({3})",
                displayName,
                maxHp,
                baseSpeed + speedModifier,
                sign);
        }
    }
}
