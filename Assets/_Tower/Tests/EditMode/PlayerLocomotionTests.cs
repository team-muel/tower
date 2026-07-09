using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class PlayerLocomotionTests
    {
        private const float Epsilon = 1e-4f;

        [Test]
        public void PlanarSpeed_IgnoresYDelta()
        {
            float speed = PlayerLocomotion.PlanarSpeed(
                new Vector3(1f, 0f, 2f),
                new Vector3(1f, 5f, 2f),
                0.5f);

            Assert.That(speed, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void PlanarSpeed_ZeroDeltaTime_ReturnsZero()
        {
            float speed = PlayerLocomotion.PlanarSpeed(
                Vector3.zero,
                new Vector3(3f, 0f, 4f),
                0f);

            Assert.That(speed, Is.EqualTo(0f));
        }

        [Test]
        public void SpeedFactor_Clamps()
        {
            Assert.That(PlayerLocomotion.SpeedFactor(12f, 6f), Is.EqualTo(1f));
            Assert.That(PlayerLocomotion.SpeedFactor(-1f, 6f), Is.EqualTo(0f));
            Assert.That(PlayerLocomotion.SpeedFactor(3f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void SpeedFactor_MoveSpeedAtMoveSpeed_ReturnsOne()
        {
            Assert.That(PlayerLocomotion.SpeedFactor(6f, 6f), Is.EqualTo(1f).Within(Epsilon));
        }
    }
}
