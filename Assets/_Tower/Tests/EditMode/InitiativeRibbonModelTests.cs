using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // T23: the ribbon mapping is display-only and engine-free, so it is tested
    // in isolation from Unity/TurnEngine. Covers current/next flagging, dead
    // filtering vs. dimming, team classification, and round-boundary wrap.
    public sealed class InitiativeRibbonModelTests
    {
        private static readonly Dictionary<string, CombatTeam> Teams = new Dictionary<string, CombatTeam>
        {
            { "returner", CombatTeam.Player },
            { "ally-1", CombatTeam.Player },
            { "ally-2", CombatTeam.Player },
            { "enemy-1", CombatTeam.Enemy },
            { "enemy-2", CombatTeam.Enemy },
        };

        private static CombatTeam TeamOf(string id) => Teams.TryGetValue(id, out var team) ? team : CombatTeam.Player;

        private static IReadOnlyList<InitiativeRibbonItem> Build(
            IReadOnlyList<string> order,
            string current,
            System.Func<string, bool> alive = null,
            bool includeDead = true)
        {
            return InitiativeRibbonModel.Build(order, current, alive ?? (_ => true), TeamOf, includeDead);
        }

        [Test]
        public void Build_PreservesRoundOrder()
        {
            var order = new[] { "returner", "enemy-1", "ally-1", "enemy-2" };

            var items = Build(order, "returner");

            Assert.That(items.Select(i => i.UnitId), Is.EqualTo(order));
            Assert.That(items.Select(i => i.OrderIndex), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void Build_FlagsCurrentAndNextLivingUnits()
        {
            var order = new[] { "returner", "enemy-1", "ally-1" };

            var items = Build(order, "returner");

            Assert.That(items[0].IsCurrent, Is.True);
            Assert.That(items[0].IsNext, Is.False);
            Assert.That(items[1].IsCurrent, Is.False);
            Assert.That(items[1].IsNext, Is.True, "first unit after current is next");
            Assert.That(items[2].IsNext, Is.False);
        }

        [Test]
        public void Build_NextSkipsDeadUnitToNextLiving()
        {
            var order = new[] { "returner", "enemy-1", "ally-1" };
            bool Alive(string id) => id != "enemy-1";

            var items = Build(order, "returner", Alive);

            Assert.That(items.Single(i => i.UnitId == "enemy-1").IsDead, Is.True);
            Assert.That(items.Single(i => i.UnitId == "enemy-1").IsNext, Is.False);
            Assert.That(items.Single(i => i.UnitId == "ally-1").IsNext, Is.True);
        }

        [Test]
        public void Build_NextWrapsWhenCurrentIsLast()
        {
            var order = new[] { "returner", "enemy-1", "ally-1" };

            var items = Build(order, "ally-1");

            Assert.That(items.Single(i => i.UnitId == "ally-1").IsCurrent, Is.True);
            Assert.That(items.Single(i => i.UnitId == "returner").IsNext, Is.True,
                "wraps to front of round for handoff readability");
        }

        [Test]
        public void Build_ClassifiesTeams()
        {
            var order = new[] { "returner", "enemy-1", "ally-2" };

            var items = Build(order, "returner");

            Assert.That(items.Single(i => i.UnitId == "returner").Team, Is.EqualTo(CombatTeam.Player));
            Assert.That(items.Single(i => i.UnitId == "ally-2").Team, Is.EqualTo(CombatTeam.Player));
            Assert.That(items.Single(i => i.UnitId == "enemy-1").Team, Is.EqualTo(CombatTeam.Enemy));
        }

        [Test]
        public void Build_IncludeDeadFalse_OmitsDefeatedUnits()
        {
            var order = new[] { "returner", "enemy-1", "ally-1" };
            bool Alive(string id) => id != "enemy-1";

            var items = Build(order, "returner", Alive, includeDead: false);

            Assert.That(items.Select(i => i.UnitId), Is.EqualTo(new[] { "returner", "ally-1" }));
            Assert.That(items.All(i => !i.IsDead), Is.True);
        }

        [Test]
        public void Build_DeadUnitIsNeverCurrentEvenIfMarkedActive()
        {
            var order = new[] { "returner", "enemy-1" };
            bool Alive(string id) => id != "returner";

            var items = Build(order, "returner", Alive);

            var returner = items.Single(i => i.UnitId == "returner");
            Assert.That(returner.IsDead, Is.True);
            Assert.That(returner.IsCurrent, Is.False, "a dead unit cannot be the highlighted current actor");
        }

        [Test]
        public void Build_EmptyOrder_ReturnsEmpty()
        {
            Assert.That(Build(new string[0], "returner"), Is.Empty);
            Assert.That(Build(null, "returner"), Is.Empty);
        }

        [Test]
        public void Build_SkipsNullOrEmptyUnitIds()
        {
            var order = new[] { "returner", null, "", "enemy-1" };

            var items = Build(order, "returner");

            Assert.That(items.Select(i => i.UnitId), Is.EqualTo(new[] { "returner", "enemy-1" }));
        }

        [Test]
        public void Build_NoLivingNextUnit_LeavesNextUnflagged()
        {
            var order = new[] { "returner", "enemy-1" };
            bool Alive(string id) => id == "returner";

            var items = Build(order, "returner", Alive);

            Assert.That(items.All(i => !i.IsNext), Is.True);
            Assert.That(items.Single(i => i.UnitId == "returner").IsCurrent, Is.True);
        }
    }
}
