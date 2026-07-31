using System;
using NUnit.Framework;
using UnityEngine;

namespace ND.Framework
{
    public sealed class GameCalendarServiceTests
    {
        private static readonly DateTime EpochUtc =
            new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc);

        [TestCase(0, 0L, 0)]
        [TestCase(119, 0L, 119)]
        [TestCase(120, 1L, 0)]
        [TestCase(239, 1L, 119)]
        [TestCase(240, 2L, 0)]
        [TestCase(3600, 30L, 0)]
        public void TickOnline_AdvancesWholeDaysAndPreservesRemainder(
            int elapsedSeconds,
            long expectedDays,
            int expectedRemainderSeconds)
        {
            var fixture = CreateFixture();
            fixture.Time.Current = EpochUtc.AddSeconds(elapsedSeconds);

            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(result.DaysAdvanced, Is.EqualTo(expectedDays));
            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.EqualTo(expectedDays));
            Assert.That(
                fixture.Time.Current.Ticks - fixture.Data.world.calendar.dayAnchorUtcTicks,
                Is.EqualTo(expectedRemainderSeconds * TimeSpan.TicksPerSecond));
            Assert.That(fixture.Save.SaveCalls, Is.EqualTo(expectedDays > 0L ? 1 : 0));
        }

        [TestCase(29L, 1, 4, 1, false)]
        [TestCase(89L, 1, 6, 1, true)]
        [TestCase(299L, 2, 1, 1, false)]
        [TestCase(359L, 2, 3, 1, true)]
        public void TickOnline_CrossesDateAndSeasonBoundaries(
            long initialDays,
            int expectedYear,
            int expectedMonth,
            int expectedDay,
            bool expectedSeasonChange)
        {
            var fixture = CreateFixture(initialDays);
            fixture.Time.Current = EpochUtc.AddSeconds(120);

            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(result.Current.Year, Is.EqualTo(expectedYear));
            Assert.That(result.Current.Month, Is.EqualTo(expectedMonth));
            Assert.That(result.Current.Day, Is.EqualTo(expectedDay));
            Assert.That(result.SeasonChanged, Is.EqualTo(expectedSeasonChange));
            Assert.That(fixture.Data.world.currentSeasonId, Is.EqualTo(result.Current.SeasonId));
            Assert.That(
                fixture.Data.world.currentDisasterId,
                Is.EqualTo(new MonthlyDisasterResolver().Resolve(
                    fixture.Data.world.worldSeed,
                    result.Current.AbsoluteMonthIndex,
                    result.Current.Season,
                    new MonthlyDisasterPolicy())));
        }

        [Test]
        public void TickOnline_SaveReloadBoundary_UsesPersistedRemainder()
        {
            var first = CreateFixture();
            first.Time.Current = EpochUtc.AddSeconds(119);
            first.Service.TickOnline(first.Data, first.Save);

            var json = JsonUtility.ToJson(first.Data);
            var reloaded = JsonUtility.FromJson<SaveData>(json);
            var secondTime = new FakeTimeProvider { Current = EpochUtc.AddSeconds(120) };
            var secondService = new GameCalendarService(secondTime);
            secondService.BeginOnlineSession(reloaded, secondTime.Current);

            var result = secondService.TickOnline(reloaded, new FakeSaveService());

            Assert.That(result.DaysAdvanced, Is.EqualTo(1L));
            Assert.That(reloaded.world.calendar.dayAnchorUtcTicks, Is.EqualTo(secondTime.Current.Ticks));
        }

        [Test]
        public void BeginOnlineSession_ReloadedSameMonth_ReconstructsSameDisasterWithoutSaving()
        {
            var policy = new MonthlyDisasterPolicy(1f, 1f);
            var first = CreateFixture(89L, new MonthlyDisasterResolver(), policy);
            first.Time.Current = EpochUtc.AddSeconds(120);
            first.Service.TickOnline(first.Data, first.Save);
            var expectedDisasterId = first.Data.world.currentDisasterId;
            var reloaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(first.Data));
            var secondTime = new FakeTimeProvider { Current = first.Time.Current };
            var secondSave = new FakeSaveService();
            var secondService = new GameCalendarService(
                secondTime,
                new MonthlyDisasterResolver(),
                policy);

            Assert.That(secondService.BeginOnlineSession(reloaded, secondTime.Current), Is.True);

            Assert.That(secondService.Current.ActiveDisasterId, Is.EqualTo(expectedDisasterId));
            Assert.That(reloaded.world.currentDisasterId, Is.EqualTo(expectedDisasterId));
            Assert.That(secondSave.SaveCalls, Is.Zero);
        }

        [Test]
        public void TickOnline_ClockRollback_DoesNotMutate()
        {
            var fixture = CreateFixture();
            var originalAnchor = fixture.Data.world.calendar.dayAnchorUtcTicks;
            fixture.Time.Current = EpochUtc.AddSeconds(-1);

            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(result.Changed, Is.False);
            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.Zero);
            Assert.That(fixture.Data.world.calendar.dayAnchorUtcTicks, Is.EqualTo(originalAnchor));
            Assert.That(fixture.Save.SaveCalls, Is.Zero);
        }

        [Test]
        public void TickOnline_MultipleDays_ProducesOneResultAndOneSave()
        {
            var fixture = CreateFixture();
            fixture.Time.Current = EpochUtc.AddSeconds(120 * 30);

            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(result.Changed, Is.True);
            Assert.That(result.DaysAdvanced, Is.EqualTo(30L));
            Assert.That(result.MonthChanged, Is.True);
            Assert.That(fixture.Save.SaveCalls, Is.EqualTo(1));
        }

        [Test]
        public void TickOnline_MonthBoundary_ResolvesOnceAndDayOnlyPreservesDisaster()
        {
            var resolver = new CountingResolver();
            var fixture = CreateFixture(
                89L,
                resolver,
                new MonthlyDisasterPolicy(1f, 1f));
            resolver.ResolveCalls = 0;
            fixture.Time.Current = EpochUtc.AddSeconds(120);

            var monthResult = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(monthResult.Current.Month, Is.EqualTo(6));
            Assert.That(monthResult.Current.ActiveDisasterId, Is.EqualTo(MonthlyDisasterResolver.FloodId));
            Assert.That(resolver.ResolveCalls, Is.EqualTo(1));

            fixture.Time.Current = EpochUtc.AddSeconds(240);
            var dayResult = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(dayResult.Current.Day, Is.EqualTo(2));
            Assert.That(dayResult.Current.ActiveDisasterId, Is.EqualTo(MonthlyDisasterResolver.FloodId));
            Assert.That(resolver.ResolveCalls, Is.EqualTo(1));
        }

        [TestCase(179L, GameSeason.Autumn, MonthlyDisasterResolver.NoneId)]
        [TestCase(269L, GameSeason.Winter, MonthlyDisasterResolver.DroughtId)]
        [TestCase(359L, GameSeason.Spring, MonthlyDisasterResolver.NoneId)]
        public void TickOnline_SeasonBoundary_UsesApprovedDisasterMatrix(
            long initialDays,
            GameSeason expectedSeason,
            string expectedDisasterId)
        {
            var fixture = CreateFixture(
                initialDays,
                new MonthlyDisasterResolver(),
                new MonthlyDisasterPolicy(1f, 1f));
            fixture.Time.Current = EpochUtc.AddSeconds(120);

            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(result.Current.Season, Is.EqualTo(expectedSeason));
            Assert.That(result.Current.ActiveDisasterId, Is.EqualTo(expectedDisasterId));
            Assert.That(fixture.Data.world.currentDisasterId, Is.EqualTo(expectedDisasterId));
        }

        [Test]
        public void TickOnline_MultiMonthAdvance_MatchesDirectFinalMonthResolution()
        {
            var policy = new MonthlyDisasterPolicy(0.5f, 0.5f);
            var resolver = new MonthlyDisasterResolver();
            var fixture = CreateFixture(0L, resolver, policy);
            fixture.Time.Current = EpochUtc.AddSeconds(120 * 300);

            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(result.Current.TotalElapsedDays, Is.EqualTo(300L));
            Assert.That(
                result.Current.ActiveDisasterId,
                Is.EqualTo(resolver.Resolve(
                    fixture.Data.world.worldSeed,
                    result.Current.AbsoluteMonthIndex,
                    result.Current.Season,
                    policy)));
            Assert.That(fixture.Save.SaveCalls, Is.EqualTo(1));
        }

        [TestCase(1f, 120, 1L)]
        [TestCase(2f, 60, 1L)]
        [TestCase(4f, 30, 1L)]
        [TestCase(0f, 600, 0L)]
        public void DebugScale_ControlsOnlyCalendarRate(float scale, int seconds, long expectedDays)
        {
            var fixture = CreateFixture();
            Assert.That(fixture.Service.TrySetDebugScale(scale), Is.True);
            var unityTimeScale = Time.timeScale;
            fixture.Time.Current = EpochUtc.AddSeconds(seconds);

            fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.EqualTo(expectedDays));
            Assert.That(Time.timeScale, Is.EqualTo(unityTimeScale));
        }

        [Test]
        public void DebugScale_ZeroToOne_DoesNotCatchUpSuppressedOnlineTime()
        {
            var fixture = CreateFixture();
            fixture.Service.TrySetDebugScale(0f);
            fixture.Time.Current = EpochUtc.AddSeconds(600);
            fixture.Service.TickOnline(fixture.Data, fixture.Save);
            fixture.Service.TrySetDebugScale(1f);
            fixture.Time.Current = EpochUtc.AddSeconds(719);
            fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.Zero);

            fixture.Time.Current = EpochUtc.AddSeconds(720);
            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);
            Assert.That(result.DaysAdvanced, Is.EqualTo(1L));
        }

        [Test]
        public void DebugScale_FourToZeroToOne_DoesNotDuplicateCompletedDays()
        {
            var fixture = CreateFixture();
            fixture.Service.TrySetDebugScale(4f);
            fixture.Time.Current = EpochUtc.AddSeconds(30);
            fixture.Service.TickOnline(fixture.Data, fixture.Save);
            fixture.Service.TrySetDebugScale(0f);
            fixture.Time.Current = EpochUtc.AddSeconds(330);
            fixture.Service.TickOnline(fixture.Data, fixture.Save);
            fixture.Service.TrySetDebugScale(1f);
            fixture.Time.Current = EpochUtc.AddSeconds(450);

            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(result.DaysAdvanced, Is.EqualTo(1L));
            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.EqualTo(2L));
        }

        [Test]
        public void TickOnline_SaveFailure_RollsBackAllCalendarState()
        {
            var fixture = CreateFixture(
                89L,
                new MonthlyDisasterResolver(),
                new MonthlyDisasterPolicy(1f, 1f));
            fixture.Save.ShouldSucceed = false;
            var before = JsonUtility.ToJson(fixture.Data);
            var previous = fixture.Service.Current;
            fixture.Time.Current = EpochUtc.AddSeconds(120);

            var result = fixture.Service.TickOnline(fixture.Data, fixture.Save);

            Assert.That(result.Changed, Is.False);
            Assert.That(JsonUtility.ToJson(fixture.Data), Is.EqualTo(before));
            Assert.That(fixture.Service.Current.TotalElapsedDays, Is.EqualTo(previous.TotalElapsedDays));
            Assert.That(fixture.Save.SaveCalls, Is.EqualTo(1));
        }

        [Test]
        public void TrySetDebugScale_RejectsUnsupportedValuesAndIsNotPersisted()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.Service.TrySetDebugScale(3f), Is.False);
            Assert.That(fixture.Service.DebugScale, Is.EqualTo(1f));
            Assert.That(JsonUtility.ToJson(fixture.Data), Does.Not.Contain("debugScale"));
        }

        [Test]
        public void LiveTransition_PublishesCommittedStateInApprovedOrder()
        {
            var fixture = CreateFixture(89L, new MonthlyDisasterResolver(), new MonthlyDisasterPolicy(1f, 1f));
            var order = new System.Collections.Generic.List<string>();
            Action<GameCalendarSnapshot, GameCalendarSnapshot> month = (previous, current) =>
            {
                order.Add("Month");
                Assert.That(fixture.Service.Current.TotalElapsedDays, Is.EqualTo(current.TotalElapsedDays));
                Assert.That(fixture.Data.world.currentSeasonId, Is.EqualTo(current.SeasonId));
                Assert.That(fixture.Data.world.currentDisasterId, Is.EqualTo(current.ActiveDisasterId));
            };
            Action<GameCalendarSnapshot, GameCalendarSnapshot> season = (previous, current) => order.Add("Season");
            Action<GameCalendarSnapshot, GameCalendarSnapshot> disaster = (previous, current) => order.Add("Disaster");
            FrameworkEvents.MonthChanged += month;
            FrameworkEvents.SeasonChanged += season;
            FrameworkEvents.DisasterChanged += disaster;
            try
            {
                fixture.Time.Current = EpochUtc.AddSeconds(120);
                fixture.Service.TickOnline(fixture.Data, fixture.Save);
                Assert.That(order[0], Is.EqualTo("Month"));
                Assert.That(order[1], Is.EqualTo("Season"));
                Assert.That(order.Count, Is.LessThanOrEqualTo(3));
            }
            finally
            {
                FrameworkEvents.MonthChanged -= month;
                FrameworkEvents.SeasonChanged -= season;
                FrameworkEvents.DisasterChanged -= disaster;
            }
        }

        [Test]
        public void LiveTransition_DayOnlyAndFailedSavePublishNothing()
        {
            var fixture = CreateFixture(1L);
            var calls = 0;
            Action<GameCalendarSnapshot, GameCalendarSnapshot> handler = (previous, current) => calls++;
            FrameworkEvents.MonthChanged += handler;
            FrameworkEvents.SeasonChanged += handler;
            FrameworkEvents.DisasterChanged += handler;
            FrameworkEvents.YearChanged += handler;
            try
            {
                fixture.Time.Current = EpochUtc.AddSeconds(120);
                fixture.Service.TickOnline(fixture.Data, fixture.Save);
                fixture.Save.ShouldSucceed = false;
                fixture.Time.Current = EpochUtc.AddSeconds(120 * 30);
                fixture.Service.TickOnline(fixture.Data, fixture.Save);
                Assert.That(calls, Is.Zero);
            }
            finally
            {
                FrameworkEvents.MonthChanged -= handler;
                FrameworkEvents.SeasonChanged -= handler;
                FrameworkEvents.DisasterChanged -= handler;
                FrameworkEvents.YearChanged -= handler;
            }
        }

        [Test]
        public void DebugAdvanceDays_PreservesDayAndPublishesOneFinalTransition()
        {
            var fixture = CreateFixture(14L);
            var monthCalls = 0;
            Action<GameCalendarSnapshot, GameCalendarSnapshot> handler = (previous, current) => monthCalls++;
            FrameworkEvents.MonthChanged += handler;
            try
            {
                var result = fixture.Service.AdvanceDebugDays(fixture.Data, 30L, fixture.Save);
                Assert.That(result.Current.Month, Is.EqualTo(4));
                Assert.That(result.Current.Day, Is.EqualTo(15));
                Assert.That(fixture.Save.SaveCalls, Is.EqualTo(1));
                Assert.That(monthCalls, Is.EqualTo(1));
            }
            finally { FrameworkEvents.MonthChanged -= handler; }
        }

        private static Fixture CreateFixture(
            long totalElapsedDays = 0L,
            MonthlyDisasterResolver resolver = null,
            MonthlyDisasterPolicy policy = null)
        {
            var time = new FakeTimeProvider { Current = EpochUtc };
            var data = new SaveData();
            data.world.calendar = new GameCalendarSaveData
            {
                totalElapsedDays = totalElapsedDays,
                dayAnchorUtcTicks = EpochUtc.Ticks
            };
            var date = GameCalendarDate.FromElapsedDays(totalElapsedDays);
            data.world.currentSeasonId = date.SeasonId;
            data.world.currentDisasterId = "unchanged-disaster";
            var save = new FakeSaveService();
            var service = resolver == null
                ? new GameCalendarService(time)
                : new GameCalendarService(time, resolver, policy ?? new MonthlyDisasterPolicy());
            Assert.That(service.BeginOnlineSession(data, time.Current), Is.True);
            return new Fixture(data, time, save, service);
        }

        private sealed class Fixture
        {
            public Fixture(
                SaveData data,
                FakeTimeProvider time,
                FakeSaveService save,
                GameCalendarService service)
            {
                Data = data;
                Time = time;
                Save = save;
                Service = service;
            }

            public SaveData Data { get; }
            public FakeTimeProvider Time { get; }
            public FakeSaveService Save { get; }
            public GameCalendarService Service { get; }
        }

        private sealed class FakeTimeProvider : IGameTimeProvider
        {
            public DateTime Current;
            public DateTime CurrentUtc => Current;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public bool ShouldSucceed = true;
            public int SaveCalls;

            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => new SaveData();
            public SaveResult Save(SaveData data)
            {
                SaveCalls++;
                return ShouldSucceed
                    ? SaveResult.Success()
                    : SaveResult.Failure(SaveFailureReason.WriteFailed, "test failure");
            }
            public void ResetSaveData() { }
        }

        private sealed class CountingResolver : MonthlyDisasterResolver
        {
            public int ResolveCalls;

            public override string Resolve(
                uint worldSeed,
                long absoluteMonthIndex,
                GameSeason season,
                MonthlyDisasterPolicy policy)
            {
                ResolveCalls++;
                return base.Resolve(worldSeed, absoluteMonthIndex, season, policy);
            }
        }
    }
}
