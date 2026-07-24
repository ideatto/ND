using System;

namespace ND.Economy.EditorTests
{
    public static class TradeEventPreviewCalculatorTests
    {
        public static void RunAll()
        {
            Calculate_UsesKilometreIntervalsAndBinomialPreview();
            Calculate_AllowsMultipleChecksWithoutCaps();
            CalculateBanditChance_UsesOneMercenaryAndClampsPercent();
            CalculateBanditChance_UsesBaseChanceWithoutMercenary();
            Calculate_RejectsInvalidPolicyValues();
            ResolveBanditRaid_OnFailureRemovesMercenaryAndRoundsLootUp();
            ResolveBanditRaid_OnSafePassKeepsAssets();
            ResolveBanditRaid_AppliesCumulativeCargoLossLimitOnlyToCargo();
            CaravanSaveDataMapper_PreservesZeroLossLimitRate();
            ApplyDurabilityLoss_UsesLossLimitWithoutRaidToggle();
            EventChecks_UseTraveledDistanceAndStableTradeKey();
            RouteEventProcessor_DoesNotRecordUnsupportedEventAsCompleted();
            RouteEventProcessor_ForcedCombatUsesTheRuntimeCombatPath();
            RouteEventProcessor_SplitAndBatchProgressProduceSameResult();
            RouteEventProcessor_SaveRoundTripPreservesLossAndPreventsReplay();
        }

        private static void Calculate_UsesKilometreIntervalsAndBinomialPreview()
        {
            TradeEventPreviewResult result = TradeEventPreviewCalculator.Calculate(new TradeEventPreviewInput
            {
                DistanceKm = 500f,
                EventIntervalKm = 100f,
                EventChancePerCheck = 0.2f
            });

            Check(result.IsValid, "Expected a valid event preview.");
            CheckEqual(5, result.EventCheckCount, "Event check count");
            CheckNear(0.67232f, result.AtLeastOneEventChance, "At-least-one chance");
            CheckNear(1f, result.ExpectedEventCount, "Expected event count");
        }

        private static void Calculate_AllowsMultipleChecksWithoutCaps()
        {
            TradeEventPreviewResult result = TradeEventPreviewCalculator.Calculate(new TradeEventPreviewInput
            {
                DistanceKm = 10000f,
                EventIntervalKm = 10f,
                EventChancePerCheck = 0.5f
            });

            CheckEqual(1000, result.EventCheckCount, "Uncapped event check count");
            CheckNear(500f, result.ExpectedEventCount, "Uncapped expected event count");
        }

        private static void CalculateBanditChance_UsesOneMercenaryAndClampsPercent()
        {
            float normal = TradeEventPreviewCalculator.CalculateBanditSafePassChancePercent(10f, 50, 100);
            float capped = TradeEventPreviewCalculator.CalculateBanditSafePassChancePercent(70f, 100, 50);

            CheckNear(35f, normal, "Bandit safe-pass chance");
            CheckNear(100f, capped, "Clamped bandit safe-pass chance");
        }

        private static void CalculateBanditChance_UsesBaseChanceWithoutMercenary()
        {
            CheckNear(
                15f,
                TradeEventPreviewCalculator.CalculateBanditSafePassChancePercent(15f, 0, 100),
                "Base chance without a mercenary");
        }

        private static void Calculate_RejectsInvalidPolicyValues()
        {
            TradeEventPreviewResult result = TradeEventPreviewCalculator.Calculate(new TradeEventPreviewInput
            {
                DistanceKm = 100f,
                EventIntervalKm = 0f,
                EventChancePerCheck = 0.2f
            });

            Check(!result.IsValid, "A non-positive event interval must fail.");
        }

        private static void ResolveBanditRaid_OnFailureRemovesMercenaryAndRoundsLootUp()
        {
            CaravanData caravan = CreateTravelingCaravan(0f);

            BanditRaidResult result = JourneyRunner.ResolveBanditRaid(
                caravan,
                banditCombatPower: 100,
                cargoLootRate: 0.25f,
                fodderLootRate: 0.25f,
                randomSeed: 1234);

            Check(result.processed, "The traveling caravan raid must be processed.");
            Check(!result.passedSafely, "Zero safety chance must fail.");
            CheckEqual(3, result.cargoLost, "Rounded-up cargo loot");
            CheckEqual(3, result.foodLost, "Rounded-up fodder loot");
            CheckEqual(0, caravan.mercenaries.Count, "Mercenary removal on defeat");
            CheckEqual(7, caravan.cargo[0].quantity + caravan.cargo[1].quantity, "Remaining cargo");
        }

