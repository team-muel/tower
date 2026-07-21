using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class EncounterResultPresenterTests
    {
        [Test]
        public void Present_ShowsProgressRewardAndCombatSummaryThenExpires()
        {
            GameObject host = new GameObject("EncounterResultPresenterTest");
            try
            {
                var presenter = host.AddComponent<EncounterResultPresenter>();
                var combat = new GeneratedEncounterResult(
                    "event-01",
                    CombatTeam.Player,
                    4,
                    2.8f);
                EncounterReward reward = EncounterReward.Create(
                    "event-01",
                    RewardType.Resource,
                    1,
                    "Run resource").Value;

                Result result = presenter.Present(combat, reward, 1, 7, 2f);

                Assert.That(result.IsSuccess, Is.True, result.Error);
                Assert.That(presenter.IsVisible, Is.True);
                Assert.That(presenter.Headline, Does.Contain("1/7"));
                Assert.That(presenter.Detail, Does.Contain("Run resource +1"));
                Assert.That(presenter.Detail, Does.Contain("4 actions / 2.8s"));
                presenter.Tick(2f);
                Assert.That(presenter.IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
