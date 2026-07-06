using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // Covers the QA-harness mark enumeration added to StatusBoard.
    public sealed class StatusBoardMarkQueryTests
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
        public void GetActiveMarkIds_ReturnsSortedActiveIds()
        {
            statusBoard.ApplyMark("unit", CreateMark("weaken", durationTurns: 2, stackable: false), 1);
            statusBoard.ApplyMark("unit", CreateMark("burn", durationTurns: 2, stackable: true), 1);

            var ids = statusBoard.GetActiveMarkIds("unit", 1);

            Assert.That(ids, Is.EqualTo(new[] { "burn", "weaken" }));
        }

        [Test]
        public void GetActiveMarkIds_ExcludesExpiredMarks()
        {
            statusBoard.ApplyMark("unit", CreateMark("burn", durationTurns: 1, stackable: false), 1);

            Assert.That(statusBoard.GetActiveMarkIds("unit", 1), Is.EqualTo(new[] { "burn" }));
            Assert.That(statusBoard.GetActiveMarkIds("unit", 2), Is.Empty);
        }

        [Test]
        public void GetActiveMarkIds_UnknownUnit_ReturnsEmpty()
        {
            Assert.That(statusBoard.GetActiveMarkIds("nobody", 1), Is.Empty);
            Assert.That(statusBoard.GetActiveMarkIds(null, 1), Is.Empty);
        }

        private MarkDef CreateMark(string id, int durationTurns, bool stackable)
        {
            var mark = ScriptableObject.CreateInstance<MarkDef>();
            createdObjects.Add(mark);
            SetPrivateField(mark, "id", id);
            SetPrivateField(mark, "displayName", id);
            SetPrivateField(mark, "durationTurns", durationTurns);
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
