using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // T19: pure orbit math - yaw wrap, pitch/zoom clamps, orbital position.
    public sealed class OrbitCameraMathTests
    {
        private const float Epsilon = 1e-4f;

        [Test]
        public void NormalizeYaw_WrapsAboveFullTurn()
        {
            Assert.That(OrbitCameraMath.NormalizeYaw(370f), Is.EqualTo(10f).Within(Epsilon));
            Assert.That(OrbitCameraMath.NormalizeYaw(360f), Is.EqualTo(0f).Within(Epsilon));
            Assert.That(OrbitCameraMath.NormalizeYaw(725f), Is.EqualTo(5f).Within(Epsilon));
        }

        [Test]
        public void NormalizeYaw_WrapsNegativeIntoRange()
        {
            Assert.That(OrbitCameraMath.NormalizeYaw(-45f), Is.EqualTo(315f).Within(Epsilon));
            Assert.That(OrbitCameraMath.NormalizeYaw(-360f), Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void NormalizeYaw_NaN_FallsBackToZero()
        {
            Assert.That(OrbitCameraMath.NormalizeYaw(float.NaN), Is.EqualTo(0f));
        }

        [Test]
        public void ClampPitch_UsesBriefRange()
        {
            Assert.That(OrbitCameraMath.ClampPitch(10f), Is.EqualTo(OrbitCameraMath.MinPitch));
            Assert.That(OrbitCameraMath.ClampPitch(89f), Is.EqualTo(OrbitCameraMath.MaxPitch));
            Assert.That(OrbitCameraMath.ClampPitch(45f), Is.EqualTo(45f));
            Assert.That(OrbitCameraMath.ClampPitch(float.NaN), Is.EqualTo(OrbitCameraMath.MinPitch));
        }

        [Test]
        public void ClampPitch_HonorsCustomRange()
        {
            Assert.That(OrbitCameraMath.ClampPitch(10f, 30f, 60f), Is.EqualTo(30f));
            Assert.That(OrbitCameraMath.ClampPitch(75f, 30f, 60f), Is.EqualTo(60f));
        }

        [Test]
        public void ClampDistance_UsesBriefRange()
        {
            Assert.That(OrbitCameraMath.ClampDistance(1f), Is.EqualTo(OrbitCameraMath.MinDistance));
            Assert.That(OrbitCameraMath.ClampDistance(50f), Is.EqualTo(OrbitCameraMath.MaxDistance));
            Assert.That(OrbitCameraMath.ClampDistance(12f), Is.EqualTo(12f));
            Assert.That(OrbitCameraMath.ClampDistance(float.NaN), Is.EqualTo(OrbitCameraMath.MinDistance));
        }

        [Test]
        public void ComputeOffset_YawZeroPitchZero_SitsBehindFocus()
        {
            var offset = OrbitCameraMath.ComputeOffset(0f, 0f, 10f);

            Assert.That(offset.X, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(offset.Y, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(offset.Z, Is.EqualTo(-10f).Within(Epsilon));
        }

        [Test]
        public void ComputeOffset_PitchNinety_SitsStraightAbove()
        {
            var offset = OrbitCameraMath.ComputeOffset(0f, 90f, 10f);

            Assert.That(offset.X, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(offset.Y, Is.EqualTo(10f).Within(Epsilon));
            Assert.That(offset.Z, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void ComputeOffset_YawNinety_MovesAlongNegativeX()
        {
            var offset = OrbitCameraMath.ComputeOffset(90f, 0f, 10f);

            Assert.That(offset.X, Is.EqualTo(-10f).Within(Epsilon));
            Assert.That(offset.Y, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(offset.Z, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void ComputeOffset_PitchFortyFive_SplitsHeightAndGroundDistance()
        {
            var offset = OrbitCameraMath.ComputeOffset(0f, 45f, 10f);
            var expected = 10f * (float)System.Math.Sin(45.0 * System.Math.PI / 180.0);

            Assert.That(offset.Y, Is.EqualTo(expected).Within(Epsilon));
            Assert.That(offset.Z, Is.EqualTo(-expected).Within(Epsilon));
        }

        [Test]
        public void ComputeOffset_PreservesDistance()
        {
            var offset = OrbitCameraMath.ComputeOffset(123f, 37f, 14f);
            var magnitude = (float)System.Math.Sqrt(
                (offset.X * offset.X) + (offset.Y * offset.Y) + (offset.Z * offset.Z));

            Assert.That(magnitude, Is.EqualTo(14f).Within(Epsilon));
        }
    }
}