        private static void ResolveBanditRaid_OnSafePassKeepsAssets()
        {
            CaravanData caravan = CreateTravelingCaravan(100f);

            BanditRaidResult result = JourneyRunner.ResolveBanditRaid(
                caravan,
                banditCombatPower: 100,
                cargoLootRate: 1f,
                fodderLootRate: 1f,
                randomSeed: 1234);

            Check(result.passedSafely, "A 100 percent safety chance must pass.");
            CheckEqual(0, result.cargoLost, "Cargo loss on safe pass");
            CheckEqual(0, result.foodLost, "Food loss on safe pass");
            CheckEqual(1, caravan.mercenaries.Count, "Mercenary preservation on safe pass");
        }

        private static void ResolveBanditRaid_AppliesCumulativeCargoLossLimitOnlyToCargo()
        {
            CaravanData caravan = CreateTravelingCaravan(0f);
            caravan.lossLimitRate = 0.2f;

            BanditRaidResult first = JourneyRunner.ResolveBanditRaid(
                caravan,
                banditCombatPower: 100,
                cargoLootRate: 1f,
                fodderLootRate: 0.1f,
                randomSeed: 1234);
            BanditRaidResult second = JourneyRunner.ResolveBanditRaid(
                caravan,
                banditCombatPower: 100,
                cargoLootRate: 1f,
                fodderLootRate: 0.1f,
                randomSeed: 5678);

            CheckEqual(2, first.cargoLost, "First raid cargo loss capped by the run limit");
            CheckEqual(0, second.cargoLost, "Later raid cargo loss blocked by the cumulative run limit");
            CheckEqual(2, caravan.runCargoLost, "Cumulative cargo loss limit");
            CheckEqual(1, first.foodLost, "First raid fodder loss");
            CheckEqual(1, second.foodLost, "Fodder loss remains independent of the cargo limit");
            CheckNear(2f, caravan.runFoodLost, "Cumulative fodder loss");
        }

        private static void CaravanSaveDataMapper_PreservesZeroLossLimitRate()
        {
            var saved = new ND.Framework.CaravanSaveData { lossLimitRate = 0f };
            CaravanData restored = ND.Framework.CaravanSaveDataMapper.ToRuntime(saved);
            CheckNear(0f, restored.lossLimitRate, "Zero loss limit restored from save");

            restored.lossLimitRate = 0f;
            ND.Framework.CaravanSaveDataMapper.CopyToSave(restored, saved);
            CheckNear(0f, saved.lossLimitRate, "Zero loss limit written to save");

            saved.lossLimitRate = -0.1f;
            ND.Framework.CaravanSaveDataMapper.Normalize(saved);
            CheckNear(1f, saved.lossLimitRate, "Invalid negative loss limit uses the safe default");

            saved.lossLimitRate = 1.1f;
            ND.Framework.CaravanSaveDataMapper.Normalize(saved);
            CheckNear(1f, saved.lossLimitRate, "Invalid excessive loss limit uses the safe default");
        }

        private static void ApplyDurabilityLoss_UsesLossLimitWithoutRaidToggle()
        {
            CaravanData protectedCaravan = CreateTravelingCaravan(0f);
            protectedCaravan.wagon.maxDurability = 100;
            protectedCaravan.currentDurability = 100;
            protectedCaravan.lossLimitRate = 0f;
            JourneyRunner.ApplyDurabilityLoss(protectedCaravan, 50);
            CheckEqual(100, protectedCaravan.currentDurability, "Zero loss limit protects durability");

            CaravanData limitedCaravan = CreateTravelingCaravan(0f);
            limitedCaravan.wagon.maxDurability = 100;
            limitedCaravan.currentDurability = 100;
            limitedCaravan.lossLimitRate = 0.2f;
            JourneyRunner.ApplyDurabilityLoss(limitedCaravan, 50);
            JourneyRunner.ApplyDurabilityLoss(limitedCaravan, 50);
            CheckEqual(80, limitedCaravan.currentDurability, "Cumulative durability loss limit");
            CheckEqual(20, limitedCaravan.runDurabilityLost, "Recorded durability loss limit");
        }

