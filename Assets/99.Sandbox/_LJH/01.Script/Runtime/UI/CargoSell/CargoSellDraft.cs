using System;
using System.Collections.Generic;
using System.Linq;

namespace ND.UI.CargoSell
{
    /// <summary>
    /// Owns only the transient arrival-sale selection. A purchase-price group is the identity
    /// boundary, so equal item IDs bought at different prices never overwrite one another.
    /// </summary>
    public sealed class CargoSellDraft
    {
        private readonly Dictionary<GroupKey, CargoSellPendingSaleRowViewData> entries =
            new Dictionary<GroupKey, CargoSellPendingSaleRowViewData>();

        public int Count => entries.Count;
        public bool IsEmpty => entries.Count == 0;

        public bool SetQuantity(CargoSellCargoItemViewData cargo, int quantity)
        {
            if (cargo == null || string.IsNullOrWhiteSpace(cargo.itemId)
                || quantity < 0 || quantity > Math.Max(0, cargo.cargoQuantity))
                return false;

            var key = new GroupKey(cargo.itemId, cargo.purchaseUnitPrice);
            if (quantity == 0)
            {
                entries.Remove(key);
                return true;
            }

            entries[key] = new CargoSellPendingSaleRowViewData
            {
                ItemId = key.ItemId,
                PurchaseUnitPrice = key.PurchaseUnitPrice,
                DisplayName = cargo.displayName ?? string.Empty,
                Icon = cargo.icon,
                Quantity = quantity,
                SellUnitPrice = Math.Max(0L, cargo.sellUnitPrice)
            };
            return true;
        }

        public bool Remove(string itemId, long purchaseUnitPrice) =>
            entries.Remove(new GroupKey(itemId, purchaseUnitPrice));

        public int GetQuantity(string itemId, long purchaseUnitPrice)
        {
            return entries.TryGetValue(new GroupKey(itemId, purchaseUnitPrice), out var entry)
                ? Math.Max(0, entry.Quantity)
                : 0;
        }

        public void Clear() => entries.Clear();

        public void Restore(
            IEnumerable<CargoSellPendingSaleRowViewData> source,
            IEnumerable<CargoSellCargoItemViewData> cargo)
        {
            entries.Clear();
            Dictionary<GroupKey, CargoSellCargoItemViewData> available =
                (cargo ?? Enumerable.Empty<CargoSellCargoItemViewData>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.itemId))
                .GroupBy(item => new GroupKey(item.itemId, item.purchaseUnitPrice))
                .ToDictionary(group => group.Key, group => group.First());

            foreach (CargoSellPendingSaleRowViewData pending in
                source ?? Enumerable.Empty<CargoSellPendingSaleRowViewData>())
            {
                if (pending == null) continue;
                var key = new GroupKey(pending.ItemId, pending.PurchaseUnitPrice);
                if (!available.TryGetValue(key, out CargoSellCargoItemViewData item)) continue;
                SetQuantity(item, Math.Min(Math.Max(0, pending.Quantity), Math.Max(0, item.cargoQuantity)));
            }
        }

        public CargoSellPendingSaleRowViewData[] Snapshot()
        {
            return entries.Values
                .OrderBy(entry => entry.ItemId, StringComparer.Ordinal)
                .ThenBy(entry => entry.PurchaseUnitPrice)
                .Select(Clone)
                .ToArray();
        }

        private static CargoSellPendingSaleRowViewData Clone(CargoSellPendingSaleRowViewData source)
        {
            return new CargoSellPendingSaleRowViewData
            {
                ItemId = source.ItemId,
                PurchaseUnitPrice = source.PurchaseUnitPrice,
                DisplayName = source.DisplayName,
                Icon = source.Icon,
                Quantity = source.Quantity,
                SellUnitPrice = source.SellUnitPrice
            };
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public GroupKey(string itemId, long purchaseUnitPrice)
            {
                ItemId = itemId?.Trim() ?? string.Empty;
                PurchaseUnitPrice = Math.Max(0L, purchaseUnitPrice);
            }

            public string ItemId { get; }
            public long PurchaseUnitPrice { get; }

            public bool Equals(GroupKey other) =>
                string.Equals(ItemId, other.ItemId, StringComparison.Ordinal)
                && PurchaseUnitPrice == other.PurchaseUnitPrice;

            public override bool Equals(object obj) => obj is GroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(ItemId) * 397)
                        ^ PurchaseUnitPrice.GetHashCode();
                }
            }
        }
    }
}
