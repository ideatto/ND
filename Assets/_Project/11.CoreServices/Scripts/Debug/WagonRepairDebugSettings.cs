using System;

namespace ND.Framework
{
    /// <summary>
    /// Holds the session-only wagon repair cost multiplier selected from debug tools.
    /// The repair feature must explicitly consume this value when its calculation is connected.
    /// </summary>
    public static class WagonRepairDebugSettings
    {
        public const double DefaultRepairCostMultiplier = 1d;
        public const double MinimumRepairCostMultiplier = 0d;
        public const double MaximumRepairCostMultiplier = 1000d;
        public const double RepairCostMultiplierStep = 0.25d;

        private static double repairCostMultiplier = DefaultRepairCostMultiplier;

        public static double RepairCostMultiplier => repairCostMultiplier;

        public static bool TrySetRepairCostMultiplier(double multiplier)
        {
            if (double.IsNaN(multiplier)
                || double.IsInfinity(multiplier)
                || multiplier < MinimumRepairCostMultiplier
                || multiplier > MaximumRepairCostMultiplier)
            {
                return false;
            }

            repairCostMultiplier = multiplier;
            return true;
        }

        public static double IncreaseRepairCostMultiplier()
        {
            repairCostMultiplier = Math.Min(
                MaximumRepairCostMultiplier,
                repairCostMultiplier + RepairCostMultiplierStep);
            return repairCostMultiplier;
        }

        public static double DecreaseRepairCostMultiplier()
        {
            repairCostMultiplier = Math.Max(
                MinimumRepairCostMultiplier,
                repairCostMultiplier - RepairCostMultiplierStep);
            return repairCostMultiplier;
        }

        public static double ResetRepairCostMultiplier()
        {
            repairCostMultiplier = DefaultRepairCostMultiplier;
            return repairCostMultiplier;
        }
    }
}
