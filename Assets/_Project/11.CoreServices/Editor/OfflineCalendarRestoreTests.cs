using System;
using NUnit.Framework;

namespace ND.Framework
{
    public sealed class OfflineCalendarRestoreTests
    {
        private static readonly DateTime EpochUtc =
            new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ResolveContext_BelowCap_UsesLoadUtc()
        {
            var policy = new InGameTimeConversionPolicy();
            var context = policy.ResolveOfflineRestoreContext(
                EpochUtc.Ticks,
                EpochUtc.AddHours(2d),
                InGameTimePolicyConfig.DefaultMaxOfflineRealSeconds);

            Assert.That(context.EvaluationUtc, Is.EqualTo(EpochUtc.AddHours(2d)));
            Assert.That(context.AcceptedElapsed, Is.EqualTo(TimeSpan.FromHours(2d)));
            Assert.That(context.WasClamped, Is.False);
            Assert.That(context.ClockRollbackDetected, Is.False);
        }

        [Test]
        public void ResolveContext_AboveCap_ClampsOnce()
        {
            var policy = new InGameTimeConversionPolicy();
            var context = policy.ResolveOfflineRestoreContext(
                EpochUtc.Ticks,
                EpochUtc.AddHours(100d),
                InGameTimePolicyConfig.DefaultMaxOfflineRealSeconds);

            Assert.That(context.EvaluationUtc, Is.EqualTo(EpochUtc.AddHours(72d)));
            Assert.That(context.AcceptedElapsed, Is.EqualTo(TimeSpan.FromHours(72d)));
            Assert.That(context.WasClamped, Is.True);
        }

        [Test]
        public void ResolveContext_ClockRollback_AcceptsNoElapsed()
        {
            var policy = new InGameTimeConversionPolicy();
            var context = policy.ResolveOfflineRestoreContext(
                EpochUtc.Ticks,
                EpochUtc.AddHours(-1d),
                InGameTimePolicyConfig.DefaultMaxOfflineRealSeconds);

            Assert.That(context.ClockRollbackDetected, Is.True);
            Assert.That(context.AcceptedElapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(context.WasClamped, Is.False);
        }

        [TestCase(119, 0L, 119)]
        [TestCase(120, 1L, 0)]
        [TestCase(3600, 30L, 0)]
        public void RestoreOffline_AdvancesWholeDaysAndPreservesRemainder(
            int elapsedSeconds,
            long expectedDays,
            int expectedRemainderSeconds)
        {
            var fixture = CreateFixture();
            var context = Context(EpochUtc.AddSeconds(elapsedSeconds));

            var result = fixture.Service.RestoreOffline(fixture.Data, context);

            Assert.That(result.DaysAdvanced, Is.EqualTo(expectedDays));
            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.EqualTo(expectedDays));
            Assert.That(
                context.EvaluationUtc.Ticks - fixture.Data.world.calendar.dayAnchorUtcTicks,
                Is.EqualTo(expectedRemainderSeconds * TimeSpan.TicksPerSecond));
        }

        [Test]
        public void RestoreOffline_ExistingRemainderCompletesDay()
        {
            var fixture = CreateFixture();
            fixture.Data.world.calendar.dayAnchorUtcTicks = EpochUtc.AddSeconds(-119d).Ticks;

            var result = fixture.Service.RestoreOffline(
                fixture.Data,
                Context(EpochUtc.AddSeconds(1d)));

            Assert.That(result.DaysAdvanced, Is.EqualTo(1L));
            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.EqualTo(1L));
            Assert.That(
                Context(EpochUtc.AddSeconds(1d)).EvaluationUtc.Ticks
                - fixture.Data.world.calendar.dayAnchorUtcTicks,
                Is.Zero);
        }

