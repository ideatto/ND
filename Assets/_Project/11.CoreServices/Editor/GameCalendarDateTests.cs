using System;
using NUnit.Framework;

namespace ND.Framework
{
    public sealed class GameCalendarDateTests
    {
        [TestCase(0L, 1, 3, 1, GameSeason.Spring)]
        [TestCase(29L, 1, 3, 30, GameSeason.Spring)]
        [TestCase(30L, 1, 4, 1, GameSeason.Spring)]
        [TestCase(89L, 1, 5, 30, GameSeason.Spring)]
        [TestCase(90L, 1, 6, 1, GameSeason.Summer)]
        [TestCase(179L, 1, 8, 30, GameSeason.Summer)]
        [TestCase(180L, 1, 9, 1, GameSeason.Autumn)]
        [TestCase(269L, 1, 11, 30, GameSeason.Autumn)]
        [TestCase(270L, 1, 12, 1, GameSeason.Winter)]
        [TestCase(299L, 1, 12, 30, GameSeason.Winter)]
        [TestCase(300L, 2, 1, 1, GameSeason.Winter)]
        [TestCase(359L, 2, 2, 30, GameSeason.Winter)]
        [TestCase(360L, 2, 3, 1, GameSeason.Spring)]
        public void FromElapsedDays_ReturnsExpectedDate(
            long elapsedDays,
            int year,
            int month,
            int day,
            GameSeason season)
        {
            var result = GameCalendarDate.FromElapsedDays(elapsedDays);
            Assert.That(result.Year, Is.EqualTo(year));
            Assert.That(result.Month, Is.EqualTo(month));
            Assert.That(result.Day, Is.EqualTo(day));
            Assert.That(result.Season, Is.EqualTo(season));
            Assert.That(result.AbsoluteMonthIndex, Is.EqualTo(elapsedDays / 30L));
        }

        [Test]
        public void FromElapsedDays_NegativeValue_NormalizesToEpoch()
        {
            var result = GameCalendarDate.FromElapsedDays(-1);
            Assert.That(result.TotalElapsedDays, Is.Zero);
            Assert.That(result.Year, Is.EqualTo(1));
            Assert.That(result.Month, Is.EqualTo(3));
        }

        [Test]
        public void FromElapsedDays_TamperedHugeValue_DoesNotWrap()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameCalendarDate.FromElapsedDays(long.MaxValue));
        }
    }
}
