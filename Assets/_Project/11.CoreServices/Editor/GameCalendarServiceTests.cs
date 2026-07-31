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
            Assert.That(fixture.Data.world.currentDisasterId, Is.EqualTo("unchanged-disaster"));
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
            var fixture = CreateFixture(89L);
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

        private static Fixture CreateFixture(long totalElapsedDays = 0L)
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
            var service = new GameCalendarService(time);
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
    }
}
