using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class FixedIsoCameraMathTests
    {
        private const float Epsilon = 1e-4f;

        [Test]
        public void ClampPitch_UsesFixedCameraRange()
        {
            Assert.That(FixedIsoCameraMath.ClampPitch(10f), Is.EqualTo(FixedIsoCameraMath.MinPitch));
            Assert.That(FixedIsoCameraMath.ClampPitch(89f), Is.EqualTo(FixedIsoCameraMath.MaxPitch));
            Assert.That(FixedIsoCameraMath.ClampPitch(45f), Is.EqualTo(45f));
            Assert.That(FixedIsoCameraMath.ClampPitch(float.NaN), Is.EqualTo(FixedIsoCameraMath.MinPitch));
        }

        [Test]
        public void ClampPitch_HonorsCustomRange()
        {
            Assert.That(FixedIsoCameraMath.ClampPitch(10f, 30f, 60f), Is.EqualTo(30f));
            Assert.That(FixedIsoCameraMath.ClampPitch(75f, 30f, 60f), Is.EqualTo(60f));
        }

        [Test]
        public void ClampDistance_UsesFixedCameraRange()
        {
            Assert.That(FixedIsoCameraMath.ClampDistance(1f), Is.EqualTo(FixedIsoCameraMath.MinDistance));
            Assert.That(FixedIsoCameraMath.ClampDistance(50f), Is.EqualTo(FixedIsoCameraMath.MaxDistance));
            Assert.That(FixedIsoCameraMath.ClampDistance(12f), Is.EqualTo(12f));
            Assert.That(FixedIsoCameraMath.ClampDistance(float.NaN), Is.EqualTo(FixedIsoCameraMath.MinDistance));
        }

        [Test]
        public void ComputeOffset_YawZeroPitchZero_SitsBehindFocus()
        {
            CameraOffset offset = FixedIsoCameraMath.ComputeOffset(0f, 0f, 10f);

            Assert.That(offset.X, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(offset.Y, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(offset.Z, Is.EqualTo(-10f).Within(Epsilon));
        }

        [Test]
        public void ComputeOffset_DefaultYawUsesStableIsoDiagonal()
        {
            CameraOffset offset = FixedIsoCameraMath.ComputeOffset(FixedIsoCameraMath.DefaultYaw, 0f, 10f);
            float expected = 10f * (float)System.Math.Sin(45.0 * System.Math.PI / 180.0);

            Assert.That(offset.X, Is.EqualTo(-expected).Within(Epsilon));
            Assert.That(offset.Y, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(offset.Z, Is.EqualTo(-expected).Within(Epsilon));
        }

        [Test]
        public void ComputeOffset_PitchNinety_SitsStraightAbove()
        {
            CameraOffset offset = FixedIsoCameraMath.ComputeOffset(FixedIsoCameraMath.DefaultYaw, 90f, 10f);

            Assert.That(offset.X, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(offset.Y, Is.EqualTo(10f).Within(Epsilon));
            Assert.That(offset.Z, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void ComputeOffset_PreservesDistance()
        {
            CameraOffset offset = FixedIsoCameraMath.ComputeOffset(123f, 37f, 14f);
            float magnitude = (float)System.Math.Sqrt(
                (offset.X * offset.X) + (offset.Y * offset.Y) + (offset.Z * offset.Z));

            Assert.That(magnitude, Is.EqualTo(14f).Within(Epsilon));
        }
    }
}
