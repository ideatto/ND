#if UNITY_EDITOR
using System;
using UnityEditor;

namespace ND.Framework.EditorTests
{
    public static class WagonRepairDebugSettingsEditorTests
    {
        [MenuItem("ND/Tests/Run Wagon Repair Debug Settings Checks")]
        public static void RunAll()
        {
            try
            {
                var commands = new FrameworkDebugCommands(null);
                AssertEqual(1d, commands.ResetWagonRepairCostMultiplier(), "reset");
                AssertTrue(commands.TrySetWagonRepairCostMultiplier(2.5d), "valid set");
                AssertEqual(2.5d, commands.WagonRepairCostMultiplier, "read after set");
                AssertEqual(2.75d, commands.IncreaseWagonRepairCostMultiplier(), "increase");
                AssertEqual(2.5d, commands.DecreaseWagonRepairCostMultiplier(), "decrease");
                AssertFalse(commands.TrySetWagonRepairCostMultiplier(-0.01d), "negative set");
                AssertFalse(commands.TrySetWagonRepairCostMultiplier(1000.01d), "over maximum set");
                AssertFalse(commands.TrySetWagonRepairCostMultiplier(double.NaN), "NaN set");
                AssertFalse(commands.TrySetWagonRepairCostMultiplier(double.PositiveInfinity), "infinite set");
                AssertEqual(2.5d, commands.WagonRepairCostMultiplier, "invalid set preserves value");

                commands.TrySetWagonRepairCostMultiplier(0d);
                AssertEqual(0d, commands.DecreaseWagonRepairCostMultiplier(), "minimum clamp");
                commands.TrySetWagonRepairCostMultiplier(1000d);
                AssertEqual(1000d, commands.IncreaseWagonRepairCostMultiplier(), "maximum clamp");

                UnityEngine.Debug.Log("[WagonRepairDebugSettingsEditorTests] PASS");
            }
            finally
            {
                WagonRepairDebugSettings.ResetRepairCostMultiplier();
            }
        }

        private static void AssertEqual(double expected, double actual, string subject)
        {
            if (Math.Abs(expected - actual) > 0.000001d)
            {
                throw new InvalidOperationException(
                    $"{subject} failed. Expected: {expected}, Actual: {actual}.");
            }
        }

        private static void AssertTrue(bool value, string subject)
        {
            if (!value)
            {
                throw new InvalidOperationException($"{subject} failed. Expected true.");
            }
        }

        private static void AssertFalse(bool value, string subject)
        {
            if (value)
            {
                throw new InvalidOperationException($"{subject} failed. Expected false.");
            }
        }
    }
}
#endif
