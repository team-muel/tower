using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class AbilityDisplayTextTests
    {
        [TestCase(AbilityTag.Apply, "부여")]
        [TestCase(AbilityTag.Consume, "소모")]
        [TestCase(AbilityTag.Amplify, "증폭")]
        public void TagLabel_MapsKnownTags(AbilityTag tag, string expected)
        {
            Assert.That(AbilityDisplayText.TagLabel(tag), Is.EqualTo(expected));
        }

        [Test]
        public void TagLabel_UnknownTag_FallsBackToEnumName()
        {
            Assert.That(AbilityDisplayText.TagLabel(AbilityTag.None), Is.EqualTo("None"));
        }

        [Test]
        public void TagColorHex_DistinctPerKnownTag()
        {
            var apply = AbilityDisplayText.TagColorHex(AbilityTag.Apply);
            var consume = AbilityDisplayText.TagColorHex(AbilityTag.Consume);
            var amplify = AbilityDisplayText.TagColorHex(AbilityTag.Amplify);

            Assert.That(apply, Is.Not.EqualTo(consume));
            Assert.That(apply, Is.Not.EqualTo(amplify));
            Assert.That(consume, Is.Not.EqualTo(amplify));
        }

        [Test]
        public void TagColorHex_UnknownTag_UsesFallback()
        {
            Assert.That(AbilityDisplayText.TagColorHex(AbilityTag.None), Is.EqualTo(AbilityDisplayText.FallbackColorHex));
        }

        [TestCase(AbilityTag.Apply)]
        [TestCase(AbilityTag.Consume)]
        [TestCase(AbilityTag.Amplify)]
        public void TagExplanation_KnownTags_HaveOneNonEmptyLine(AbilityTag tag)
        {
            var explanation = AbilityDisplayText.TagExplanation(tag);

            Assert.That(explanation, Is.Not.Empty);
            Assert.That(explanation, Does.Not.Contain("\n"));
        }

        [TestCase(AbilityTag.Apply)]
        [TestCase(AbilityTag.Consume)]
        [TestCase(AbilityTag.Amplify)]
        public void BuildBadge_WrapsLabelInColorMarkup(AbilityTag tag)
        {
            var badge = AbilityDisplayText.BuildBadge(tag);

            Assert.That(badge, Does.StartWith("<color=" + AbilityDisplayText.TagColorHex(tag) + ">"));
            Assert.That(badge, Does.Contain("[" + AbilityDisplayText.TagLabel(tag) + "]"));
            Assert.That(badge, Does.EndWith("</color>"));
        }

        [Test]
        public void BuildAbilityLine_ContainsNameAndBadge()
        {
            var line = AbilityDisplayText.BuildAbilityLine("Frost Bolt", AbilityTag.Apply);

            Assert.That(line, Does.StartWith("Frost Bolt"));
            Assert.That(line, Does.Contain(AbilityDisplayText.BuildBadge(AbilityTag.Apply)));
        }

        [Test]
        public void BuildTooltip_ContainsNamePowerRangeAndExplanation()
        {
            var tooltip = AbilityDisplayText.BuildTooltip("Frost Bolt", AbilityTag.Apply, 2, 4);

            Assert.That(tooltip, Does.Contain("Frost Bolt"));
            Assert.That(tooltip, Does.Contain("위력 2"));
            Assert.That(tooltip, Does.Contain("사거리 4"));
            Assert.That(tooltip, Does.Contain(AbilityDisplayText.TagExplanation(AbilityTag.Apply)));
            Assert.That(tooltip.Split('\n'), Has.Length.EqualTo(3));
        }

        [Test]
        public void BuildTooltip_AmplifyMultiplier_IncludedOnlyWhenNotOne()
        {
            var amplified = AbilityDisplayText.BuildTooltip("Hold Line", AbilityTag.Amplify, 0, 3, 1.5f);
            var plain = AbilityDisplayText.BuildTooltip("Quick Slash", AbilityTag.Consume, 4, 1, 1f);

            Assert.That(amplified, Does.Contain("증폭 x1.5"));
            Assert.That(plain, Does.Not.Contain("증폭 x"));
        }

        [TestCase(1, "속도 7 (+1)")]
        [TestCase(0, "속도 6 (0)")]
        [TestCase(-2, "속도 4 (-2)")]
        public void BuildMemberStatsLine_FormatsSpeedWithSignedModifier(int modifier, string expectedFragment)
        {
            var line = AbilityDisplayText.BuildMemberStatsLine("Regressor", 28, 6, modifier);

            Assert.That(line, Does.StartWith("Regressor"));
            Assert.That(line, Does.Contain("HP 28"));
            Assert.That(line, Does.Contain(expectedFragment));
        }
    }
}