        private static CaravanData CreateTravelingCaravan(float baseSafetyChancePercent)
        {
            CaravanData caravan = new CaravanData
            {
                state = JourneyState.Traveling,
                baseSafetyChancePercent = baseSafetyChancePercent,
                foodAmount = 10,
                lossLimitRate = 1f,
                runOriginalCargoCount = 10
            };
            caravan.mercenaries.Add(new imsiMercenaryData
            {
                instanceId = "merc-1",
                combatPower = 25
            });
            caravan.cargo.Add(new CargoEntry
            {
                item = new imsiTradeItemData { id = "item-a" },
                quantity = 7
            });
            caravan.cargo.Add(new CargoEntry
            {
                item = new imsiTradeItemData { id = "item-b" },
                quantity = 3
            });
            return caravan;
        }

        private static void EventChecks_UseTraveledDistanceAndStableTradeKey()
        {
            CheckEqual(
                2,
                TradeEventPreviewCalculator.CalculateCompletedEventCheckCount(500f, 0.5f, 100f),
                "Completed event checks");

            bool first = TradeEventPreviewCalculator.IsEventTriggered("trade-a", 2, 0.5f);
            bool replay = TradeEventPreviewCalculator.IsEventTriggered("trade-a", 2, 0.5f);
            Check(first == replay, "The same trade and check index must replay deterministically.");

            int seed = TradeEventPreviewCalculator.CalculateStableSeed("trade-a", 2, 7u);
            Check(seed != 0, "A deterministic event seed must not be zero.");
        }

        private static void RouteEventProcessor_SplitAndBatchProgressProduceSameResult()
        {
            CaravanData split = CreateEventTestCaravan();
            CaravanData batch = CreateEventTestCaravan();
            ND.Framework.SharedRouteDefinition route = CreateCombatRoute();

            split.progress01 = 0.4f;
            ND.Framework.TradeRouteEventProcessor.Process(split, route, "trade-replay", 100f, 1f);
            split.progress01 = 1f;
            ND.Framework.TradeRouteEventProcessor.Process(split, route, "trade-replay", 100f, 1f);

            batch.progress01 = 1f;
            ND.Framework.TradeRouteEventProcessor.Process(batch, route, "trade-replay", 100f, 1f);
            ND.Framework.TradeRouteEventProcessor.Process(batch, route, "trade-replay", 100f, 1f);

            CheckEqual(batch.runEventChecksProcessed, split.runEventChecksProcessed, "Replay check count");
            CheckEqual(batch.runEventsOccurred, split.runEventsOccurred, "Replay event count");
            CheckEqual(batch.runBattlesFought, split.runBattlesFought, "Replay battle count");
            CheckEqual(batch.runCargoLost, split.runCargoLost, "Replay cargo loss");
            CheckNear(batch.runFoodLost, split.runFoodLost, "Replay fodder loss");
            CheckEqual(batch.mercenaries.Count, split.mercenaries.Count, "Replay mercenary count");
            CheckEqual(5, split.runEventChecksProcessed, "Uncapped completed checks");
        }

        private static void RouteEventProcessor_DoesNotRecordUnsupportedEventAsCompleted()
        {
            CaravanData caravan = CreateEventTestCaravan();
            caravan.currentDistanceKm = 100f;
            caravan.progress01 = 1f;
            var route = new ND.Framework.SharedRouteDefinition
            {
                Id = "route-lucky",
                Events = new[]
                {
                    new ND.Framework.SharedRouteEventDefinition
                    {
                        Id = "lucky",
                        EventType = RouteEvent.Lucky
                    }
                }
            };

            ND.Framework.TradeRouteEventProcessor.Process(
                caravan,
                route,
                "trade-non-combat",
                100f,
                1f);

            CheckEqual(1, caravan.runEventChecksProcessed, "Unsupported event check is consumed");
            CheckEqual(0, caravan.runEventsOccurred, "Unsupported event is not recorded as completed");
            CheckEqual(0, caravan.runBattlesFought, "Unsupported event is not recorded as combat");

            var saved = new ND.Framework.CaravanSaveData
            {
                runEventsOccurred = -1,
                runBattlesFought = -1
            };
            ND.Framework.CaravanSaveDataMapper.Normalize(saved);
            CheckEqual(0, saved.runEventsOccurred, "Normalized route event count");
            CheckEqual(0, saved.runBattlesFought, "Normalized combat event count");
        }

