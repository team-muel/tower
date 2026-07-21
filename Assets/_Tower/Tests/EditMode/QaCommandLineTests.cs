using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class QaCommandLineTests
    {
        [Test]
        public void TryGetQaPort_ParsesPortValue()
        {
            var found = QaCommandLine.TryGetQaPort(new[] { "Tower.exe", "-qaPort", "7777" }, out var port);

            Assert.That(found, Is.True);
            Assert.That(port, Is.EqualTo(7777));
        }

        [Test]
        public void TryGetQaPort_FlagNameIsCaseInsensitive()
        {
            Assert.That(QaCommandLine.TryGetQaPort(new[] { "-QAPORT", "1234" }, out var port), Is.True);
            Assert.That(port, Is.EqualTo(1234));
        }

        [Test]
        public void TryGetQaPort_MissingFlag_ReturnsFalse()
        {
            Assert.That(QaCommandLine.TryGetQaPort(new[] { "Tower.exe" }, out _), Is.False);
        }

        [Test]
        public void TryGetQaPort_MissingValue_ReturnsFalse()
        {
            Assert.That(QaCommandLine.TryGetQaPort(new[] { "Tower.exe", "-qaPort" }, out _), Is.False);
        }

        [TestCase("abc")]
        [TestCase("0")]
        [TestCase("-5")]
        [TestCase("65536")]
        public void TryGetQaPort_InvalidValue_ReturnsFalse(string value)
        {
            Assert.That(QaCommandLine.TryGetQaPort(new[] { "-qaPort", value }, out var port), Is.False);
            Assert.That(port, Is.EqualTo(0));
        }

        [Test]
        public void TryGetQaPort_NullArgs_ReturnsFalse()
        {
            Assert.That(QaCommandLine.TryGetQaPort(null, out _), Is.False);
        }

        [Test]
        public void HasDevCameraFlag_DetectsFlag()
        {
            Assert.That(QaCommandLine.HasDevCameraFlag(new[] { "Tower.exe", "-devcam" }), Is.True);
            Assert.That(QaCommandLine.HasDevCameraFlag(new[] { "Tower.exe" }), Is.False);
            Assert.That(QaCommandLine.HasDevCameraFlag(null), Is.False);
        }

        [Test]
        public void HasAutoEncounterFlag_RequiresExplicitGateFlag()
        {
            Assert.That(QaCommandLine.HasAutoEncounterFlag(new[] { "Tower.exe", "-qaAutoEncounter" }), Is.True);
            Assert.That(QaCommandLine.HasAutoEncounterFlag(new[] { "Tower.exe" }), Is.False);
        }
    }
}
