using NUnit.Framework;
using Tower.Combat;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class RunTransitionPresenterTests
    {
        [Test]
        public void Show_FadesInHoldsAndFadesOut()
        {
            GameObject host = new GameObject("run-transition");
            try
            {
                RunTransitionPresenter presenter = host.AddComponent<RunTransitionPresenter>();
                Assert.That(presenter.Show("", null).IsFailure, Is.True, "headline required");
                Assert.That(presenter.Show("후퇴", "사유", 2.0f).IsSuccess, Is.True);
                Assert.That(presenter.IsVisible, Is.True);

                presenter.Tick(0.1f);
                float fadingIn = presenter.CurrentAlpha();
                Assert.That(fadingIn, Is.GreaterThan(0f).And.LessThan(1f));

                presenter.Tick(0.5f);
                Assert.That(presenter.CurrentAlpha(), Is.EqualTo(1f).Within(0.001f), "hold phase");

                presenter.Tick(1.2f);
                Assert.That(presenter.CurrentAlpha(), Is.LessThan(1f), "fade out phase");

                presenter.Tick(5f);
                Assert.That(presenter.IsVisible, Is.False);
                Assert.That(presenter.CurrentAlpha(), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