        private static void RouteEventProcessor_ForcedCombatUsesTheRuntimeCombatPath()
        {
            CaravanData caravan = CreateEventTestCaravan();
            ND.Framework.SharedRouteDefinition route = CreateCombatRoute();

            bool processed = ND.Framework.TradeRouteEventProcessor.ProcessForced(
                caravan,
                route,
                "trade-force",
                "bandit");
            bool missing = ND.Framework.TradeRouteEventProcessor.ProcessForced(
                caravan,
                route,
                "trade-force",
                "missing");

            Check(processed, "Known combat event must be force processed.");
            Check(!missing, "Unknown event must not be reported as processed.");
            CheckEqual(1, caravan.runEventsOccurred, "Forced completed event count");
            CheckEqual(1, caravan.runBattlesFought, "Forced combat count");
            CheckEqual(0, caravan.runEventChecksProcessed, "Forced event bypasses distance checks");
        }

        private static CaravanData CreateEventTestCaravan()
        {
            CaravanData caravan = CreateTravelingCaravan(0f);
            caravan.currentDistanceKm = 500f;
            caravan.foodAmount = 1000;
            return caravan;
        }

        private static ND.Framework.SharedRouteDefinition CreateCombatRoute()
        {
            return new ND.Framework.SharedRouteDefinition
            {
                Id = "route-long",
                Events = new[]
                {
                    new ND.Framework.SharedRouteEventDefinition
                    {
                        Id = "bandit",
                        EventType = RouteEvent.Combat,
                        BanditCombatPower = 100,
                        CargoLootRate = 0.1f,
                        FodderLootRate = 0.1f
                    }
                }
            };
        }

        private static void RouteEventProcessor_SaveRoundTripPreservesLossAndPreventsReplay()
        {
            CaravanData source = CreateEventTestCaravan();
            ND.Framework.SharedRouteDefinition route = CreateCombatRoute();
            source.progress01 = 0.6f;
            ND.Framework.TradeRouteEventProcessor.Process(
                source,
                route,
                "trade-save-roundtrip",
                100f,
                1f);

            ND.Framework.CaravanSaveData saved = new ND.Framework.CaravanSaveData();
            ND.Framework.CaravanSaveDataMapper.CopyToSave(source, saved);
            CaravanData restored = ND.Framework.CaravanSaveDataMapper.ToRuntime(saved);

            CheckEqual(source.runEventChecksProcessed, restored.runEventChecksProcessed, "Saved event checks");
            CheckEqual(source.runEventsOccurred, restored.runEventsOccurred, "Saved event count");
            CheckEqual(source.runBattlesFought, restored.runBattlesFought, "Saved battle count");
            CheckEqual(source.runCargoLost, restored.runCargoLost, "Saved cargo loss");
            CheckNear(source.runFoodLost, restored.runFoodLost, "Saved fodder loss");
            CheckEqual(source.mercenaries.Count, restored.mercenaries.Count, "Saved mercenary count");
            CheckEqual(GetCargoCount(source), GetCargoCount(restored), "Saved remaining cargo");

            int checksBeforeReplay = restored.runEventChecksProcessed;
            int eventsBeforeReplay = restored.runEventsOccurred;
            int cargoLostBeforeReplay = restored.runCargoLost;
            float foodLostBeforeReplay = restored.runFoodLost;

            ND.Framework.TradeRouteEventProcessor.Process(
                restored,
                route,
                "trade-save-roundtrip",
                100f,
                1f);

            CheckEqual(checksBeforeReplay, restored.runEventChecksProcessed, "Restored replay checks");
            CheckEqual(eventsBeforeReplay, restored.runEventsOccurred, "Restored replay events");
            CheckEqual(cargoLostBeforeReplay, restored.runCargoLost, "Restored replay cargo");
            CheckNear(foodLostBeforeReplay, restored.runFoodLost, "Restored replay fodder");
        }

        private static int GetCargoCount(CaravanData caravan)
        {
            int total = 0;
            if (caravan?.cargo == null) return total;
            foreach (CargoEntry entry in caravan.cargo)
            {
                if (entry != null && entry.quantity > 0) total += entry.quantity;
            }
            return total;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void CheckEqual(int expected, int actual, string label)
        {
            if (expected != actual) throw new InvalidOperationException(
                label + ": expected " + expected + ", actual " + actual + ".");
        }

        private static void CheckNear(float expected, float actual, string label)
        {
            if (Math.Abs(expected - actual) > 0.0001f) throw new InvalidOperationException(
                label + ": expected " + expected + ", actual " + actual + ".");
        }
    }
}
