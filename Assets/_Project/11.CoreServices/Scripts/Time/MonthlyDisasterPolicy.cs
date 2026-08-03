using System;

namespace ND.Framework
{
    /// <summary>
    /// Stores normalized seasonal probabilities for deterministic monthly disasters.
    /// </summary>
    [Serializable]
    public sealed class MonthlyDisasterPolicy
    {
        public const float DefaultSummerFloodChance = 0.25f;
        public const float DefaultWinterDroughtChance = 0.25f;

        public MonthlyDisasterPolicy(
            float summerFloodChance = DefaultSummerFloodChance,
            float winterDroughtChance = DefaultWinterDroughtChance)
        {
            SummerFloodChance = NormalizeChance(summerFloodChance);
            WinterDroughtChance = NormalizeChance(winterDroughtChance);
        }

        public float SummerFloodChance { get; }
        public float WinterDroughtChance { get; }

        private static float NormalizeChance(float chance)
        {
            if (float.IsNaN(chance) || chance <= 0f)
            {
                return 0f;
            }

            return chance >= 1f ? 1f : chance;
        }
    }
}
