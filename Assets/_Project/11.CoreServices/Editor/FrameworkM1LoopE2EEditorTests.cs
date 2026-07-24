/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Multi-active runtime registry, online tick, offline restore, explicit Economy ID, and claim regression paths를
 *   현재 production 서비스 조립으로 Editor에서 검증한다.
 *
 * Usage for Team Members
 * - Unity Editor: ND/Framework/Run Multi-active Progress E2E Checks
 * - CI/batchmode: ND.Framework.Editor.FrameworkM1LoopE2EEditorTests.RunAllFromBatchMode
 *
 * Important Notes
 * - Editor 전용이며 Player build에 포함되지 않는다.
 * - 성공 로그는 정적·Editor 경로 검증 결과이며 Play Mode runtime PASS를 의미하지 않는다.
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using ND.Economy;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    public static class FrameworkM1LoopE2EEditorTests
    {
        private const string RouteId = "BaseToRiver";
        private const string ItemId = "Apple";
        private const float DistanceKm = 100f;

        [MenuItem("ND/Framework/Run Multi-active Progress E2E Checks")]
        public static void RunAll()
        {
            RunOnlineTickLifecycleGateChecks();
            RunRuntimeRegistryChecks();
            RunMultiActiveOnlineTickChecks();
            RunMultiActiveOfflineRestoreChecks();
            RunExplicitEconomyTradeIdChecks();
            RunClaimRegressionChecks();
            Debug.Log("[Framework Multi-active E2E] All checks passed.");
        }

        public static void RunAllFromBatchMode()
        {
            RunAll();
        }

        private static void RunOnlineTickLifecycleGateChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            InitializeSelectedCaravan(context);
            var command = CreateDepartureCommand(context, save);
            var departure = command.Depart(new TradeDepartureRequest
            {
                CaravanId = context.SaveData.selectedCaravanId,
                RouteId = RouteId
            });
            if (!departure.DepartureSucceeded)
                throw new InvalidOperationException("Online tick gate setup departure failed.");

            SaveDataLookup.TryGetTradeProgress(
                context.SaveData, context.SaveData.selectedCaravanId, out var progress);
            var now = context.GameTime.CurrentUtc;
            progress.tradeStartUtcTick = now.AddMinutes(-2).Ticks;
            progress.expectedTradeEndUtcTick = now.AddMinutes(-1).Ticks;
            context.SaveData.lastSavedUtcTicks = now.AddMinutes(-3).Ticks;
            var savesBeforeGate = save.SaveCalls;
            var readyCount = 0;
            var offlineCount = 0;
            Action<string, string, JourneyResultData> onReady = (_, __, ___) => readyCount++;
            Action<string> onOffline = _ => offlineCount++;
            FrameworkEvents.TradeSettlementReady += onReady;
            FrameworkEvents.TradeOfflineCompleted += onOffline;
            try
            {
                if (CanRunOnlineProgressTick(
                        isEnabled: false,
                        context.SaveData,
                        context.SharedGameData))
                {
                    context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
                }

                if (progress.state != TradeProgressState.Traveling
                    || context.SaveData.pendingSettlements.Count != 0
                    || save.SaveCalls != savesBeforeGate
                    || readyCount != 0
                    || offlineCount != 0)
                {
                    throw new InvalidOperationException(
                        "Disabled online tick consumed a Traveling entry before offline restore.");
                }

                if (!context.Coordinator.ApplyOfflineProgressOnLoad(context.SaveData)
                    || progress.state != TradeProgressState.SettlementPending
                    || offlineCount != 1
                    || readyCount != 1)
                {
                    throw new InvalidOperationException(
                        "Offline restore did not run before online tick activation.");
                }

                var pendingCount = context.SaveData.pendingSettlements.Count;
                if (!CanRunOnlineProgressTick(
                        isEnabled: true,
                        context.SaveData,
                        context.SharedGameData))
                {
                    throw new InvalidOperationException(
                        "Online tick gate did not enable after load prerequisites completed.");
                }
                context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
                if (context.SaveData.pendingSettlements.Count != pendingCount
                    || readyCount != 1
                    || offlineCount != 1)
                {
                    throw new InvalidOperationException(
                        "Online tick duplicated settlement after offline restore.");
                }

                if (CanRunOnlineProgressTick(
                        isEnabled: false,
                        context.SaveData,
                        context.SharedGameData))
                {
                    throw new InvalidOperationException(
                        "Title-session disable state still allowed online tick.");
                }
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
                FrameworkEvents.TradeOfflineCompleted -= onOffline;
            }
        }

        private static bool CanRunOnlineProgressTick(
            bool isEnabled,
            SaveData saveData,
            ISharedGameDataProvider sharedGameData)
        {
            var method = typeof(FrameworkRoot).GetMethod(
                "CanRunOnlineProgressTick",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                throw new MissingMethodException("FrameworkRoot online tick gate was not found.");
            return (bool)method.Invoke(null, new object[] { isEnabled, saveData, sharedGameData });
        }

        private static void RunRuntimeRegistryChecks()
        {
            var context = TestContext.Create();
            var selectedId = context.SaveData.selectedCaravanId;
            var secondSave = AddCaravan(context, "registry-second");

            context.Coordinator.RebuildRuntimeCaravans();
            if (!context.Coordinator.TryGetRuntimeCaravan(selectedId, out var selectedRuntime)
                || !context.Coordinator.TryGetRuntimeCaravan(secondSave.caravanId, out var secondRuntime)
                || ReferenceEquals(selectedRuntime, secondRuntime)
                || !ReferenceEquals(context.Coordinator.ActiveCaravan, selectedRuntime))
            {
                throw new InvalidOperationException("Runtime registry did not retain every Caravan by ID.");
            }

            context.SaveData.selectedCaravanId = secondSave.caravanId;
            if (!ReferenceEquals(context.Coordinator.ActiveCaravan, secondRuntime)
                || !context.Coordinator.RegisterRuntimeCaravan(secondSave.caravanId, secondRuntime))
            {
                throw new InvalidOperationException("Runtime registry lookup changed or rejected the same registered instance.");
            }

            var duplicate = CaravanSaveDataMapper.ToRuntime(secondSave);
            if (context.Coordinator.RegisterRuntimeCaravan(secondSave.caravanId, duplicate))
            {
                throw new InvalidOperationException("Runtime registry replaced an existing ID with another instance.");
            }
        }

        private static void RunMultiActiveOnlineTickChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var firstId = context.SaveData.selectedCaravanId;
            InitializeSelectedCaravan(context);
            var secondSave = AddCaravan(context, "online-second");
            var command = CreateDepartureCommand(context, save);
            var firstDeparture = command.Depart(new TradeDepartureRequest
            {
                CaravanId = firstId,
                RouteId = RouteId
            });
            var secondDeparture = command.Depart(new TradeDepartureRequest
            {
                CaravanId = secondSave.caravanId,
                RouteId = RouteId
            });
            if (!firstDeparture.DepartureSucceeded || !secondDeparture.DepartureSucceeded)
                throw new InvalidOperationException("Multi-active online setup departure failed.");

            SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out var firstProgress);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, secondSave.caravanId, out var secondProgress);
            var now = context.GameTime.CurrentUtc;
            firstProgress.tradeStartUtcTick = now.AddMinutes(-2).Ticks;
            firstProgress.expectedTradeEndUtcTick = now.AddMinutes(-1).Ticks;
            secondProgress.tradeStartUtcTick = now.AddMinutes(-1).Ticks;
            secondProgress.expectedTradeEndUtcTick = now.AddMinutes(1).Ticks;
            context.SaveData.tradeProgressEntries.Insert(0, null);
            context.SaveData.tradeProgressEntries.Insert(1, new TradeProgressSaveData
            {
                caravanId = "missing-online-caravan",
                activeTradeId = "missing-online-trade",
                state = TradeProgressState.Traveling,
                tradeStartUtcTick = now.AddMinutes(-1).Ticks,
                expectedTradeEndUtcTick = now.AddMinutes(1).Ticks
            });
            context.SaveData.selectedCaravanId = secondSave.caravanId;

            var selectedBefore = context.SaveData.selectedCaravanId;
            var savesBefore = save.SaveCalls;
            var ready = new List<string>();
            Action<string, string, JourneyResultData> onReady =
                (caravanId, tradeId, _) => ready.Add(caravanId + ":" + tradeId);
            FrameworkEvents.TradeSettlementReady += onReady;
            try
            {
                if (!context.Coordinator.CheckProgressAndCompletion())
                    throw new InvalidOperationException("Multi-active online tick did not report completion.");
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
            }

            if (firstProgress.state != TradeProgressState.SettlementPending
                || secondProgress.state != TradeProgressState.Traveling
                || context.SaveData.selectedCaravanId != selectedBefore
                || !SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, firstId, firstDeparture.TradeId, out _)
                || ready.Count != 1
                || ready[0] != firstId + ":" + firstDeparture.TradeId
                || save.SaveCalls != savesBefore + 1)
            {
                throw new InvalidOperationException(
                    "Online tick did not isolate IDs, selection, invalid entries, events, or the batch save.");
            }

            RunSimultaneousOnlineCompletionCheck();
            RunSaveFailureEventSuppressionCheck();
        }

        private static void RunSimultaneousOnlineCompletionCheck()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var firstId = context.SaveData.selectedCaravanId;
            InitializeSelectedCaravan(context);
            var secondSave = AddCaravan(context, "online-simultaneous");
            var command = CreateDepartureCommand(context, save);
            var first = command.Depart(new TradeDepartureRequest { CaravanId = firstId, RouteId = RouteId });
            var second = command.Depart(new TradeDepartureRequest { CaravanId = secondSave.caravanId, RouteId = RouteId });
            SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out var firstProgress);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, secondSave.caravanId, out var secondProgress);
            var now = context.GameTime.CurrentUtc;
            firstProgress.expectedTradeEndUtcTick = now.AddSeconds(-1).Ticks;
            secondProgress.expectedTradeEndUtcTick = now.AddSeconds(-1).Ticks;
            var savesBefore = save.SaveCalls;
            var readyCount = 0;
            Action<string, string, JourneyResultData> onReady = (_, __, ___) => readyCount++;
            FrameworkEvents.TradeSettlementReady += onReady;
            try
            {
                context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
                context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
            }

            if (firstProgress.state != TradeProgressState.SettlementPending
                || secondProgress.state != TradeProgressState.SettlementPending
                || !SaveDataLookup.TryGetPendingSettlement(context.SaveData, firstId, first.TradeId, out _)
                || !SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, secondSave.caravanId, second.TradeId, out _)
                || readyCount != 2
                || save.SaveCalls != savesBefore + 1)
            {
                throw new InvalidOperationException("Simultaneous online completions were not independently persisted once.");
            }
        }

        private static void RunSaveFailureEventSuppressionCheck()
        {
            var save = new ConfigurableSaveService { ShouldSucceed = false };
            var context = TestContext.Create(save);
            InitializeSelectedCaravan(context);
            var command = CreateDepartureCommand(context, save);
            save.ShouldSucceed = true;
            var departure = command.Depart(new TradeDepartureRequest
            {
                CaravanId = context.SaveData.selectedCaravanId,
                RouteId = RouteId
            });
            SaveDataLookup.TryGetTradeProgress(
                context.SaveData, context.SaveData.selectedCaravanId, out var progress);
            progress.expectedTradeEndUtcTick = context.GameTime.CurrentUtc.AddSeconds(-1).Ticks;
            save.ShouldSucceed = false;
            var readyCount = 0;
            Action<string, string, JourneyResultData> onReady = (_, __, ___) => readyCount++;
            FrameworkEvents.TradeSettlementReady += onReady;
            try
            {
                if (!context.Coordinator.CheckProgressAndCompletion()
                    || progress.activeTradeId != departure.TradeId)
                {
                    throw new InvalidOperationException("Save-failure setup did not reach explicit settlement.");
                }
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
            }
            if (readyCount != 0)
                throw new InvalidOperationException("Settlement success event was raised before persistence succeeded.");
        }

        private static void RunMultiActiveOfflineRestoreChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var firstId = context.SaveData.selectedCaravanId;
            InitializeSelectedCaravan(context);
            var secondSave = AddCaravan(context, "offline-second");
            var preparingSave = AddCaravan(context, "offline-preparing");
            var command = CreateDepartureCommand(context, save);
            command.Depart(new TradeDepartureRequest { CaravanId = firstId, RouteId = RouteId });
            command.Depart(new TradeDepartureRequest { CaravanId = secondSave.caravanId, RouteId = RouteId });
            SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out var firstProgress);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, secondSave.caravanId, out var secondProgress);
            var now = context.GameTime.CurrentUtc;
            firstProgress.activeTradeId = "offline-complete";
            firstProgress.tradeStartUtcTick = now.AddMinutes(-3).Ticks;
            firstProgress.expectedTradeEndUtcTick = now.AddMinutes(-2).Ticks;
            secondProgress.activeTradeId = "offline-traveling";
            secondProgress.tradeStartUtcTick = now.AddMinutes(-1).Ticks;
            secondProgress.expectedTradeEndUtcTick = now.AddMinutes(1).Ticks;
            context.SaveData.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = preparingSave.caravanId,
                state = TradeProgressState.Preparing
            });
            context.SaveData.tradeProgressEntries.Insert(0, new TradeProgressSaveData
            {
                caravanId = "missing-offline-caravan",
                activeTradeId = "missing-offline-trade",
                state = TradeProgressState.Traveling,
                tradeStartUtcTick = now.AddMinutes(-1).Ticks,
                expectedTradeEndUtcTick = now.AddMinutes(1).Ticks
            });
            context.SaveData.selectedCaravanId = preparingSave.caravanId;
            context.SaveData.lastSavedUtcTicks = now.AddMinutes(-4).Ticks;

            var selectedBefore = context.SaveData.selectedCaravanId;
            var savesBefore = save.SaveCalls;
            var ready = new List<string>();
            var offline = new List<string>();
            Action<string, string, JourneyResultData> onReady =
                (caravanId, tradeId, _) => ready.Add(caravanId + ":" + tradeId);
            Action<string> onOffline = tradeId => offline.Add(tradeId);
            FrameworkEvents.TradeSettlementReady += onReady;
            FrameworkEvents.TradeOfflineCompleted += onOffline;
            try
            {
                if (!context.Coordinator.ApplyOfflineProgressOnLoad(context.SaveData))
                    throw new InvalidOperationException("Multi-active offline restore did not report completion.");
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
                FrameworkEvents.TradeOfflineCompleted -= onOffline;
            }

            if (firstProgress.state != TradeProgressState.SettlementPending
                || secondProgress.state != TradeProgressState.Traveling
                || context.SaveData.selectedCaravanId != selectedBefore
                || !SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, firstId, "offline-complete", out _)
                || ready.Count != 1 || offline.Count != 1
                || ready[0] != firstId + ":offline-complete"
                || offline[0] != "offline-complete"
                || save.SaveCalls != savesBefore + 1)
            {
                throw new InvalidOperationException(
                    "Offline restore did not isolate mixed states, IDs, selection, errors, or the batch save.");
            }
        }

        private static void RunExplicitEconomyTradeIdChecks()
        {
            var context = TestContext.Create();
            var selectedProgress = new TradeProgressSaveData
            {
                caravanId = context.SaveData.selectedCaravanId,
                activeTradeId = "selected-trade",
                activeRouteId = RouteId,
                state = TradeProgressState.Traveling
            };
            context.SaveData.tradeProgressEntries.Add(selectedProgress);
            var explicitProgress = new TradeProgressSaveData
            {
                caravanId = "economy-explicit",
                activeTradeId = "explicit-trade",
                activeRouteId = RouteId,
                state = TradeProgressState.Traveling
            };
            var runtime = CreateSampleCaravan(context.GameTime);
            runtime.caravanId = explicitProgress.caravanId;
            var result = new JourneyResultData();
            var input = FrameworkEconomyM1InputBuilder.TryBuild(
                context.SaveData, explicitProgress, runtime, result, context.SharedGameData);
            var bridge = new EconomyM1SettlementBridge();
            if (input == null || input.TradeId != explicitProgress.activeTradeId
                || !bridge.TryCalculateAndFill(
                    context.SaveData, explicitProgress, runtime, result, context.SharedGameData)
                || bridge.TryApplyPendingEconomy(
                    context.SaveData, runtime, selectedProgress.activeTradeId)
                || !bridge.TryApplyPendingEconomy(
                    context.SaveData, runtime, explicitProgress.activeTradeId))
            {
                throw new InvalidOperationException("Economy settlement used the selected trade ID.");
            }
        }

        private static void RunClaimRegressionChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var caravan = CreateSampleCaravan(context.GameTime);
            if (!context.TradeStart.TryStartTrade(
                    caravan, DistanceKm, "claim-regression", RouteId).canDepart)
                throw new InvalidOperationException("Claim regression setup failed to start.");

            context.Coordinator.ForceCompleteActiveTrade();
            var caravanId = context.SaveData.selectedCaravanId;
            var tradeId = context.SaveData.tradeProgress.activeTradeId;
            var snapshot = JsonUtility.ToJson(context.SaveData);
            save.ShouldSucceed = false;
            var failed = context.Coordinator.ClaimSettlement(caravanId, tradeId);
            if (failed.Succeeded
                || failed.FailureReason != ClaimSettlementFailureReason.SaveFailed
                || JsonUtility.ToJson(context.SaveData) != snapshot)
            {
                throw new InvalidOperationException("Claim save failure did not roll back the staged state.");
            }

            save.ShouldSucceed = true;
            var succeeded = context.Coordinator.ClaimSettlement(caravanId, tradeId);
            if (!succeeded.Succeeded
                || context.Coordinator.ClaimSettlement(caravanId, tradeId).Succeeded)
            {
                throw new InvalidOperationException("Explicit claim or duplicate claim prevention regressed.");
            }
            SaveDataLookup.TryGetCaravan(context.SaveData, caravanId, out var caravanSave);
            if (string.IsNullOrWhiteSpace(caravanSave.currentTownId))
                throw new InvalidOperationException("Claim did not retain the destination currentTownId.");
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
            {
                throw new InvalidOperationException("Selected Caravan save data is missing.");
            }

            var runtime = CreateSampleCaravan(context.GameTime);
            runtime.caravanId = selectedSave.caravanId;
            CaravanSaveDataMapper.CopyToSave(runtime, selectedSave);
        }

        private static TradeStartService CreateDepartureCommand(
            TestContext context,
            ISaveService saveService)
        {
            return new TradeStartService(
                () => context.SaveData,
                saveService,
                new TradeProgressRecorder(context.GameTime, context.GameTime),
                context.ScreenRouter,
                getSharedGameData: () => context.SharedGameData);
        }

        private static CaravanData CreateSampleCaravan(IInGameTimeProvider timeProvider)
        {
            var caravan = new CaravanData
            {
                wagon = new imsiWagonData
                {
                    instanceId = SaveDataLookup.NewInstanceId(),
                    wagonName = "Editor Test Wagon",
                    overLoad = 30f,
                    maxLoad = 60f,
                    minAnimals = 1,
                    maxAnimals = 5,
                    maxDurability = 100,
                    inventorySlotCount = 8
                },
                foodAmount = 30,
                starveGraceSeconds = 5f
            };
            caravan.animals.Add(new imsiAnimalData
            {
                instanceId = SaveDataLookup.NewInstanceId(),
                animalName = "Editor Horse",
                foodPerKm = 8640f,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });
            caravan.mercenaries.Add(new imsiMercenaryData
            {
                instanceId = SaveDataLookup.NewInstanceId(),
                mercName = "Editor Guard",
                combatPower = 10,
                contractCount = 1
            });
            caravan.cargo.Add(new CargoEntry
            {
                item = new imsiTradeItemData
                {
                    id = ItemId,
                    itemName = "Editor Apple",
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

        private sealed class ConfigurableSaveService : ISaveService
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
            public TradeStartService TradeStart { get; private set; }
            public InGameScreenStateRouter ScreenRouter { get; private set; }

            public static TestContext Create(ISaveService saveServiceOverride = null)
            {
                var policy = Resources.Load<InGameTimePolicyConfig>(InGameTimePolicyConfig.ResourceName);
                if (policy == null) policy = ScriptableObject.CreateInstance<InGameTimePolicyConfig>();
                var gameTime = new GameTimeService(policy);
                var saveService = saveServiceOverride ?? new ConfigurableSaveService();
                var sharedService = new SharedGameDataService();
                if (!sharedService.LoadInitialData())
                    throw new InvalidOperationException(
                        "Shared game data load failed: " + sharedService.LastErrorSummary);
                var sharedData = sharedService.CurrentData;
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
                var tradeStart = new TradeStartService(
                    () => saveData,
                    saveService,
                    recorder,
                    router,
                    () =>
                    {
                        coordinator.ClearSettlementCache();
                        coordinator.ClearPendingSettlementSave(saveData);
                    },
                    caravan =>
                    {
                        StageCommit(saveData, sharedData, commitStore, caravan.caravanId);
                        coordinator.SetActiveCaravan(caravan);
                    },
                    () => sharedData,
                    coordinator.GetOrCreateRuntimeCaravan);

                return new TestContext
                {
                    SaveData = saveData,
                    GameTime = gameTime,
                    SharedGameData = sharedData,
                    Coordinator = coordinator,
                    TradeStart = tradeStart,
                    ScreenRouter = router
                };
            }

            private static void StageCommit(
                SaveData saveData,
                ISharedGameDataProvider sharedData,
                FrameworkTradePrepareCommitStore commitStore,
                string caravanId)
            {
                if (!SaveDataLookup.TryGetTradeProgress(saveData, caravanId, out var progress)
                    || !sharedData.TryGetRoute(progress.activeRouteId, out var route)
                    || route == null
                    || !commitStore.TryStage(new global::TradePrepareCommitData
                    {
                        tradeId = progress.activeTradeId,
                        currentTownId = saveData.player.currentTownId,
                        selectedDestinationTownId = route.ToTownId,
                        routeId = progress.activeRouteId
                    }))
                {
                    throw new InvalidOperationException("Test trade preparation commit could not be staged.");
                }
            }
        }
    }
}
#endif
