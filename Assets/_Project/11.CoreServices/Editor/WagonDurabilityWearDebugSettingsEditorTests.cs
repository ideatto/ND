#if UNITY_EDITOR
using System;
using UnityEditor;

namespace ND.Framework.EditorTests
{
    public static class WagonDurabilityWearDebugSettingsEditorTests
    {
        [MenuItem("ND/Tests/Run Wagon Durability Wear Debug Settings Checks")]
        public static void RunAll()
        {
            try
            {
                AssertWear(1d, 10, "1x distance wear");
                AssertWear(0d, 0, "0x distance wear");
                AssertWear(0.5d, 5, "0.5x distance wear");
                AssertWear(2d, 20, "2x distance wear");
                AssertDepartureSnapshotIsFixed();
                AssertSegmentedProgressIsDeterministic();
                AssertEventLossIsUnaffected();
                AssertSaveRoundTripPreservesSnapshot();
                AssertInvalidValuesAreRejected();

                UnityEngine.Debug.Log("[WagonDurabilityWearDebugSettingsEditorTests] PASS");
            }
            finally
            {
                WagonDurabilityWearDebugSettings.ResetMultiplier();
            }
        }

        private static void AssertWear(double multiplier, int expectedLoss, string subject)
        {
            WagonDurabilityWearDebugSettings.TrySetMultiplier(multiplier);
            CaravanData caravan = Depart(100f);
            JourneyRunner.SetProgress(caravan, 1f);
            AssertEqual(100 - expectedLoss, caravan.currentDurability, subject);
        }

        private static void AssertDepartureSnapshotIsFixed()
        {
            WagonDurabilityWearDebugSettings.TrySetMultiplier(2d);
            CaravanData caravan = Depart(100f);
            WagonDurabilityWearDebugSettings.TrySetMultiplier(0d);
            JourneyRunner.SetProgress(caravan, 1f);

            AssertEqual(20, 100 - caravan.currentDurability, "departure snapshot");
            AssertEqual(2d, caravan.runDurabilityWearMultiplier, "captured multiplier");
        }

        private static void AssertSegmentedProgressIsDeterministic()
        {
            WagonDurabilityWearDebugSettings.TrySetMultiplier(0.5d);
            CaravanData single = Depart(100f);
            CaravanData segmented = Depart(100f);

            JourneyRunner.SetProgress(single, 1f);
            for (int step = 1; step <= 10; step++)
                JourneyRunner.SetProgress(segmented, step / 10f);

            AssertEqual(single.currentDurability, segmented.currentDurability, "segmented progress");
        }

        private static void AssertEventLossIsUnaffected()
        {
            WagonDurabilityWearDebugSettings.TrySetMultiplier(0d);
            CaravanData caravan = Depart(100f);
            JourneyRunner.ApplyDurabilityLoss(caravan, 7);

            AssertEqual(93, caravan.currentDurability, "event durability loss");
        }

        private static void AssertSaveRoundTripPreservesSnapshot()
        {
            WagonDurabilityWearDebugSettings.TrySetMultiplier(2d);
            CaravanData caravan = Depart(100f);
            var save = new CaravanSaveData();
            CaravanSaveDataMapper.CopyToSave(caravan, save);
            CaravanData restored = CaravanSaveDataMapper.ToRuntime(save);

            AssertEqual(2d, restored.runDurabilityWearMultiplier, "save round trip");
        }

        private static void AssertInvalidValuesAreRejected()
        {
            WagonDurabilityWearDebugSettings.ResetMultiplier();
            AssertFalse(WagonDurabilityWearDebugSettings.TrySetMultiplier(-0.01d), "negative multiplier");
            AssertFalse(WagonDurabilityWearDebugSettings.TrySetMultiplier(double.NaN), "NaN multiplier");
            AssertFalse(WagonDurabilityWearDebugSettings.TrySetMultiplier(double.PositiveInfinity), "infinite multiplier");
            AssertEqual(1d, WagonDurabilityWearDebugSettings.Multiplier, "invalid values preserve multiplier");
        }

        private static CaravanData Depart(float distanceKm)
        {
            var caravan = new CaravanData
            {
                wagon = new imsiWagonData
                {
                    maxLoad = 100f,
                    maxDurability = 100,
                    inventorySlotCount = 1,
                    minAnimals = 0,
                    maxAnimals = 0
                },
                currentDurability = 100,
                foodAmount = 100,
                starveGraceSeconds = float.MaxValue
            };
            caravan.cargo.Add(new CargoEntry
            {
                item = new imsiTradeItemData { weight = 0f, maxCount = 1 },
                quantity = 1
            });

            DepartureValidationResult departure = JourneyRunner.TryDepart(caravan, distanceKm);
            if (!departure.canDepart)
                throw new InvalidOperationException("Test caravan could not depart.");
            return caravan;
        }

        private static void AssertEqual(int expected, int actual, string subject)
        {
            if (expected != actual)
                throw new InvalidOperationException($"{subject} failed. Expected: {expected}, Actual: {actual}.");
        }

        private static void AssertEqual(double expected, double actual, string subject)
        {
            if (Math.Abs(expected - actual) > 0.000001d)
                throw new InvalidOperationException($"{subject} failed. Expected: {expected}, Actual: {actual}.");
        }

        private static void AssertFalse(bool value, string subject)
        {
            if (value)
                throw new InvalidOperationException($"{subject} failed. Expected false.");
        }
    }
}
#endif
