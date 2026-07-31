using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace ND.Framework
{
    public sealed class MonthlyDisasterResolverTests
    {
        private readonly MonthlyDisasterResolver resolver = new MonthlyDisasterResolver();

        [TestCase(GameSeason.Spring)]
        [TestCase(GameSeason.Autumn)]
        public void Resolve_NonDisasterSeason_AlwaysReturnsNone(GameSeason season)
        {
            Assert.That(
                resolver.Resolve(123u, 99L, season, new MonthlyDisasterPolicy(1f, 1f)),
                Is.EqualTo(MonthlyDisasterResolver.NoneId));
        }

        [TestCase(GameSeason.Summer, 0f, MonthlyDisasterResolver.NoneId)]
        [TestCase(GameSeason.Summer, 1f, MonthlyDisasterResolver.FloodId)]
        [TestCase(GameSeason.Winter, 0f, MonthlyDisasterResolver.NoneId)]
        [TestCase(GameSeason.Winter, 1f, MonthlyDisasterResolver.DroughtId)]
        public void Resolve_SeasonalChance_UsesApprovedCandidate(
            GameSeason season,
            float chance,
            string expectedId)
        {
            var policy = new MonthlyDisasterPolicy(chance, chance);

            var result = resolver.Resolve(42u, 7L, season, policy);

            Assert.That(result, Is.EqualTo(expectedId));
            if (season == GameSeason.Summer)
            {
                Assert.That(result, Is.Not.EqualTo(MonthlyDisasterResolver.DroughtId));
            }
            else
            {
                Assert.That(result, Is.Not.EqualTo(MonthlyDisasterResolver.FloodId));
            }
        }

        [Test]
        public void Resolve_RepeatedAndReconstructedCalls_AreStableAndOrderIndependent()
        {
            var policy = new MonthlyDisasterPolicy(0.5f, 0.5f);
            var expected = resolver.Resolve(987654321u, 123L, GameSeason.Summer, policy);

            for (var index = 0; index < 10; index++)
            {
                resolver.Resolve(987654321u, index, GameSeason.Winter, policy);
                Random.InitState(index);
                Assert.That(
                    new MonthlyDisasterResolver().Resolve(
                        987654321u,
                        123L,
                        GameSeason.Summer,
                        new MonthlyDisasterPolicy(0.5f, 0.5f)),
                    Is.EqualTo(expected));
            }
        }

        [Test]
        public void ComputeOccurrenceHash_DifferentMonthsChangeTheKey()
        {
            var method = typeof(MonthlyDisasterResolver).GetMethod(
                "ComputeOccurrenceHash",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var first = (uint)method.Invoke(null, new object[] { 77u, 10L });
            var second = (uint)method.Invoke(null, new object[] { 77u, 11L });

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void Policy_InvalidChances_AreNormalized()
        {
            var policy = new MonthlyDisasterPolicy(float.NaN, -1f);
            var clamped = new MonthlyDisasterPolicy(2f, 2f);

            Assert.That(policy.SummerFloodChance, Is.Zero);
            Assert.That(policy.WinterDroughtChance, Is.Zero);
            Assert.That(clamped.SummerFloodChance, Is.EqualTo(1f));
            Assert.That(clamped.WinterDroughtChance, Is.EqualTo(1f));
        }

        [Test]
        public void DefaultPolicy_UsesDocumentedPlaceholderProbabilities()
        {
            var policy = new MonthlyDisasterPolicy();

            Assert.That(policy.SummerFloodChance, Is.EqualTo(0.25f));
            Assert.That(policy.WinterDroughtChance, Is.EqualTo(0.25f));
        }
    }
}
