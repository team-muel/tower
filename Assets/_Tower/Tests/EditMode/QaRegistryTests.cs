using System;
using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class QaRegistryTests
    {
        private QaRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new QaRegistry();
        }

        [Test]
        public void Press_RegisteredButton_InvokesHandler()
        {
            var pressed = 0;
            Assert.That(registry.RegisterButton("Move Button", () => pressed++).IsSuccess, Is.True);

            var result = registry.Press("Move Button");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(pressed, Is.EqualTo(1));
        }

        [Test]
        public void Press_UnknownButton_FailsWithReason()
        {
            var result = registry.Press("Missing Button");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("Missing Button"));
            Assert.That(result.Error, Does.Contain("not registered"));
        }

        [Test]
        public void Press_ButtonNamesAreCaseSensitive()
        {
            registry.RegisterButton("Move Button", () => { });

            Assert.That(registry.Press("move button").IsFailure, Is.True);
        }

        [Test]
        public void RegisterButton_DuplicateName_Fails()
        {
            Assert.That(registry.RegisterButton("Move Button", () => { }).IsSuccess, Is.True);

            var duplicate = registry.RegisterButton("Move Button", () => { });

            Assert.That(duplicate.IsFailure, Is.True);
            Assert.That(duplicate.Error, Does.Contain("already registered"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void RegisterButton_InvalidName_Fails(string name)
        {
            Assert.That(registry.RegisterButton(name, () => { }).IsFailure, Is.True);
        }

        [Test]
        public void RegisterButton_NullHandler_Fails()
        {
            Assert.That(registry.RegisterButton("Move Button", null).IsFailure, Is.True);
        }

        [Test]
        public void UnregisterButton_RemovesButton()
        {
            registry.RegisterButton("Move Button", () => { });

            Assert.That(registry.UnregisterButton("Move Button").IsSuccess, Is.True);
            Assert.That(registry.Press("Move Button").IsFailure, Is.True);
        }

        [Test]
        public void UnregisterButton_Unknown_Fails()
        {
            Assert.That(registry.UnregisterButton("Missing Button").IsFailure, Is.True);
        }

        [Test]
        public void Press_HandlerThrows_ReturnsFailureInsteadOfThrowing()
        {
            registry.RegisterButton("Broken Button", () => throw new InvalidOperationException("boom"));

            var result = registry.Press("Broken Button");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("boom"));
        }

        [Test]
        public void BuildState_AppliesRegisteredContributors()
        {
            registry.RegisterStateContributor("expedition", snapshot =>
            {
                snapshot.expedition = new QaExpeditionSnapshot { floorIndex = 2, floorCount = 3 };
            });

            var state = registry.BuildState("Expedition");

            Assert.That(state.sceneName, Is.EqualTo("Expedition"));
            Assert.That(state.expedition, Is.Not.Null);
            Assert.That(state.expedition.floorIndex, Is.EqualTo(2));
            Assert.That(state.combat, Is.Null);
        }

        [Test]
        public void BuildState_WithoutContributors_ReturnsSceneOnlySnapshot()
        {
            var state = registry.BuildState("Boot");

            Assert.That(state.sceneName, Is.EqualTo("Boot"));
            Assert.That(state.combat, Is.Null);
            Assert.That(state.expedition, Is.Null);
        }

        [Test]
        public void RegisterStateContributor_DuplicateKey_Fails()
        {
            Assert.That(registry.RegisterStateContributor("expedition", _ => { }).IsSuccess, Is.True);
            Assert.That(registry.RegisterStateContributor("expedition", _ => { }).IsFailure, Is.True);
        }

        [Test]
        public void RegisterStateContributor_NullContributor_Fails()
        {
            Assert.That(registry.RegisterStateContributor("expedition", null).IsFailure, Is.True);
        }

        [Test]
        public void BuildState_AfterUnregister_DoesNotInvokeContributor()
        {
            var invoked = false;
            registry.RegisterStateContributor("expedition", _ => invoked = true);
            Assert.That(registry.UnregisterStateContributor("expedition").IsSuccess, Is.True);

            registry.BuildState("Boot");

            Assert.That(invoked, Is.False);
        }

        [Test]
        public void QaRuntime_Disabled_HelpersAreSafeNoOps()
        {
            QaRuntime.Disable();

            Assert.That(QaRuntime.IsEnabled, Is.False);
            Assert.DoesNotThrow(() => QaRuntime.RegisterButton("Move Button", () => { }));
            Assert.DoesNotThrow(() => QaRuntime.UnregisterButton("Move Button"));
            Assert.DoesNotThrow(() => QaRuntime.RegisterStateContributor("expedition", _ => { }));
            Assert.DoesNotThrow(() => QaRuntime.UnregisterStateContributor("expedition"));
        }

        [Test]
        public void QaRuntime_Enabled_RoutesToRegistry()
        {
            try
            {
                QaRuntime.Enable(registry);
                var pressed = 0;
                QaRuntime.RegisterButton("Move Button", () => pressed++);

                Assert.That(QaRuntime.Registry.Press("Move Button").IsSuccess, Is.True);
                Assert.That(pressed, Is.EqualTo(1));
            }
            finally
            {
                QaRuntime.Disable();
            }
        }
    }
}
