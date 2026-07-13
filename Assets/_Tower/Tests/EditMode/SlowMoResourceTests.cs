using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class SlowMoResourceTests
    {
        [Test]
        public void Drain_ToEmpty_DisablesEngagementUntilMinimumRecharge()
        {
            var resource = new SlowMoResource(1f, 2.5f, 8f, 0.3f);

            resource.Drain(2.5f);

            Assert.That(resource.Charge, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(resource.CanEngage, Is.False);
            resource.Recharge(2.39f);
            Assert.That(resource.CanEngage, Is.False);
            resource.Recharge(0.01f);
            Assert.That(resource.CanEngage, Is.True);
        }

        [Test]
        public void Recharge_IsMonotonicAndClamped()
        {
            var resource = new SlowMoResource(0.1f, 2.5f, 8f, 0.3f);

            resource.Recharge(1f);
            var afterFirst = resource.Charge;
            resource.Recharge(20f);

            Assert.That(afterFirst, Is.GreaterThan(0.1f));
            Assert.That(resource.Charge, Is.EqualTo(1f));
        }

        [Test]
        public void Drain_IsFrameRateIndependent()
        {
            var singleStep = new SlowMoResource(1f, 2.5f, 8f, 0.3f);
            var manySteps = new SlowMoResource(1f, 2.5f, 8f, 0.3f);

            singleStep.Drain(0.5f);
            for (var index = 0; index < 50; index++)
            {
                manySteps.Drain(0.01f);
            }

            Assert.That(manySteps.Charge, Is.EqualTo(singleStep.Charge).Within(0.0001f));
        }
    }
}
