using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class CampMoverTests
    {
        [Test]
        public void StepTowards_FarDestination_MovesBySpeedTimesDelta()
        {
            var step = CampMover.StepTowards(0f, 0f, 10f, 0f, 5f, 0.2f);

            Assert.That(step.Arrived, Is.False);
            Assert.That(step.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(step.Z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void StepTowards_DiagonalDestination_MovesAlongDirection()
        {
            var step = CampMover.StepTowards(0f, 0f, 3f, 4f, 5f, 0.2f);

            // Distance 5, step 1: expect one fifth of the way along (3,4).
            Assert.That(step.Arrived, Is.False);
            Assert.That(step.X, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(step.Z, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void StepTowards_StepLargerThanDistance_SnapsToDestinationWithoutOvershoot()
        {
            var step = CampMover.StepTowards(0f, 0f, 1f, 1f, 100f, 1f);

            Assert.That(step.Arrived, Is.True);
            Assert.That(step.X, Is.EqualTo(1f));
            Assert.That(step.Z, Is.EqualTo(1f));
        }

        [Test]
        public void StepTowards_WithinArrivalEpsilon_Arrives()
        {
            var step = CampMover.StepTowards(0.99f, 0f, 1f, 0f, 5f, 0.016f);

            Assert.That(step.Arrived, Is.True);
            Assert.That(step.X, Is.EqualTo(1f));
        }

        [Test]
        public void StepTowards_AlreadyAtDestination_ArrivesImmediately()
        {
            var step = CampMover.StepTowards(2f, 3f, 2f, 3f, 5f, 0.016f);

            Assert.That(step.Arrived, Is.True);
            Assert.That(step.X, Is.EqualTo(2f));
            Assert.That(step.Z, Is.EqualTo(3f));
        }

        [Test]
        public void StepTowards_ZeroDelta_DoesNotMove()
        {
            var step = CampMover.StepTowards(0f, 0f, 10f, 0f, 5f, 0f);

            Assert.That(step.Arrived, Is.False);
            Assert.That(step.X, Is.EqualTo(0f));
            Assert.That(step.Z, Is.EqualTo(0f));
        }

        [Test]
        public void StepTowards_RepeatedSteps_ConvergeAndStop()
        {
            float x = -4f;
            float z = 6f;
            var arrived = false;
            for (var frame = 0; frame < 300 && !arrived; frame++)
            {
                var step = CampMover.StepTowards(x, z, 5f, -2f, 5f, 0.016f);
                x = step.X;
                z = step.Z;
                arrived = step.Arrived;
            }

            Assert.That(arrived, Is.True);
            Assert.That(x, Is.EqualTo(5f));
            Assert.That(z, Is.EqualTo(-2f));
        }
    }
}
