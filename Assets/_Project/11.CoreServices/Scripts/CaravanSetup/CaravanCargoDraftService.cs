using System;
using System.Collections.Generic;

namespace ND.Framework
{
    public sealed class CaravanCargoDraftItem
    {
        public string ItemId { get; }
        public int Quantity { get; }

        public CaravanCargoDraftItem(string itemId, int quantity)
        {
            ItemId = NormalizeId(itemId);
            Quantity = Math.Max(0, quantity);
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }

    public sealed class CaravanCargoDraftSnapshot
    {
        public string CaravanId { get; }
        public string SaveBaseline { get; }
        public IReadOnlyList<CaravanCargoDraftItem> Items { get; }

        internal CaravanCargoDraftSnapshot(
            string caravanId,
            string saveBaseline,
            IReadOnlyList<CaravanCargoDraftItem> items)
        {
            CaravanId = caravanId;
            SaveBaseline = saveBaseline;
            Items = items;
        }
    }

    /// <summary>
    /// Owns detached S4 load plans per persistent caravanId. A draft is valid only while the
    /// committed SaveData cargo still matches the baseline captured when the draft was written.
    /// </summary>
    public sealed class CaravanCargoDraftService
    {
        private sealed class DraftState
        {
            public string saveBaseline = string.Empty;
            public readonly List<CaravanCargoDraftItem> items = new List<CaravanCargoDraftItem>();
        }

        private readonly Dictionary<string, DraftState> drafts =
            new Dictionary<string, DraftState>(StringComparer.Ordinal);

        public bool Set(
            string caravanId,
            string saveBaseline,
            IReadOnlyList<CaravanCargoDraftItem> items)
        {
            string normalizedCaravanId = NormalizeId(caravanId);
            if (string.IsNullOrEmpty(normalizedCaravanId))
                return false;

            var next = new DraftState
            {
                saveBaseline = saveBaseline ?? string.Empty
            };
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            if (items != null)
            {
                for (int index = 0; index < items.Count; index++)
                {
                    CaravanCargoDraftItem item = items[index];
                    if (item == null
                        || string.IsNullOrEmpty(item.ItemId)
                        || item.Quantity <= 0
                        || !itemIds.Add(item.ItemId))
                    {
                        return false;
                    }

                    next.items.Add(new CaravanCargoDraftItem(item.ItemId, item.Quantity));
                }
            }

            drafts[normalizedCaravanId] = next;
            return true;
        }

        public bool TryGet(string caravanId, out CaravanCargoDraftSnapshot snapshot)
        {
            string normalizedCaravanId = NormalizeId(caravanId);
            if (!drafts.TryGetValue(normalizedCaravanId, out DraftState state))
            {
                snapshot = null;
                return false;
            }

            snapshot = CreateSnapshot(normalizedCaravanId, state);
            return true;
        }

        public bool TryGetCompatible(
            string caravanId,
            string currentSaveBaseline,
            out CaravanCargoDraftSnapshot snapshot)
        {
            if (!TryGet(caravanId, out snapshot))
                return false;

            if (string.Equals(
                    snapshot.SaveBaseline,
                    currentSaveBaseline ?? string.Empty,
                    StringComparison.Ordinal))
            {
                return true;
            }

            Clear(caravanId);
            snapshot = null;
            return false;
        }

        public void Clear(string caravanId)
        {
            drafts.Remove(NormalizeId(caravanId));
        }

        public void ClearAll()
        {
            drafts.Clear();
        }

        private static CaravanCargoDraftSnapshot CreateSnapshot(
            string caravanId,
            DraftState state)
        {
            var items = new CaravanCargoDraftItem[state.items.Count];
            for (int index = 0; index < state.items.Count; index++)
            {
                CaravanCargoDraftItem item = state.items[index];
                items[index] = new CaravanCargoDraftItem(item.ItemId, item.Quantity);
            }

            return new CaravanCargoDraftSnapshot(caravanId, state.saveBaseline, items);
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }
}
