using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class QaStateSerializerTests
    {
        [Test]
        public void ToJson_SceneOnlySnapshot_WritesNullSections()
        {
            var snapshot = new QaStateSnapshot { sceneName = "Boot" };

            var json = QaStateSerializer.ToJson(snapshot);

            Assert.That(json, Is.EqualTo("{\"sceneName\":\"Boot\",\"combat\":null,\"expedition\":null,\"camp\":null}"));
        }

        [Test]
        public void ToJson_FixedCombatScenario_MatchesExpectedLine()
        {
            var snapshot = new QaStateSnapshot
            {
                sceneName = "Expedition",
                combat = new QaCombatSnapshot
                {
                    round = 2,
                    activeUnitId = "regressor",
                    remainingOrders = 1,
                    initiativeOrder = new List<string> { "regressor", "enemy-1-0" },
                    units = new List<QaUnitSnapshot>
                    {
                        new QaUnitSnapshot
                        {
                            unitId = "regressor",
                            team = "Player",
                            currentHp = 18,
                            maxHp = 24,
                            alive = true,
                            x = 1,
                            y = 2,
                            marks = new List<string> { "burn" }
                        },
                        new QaUnitSnapshot
                        {
                            unitId = "enemy-1-0",
                            team = "Enemy",
                            currentHp = 0,
                            maxHp = 10,
                            alive = false,
                            x = -1,
                            y = -1
                        }
                    }
                },
                expedition = new QaExpeditionSnapshot
                {
                    stairwayIndex = 1,
                    stairwayCount = 1,
                    floorIndex = 2,
                    floorCount = 3,
                    roomIndex = 1,
                    roomCount = 4,
                    retreatCount = 1,
                    isComplete = false,
                    phase = "combat",
                    nextRoomPreview = "강적",
                    lastOutcome = "Retreated"
                }
            };

            var json = QaStateSerializer.ToJson(snapshot);

            const string expected =
                "{\"sceneName\":\"Expedition\","
                + "\"combat\":{\"round\":2,\"activeUnitId\":\"regressor\",\"remainingOrders\":1,"
                + "\"initiativeOrder\":[\"regressor\",\"enemy-1-0\"],"
                + "\"units\":["
                + "{\"unitId\":\"regressor\",\"team\":\"Player\",\"currentHp\":18,\"maxHp\":24,\"alive\":true,\"x\":1,\"y\":2,\"marks\":[\"burn\"]},"
                + "{\"unitId\":\"enemy-1-0\",\"team\":\"Enemy\",\"currentHp\":0,\"maxHp\":10,\"alive\":false,\"x\":-1,\"y\":-1,\"marks\":[]}"
                + "]},"
                + "\"expedition\":{\"stairwayIndex\":1,\"stairwayCount\":1,\"floorIndex\":2,\"floorCount\":3,"
                + "\"roomIndex\":1,\"roomCount\":4,\"retreatCount\":1,\"isComplete\":false,"
                + "\"phase\":\"combat\",\"nextRoomPreview\":\"강적\",\"lastOutcome\":\"Retreated\"},"
                + "\"camp\":null}";
            Assert.That(json, Is.EqualTo(expected));
        }

        [Test]
        public void ToJson_CampSnapshot_WritesPositionAndZone()
        {
            var snapshot = new QaStateSnapshot
            {
                sceneName = "Camp",
                camp = new QaCampSnapshot { x = 1.5f, z = -2.25f, zoneId = "depart-gate" }
            };

            var json = QaStateSerializer.ToJson(snapshot);

            Assert.That(
                json,
                Is.EqualTo("{\"sceneName\":\"Camp\",\"combat\":null,\"expedition\":null,\"camp\":{\"x\":1.5,\"z\":-2.25,\"zoneId\":\"depart-gate\"}}"));
        }

        [Test]
        public void ToJson_IsSingleLine()
        {
            var snapshot = new QaStateSnapshot
            {
                sceneName = "Expedition",
                combat = new QaCombatSnapshot { round = 1 }
            };

            var json = QaStateSerializer.ToJson(snapshot);

            Assert.That(json, Does.Not.Contain("\n"));
            Assert.That(json, Does.Not.Contain("\r"));
        }

        [Test]
        public void ToJson_EscapesSpecialCharacters()
        {
            var snapshot = new QaStateSnapshot { sceneName = "a\"b\\c\nd\te" };

            var json = QaStateSerializer.ToJson(snapshot);

            Assert.That(json, Does.Contain("\"a\\\"b\\\\c\\nd\\te\""));
            Assert.That(json, Does.Not.Contain("\n"));
        }

        [Test]
        public void ToJson_ControlCharacter_UsesUnicodeEscape()
        {
            var snapshot = new QaStateSnapshot { sceneName = "a\u0001b" };

            var json = QaStateSerializer.ToJson(snapshot);

            Assert.That(json, Does.Contain("a\\u0001b"));
        }

        [Test]
        public void ToJson_NullSnapshot_WritesEmptySnapshot()
        {
            var json = QaStateSerializer.ToJson(null);

            Assert.That(json, Is.EqualTo("{\"sceneName\":\"\",\"combat\":null,\"expedition\":null,\"camp\":null}"));
        }
    }
}
