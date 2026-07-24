/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Multi-active Route Event Processor 및 Forced Route Event API를
 *   production SharedRoute asset을 영구 수정하지 않고 Editor runtime fixture로 검증한다.
 *
 * Usage for Team Members
 * - Unity Editor: ND/Framework/Run Route Event Runtime Verification
 *
 * Important Notes
 * - Editor 전용이며 Player build에 포함되지 않는다.
 * - BaseToRiver SharedRouteDefinition의 Events/Risk/MaxEventCount만 검증 동안 임시 주입한다.
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using ND.Economy;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    public static class TradeRouteEventRuntimeVerification
    {
        private const string RouteId = "BaseToRiver";
        private const string ItemId = "Apple";
        private const float DistanceKm = 20f;

        [MenuItem("ND/Framework/Run Route Event Runtime Verification")]
        public static void RunAll()
        {
            var report = RunAllInternal();
            if (report.Failed)
                throw new InvalidOperationException(report.ToString());
            Debug.Log(report.ToString());
        }

        public static string RunAllAndReturnReport()
        {
            return RunAllInternal().ToString();
        }

        private static Report RunAllInternal()
        {
            var report = new Report();
            SharedRouteDefinition route = null;
            SharedRouteEventDefinition[] originalEvents = null;
            float originalRisk = 0f;
            int originalMax = 0;

            try
            {
                var sharedService = new SharedGameDataService();
                if (!sharedService.LoadInitialData())
                    throw new InvalidOperationException("Shared load failed: " + sharedService.LastErrorSummary);

                var sharedData = sharedService.CurrentData;
                if (!sharedData.TryGetRoute(RouteId, out route) || route == null)
                    throw new InvalidOperationException("BaseToRiver route missing.");

                originalEvents = route.Events;
                originalRisk = route.BaseRiskLevel;
                originalMax = route.MaxEventCount;
                InjectRouteFixture(route);

                RunOnlineMultiActive(report, sharedData);
                RunForcedAndRollback(report, sharedData);
                RunOfflineParity(report, sharedData);
                RunCursorRestore(report, sharedData);
                RunSettlementOrder(report, sharedData);
                RunSharedCopySanity(report);
            }
            catch (Exception exception)
            {
                report.Fail("harness-exception", exception.Message);
            }
            finally
            {
                if (route != null)
                {
                    route.Events = originalEvents;
                    route.BaseRiskLevel = originalRisk;
                    route.MaxEventCount = originalMax;
                }
            }

            return report;
        }

        private static void InjectRouteFixture(SharedRouteDefinition route)
        {
            route.BaseRiskLevel = 1f;
            route.MaxEventCount = 10;
            route.Events = new[]
            {
                new SharedRouteEventDefinition
                {
                    Id = "weather-auto",
                    EventType = RouteEvent.Weather
                },
                new SharedRouteEventDefinition
                {
                    Id = "combat-auto",
                    EventType = RouteEvent.Combat,
                    BanditCombatPower = 1,
                    CargoLootRate = 0.1f,
                    FodderLootRate = 0.1f
                }
            };
        }

        private static void RunOnlineMultiActive(Report report, ISharedGameDataProvider sharedData)
        {
            var save = new ProbeSaveService();
            var context = TestContext.Create(save, sharedData);
            var firstId = context.SaveData.selectedCaravanId;
            InitializeSelectedCaravan(context);
            var second = AddCaravan(context, "route-online-b");
            var preparing = AddCaravan(context, "route-online-c");
            var command = CreateDepartureCommand(context, save);

            var d1 = command.Depart(new TradeDepartureRequest { CaravanId = firstId, RouteId = RouteId });
            var d2 = command.Depart(new TradeDepartureRequest { CaravanId = second.caravanId, RouteId = RouteId });
            report.Check("online-depart", d1.DepartureSucceeded && d2.DepartureSucceeded);

            SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out var progressA);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, second.caravanId, out var progressB);
            var now = context.GameTime.CurrentUtc;
            // ~25% => 5km / interval 2km => 2 checks
            progressA.tradeStartUtcTick = now.AddMinutes(-2.5).Ticks;
            progressA.expectedTradeEndUtcTick = now.AddMinutes(7.5).Ticks;
            progressB.tradeStartUtcTick = now.AddMinutes(-3).Ticks;
            progressB.expectedTradeEndUtcTick = now.AddMinutes(7).Ticks;
            context.SaveData.selectedCaravanId = firstId;
            context.SaveData.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = preparing.caravanId,
                state = TradeProgressState.Preparing
            });
            context.SaveData.tradeProgressEntries.Insert(0, new TradeProgressSaveData
            {
                caravanId = "missing-route-caravan",
                activeTradeId = "missing-route-trade",
                activeRouteId = RouteId,
                state = TradeProgressState.Traveling,
                tradeStartUtcTick = now.AddMinutes(-1).Ticks,
                expectedTradeEndUtcTick = now.AddMinutes(1).Ticks
            });

            context.Coordinator.TryGetRuntimeCaravan(firstId, out var runtimeA);
            context.Coordinator.TryGetRuntimeCaravan(second.caravanId, out var runtimeB);
            context.Coordinator.TryGetRuntimeCaravan(preparing.caravanId, out var runtimeC);
            var selectedBefore = context.SaveData.selectedCaravanId;
            var savesBefore = save.SaveCalls;

            context.Coordinator.CheckProgressAndCompletion();

            SaveDataLookup.TryGetCaravan(context.SaveData, firstId, out var saveA);
            SaveDataLookup.TryGetCaravan(context.SaveData, second.caravanId, out var saveB);
            SaveDataLookup.TryGetCaravan(context.SaveData, preparing.caravanId, out var saveC);

            report.Info(
                "online A cursor=" + runtimeA.runEventChecksProcessed
                + " events=" + runtimeA.runEventsOccurred
                + " | B cursor=" + runtimeB.runEventChecksProcessed
                + " events=" + runtimeB.runEventsOccurred
                + " | saves=" + (save.SaveCalls - savesBefore));

            report.Check("online-A-processed", runtimeA.runEventChecksProcessed >= 2
                && saveA.runEventChecksProcessed == runtimeA.runEventChecksProcessed);
            report.Check("online-B-nonselected-processed", runtimeB.runEventChecksProcessed >= 2
                && saveB.runEventChecksProcessed == runtimeB.runEventChecksProcessed);
            report.Check("online-C-preparing-untouched", saveC.runEventChecksProcessed == 0
                && (runtimeC == null || runtimeC.runEventChecksProcessed == 0));
            report.Check("online-events-occurred", runtimeA.runEventsOccurred > 0 && runtimeB.runEventsOccurred > 0);
            report.Check("online-selected-unchanged", context.SaveData.selectedCaravanId == selectedBefore);
            report.Check("online-save-batch-once", save.SaveCalls == savesBefore + 1);
            report.Check("online-A-B-isolated",
                runtimeA.runEventChecksProcessed == saveA.runEventChecksProcessed
                && runtimeB.runEventChecksProcessed == saveB.runEventChecksProcessed);

            var cursorA = runtimeA.runEventChecksProcessed;
            var eventsA = runtimeA.runEventsOccurred;
            context.Coordinator.CheckProgressAndCompletion();
            report.Check("online-no-reprocess",
                runtimeA.runEventChecksProcessed == cursorA && runtimeA.runEventsOccurred == eventsA);
        }

        private static void RunForcedAndRollback(Report report, ISharedGameDataProvider sharedData)
        {
            var save = new ProbeSaveService();
            var context = TestContext.Create(save, sharedData);
            var firstId = context.SaveData.selectedCaravanId;
            InitializeSelectedCaravan(context);
            var second = AddCaravan(context, "route-forced-b");
            var preparing = AddCaravan(context, "route-forced-c");
            var command = CreateDepartureCommand(context, save);
            command.Depart(new TradeDepartureRequest { CaravanId = firstId, RouteId = RouteId });
            command.Depart(new TradeDepartureRequest { CaravanId = second.caravanId, RouteId = RouteId });

            SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out var progressA);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, second.caravanId, out var progressB);
            var now = context.GameTime.CurrentUtc;
            progressA.tradeStartUtcTick = now.AddMinutes(-1).Ticks;
            progressA.expectedTradeEndUtcTick = now.AddMinutes(9).Ticks;
            progressB.tradeStartUtcTick = now.AddMinutes(-1).Ticks;
            progressB.expectedTradeEndUtcTick = now.AddMinutes(9).Ticks;
            context.SaveData.selectedCaravanId = firstId;

            context.Coordinator.TryGetRuntimeCaravan(firstId, out var runtimeA);
            context.Coordinator.TryGetRuntimeCaravan(second.caravanId, out var runtimeB);
            SaveDataLookup.TryGetCaravan(context.SaveData, second.caravanId, out var saveB);

            context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
            var cursorB = runtimeB.runEventChecksProcessed;
            var eventsB = runtimeB.runEventsOccurred;
            var cursorA = runtimeA.runEventChecksProcessed;
            var eventsA = runtimeA.runEventsOccurred;
            var foodA = runtimeA.foodAmount;

            var forced = context.Coordinator.TryProcessForcedRouteEvent(
                second.caravanId, progressB.activeTradeId, "weather-auto");
            report.Check("forced-success", forced.Succeeded);
            report.Check("forced-cursor-isolated", runtimeB.runEventChecksProcessed == cursorB);
            report.Check("forced-occurred-increment", runtimeB.runEventsOccurred == eventsB + 1);
            report.Check("forced-A-unchanged",
                runtimeA.runEventChecksProcessed == cursorA
                && runtimeA.runEventsOccurred == eventsA
                && Math.Abs(runtimeA.foodAmount - foodA) < 0.001f);
            report.Check("forced-selected-still-A", context.SaveData.selectedCaravanId == firstId);

            report.Check("forced-empty-caravan",
                !context.Coordinator.TryProcessForcedRouteEvent("", progressB.activeTradeId, "weather-auto").Succeeded);
            report.Check("forced-missing-caravan",
                !context.Coordinator.TryProcessForcedRouteEvent("missing", progressB.activeTradeId, "weather-auto").Succeeded);
            report.Check("forced-trade-mismatch",
                context.Coordinator.TryProcessForcedRouteEvent(
                    second.caravanId, "bad-trade", "weather-auto").FailureReason
                == ForcedRouteEventFailureReason.TradeMismatch);
            report.Check("forced-event-missing",
                context.Coordinator.TryProcessForcedRouteEvent(
                    second.caravanId, progressB.activeTradeId, "no-event").FailureReason
                == ForcedRouteEventFailureReason.EventNotFound);
            report.Check("forced-not-traveling",
                !context.Coordinator.TryProcessForcedRouteEvent(
                    preparing.caravanId, "x", "weather-auto").Succeeded);

            cursorB = runtimeB.runEventChecksProcessed;
            eventsB = runtimeB.runEventsOccurred;
            var foodB = runtimeB.foodAmount;
            var saveSnapshot = JsonUtility.ToJson(saveB);
            var refBefore = runtimeB;
            save.ShouldSucceed = false;
            var failed = context.Coordinator.TryProcessForcedRouteEvent(
                second.caravanId, progressB.activeTradeId, "combat-auto");
            context.Coordinator.TryGetRuntimeCaravan(second.caravanId, out var runtimeAfter);
            report.Check("forced-save-failed",
                !failed.Succeeded && failed.FailureReason == ForcedRouteEventFailureReason.SaveFailed);
            report.Check("forced-rollback-runtime",
                runtimeB.runEventChecksProcessed == cursorB
                && runtimeB.runEventsOccurred == eventsB
                && Math.Abs(runtimeB.foodAmount - foodB) < 0.001f);
            report.Check("forced-rollback-save", JsonUtility.ToJson(saveB) == saveSnapshot);
            report.Check("forced-rollback-same-ref", ReferenceEquals(refBefore, runtimeAfter));
            save.ShouldSucceed = true;
        }

        private static void RunOfflineParity(Report report, ISharedGameDataProvider sharedData)
        {
            var onlineSave = new ProbeSaveService();
            var offlineSave = new ProbeSaveService();
            var online = TestContext.Create(onlineSave, sharedData);
            var offline = TestContext.Create(offlineSave, sharedData);
            InitializeSelectedCaravan(online);
            InitializeSelectedCaravan(offline);

            var onlineCommand = CreateDepartureCommand(online, onlineSave);
            var offlineCommand = CreateDepartureCommand(offline, offlineSave);
            var onlineDepart = onlineCommand.Depart(new TradeDepartureRequest
            {
                CaravanId = online.SaveData.selectedCaravanId,
                RouteId = RouteId
            });
            var offlineDepart = offlineCommand.Depart(new TradeDepartureRequest
            {
                CaravanId = offline.SaveData.selectedCaravanId,
                RouteId = RouteId
            });
            report.Check("parity-depart", onlineDepart.DepartureSucceeded && offlineDepart.DepartureSucceeded);

            // Force identical trade IDs for deterministic seed parity.
            SaveDataLookup.TryGetTradeProgress(
                online.SaveData, online.SaveData.selectedCaravanId, out var onlineProgress);
            SaveDataLookup.TryGetTradeProgress(
                offline.SaveData, offline.SaveData.selectedCaravanId, out var offlineProgress);
            const string tradeId = "parity-trade-id";
            onlineProgress.activeTradeId = tradeId;
            offlineProgress.activeTradeId = tradeId;

            var now = online.GameTime.CurrentUtc;
            onlineProgress.tradeStartUtcTick = now.AddMinutes(-8).Ticks;
            onlineProgress.expectedTradeEndUtcTick = now.AddMinutes(2).Ticks;
            offlineProgress.tradeStartUtcTick = now.AddMinutes(-8).Ticks;
            offlineProgress.expectedTradeEndUtcTick = now.AddMinutes(2).Ticks;
            offline.SaveData.lastSavedUtcTicks = now.AddMinutes(-8).Ticks;

            online.Coordinator.CheckProgressAndCompletion();
            offline.Coordinator.ApplyOfflineProgressOnLoad(offline.SaveData);

            online.Coordinator.TryGetRuntimeCaravan(online.SaveData.selectedCaravanId, out var onlineRuntime);
            offline.Coordinator.TryGetRuntimeCaravan(offline.SaveData.selectedCaravanId, out var offlineRuntime);
            SaveDataLookup.TryGetCaravan(online.SaveData, online.SaveData.selectedCaravanId, out var onlineCaravan);
            SaveDataLookup.TryGetCaravan(offline.SaveData, offline.SaveData.selectedCaravanId, out var offlineCaravan);

            report.Info(
                "parity online cursor=" + onlineRuntime.runEventChecksProcessed
                + " events=" + onlineRuntime.runEventsOccurred
                + " | offline cursor=" + offlineRuntime.runEventChecksProcessed
                + " events=" + offlineRuntime.runEventsOccurred);

            report.Check("offline-online-cursor",
                onlineRuntime.runEventChecksProcessed == offlineRuntime.runEventChecksProcessed
                && onlineCaravan.runEventChecksProcessed == offlineCaravan.runEventChecksProcessed);
            report.Check("offline-online-events",
                onlineRuntime.runEventsOccurred == offlineRuntime.runEventsOccurred
                && onlineCaravan.runEventsOccurred == offlineCaravan.runEventsOccurred);
            report.Check("offline-online-fatal",
                onlineRuntime.runFatalReason == offlineRuntime.runFatalReason);
            report.Check("offline-save-batch-once", offlineSave.SaveCalls >= 1);
        }

        private static void RunCursorRestore(Report report, ISharedGameDataProvider sharedData)
        {
            var save = new ProbeSaveService();
            var context = TestContext.Create(save, sharedData);
            InitializeSelectedCaravan(context);
            var command = CreateDepartureCommand(context, save);
            command.Depart(new TradeDepartureRequest
            {
                CaravanId = context.SaveData.selectedCaravanId,
                RouteId = RouteId
            });

            SaveDataLookup.TryGetTradeProgress(
                context.SaveData, context.SaveData.selectedCaravanId, out var progress);
            var now = context.GameTime.CurrentUtc;
            progress.tradeStartUtcTick = now.AddMinutes(-3).Ticks;
            progress.expectedTradeEndUtcTick = now.AddMinutes(7).Ticks;
            context.Coordinator.CheckProgressAndCompletion();

            context.Coordinator.TryGetRuntimeCaravan(context.SaveData.selectedCaravanId, out var runtime);
            SaveDataLookup.TryGetCaravan(context.SaveData, context.SaveData.selectedCaravanId, out var caravanSave);
            var cursor = runtime.runEventChecksProcessed;
            var events = runtime.runEventsOccurred;
            report.Check("restore-runtime-save-match-before",
                cursor == caravanSave.runEventChecksProcessed && events == caravanSave.runEventsOccurred);

            // Simulate reload by rebuilding registry from save DTO.
            context.Coordinator.RebuildRuntimeCaravans();
            context.Coordinator.TryGetRuntimeCaravan(context.SaveData.selectedCaravanId, out var restored);
            SaveDataLookup.TryGetCaravan(context.SaveData, context.SaveData.selectedCaravanId, out caravanSave);
            report.Check("restore-after-rebuild",
                restored.runEventChecksProcessed == cursor
                && restored.runEventsOccurred == events
                && caravanSave.runEventChecksProcessed == cursor);

            context.Coordinator.CheckProgressAndCompletion();
            report.Check("restore-no-duplicate",
                restored.runEventChecksProcessed == cursor && restored.runEventsOccurred == events);

            progress.tradeStartUtcTick = now.AddMinutes(-6).Ticks;
            progress.expectedTradeEndUtcTick = now.AddMinutes(4).Ticks;
            context.Coordinator.CheckProgressAndCompletion();
            report.Check("restore-new-checks-only", restored.runEventChecksProcessed > cursor);
        }

        private static void RunSettlementOrder(Report report, ISharedGameDataProvider sharedData)
        {
            var save = new ProbeSaveService();
            var context = TestContext.Create(save, sharedData);
            InitializeSelectedCaravan(context);
            var command = CreateDepartureCommand(context, save);
            var departure = command.Depart(new TradeDepartureRequest
            {
                CaravanId = context.SaveData.selectedCaravanId,
                RouteId = RouteId
            });
            SaveDataLookup.TryGetTradeProgress(
                context.SaveData, context.SaveData.selectedCaravanId, out var progress);
            var now = context.GameTime.CurrentUtc;
            progress.tradeStartUtcTick = now.AddMinutes(-10).Ticks;
            progress.expectedTradeEndUtcTick = now.AddMinutes(-1).Ticks;

            context.Coordinator.CheckProgressAndCompletion();
            context.Coordinator.TryGetRuntimeCaravan(context.SaveData.selectedCaravanId, out var runtime);
            SaveDataLookup.TryGetCaravan(context.SaveData, context.SaveData.selectedCaravanId, out var caravanSave);

            report.Check("arrival-settlement-pending", progress.state == TradeProgressState.SettlementPending);
            report.Check("arrival-events-processed-before-settle",
                runtime.runEventChecksProcessed > 0
                && caravanSave.runEventChecksProcessed == runtime.runEventChecksProcessed);
            report.Check("arrival-pending-unique",
                SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, context.SaveData.selectedCaravanId, departure.TradeId, out _)
                && context.SaveData.pendingSettlements.Count == 1);
        }

        private static void RunSharedCopySanity(Report report)
        {
            var service = new SharedGameDataService();
            report.Check("shared-reload", service.LoadInitialData());
            var data = service.CurrentData;
            var routes = 0;
            var emptyIdEvents = 0;
            foreach (var id in data.RouteIds)
            {
                if (!data.TryGetRoute(id, out var route) || route == null)
                {
                    report.Fail("shared-route-missing", id);
                    continue;
                }

                routes++;
                if (string.IsNullOrEmpty(route.FromTownId) || string.IsNullOrEmpty(route.ToTownId))
                    report.Fail("shared-route-towns", id);
                if (route.Events == null)
                    report.Fail("shared-events-null", id);
                else
                {
                    for (var i = 0; i < route.Events.Length; i++)
                    {
                        var ev = route.Events[i];
                        if (ev != null && string.IsNullOrWhiteSpace(ev.Id))
                            emptyIdEvents++;
                    }
                }
            }

            report.Check("shared-route-count", routes == 8);
            report.Info("shared empty-id events=" + emptyIdEvents);
        }

        private static CaravanSaveData AddCaravan(TestContext context, string caravanId)
        {
            var runtime = CreateSampleCaravan(context.GameTime);
            runtime.caravanId = caravanId;
            var save = new CaravanSaveData();
            CaravanSaveDataMapper.CopyToSave(runtime, save);
            context.SaveData.caravans.Add(save);
            return save;
        }

        private static void InitializeSelectedCaravan(TestContext context)
        {
            if (!SaveDataLookup.TryGetCaravan(
                    context.SaveData, context.SaveData.selectedCaravanId, out var selectedSave))
                throw new InvalidOperationException("Selected Caravan save data is missing.");

            var runtime = CreateSampleCaravan(context.GameTime);
            runtime.caravanId = selectedSave.caravanId;
            CaravanSaveDataMapper.CopyToSave(runtime, selectedSave);
        }

        private static TradeStartService CreateDepartureCommand(TestContext context, ISaveService saveService)
        {
            return new TradeStartService(
                () => context.SaveData,
                saveService,
                new TradeProgressRecorder(context.GameTime, context.GameTime),
                context.ScreenRouter,
                clearSettlementCache: null,
                setActiveCaravan: caravan => context.Coordinator.SetActiveCaravan(caravan),
                getSharedGameData: () => context.SharedGameData,
                getRuntimeCaravan: context.Coordinator.GetOrCreateRuntimeCaravan);
        }

        private static CaravanData CreateSampleCaravan(IInGameTimeProvider timeProvider)
        {
            var caravan = new CaravanData
            {
                wagon = new imsiWagonData
                {
                    instanceId = SaveDataLookup.NewInstanceId(),
                    wagonName = "Route Event Wagon",
                    overLoad = 30f,
                    maxLoad = 60f,
                    minAnimals = 1,
                    maxAnimals = 5,
                    maxDurability = 100,
                    inventorySlotCount = 8
                },
                foodAmount = 30,
                starveGraceSeconds = 5f,
                baseSafetyChancePercent = 100f
            };
            caravan.animals.Add(new imsiAnimalData
            {
                instanceId = SaveDataLookup.NewInstanceId(),
                animalName = "Route Horse",
                foodPerKm = 8640f,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });
            caravan.mercenaries.Add(new imsiMercenaryData
            {
                instanceId = SaveDataLookup.NewInstanceId(),
                mercName = "Route Guard",
                combatPower = 10,
                contractCount = 1
            });
            caravan.cargo.Add(new CargoEntry
            {
                item = new imsiTradeItemData
                {
                    id = ItemId,
                    itemName = "Apple",
                    weight = 5f,
                    basePrice = 10,
                    maxCount = 10
                },
                quantity = 5
            });
            caravan.currentDurability = caravan.wagon.maxDurability;
            CaravanConsumptionRateNormalizer.ApplyToCaravan(caravan, timeProvider);
            return caravan;
        }

        private sealed class ProbeSaveService : ISaveService
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

        private sealed class TestContext
        {
            public SaveData SaveData { get; private set; }
            public GameTimeService GameTime { get; private set; }
            public ISharedGameDataProvider SharedGameData { get; private set; }
            public TradeProgressCoordinator Coordinator { get; private set; }
            public InGameScreenStateRouter ScreenRouter { get; private set; }

            public static TestContext Create(ISaveService saveService, ISharedGameDataProvider sharedData)
            {
                var policy = Resources.Load<InGameTimePolicyConfig>(InGameTimePolicyConfig.ResourceName);
                if (policy == null) policy = ScriptableObject.CreateInstance<InGameTimePolicyConfig>();
                var gameTime = new GameTimeService(policy);
                var saveData = saveService.CreateNewGameData();
                var recorder = new TradeProgressRecorder(gameTime, gameTime);
                var router = new InGameScreenStateRouter();
                var commitStore = new FrameworkTradePrepareCommitStore(() => saveData);
                var coordinator = new TradeProgressCoordinator(
                    () => saveData,
                    saveService,
                    gameTime,
                    recorder,
                    router,
                    gameTime,
                    () => sharedData,
                    commitStore,
                    commitStore);

                return new TestContext
                {
                    SaveData = saveData,
                    GameTime = gameTime,
                    SharedGameData = sharedData,
                    Coordinator = coordinator,
                    ScreenRouter = router
                };
            }
        }

        private sealed class Report
        {
            private readonly StringBuilder builder = new StringBuilder();
            public bool Failed { get; private set; }

            public void Check(string name, bool ok)
            {
                if (!ok) Failed = true;
                builder.AppendLine((ok ? "PASS" : "FAIL") + ": " + name);
            }

            public void Fail(string name, string detail)
            {
                Failed = true;
                builder.AppendLine("FAIL: " + name + " | " + detail);
            }

            public void Info(string message)
            {
                builder.AppendLine("INFO: " + message);
            }

            public override string ToString()
            {
                builder.Insert(0, (Failed ? "RESULT: FAIL" : "RESULT: PASS") + Environment.NewLine);
                return builder.ToString();
            }
        }
    }
}
#endif
