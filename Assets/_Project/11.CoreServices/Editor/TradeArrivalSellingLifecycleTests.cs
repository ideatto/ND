#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace ND.Framework.Editor
{
    public sealed class TradeArrivalSellingLifecycleTests
    {
        [Test]
        public void SuccessfulArrival_EntersSellingBeforeSettlement()
        {
            CaravanData caravan = CreateArrivedCaravan();

            JourneyResultData result = JourneyRunner.Settle(caravan);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.grade, Is.Not.EqualTo(JourneyResultGrade.Failed));
            Assert.That(caravan.state, Is.EqualTo(JourneyState.Selling));
            Assert.That(JourneyRunner.BeginSettlement(caravan), Is.True);
            Assert.That(caravan.state, Is.EqualTo(JourneyState.Settling));
            Assert.That(JourneyRunner.ClaimSettlement(caravan), Is.True);
            Assert.That(caravan.state, Is.EqualTo(JourneyState.Completed));
            Assert.That(JourneyRunner.ResetToPrepare(caravan), Is.True);
            Assert.That(caravan.state, Is.EqualTo(JourneyState.Prepare));
        }

        [Test]
        public void FailedArrival_SkipsSellingAndEntersSettlement()
        {
            CaravanData caravan = CreateArrivedCaravan();
            caravan.runFatalReason = JourneyFailureReason.FoodDepleted;

            JourneyResultData result = JourneyRunner.Settle(caravan);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.grade, Is.EqualTo(JourneyResultGrade.Failed));
            Assert.That(caravan.state, Is.EqualTo(JourneyState.Settling));
        }

        [Test]
        public void FailedArrival_ResultIncludesPreviouslyLostAndRemainingCargoAndFood()
        {
            CaravanData caravan = CreateArrivedCaravan();
            caravan.runFatalReason = JourneyFailureReason.FoodDepleted;
            caravan.runCargoLost = 3;
            caravan.cargo.Add(new CargoEntry { quantity = 4 });
            caravan.cargo.Add(new CargoEntry { quantity = 6 });
            caravan.cargo.Add(new CargoEntry { quantity = 0 });
            caravan.foodAmount = 7;
            caravan.runFoodLost = 2f;

            JourneyResultData result = JourneyRunner.Settle(caravan);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.cargoLost, Is.EqualTo(13));
            Assert.That(result.foodLost, Is.EqualTo(7f));
            Assert.That(caravan.runCargoLost, Is.EqualTo(3), "S8 snapshot must not mutate accumulated loss.");
            Assert.That(caravan.runFoodLost, Is.EqualTo(2f), "S8 snapshot must not mutate accumulated loss.");
            Assert.That(caravan.cargo[0].quantity, Is.EqualTo(4), "Cargo is removed only after S9 Claim succeeds.");
            Assert.That(caravan.cargo[1].quantity, Is.EqualTo(6), "Cargo is removed only after S9 Claim succeeds.");
        }

        [Test]
        public void FailedArrival_CargoLossSnapshotClampsAtIntMaxValue()
        {
            CaravanData caravan = CreateArrivedCaravan();
            caravan.runFatalReason = JourneyFailureReason.FoodDepleted;
            caravan.runCargoLost = int.MaxValue;
            caravan.cargo.Add(new CargoEntry { quantity = 1 });

            JourneyResultData result = JourneyRunner.Settle(caravan);

            Assert.That(result.cargoLost, Is.EqualTo(int.MaxValue));
            Assert.That(caravan.cargo[0].quantity, Is.EqualTo(1));
        }

        [Test]
        public void WagonBroken_FailureKeepsCargoAndFoodUntilClaimTransaction()
        {
            CaravanData caravan = CreateArrivedCaravan();
            caravan.cargo.Add(new CargoEntry { quantity = 5 });
            caravan.foodAmount = 75;
            caravan.currentDurability = 1;
            caravan.wagon = new imsiWagonData { maxDurability = 100 };
            caravan.lossLimitRate = 1f;

            JourneyRunner.ApplyDurabilityLoss(caravan, 1);

            JourneyResultData result = JourneyRunner.Settle(caravan);

            Assert.That(caravan.runWagonDestroyed, Is.True);
            Assert.That(result.failureReason, Is.EqualTo(JourneyFailureReason.WagonBroken));
            Assert.That(result.grade, Is.EqualTo(JourneyResultGrade.Failed));
            Assert.That(result.cargoLost, Is.EqualTo(5));
            Assert.That(result.foodLost, Is.EqualTo(75f));
            Assert.That(caravan.cargo[0].quantity, Is.EqualTo(5));
            Assert.That(caravan.foodAmount, Is.EqualTo(75));
        }

        [Test]
        public void ArrivalSaleSaveSuccess_ChangesOnlyTargetAndPublishesAfterSave()
        {
            LifecycleContext context = LifecycleContext.Create(saveSucceeds: true);
            string publishedCaravanId = string.Empty;
            JourneyState publishedState = JourneyState.Prepare;
            int publishedCount = 0;
            Action<string, JourneyState> handler = (caravanId, state) =>
            {
                publishedCaravanId = caravanId;
                publishedState = state;
                publishedCount++;
            };

            FrameworkEvents.CaravanJourneyStateChanged += handler;
            try
            {
                bool committed = context.Coordinator.TryCommitEmptyArrivalSaleCompletion(
                    context.TargetCaravanId,
                    context.TradeId,
                    out bool saveFailed);

                Assert.That(committed, Is.True);
                Assert.That(saveFailed, Is.False);
                Assert.That(context.SaveService.SaveCalls, Is.EqualTo(1));
                Assert.That(context.TargetCaravan.state, Is.EqualTo(JourneyState.Settling));
                Assert.That(context.TargetProgress.state, Is.EqualTo(TradeProgressState.SettlementPending));
                Assert.That(context.OtherCaravan.state, Is.EqualTo(JourneyState.Prepare));
                Assert.That(context.OtherProgress.state, Is.EqualTo(TradeProgressState.None));
                Assert.That(publishedCount, Is.EqualTo(1));
                Assert.That(publishedCaravanId, Is.EqualTo(context.TargetCaravanId));
                Assert.That(publishedState, Is.EqualTo(JourneyState.Settling));
            }
            finally
            {
                FrameworkEvents.CaravanJourneyStateChanged -= handler;
            }
        }

        [Test]
        public void ArrivalSaleSaveFailure_RollsBackAndDoesNotPublish()
        {
            LifecycleContext context = LifecycleContext.Create(saveSucceeds: false);
            int publishedCount = 0;
            Action<string, JourneyState> handler = (_, __) => publishedCount++;

            FrameworkEvents.CaravanJourneyStateChanged += handler;
            try
            {
                bool committed = context.Coordinator.TryCommitEmptyArrivalSaleCompletion(
                    context.TargetCaravanId,
                    context.TradeId,
                    out bool saveFailed);

                Assert.That(committed, Is.False);
                Assert.That(saveFailed, Is.True);
                Assert.That(context.SaveService.SaveCalls, Is.EqualTo(1));
                Assert.That(context.TargetCaravan.state, Is.EqualTo(JourneyState.Selling));
                Assert.That(context.TargetProgress.state, Is.EqualTo(TradeProgressState.Selling));
                Assert.That(context.OtherCaravan.state, Is.EqualTo(JourneyState.Prepare));
                Assert.That(context.OtherProgress.state, Is.EqualTo(TradeProgressState.None));
                Assert.That(publishedCount, Is.Zero);
            }
            finally
            {
                FrameworkEvents.CaravanJourneyStateChanged -= handler;
            }
        }

        private static CaravanData CreateArrivedCaravan()
        {
            return new CaravanData
            {
                caravanId = "arrival-test-caravan",
                state = JourneyState.Traveling,
                progress01 = JourneyRunner.ArrivalProgress,
                totalSeconds = 1f,
                foodAmount = 0
            };
        }

        private sealed class LifecycleContext
        {
            private const string TargetId = "arrival-sale-target";
            private const string OtherId = "arrival-sale-other";
            private const string ActiveTradeId = "arrival-sale-trade";

            public ConfigurableSaveService SaveService { get; private set; }
            public TradeProgressCoordinator Coordinator { get; private set; }
            public CaravanSaveData TargetCaravan { get; private set; }
            public CaravanSaveData OtherCaravan { get; private set; }
            public TradeProgressSaveData TargetProgress { get; private set; }
            public TradeProgressSaveData OtherProgress { get; private set; }
            public string TargetCaravanId => TargetId;
            public string TradeId => ActiveTradeId;

            public static LifecycleContext Create(bool saveSucceeds)
            {
                var saveData = new SaveData();
                saveData.caravans.Clear();
                saveData.tradeProgressEntries.Clear();
                saveData.pendingSettlements.Clear();

                var targetCaravan = new CaravanSaveData
                {
                    caravanId = TargetId,
                    state = JourneyState.Selling
                };
                var otherCaravan = new CaravanSaveData
                {
                    caravanId = OtherId,
                    state = JourneyState.Prepare
                };
                var targetProgress = new TradeProgressSaveData
                {
                    caravanId = TargetId,
                    activeTradeId = ActiveTradeId,
                    state = TradeProgressState.Selling
                };
                var otherProgress = new TradeProgressSaveData
                {
                    caravanId = OtherId,
                    state = TradeProgressState.None
                };

                saveData.caravans.Add(targetCaravan);
                saveData.caravans.Add(otherCaravan);
                saveData.tradeProgressEntries.Add(targetProgress);
                saveData.tradeProgressEntries.Add(otherProgress);
                saveData.pendingSettlements.Add(new PendingSettlementSaveData
                {
                    caravanId = TargetId,
                    tradeId = ActiveTradeId,
                    hasResult = true,
                    grade = JourneyResultGrade.Success
                });
                saveData.selectedCaravanId = OtherId;

                var saveService = new ConfigurableSaveService(saveSucceeds);
                var coordinator = new TradeProgressCoordinator(
                    () => saveData,
                    saveService,
                    null,
                    null);

                return new LifecycleContext
                {
                    SaveService = saveService,
                    Coordinator = coordinator,
                    TargetCaravan = targetCaravan,
                    OtherCaravan = otherCaravan,
                    TargetProgress = targetProgress,
                    OtherProgress = otherProgress
                };
            }
        }

        private sealed class ConfigurableSaveService : ISaveService
        {
            private readonly bool succeeds;

            public ConfigurableSaveService(bool succeeds)
            {
                this.succeeds = succeeds;
            }

            public int SaveCalls { get; private set; }
            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => new SaveData();

            public SaveResult Save(SaveData data)
            {
                SaveCalls++;
                return succeeds
                    ? SaveResult.Success()
                    : SaveResult.Failure(
                        SaveFailureReason.WriteFailed,
                        "Forced arrival sale save failure.");
            }

            public void ResetSaveData()
            {
            }
        }
    }
}
#endif
