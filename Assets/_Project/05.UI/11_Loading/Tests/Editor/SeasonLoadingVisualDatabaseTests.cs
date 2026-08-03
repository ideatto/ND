using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ND.UI.Loading.Tests
{
    public sealed class SeasonLoadingVisualDatabaseTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        [TestCase("spring")]
        [TestCase("summer")]
        [TestCase("autumn")]
        [TestCase("winter")]
        public void Resolve_KnownSeason_ReturnsConfiguredSprite(string seasonId)
        {
            var expected = CreateSprite();
            var database = CreateDatabase(null, Entry(seasonId, expected));

            Assert.That(database.Resolve(seasonId), Is.SameAs(expected));
        }

        [Test]
        public void Resolve_MissingInvalidOrNullSprite_ReturnsDefault()
        {
            var fallback = CreateSprite();
            var database = CreateDatabase(fallback, Entry("spring", null));

            Assert.That(database.Resolve(null), Is.SameAs(fallback));
            Assert.That(database.Resolve(string.Empty), Is.SameAs(fallback));
            Assert.That(database.Resolve("unknown"), Is.SameAs(fallback));
            Assert.That(database.Resolve("spring"), Is.SameAs(fallback));
        }

        [Test]
        public void Resolve_DuplicateId_ReturnsFirstValidSprite()
        {
            var first = CreateSprite();
            var second = CreateSprite();
            var database = CreateDatabase(null, Entry("spring", first), Entry("spring", second));

            Assert.That(database.Resolve("spring"), Is.SameAs(first));
        }

        private SeasonLoadingVisualDatabase CreateDatabase(
            Sprite fallback,
            params SeasonLoadingVisualEntry[] entries)
        {
            var database = ScriptableObject.CreateInstance<SeasonLoadingVisualDatabase>();
            createdObjects.Add(database);
            SetField(database, "defaultBackground", fallback);
            SetField(database, "entries", new List<SeasonLoadingVisualEntry>(entries));
            return database;
        }

        private static SeasonLoadingVisualEntry Entry(string seasonId, Sprite sprite)
        {
            var entry = new SeasonLoadingVisualEntry();
            SetField(entry, "seasonId", seasonId);
            SetField(entry, "backgroundSprite", sprite);
            return entry;
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
            createdObjects.Add(sprite);
            createdObjects.Add(texture);
            return sprite;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }
    }
}
