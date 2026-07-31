using System;

namespace ND.Framework
{
    /// <summary>
    /// Resolves one stable disaster ID from the save-backed world seed and absolute month.
    /// </summary>
    public class MonthlyDisasterResolver
    {
        public const string NoneId = "";
        public const string FloodId = "flood";
        public const string DroughtId = "drought";
        internal const string OccurrenceDomain = "monthly_disaster_occur";

        public virtual string Resolve(
            uint worldSeed,
            long absoluteMonthIndex,
            GameSeason season,
            MonthlyDisasterPolicy policy)
        {
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            switch (season)
            {
                case GameSeason.Spring:
                case GameSeason.Autumn:
                    return NoneId;
                case GameSeason.Summer:
                    return Occurs(worldSeed, absoluteMonthIndex, policy.SummerFloodChance)
                        ? FloodId
                        : NoneId;
                case GameSeason.Winter:
                    return Occurs(worldSeed, absoluteMonthIndex, policy.WinterDroughtChance)
                        ? DroughtId
                        : NoneId;
                default:
                    throw new ArgumentOutOfRangeException(nameof(season));
            }
        }

        private static bool Occurs(uint worldSeed, long absoluteMonthIndex, float chance)
        {
            if (chance <= 0f)
            {
                return false;
            }

            if (chance >= 1f)
            {
                return true;
            }

            var roll = ComputeOccurrenceHash(worldSeed, absoluteMonthIndex)
                / ((double)uint.MaxValue + 1d);
            return roll < chance;
        }

        internal static uint ComputeOccurrenceHash(uint worldSeed, long absoluteMonthIndex)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            var hash = offset;
            AppendUInt32(ref hash, worldSeed, prime);
            AppendAscii(ref hash, OccurrenceDomain, prime);
            AppendInt64(ref hash, absoluteMonthIndex, prime);
            return hash;
        }

        private static void AppendUInt32(ref uint hash, uint value, uint prime)
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= prime;
            }
        }

        private static void AppendInt64(ref uint hash, long value, uint prime)
        {
            var bits = unchecked((ulong)value);
            for (var shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(bits >> shift);
                hash *= prime;
            }
        }

        private static void AppendAscii(ref uint hash, string value, uint prime)
        {
            for (var index = 0; index < value.Length; index++)
            {
                hash ^= (byte)value[index];
                hash *= prime;
            }
        }
    }
}
