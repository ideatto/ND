using System;
using System.Collections.Generic;
using System.Text;

namespace ND.Framework
{
    public sealed class CaravanSavedCargoItem
    {
        public string ItemId { get; }
        public int Quantity { get; }
        public TradeItemSaveData SavedItem { get; }

        internal CaravanSavedCargoItem(string itemId, int quantity, TradeItemSaveData savedItem)
        {
            ItemId = itemId;
            Quantity = quantity;
            SavedItem = savedItem;
        }
    }

    public sealed class CaravanSavedCargoSnapshot
    {
        public IReadOnlyList<CaravanSavedCargoItem> Items { get; }
        public string BaselineSignature { get; }

        internal CaravanSavedCargoSnapshot(
            IReadOnlyList<CaravanSavedCargoItem> items,
            string baselineSignature)
        {
            Items = items;
            BaselineSignature = baselineSignature;
        }
    }

    /// <summary>
    /// Normalizes persisted cargo into one entry per item and produces a stable draft baseline.
    /// </summary>
    public sealed class CaravanSavedCargoService
    {
        public CaravanSavedCargoSnapshot CreateSnapshot(CaravanSaveData caravan)
        {
            if (caravan?.cargo == null || caravan.cargo.Count == 0)
            {
                return new CaravanSavedCargoSnapshot(
                    Array.Empty<CaravanSavedCargoItem>(),
                    string.Empty);
            }

            var order = new List<string>();
            var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
            var savedItems = new Dictionary<string, TradeItemSaveData>(StringComparer.Ordinal);
            for (int index = 0; index < caravan.cargo.Count; index++)
            {
                CargoEntrySaveData entry = caravan.cargo[index];
                string itemId = NormalizeId(entry?.item?.itemId);
                if (string.IsNullOrEmpty(itemId) || entry.quantity <= 0)
                    continue;

                if (!quantities.TryGetValue(itemId, out int quantity))
                {
                    order.Add(itemId);
                    savedItems.Add(itemId, entry.item);
                }
                quantities[itemId] = checked(quantity + entry.quantity);
            }

            var items = new CaravanSavedCargoItem[order.Count];
            for (int index = 0; index < order.Count; index++)
            {
                string itemId = order[index];
                items[index] = new CaravanSavedCargoItem(
                    itemId,
                    quantities[itemId],
                    savedItems[itemId]);
            }

            var sortedIds = new List<string>(quantities.Keys);
            sortedIds.Sort(StringComparer.Ordinal);
            var signature = new StringBuilder();
            for (int index = 0; index < sortedIds.Count; index++)
            {
                string itemId = sortedIds[index];
                signature.Append(itemId).Append(':').Append(quantities[itemId]).Append(';');
            }

            return new CaravanSavedCargoSnapshot(items, signature.ToString());
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }
}
