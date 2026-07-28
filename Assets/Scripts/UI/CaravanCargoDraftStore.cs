using System;
using System.Collections.Generic;
using System.Linq;

namespace ND.Framework.CargoLoading
{
    /// <summary>
    /// Owns uncommitted market purchases per Caravan.
    /// UI panels are projections of this store and never become the durable draft owner.
    /// </summary>
    public static class CaravanCargoDraftStore
    {
        public sealed class Snapshot
        {
            public string MarketId { get; internal set; } = string.Empty;
            public string CaravanId { get; internal set; } = string.Empty;
            public int Revision { get; internal set; }
            public IReadOnlyDictionary<string, int> PurchaseQuantities { get; internal set; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, int>>> drafts =
            new Dictionary<string, Dictionary<string, Dictionary<string, int>>>(StringComparer.Ordinal);
        private static int revision;

        public static event Action<Snapshot> Changed;

        public static Snapshot GetSnapshot(string marketId, string caravanId)
        {
            string marketKey = Normalize(marketId);
            string caravanKey = Normalize(caravanId);
            Dictionary<string, int> quantities = null;
            if (drafts.TryGetValue(marketKey, out Dictionary<string, Dictionary<string, int>> byCaravan))
                byCaravan.TryGetValue(caravanKey, out quantities);

            return CreateSnapshot(marketKey, caravanKey, quantities);
        }

        public static int GetReservedByOtherCaravans(
            string marketId,
            string caravanId,
            string itemId)
        {
            string marketKey = Normalize(marketId);
            string caravanKey = Normalize(caravanId);
            string itemKey = Normalize(itemId);
            if (!drafts.TryGetValue(
                    marketKey,
                    out Dictionary<string, Dictionary<string, int>> byCaravan))
            {
                return 0;
            }

            return byCaravan
                .Where(pair => !string.Equals(pair.Key, caravanKey, StringComparison.Ordinal))
                .Sum(pair => pair.Value.TryGetValue(itemKey, out int quantity)
                    ? Math.Max(0, quantity)
                    : 0);
        }

        public static Snapshot SetPurchases(
            string marketId,
            string caravanId,
            IEnumerable<KeyValuePair<string, int>> quantities)
        {
            string marketKey = Normalize(marketId);
            string caravanKey = Normalize(caravanId);
            if (string.IsNullOrEmpty(marketKey) || string.IsNullOrEmpty(caravanKey))
                return CreateSnapshot(marketKey, caravanKey, null);

            Dictionary<string, int> normalized = (quantities
                    ?? Enumerable.Empty<KeyValuePair<string, int>>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                .GroupBy(pair => pair.Key.Trim(), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(pair => Math.Max(0, pair.Value)),
                    StringComparer.Ordinal);

            if (!drafts.TryGetValue(
                    marketKey,
                    out Dictionary<string, Dictionary<string, int>> byCaravan))
            {
                byCaravan =
                    new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
                drafts.Add(marketKey, byCaravan);
            }

            if (byCaravan.TryGetValue(caravanKey, out Dictionary<string, int> current)
                && DictionariesEqual(current, normalized))
            {
                return CreateSnapshot(marketKey, caravanKey, current);
            }

            if (normalized.Count == 0)
            {
                byCaravan.Remove(caravanKey);
                if (byCaravan.Count == 0)
                    drafts.Remove(marketKey);
            }
            else
            {
                byCaravan[caravanKey] = normalized;
            }

            revision = revision == int.MaxValue ? 1 : revision + 1;
            Snapshot snapshot = CreateSnapshot(marketKey, caravanKey, normalized);
            Changed?.Invoke(snapshot);
            return snapshot;
        }

        public static Snapshot Clear(string marketId, string caravanId)
        {
            return SetPurchases(
                marketId,
                caravanId,
                Enumerable.Empty<KeyValuePair<string, int>>());
        }

        internal static void ClearAllForTests()
        {
            drafts.Clear();
            revision = 0;
        }

        private static Snapshot CreateSnapshot(
            string marketId,
            string caravanId,
            IReadOnlyDictionary<string, int> quantities)
        {
            return new Snapshot
            {
                MarketId = marketId,
                CaravanId = caravanId,
                Revision = revision,
                PurchaseQuantities = quantities != null
                    ? new Dictionary<string, int>(quantities, StringComparer.Ordinal)
                    : new Dictionary<string, int>(StringComparer.Ordinal)
            };
        }

        private static bool DictionariesEqual(
            IReadOnlyDictionary<string, int> left,
            IReadOnlyDictionary<string, int> right)
        {
            return left.Count == right.Count
                && left.All(pair => right.TryGetValue(pair.Key, out int value)
                    && value == pair.Value);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
