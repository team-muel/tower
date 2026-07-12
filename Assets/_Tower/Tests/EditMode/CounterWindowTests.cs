using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class CounterWindowTests
    {
        private readonly CounterWindow window = new CounterWindow(0.33f, 0.78f, 0.5f);

        [Test]
        public void Instant_UsesExactZoneBoundaries()
        {
            const float start = 10f;
            const float duration = 0.9f;

            Assert.That(window.ClassifyInstant(start + (0.33f * duration), start, duration), Is.EqualTo(CounterInstantResult.Clean));
            Assert.That(window.ClassifyInstant(start + (0.78f * duration), start, duration), Is.EqualTo(CounterInstantResult.Clean));
            Assert.That(window.ClassifyInstant(start + (0.7801f * duration), start, duration), Is.EqualTo(CounterInstantResult.Late));
        }

        [Test]
        public void Instant_DuringCommit_IsMissed()
        {
            var telegraph = new TelegraphState(new TelegraphDurations(0.9f, 0.25f, 0.6f));
            telegraph.TryBeginWindup();
            telegraph.Advance(0.9f);

            Assert.That(window.ClassifyInstant(telegraph.ElapsedSeconds, telegraph), Is.EqualTo(CounterInstantResult.Missed));
        }

        [Test]
        public void Coverage_AtThreshold_IsClean()
        {
            const float windupStart = 5f;
            const float commitStart = 5.9f;
            var lateStart = windupStart + (0.78f * 0.9f);
            var holdStart = commitStart - ((commitStart - lateStart) * 0.5f);

            Assert.That(
                window.ClassifyCoverage(holdStart, commitStart, windupStart, commitStart),
                Is.EqualTo(CounterCoverageResult.Clean));
        }

        [Test]
        public void InstantAndCoverage_CanDisagree()
        {
            const float windupStart = 0f;
            const float commitStart = 0.9f;
            var cleanPress = 0.5f;

            Assert.That(window.ClassifyInstant(cleanPress, windupStart, 0.9f), Is.EqualTo(CounterInstantResult.Clean));
            Assert.That(
                window.ClassifyCoverage(cleanPress, cleanPress + 0.01f, windupStart, commitStart),
                Is.EqualTo(CounterCoverageResult.Missed));
        }
    }
}
