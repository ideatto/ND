using System;

namespace ND.Framework
{
    /// <summary>
    /// Holds the session-only distance wear multiplier captured by a caravan at departure.
    /// Event-driven durability loss does not consume this value.
    /// </summary>
    public static class WagonDurabilityWearDebugSettings
    {
        public const double DefaultMultiplier = 1d;
        public const double MinimumMultiplier = 0d;
        public const double MaximumMultiplier = 1000d;
        public const double MultiplierStep = 0.25d;

        private static double multiplier = DefaultMultiplier;

        public static double Multiplier => multiplier;

        public static bool TrySetMultiplier(double value)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < MinimumMultiplier
                || value > MaximumMultiplier)
            {
                return false;
            }

            multiplier = value;
            return true;
        }

        public static double IncreaseMultiplier()
        {
            multiplier = Math.Min(MaximumMultiplier, multiplier + MultiplierStep);
            return multiplier;
        }

        public static double DecreaseMultiplier()
        {
            multiplier = Math.Max(MinimumMultiplier, multiplier - MultiplierStep);
            return multiplier;
        }

        public static double ResetMultiplier()
        {
            multiplier = DefaultMultiplier;
            return multiplier;
        }
    }
}
