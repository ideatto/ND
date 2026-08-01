using System;
using System.Reflection;
using ND.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        [TestCase(false, false, 0)]
        [TestCase(true, false, 1)]
        [TestCase(false, true, 1)]
        [TestCase(true, true, 1)]
        public void MergedSave_DirtyMatrix_UsesExpectedSaveCount(
            bool calendarDirty,
            bool tradeDirty,
            int expectedSaveCount)
        {
            var fixture = TransactionFixture.Create();
            if (tradeDirty)
            {
                fixture.MakeTradeTraveling(EpochUtc, EpochUtc.AddMinutes(10d));
            }

            var evaluationUtc = EpochUtc.AddSeconds(calendarDirty ? 120d : tradeDirty ? 60d : 0d);
            var result = fixture.Execute(evaluationUtc);

            Assert.That(result.CalendarDirty, Is.EqualTo(calendarDirty));
            Assert.That(result.TradeDirty, Is.EqualTo(tradeDirty));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.Save.SaveCallCount, Is.EqualTo(expectedSaveCount));
            Assert.That(
                fixture.Coordinator.LastOfflineEvaluationUtc,
                Is.EqualTo(evaluationUtc));
            if (tradeDirty)
            {
                Assert.That(fixture.SelectedCaravan.elapsedInGameSeconds, Is.GreaterThan(0f));
                Assert.That(
                    fixture.Save.LastSerializedSnapshot,
                    Does.Contain("\"elapsedInGameSeconds\":"));
            }
        }

        [Test]
        public void MergedSave_CalendarDirtyOnly_CrossesMonthAndUpdatesCaches()
        {
            var fixture = TransactionFixture.Create(totalElapsedDays: 29L);

            var result = fixture.Execute(EpochUtc.AddSeconds(120d));

            Assert.That(result.CalendarDirty, Is.True);
            Assert.That(result.TradeDirty, Is.False);
            Assert.That(fixture.Save.SaveCallCount, Is.EqualTo(1));
            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.EqualTo(30L));
            Assert.That(result.CalendarResult.PassedMonths, Has.Count.EqualTo(1));
            Assert.That(
                fixture.Data.world.currentSeasonId,
                Is.EqualTo(result.CalendarResult.Current.SeasonId));
            Assert.That(
                fixture.Data.world.currentDisasterId,
                Is.EqualTo(result.CalendarResult.Current.ActiveDisasterId));
        }

        [Test]
        public void MergedSave_BothDirty_SaveFailure_RollsBackCalendarTradeAndNotifications()
        {
            var fixture = TransactionFixture.Create();
            fixture.MakeTradeTraveling(EpochUtc, EpochUtc.AddMinutes(1d));
            fixture.Save.ShouldSucceed = false;
            var beforeJson = JsonUtility.ToJson(fixture.Data);
            var beforeCalendar = fixture.Calendar.Current;
            var beforeElapsed = fixture.SelectedCaravan.elapsedInGameSeconds;
            var readyCount = 0;
            var offlineCount = 0;
            Action<string, string, JourneyResultData> onReady = (_, __, ___) => readyCount++;
            Action<string> onOffline = _ => offlineCount++;
            FrameworkEvents.TradeSettlementReady += onReady;
            FrameworkEvents.TradeOfflineCompleted += onOffline;
            TransactionResult result;
            try
            {
                result = fixture.Execute(EpochUtc.AddSeconds(120d));
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
                FrameworkEvents.TradeOfflineCompleted -= onOffline;
            }

            Assert.That(result.CalendarDirty, Is.True);
            Assert.That(result.TradeDirty, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.CalendarResult, Is.Null);
            Assert.That(fixture.Save.SaveCallCount, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(fixture.Data), Is.EqualTo(beforeJson));
            Assert.That(fixture.Calendar.Current, Is.EqualTo(beforeCalendar));
            Assert.That(
                fixture.Coordinator.GetOrCreateRuntimeCaravan(
                    fixture.Data.selectedCaravanId).elapsedInGameSeconds,
                Is.EqualTo(beforeElapsed));
            Assert.That(fixture.Coordinator.LastSettlementResult, Is.Null);
            Assert.That(fixture.Data.pendingSettlements, Is.Empty);
            Assert.That(readyCount, Is.Zero);
            Assert.That(offlineCount, Is.Zero);
        }

        [Test]
        public void MergedRestore_PublishesCalendarExactlyOnce()
        {
            var fixture = TransactionFixture.Create();
            fixture.MakeTradeTraveling(EpochUtc, EpochUtc.AddMinutes(1d));
            var order = new System.Collections.Generic.List<string>();
            Action<CalendarRestoreResult> calendar = _ => order.Add("Calendar");
            Action<string> trade = _ => order.Add("Trade");
            Action<string, string, JourneyResultData> ready = (_, __, ___) => order.Add("Trade");
            FrameworkEvents.CalendarRestored += calendar;
            FrameworkEvents.TradeOfflineCompleted += trade;
            FrameworkEvents.TradeSettlementReady += ready;
            try
            {
                var result = fixture.Execute(EpochUtc.AddSeconds(120d));
                Assert.That(result.Succeeded, Is.True);
                Assert.That(order[0], Is.EqualTo("Calendar"));
                Assert.That(order.FindAll(value => value == "Calendar"), Has.Count.EqualTo(1));
            }
            finally
            {
                FrameworkEvents.CalendarRestored -= calendar;
                FrameworkEvents.TradeOfflineCompleted -= trade;
                FrameworkEvents.TradeSettlementReady -= ready;
            }
        }

        [Test]
        public void MergedRestore_NoChangePublishesCalendarAndFailurePublishesNothing()
        {
            var success = TransactionFixture.Create();
            var calls = 0;
            Action<CalendarRestoreResult> handler = _ => calls++;
            FrameworkEvents.CalendarRestored += handler;
            try
            {
                var unchanged = success.Execute(EpochUtc);
                Assert.That(unchanged.Succeeded, Is.True);
                Assert.That(calls, Is.EqualTo(1));

                var failure = TransactionFixture.Create();
                failure.Save.ShouldSucceed = false;
                failure.Execute(EpochUtc.AddSeconds(120d));
                Assert.That(calls, Is.EqualTo(1));
            }
            finally { FrameworkEvents.CalendarRestored -= handler; }
        }

        [Test]
        public void MergedSave_BothDirty_SaveException_RollsBack()
        {
            var fixture = TransactionFixture.Create();
            fixture.MakeTradeTraveling(EpochUtc, EpochUtc.AddMinutes(1d));
            fixture.Save.ThrowOnSave = true;
            var beforeJson = JsonUtility.ToJson(fixture.Data);
            LogAssert.Expect(
                LogType.Error,
                "[Framework] Offline restore save threw an exception: test save exception");

            var result = fixture.Execute(EpochUtc.AddSeconds(120d));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(fixture.Save.SaveCallCount, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(fixture.Data), Is.EqualTo(beforeJson));
        }

        [Test]
        public void OfflineRestore_UsesSameEvaluationUtcForCalendarAndTrade()
        {
            var fixture = TransactionFixture.Create();
            fixture.MakeTradeTraveling(EpochUtc, EpochUtc.AddMinutes(10d));
            var evaluationUtc = EpochUtc.AddSeconds(241d);

            var result = fixture.Execute(evaluationUtc);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                fixture.Coordinator.LastOfflineEvaluationUtc,
                Is.EqualTo(evaluationUtc));
            Assert.That(
                fixture.Data.world.calendar.dayAnchorUtcTicks,
                Is.EqualTo(EpochUtc.AddSeconds(240d).Ticks));
        }

        [TestCase(0f)]
        [TestCase(2f)]
        [TestCase(4f)]
        public void OfflineRestore_FirstOnlineTick_DoesNotRecountAcceptedInterval(float debugScale)
        {
            var fixture = TransactionFixture.Create();
            Assert.That(fixture.Calendar.TrySetDebugScale(debugScale), Is.True);
            var loadUtc = EpochUtc.AddSeconds(120d);

            var restore = fixture.Execute(loadUtc);
            var savesAfterRestore = fixture.Save.SaveCallCount;
            fixture.Time.Current = loadUtc;
            var first = fixture.Calendar.TickOnline(fixture.Data, fixture.Save);
            fixture.Time.Current = loadUtc.AddSeconds(119d);
            var belowBoundary = fixture.Calendar.TickOnline(fixture.Data, fixture.Save);
            fixture.Time.Current = loadUtc.AddSeconds(120d);
            var boundary = fixture.Calendar.TickOnline(fixture.Data, fixture.Save);

            Assert.That(restore.Succeeded, Is.True);
            Assert.That(fixture.Calendar.DebugScale, Is.EqualTo(1f));
            Assert.That(first.Changed, Is.False);
            Assert.That(belowBoundary.Changed, Is.False);
            Assert.That(boundary.Changed, Is.True);
            Assert.That(boundary.DaysAdvanced, Is.EqualTo(1L));
            Assert.That(fixture.Data.world.calendar.totalElapsedDays, Is.EqualTo(2L));
            Assert.That(fixture.Save.SaveCallCount, Is.EqualTo(savesAfterRestore + 1));
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

        private sealed class CountingSaveService : ISaveService
        {
            public bool ShouldSucceed = true;
            public bool ThrowOnSave;
            public int SaveCallCount;
            public string LastSerializedSnapshot;

            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => new SaveData();

            public SaveResult Save(SaveData data)
            {
                SaveCallCount++;
                LastSerializedSnapshot = JsonUtility.ToJson(data);
                if (ThrowOnSave)
                {
                    throw new InvalidOperationException("test save exception");
                }

                return ShouldSucceed
                    ? SaveResult.Success()
                    : SaveResult.Failure(SaveFailureReason.WriteFailed, "test failure");
            }

            public void ResetSaveData() { }
        }

        private sealed class TransactionFixture
        {
            private const string RouteId = "BaseToRiver";

            private TransactionFixture(
                SaveData data,
                FakeTimeProvider time,
                GameCalendarService calendar,
                TradeProgressCoordinator coordinator,
                CountingSaveService save)
            {
                Data = data;
                Time = time;
                Calendar = calendar;
                Coordinator = coordinator;
                Save = save;
            }

            public SaveData Data { get; }
            public FakeTimeProvider Time { get; }
            public GameCalendarService Calendar { get; }
            public TradeProgressCoordinator Coordinator { get; }
            public CountingSaveService Save { get; }
            public CaravanSaveData SelectedCaravan =>
                SaveDataLookup.TryGetSelectedCaravan(Data, out var caravan) ? caravan : null;

            public static TransactionFixture Create(long totalElapsedDays = 0L)
            {
                var time = new FakeTimeProvider { Current = EpochUtc };
                var save = new CountingSaveService();
                var data = new SaveData();
                data.world.worldSeed = 1234u;
                data.world.calendar = new GameCalendarSaveData
                {
                    totalElapsedDays = totalElapsedDays,
                    dayAnchorUtcTicks = EpochUtc.Ticks
                };
                var calendar = new GameCalendarService(
                    time,
                    new MonthlyDisasterResolver(),
                    new MonthlyDisasterPolicy(1f, 1f));
                calendar.RestoreOffline(data, Context(EpochUtc));

                var sharedService = new SharedGameDataService();
                Assert.That(sharedService.LoadInitialData(), Is.True, sharedService.LastErrorSummary);
                var sharedData = sharedService.CurrentData;
                var policy = Resources.Load<InGameTimePolicyConfig>(
                    InGameTimePolicyConfig.ResourceName);
                var gameTime = new GameTimeService(
                    policy != null ? policy : ScriptableObject.CreateInstance<InGameTimePolicyConfig>());
                var recorder = new TradeProgressRecorder(gameTime, gameTime);
                var router = new InGameScreenStateRouter();
                var commitStore = new FrameworkTradePrepareCommitStore(() => data);
                var coordinator = new TradeProgressCoordinator(
                    () => data,
                    save,
                    gameTime,
                    recorder,
                    router,
                    gameTime,
                    () => sharedData,
                    commitStore,
                    commitStore);
                return new TransactionFixture(data, time, calendar, coordinator, save);
            }

            public void MakeTradeTraveling(DateTime startUtc, DateTime endUtc)
            {
                var caravan = new CaravanData
                {
                    caravanId = "offline-transaction-caravan",
                    wagon = new imsiWagonData
                    {
                        instanceId = SaveDataLookup.NewInstanceId(),
                        wagonName = "Test Wagon",
                        overLoad = 30f,
                        maxLoad = 60f,
                        minAnimals = 1,
                        maxAnimals = 5,
                        maxDurability = 100,
                        inventorySlotCount = 8
                    },
                    foodAmount = 30,
                    starveGraceSeconds = 5f,
                    currentDurability = 100
                };
                caravan.animals.Add(new imsiAnimalData
                {
                    instanceId = SaveDataLookup.NewInstanceId(),
                    animalName = "Test Horse",
                    foodPerKm = 1f,
                    animalType = DraftAnimalType.Horse,
                    increaseOverLoad = 5f
                });
                CaravanSaveDataMapper.CopyToSave(caravan, Data.caravan);
                Data.selectedCaravanId = caravan.caravanId;
                var progress = new TradeProgressSaveData
                {
                    caravanId = caravan.caravanId,
                    activeTradeId = "offline-transaction-trade",
                    activeRouteId = RouteId,
                    state = TradeProgressState.Traveling,
                    tradeStartUtcTick = startUtc.Ticks,
                    expectedTradeEndUtcTick = endUtc.Ticks,
                    inGameTimeMultiplierAtStart = 1f
                };
                Data.tradeProgressEntries.Add(progress);
                Coordinator.SetActiveCaravan(caravan);
            }

            public TransactionResult Execute(DateTime evaluationUtc)
            {
                var context = new OfflineRestoreContext(
                    EpochUtc,
                    evaluationUtc,
                    evaluationUtc,
                    evaluationUtc - EpochUtc,
                    false,
                    false);
                var method = typeof(FrameworkRoot).GetMethod(
                    "ExecuteOfflineRestore",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                return new TransactionResult(method.Invoke(
                    null,
                    new object[]
                    {
                        Data,
                        context,
                        evaluationUtc,
                        Calendar,
                        Coordinator,
                        Save
                    }));
            }
        }

        private sealed class TransactionResult
        {
            private readonly object value;
            private readonly Type type;

            public TransactionResult(object value)
            {
                this.value = value;
                type = value.GetType();
            }

            public CalendarRestoreResult CalendarResult =>
                (CalendarRestoreResult)Get(nameof(CalendarResult));
            public bool CalendarDirty => (bool)Get(nameof(CalendarDirty));
            public bool TradeDirty => (bool)Get(nameof(TradeDirty));
            public bool Succeeded => (bool)Get(nameof(Succeeded));

            private object Get(string propertyName)
            {
                return type.GetProperty(propertyName)?.GetValue(value);
            }
        }
    }
}