        [Test]
        public void RestoreOffline_SeventyTwoHours_ProducesSeventyTwoMonths()
        {
            var fixture = CreateFixture();
            var context = Context(EpochUtc.AddHours(72d));

            var result = fixture.Service.RestoreOffline(fixture.Data, context);

            Assert.That(result.DaysAdvanced, Is.EqualTo(2160L));
            Assert.That(result.PassedMonths.Count, Is.EqualTo(72));
            Assert.That(result.PassedMonths[0].AbsoluteMonthIndex, Is.EqualTo(1L));
            Assert.That(result.PassedMonths[71].AbsoluteMonthIndex, Is.EqualTo(72L));
            Assert.That(result.Current.AbsoluteMonthIndex, Is.EqualTo(72L));
            Assert.That(
                result.PassedMonths[71].DisasterId,
                Is.EqualTo(result.Current.ActiveDisasterId));
        }

        [Test]
        public void RestoreOffline_SameMonth_RepairsCacheWithoutTimelineEntry()
        {
            var fixture = CreateFixture();
            fixture.Data.world.currentSeasonId = "invalid";
            fixture.Data.world.currentDisasterId = "invalid";

            var result = fixture.Service.RestoreOffline(
                fixture.Data,
                Context(EpochUtc.AddSeconds(120d)));

            Assert.That(result.Changed, Is.True);
            Assert.That(result.PassedMonths, Is.Empty);
            Assert.That(fixture.Data.world.currentSeasonId, Is.EqualTo(result.Current.SeasonId));
            Assert.That(
                fixture.Data.world.currentDisasterId,
                Is.EqualTo(result.Current.ActiveDisasterId));
        }

        [Test]
        public void RestoreOffline_RebasesFirstOnlineTickAndIgnoresDebugScale()
        {
            var fixture = CreateFixture();
            fixture.Service.TrySetDebugScale(4f);
            var loadUtc = EpochUtc.AddSeconds(120d);
            var context = new OfflineRestoreContext(
                EpochUtc,
                loadUtc,
                loadUtc,
                TimeSpan.FromSeconds(120d),
                false,
                false);
            fixture.Service.RestoreOffline(fixture.Data, context);

            fixture.Time.Current = loadUtc;
            var firstTick = fixture.Service.TickOnline(
                fixture.Data,
                new FakeSaveService());

            Assert.That(fixture.Service.DebugScale, Is.EqualTo(1f));
            Assert.That(firstTick.Changed, Is.False);
            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.EqualTo(1L));
        }

        private static OfflineRestoreContext Context(DateTime evaluationUtc)
        {
            return new OfflineRestoreContext(
                EpochUtc,
                evaluationUtc,
                evaluationUtc,
                evaluationUtc - EpochUtc,
                false,
                false);
        }

        private static Fixture CreateFixture()
        {
            var time = new FakeTimeProvider { Current = EpochUtc };
            var data = new SaveData();
            data.world.worldSeed = 1234u;
            data.world.calendar = new GameCalendarSaveData
            {
                totalElapsedDays = 0L,
                dayAnchorUtcTicks = EpochUtc.Ticks
            };
            data.world.currentSeasonId = GameCalendarDate.SpringId;
            data.world.currentDisasterId = string.Empty;
            return new Fixture(
                data,
                time,
                new GameCalendarService(
                    time,
                    new MonthlyDisasterResolver(),
                    new MonthlyDisasterPolicy(1f, 1f)));
        }

        private sealed class Fixture
        {
            public Fixture(
                SaveData data,
                FakeTimeProvider time,
                GameCalendarService service)
            {
                Data = data;
                Time = time;
                Service = service;
            }

            public SaveData Data { get; }
            public FakeTimeProvider Time { get; }
            public GameCalendarService Service { get; }
        }

        private sealed class FakeTimeProvider : IGameTimeProvider
        {
            public DateTime Current;
            public DateTime CurrentUtc => Current;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => new SaveData();
            public SaveResult Save(SaveData data) => SaveResult.Success();
            public void ResetSaveData() { }
        }
    }
}
