using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class StatusBoardTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private StatusBoard statusBoard;

        [SetUp]
        public void SetUp()
        {
            statusBoard = new StatusBoard();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void ApplyMark_StackableMarkAddsStacks()
        {
            var mark = CreateMark("burn", durationSeconds: 2f, stackable: true);

            Assert.That(statusBoard.ApplyMark("unit", mark, 1).IsSuccess, Is.True);
            Assert.That(statusBoard.ApplyMark("unit", mark, 1).IsSuccess, Is.True);

            Assert.That(statusBoard.GetMarkStacks("unit", "burn", 1), Is.EqualTo(2));
        }

        [Test]
        public void ApplyMark_NonStackableMarkRefreshesDuration()
        {
            var mark = CreateMark("burn", durationSeconds: 1f, stackable: false);

            Assert.That(statusBoard.ApplyMark("unit", mark, 1).IsSuccess, Is.True);
            Assert.That(statusBoard.ApplyMark("unit", mark, 1).IsSuccess, Is.True);
            Assert.That(statusBoard.GetMarkStacks("unit", "burn", 1), Is.EqualTo(1));

            // Refresh in a later round extends the expiry from that round.
            Assert.That(statusBoard.ApplyMark("unit", mark, 2).IsSuccess, Is.True);
            Assert.That(statusBoard.HasMark("unit", "burn", 2), Is.True);
            Assert.That(statusBoard.HasMark("unit", "burn", 3), Is.False);
        }

        [Test]
        public void Mark_ExpiresAfterDuration()
        {
            var mark = CreateMark("burn", durationSeconds: 1f, stackable: false);

            Assert.That(statusBoard.ApplyMark("unit", mark, 1).IsSuccess, Is.True);

            Assert.That(statusBoard.HasMark("unit", "burn", 1), Is.True);
            Assert.That(statusBoard.HasMark("unit", "burn", 2), Is.False);
        }

        [Test]
        public void PruneExpired_RemovesElapsedStatuses()
        {
            var mark = CreateMark("burn", durationSeconds: 1f, stackable: false);
            Assert.That(statusBoard.ApplyMark("unit", mark, 1).IsSuccess, Is.True);
            Assert.That(statusBoard.ApplyAmplify("unit", 2f, 1).IsSuccess, Is.True);

            statusBoard.PruneExpired(2f);

            Assert.That(statusBoard.HasMark("unit", "burn", 1f), Is.False, "Pruned mark should be gone even for older elapsed-time queries.");
            Assert.That(statusBoard.IsAmplified("unit", 1), Is.False);
        }

        [Test]
        public void ApplyAmplify_ReapplyRefreshesWithoutStacking()
        {
            Assert.That(statusBoard.ApplyAmplify("unit", 2f, 1).IsSuccess, Is.True);
            Assert.That(statusBoard.ApplyAmplify("unit", 3f, 1).IsSuccess, Is.True);

            Assert.That(statusBoard.GetAmplifyMultiplier("unit", 1), Is.EqualTo(3f), "Latest application wins; multipliers never combine.");
        }

        [Test]
        public void Amplify_ExpiresAfterItsDurationSeconds()
        {
            Assert.That(statusBoard.ApplyAmplify("unit", 2f, 1).IsSuccess, Is.True);

            Assert.That(statusBoard.IsAmplified("unit", 1), Is.True);
            Assert.That(statusBoard.IsAmplified("unit", 1f + StatusBoard.AmplifyDurationSeconds), Is.False);
        }

        [Test]
        public void TryConsumeAmplify_ReturnsMultiplierOnlyOnce()
        {
            Assert.That(statusBoard.ApplyAmplify("unit", 2f, 1).IsSuccess, Is.True);

            Assert.That(statusBoard.TryConsumeAmplify("unit", 1, out var multiplier), Is.True);
            Assert.That(multiplier, Is.EqualTo(2f));
            Assert.That(statusBoard.TryConsumeAmplify("unit", 1, out _), Is.False);
        }

        [Test]
        public void ClearUnit_RemovesAllStatuses()
        {
            var mark = CreateMark("burn", durationSeconds: 3f, stackable: false);
            Assert.That(statusBoard.ApplyMark("unit", mark, 1).IsSuccess, Is.True);
            Assert.That(statusBoard.ApplyAmplify("unit", 2f, 1).IsSuccess, Is.True);

            statusBoard.ClearUnit("unit");

            Assert.That(statusBoard.HasMark("unit", "burn", 1), Is.False);
            Assert.That(statusBoard.IsAmplified("unit", 1), Is.False);
        }

        [Test]
        public void ApplyMark_FailsOnInvalidInput()
        {
            var mark = CreateMark("burn", durationSeconds: 2f, stackable: false);

            Assert.That(statusBoard.ApplyMark(null, mark, 1).IsFailure, Is.True);
            Assert.That(statusBoard.ApplyMark("unit", null, 1).IsFailure, Is.True);
            Assert.That(statusBoard.ApplyAmplify("unit", 0f, 1).IsFailure, Is.True);
        }

        private MarkDef CreateMark(string id, float durationSeconds, bool stackable)
        {
            var mark = ScriptableObject.CreateInstance<MarkDef>();
            createdObjects.Add(mark);
            SetPrivateField(mark, "id", id);
            SetPrivateField(mark, "displayName", id);
            SetPrivateField(mark, "durationSeconds", durationSeconds);
            SetPrivateField(mark, "stackable", stackable);
            return mark;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
