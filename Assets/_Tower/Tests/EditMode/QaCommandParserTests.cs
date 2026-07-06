using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class QaCommandParserTests
    {
        [Test]
        public void Parse_Press_ReturnsPressWithArgument()
        {
            var parsed = QaCommandParser.Parse("press Move Button");

            Assert.That(parsed.IsSuccess, Is.True);
            Assert.That(parsed.Value.Kind, Is.EqualTo(QaCommandKind.Press));
            Assert.That(parsed.Value.Argument, Is.EqualTo("Move Button"));
        }

        [Test]
        public void Parse_Press_KeepsArgumentSpacesAndCasing()
        {
            var parsed = QaCommandParser.Parse("press Order: Focus Nearest Button");

            Assert.That(parsed.IsSuccess, Is.True);
            Assert.That(parsed.Value.Argument, Is.EqualTo("Order: Focus Nearest Button"));
        }

        [Test]
        public void Parse_Press_MissingName_Fails()
        {
            var parsed = QaCommandParser.Parse("press");

            Assert.That(parsed.IsFailure, Is.True);
            Assert.That(parsed.Error, Does.Contain("button name"));
        }

        [Test]
        public void Parse_State_ReturnsStateWithoutArgument()
        {
            var parsed = QaCommandParser.Parse("state");

            Assert.That(parsed.IsSuccess, Is.True);
            Assert.That(parsed.Value.Kind, Is.EqualTo(QaCommandKind.State));
            Assert.That(parsed.Value.Argument, Is.Empty);
        }

        [Test]
        public void Parse_State_WithArgument_Fails()
        {
            var parsed = QaCommandParser.Parse("state now");

            Assert.That(parsed.IsFailure, Is.True);
            Assert.That(parsed.Error, Does.Contain("no argument"));
        }

        [Test]
        public void Parse_Scene_ReturnsSceneName()
        {
            var parsed = QaCommandParser.Parse("scene Expedition");

            Assert.That(parsed.IsSuccess, Is.True);
            Assert.That(parsed.Value.Kind, Is.EqualTo(QaCommandKind.Scene));
            Assert.That(parsed.Value.Argument, Is.EqualTo("Expedition"));
        }

        [Test]
        public void Parse_Scene_MissingName_Fails()
        {
            var parsed = QaCommandParser.Parse("scene");

            Assert.That(parsed.IsFailure, Is.True);
            Assert.That(parsed.Error, Does.Contain("scene name"));
        }

        [Test]
        public void Parse_Quit_ReturnsQuit()
        {
            var parsed = QaCommandParser.Parse("quit");

            Assert.That(parsed.IsSuccess, Is.True);
            Assert.That(parsed.Value.Kind, Is.EqualTo(QaCommandKind.Quit));
        }

        [Test]
        public void Parse_Quit_WithArgument_Fails()
        {
            Assert.That(QaCommandParser.Parse("quit now").IsFailure, Is.True);
        }

        [Test]
        public void Parse_VerbIsCaseInsensitive()
        {
            Assert.That(QaCommandParser.Parse("PRESS Move Button").Value.Kind, Is.EqualTo(QaCommandKind.Press));
            Assert.That(QaCommandParser.Parse("State").Value.Kind, Is.EqualTo(QaCommandKind.State));
            Assert.That(QaCommandParser.Parse("QUIT").Value.Kind, Is.EqualTo(QaCommandKind.Quit));
        }

        [Test]
        public void Parse_TrimsSurroundingWhitespace()
        {
            var parsed = QaCommandParser.Parse("   press  Move Button  ");

            Assert.That(parsed.IsSuccess, Is.True);
            Assert.That(parsed.Value.Kind, Is.EqualTo(QaCommandKind.Press));
            Assert.That(parsed.Value.Argument, Is.EqualTo("Move Button"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Parse_EmptyLine_Fails(string line)
        {
            Assert.That(QaCommandParser.Parse(line).IsFailure, Is.True);
        }

        [Test]
        public void Parse_UnknownVerb_FailsWithVerbInError()
        {
            var parsed = QaCommandParser.Parse("dance hard");

            Assert.That(parsed.IsFailure, Is.True);
            Assert.That(parsed.Error, Does.Contain("dance"));
        }

        [Test]
        public void QaProtocol_Error_CollapsesNewlinesToSingleLine()
        {
            var error = QaProtocol.Error("first\nsecond");

            Assert.That(error, Does.StartWith("ERR "));
            Assert.That(error, Does.Not.Contain("\n"));
        }
    }
}
