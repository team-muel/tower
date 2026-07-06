using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class CameraTuningTests
    {
        [Test]
        public void Defaults_MatchV0Brief()
        {
            var tuning = CameraTuningState.Default;

            Assert.That(tuning.Pitch, Is.EqualTo(52f));
            Assert.That(tuning.Distance, Is.EqualTo(14f));
            Assert.That(tuning.Fov, Is.EqualTo(38f));
            Assert.That(tuning.FollowDamping, Is.EqualTo(0.12f));
            Assert.That(CameraTuning.MinDistance, Is.EqualTo(8f));
            Assert.That(CameraTuning.MaxDistance, Is.EqualTo(20f));
        }

        [Test]
        public void ClampPitch_EnforcesRange()
        {
            Assert.That(CameraTuning.ClampPitch(10f), Is.EqualTo(CameraTuning.MinPitch));
            Assert.That(CameraTuning.ClampPitch(89f), Is.EqualTo(CameraTuning.MaxPitch));
            Assert.That(CameraTuning.ClampPitch(52f), Is.EqualTo(52f));
        }

        [Test]
        public void ClampDistance_EnforcesZoomRange()
        {
            Assert.That(CameraTuning.ClampDistance(0f), Is.EqualTo(8f));
            Assert.That(CameraTuning.ClampDistance(8f), Is.EqualTo(8f));
            Assert.That(CameraTuning.ClampDistance(20f), Is.EqualTo(20f));
            Assert.That(CameraTuning.ClampDistance(99f), Is.EqualTo(20f));
        }

        [Test]
        public void ClampFov_EnforcesRange()
        {
            Assert.That(CameraTuning.ClampFov(1f), Is.EqualTo(CameraTuning.MinFov));
            Assert.That(CameraTuning.ClampFov(179f), Is.EqualTo(CameraTuning.MaxFov));
            Assert.That(CameraTuning.ClampFov(38f), Is.EqualTo(38f));
        }

        [Test]
        public void Clamp_State_ClampsEveryComponent()
        {
            var wild = new CameraTuningState(pitch: 5f, distance: 200f, fov: 5f, followDamping: -1f);

            var clamped = CameraTuning.Clamp(wild);

            Assert.That(clamped.Pitch, Is.EqualTo(CameraTuning.MinPitch));
            Assert.That(clamped.Distance, Is.EqualTo(CameraTuning.MaxDistance));
            Assert.That(clamped.Fov, Is.EqualTo(CameraTuning.MinFov));
            Assert.That(clamped.FollowDamping, Is.EqualTo(0f));
        }

        [Test]
        public void Clamp_NaN_FallsBackToMinimum()
        {
            Assert.That(CameraTuning.ClampPitch(float.NaN), Is.EqualTo(CameraTuning.MinPitch));
            Assert.That(CameraTuning.ClampDistance(float.NaN), Is.EqualTo(CameraTuning.MinDistance));
        }

        [Test]
        public void ToJson_Default_WritesInvariantSingleLine()
        {
            var json = CameraTuningState.Default.ToJson();

            Assert.That(json, Is.EqualTo("{\"pitch\":52,\"distance\":14,\"fov\":38,\"followDamping\":0.12}"));
        }

        [Test]
        public void ToJson_FractionalValues_UseDotSeparator()
        {
            var json = new CameraTuningState(52.5f, 14.25f, 38f, 0.12f).ToJson();

            Assert.That(json, Does.Contain("\"pitch\":52.5"));
            Assert.That(json, Does.Contain("\"distance\":14.25"));
            Assert.That(json, Does.Not.Contain("52,5"));
        }
    }
}
