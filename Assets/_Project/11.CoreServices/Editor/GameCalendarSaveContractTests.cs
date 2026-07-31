using NUnit.Framework;

namespace ND.Framework
{
    public sealed class GameCalendarSaveContractTests
    {
        [Test]
        public void CreateNewGameData_StartsAtMarchSpringWithoutDisaster()
        {
            var service = new JsonSaveService();
            var data = service.CreateNewGameData();
            var date = GameCalendarDate.FromElapsedDays(data.world.calendar.totalElapsedDays);

            Assert.That(data.world.worldSeed, Is.Not.Zero);
            Assert.That(data.world.calendar.dayAnchorUtcTicks, Is.GreaterThan(0));
            Assert.That(date.Year, Is.EqualTo(1));
            Assert.That(date.Month, Is.EqualTo(3));
            Assert.That(date.Day, Is.EqualTo(1));
            Assert.That(data.world.currentSeasonId, Is.EqualTo(GameCalendarDate.SpringId));
            Assert.That(data.world.currentDisasterId, Is.Empty);
        }

        [Test]
        public void NormalizeData_MissingCalendarAndSeed_CreatesThemOnce()
        {
            var data = new SaveData();
            data.world.calendar = null;
            data.world.worldSeed = 0;

            Assert.That(JsonSaveService.NormalizeData(data), Is.True);
            var seed = data.world.worldSeed;
            var anchor = data.world.calendar.dayAnchorUtcTicks;
            Assert.That(JsonSaveService.NormalizeData(data), Is.False);
            Assert.That(data.world.worldSeed, Is.EqualTo(seed));
            Assert.That(data.world.calendar.dayAnchorUtcTicks, Is.EqualTo(anchor));
        }

        [Test]
        public void NormalizeData_RepairsDaysAndCachesWithoutReplacingValidAnchor()
        {
            const long anchor = 123456789L;
            var data = new SaveData();
            data.world.worldSeed = 42;
            data.world.calendar = new GameCalendarSaveData
            {
                totalElapsedDays = -30,
                dayAnchorUtcTicks = anchor
            };
            data.world.currentSeasonId = GameCalendarDate.SummerId;
            data.world.currentDisasterId = null;

            Assert.That(JsonSaveService.NormalizeData(data), Is.True);
            Assert.That(data.world.calendar.totalElapsedDays, Is.Zero);
            Assert.That(data.world.calendar.dayAnchorUtcTicks, Is.EqualTo(anchor));
            Assert.That(data.world.currentSeasonId, Is.EqualTo(GameCalendarDate.SpringId));
            Assert.That(data.world.currentDisasterId, Is.Empty);
        }
    }
}
