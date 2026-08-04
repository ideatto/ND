using System;
using System.Collections.Generic;
using System.Text;

namespace ND.Framework
{
    /// <summary>
    /// SaveData에서 읽은 실제 Cargo 항목이다. UI 편집 초안이나 시장 예약 수량을 포함하지 않는다.
    /// </summary>
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

    /// <summary>
    /// 편집 시작 시점의 저장 Cargo와 변경 감지 서명을 함께 보존한다.
    /// 서명은 다른 UI가 같은 Caravan Cargo를 갱신했는지 판단하는 낙관적 경계로 사용한다.
    /// </summary>
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
    /// 저장 항목에 카탈로그 정의를 선택적으로 결합한 표시 모델이다.
    /// 현재 시장에 없는 상품도 SavedItem을 유지하여 Cargo UI에서 누락되지 않게 한다.
    /// </summary>
    public sealed class CaravanSavedCargoPresentationItem
    {
        public string ItemId { get; }
        public int Quantity { get; }
        public TradeItemSaveData SavedItem { get; }
        public SharedTradeItemDefinition Definition { get; }

        internal CaravanSavedCargoPresentationItem(
            CaravanSavedCargoItem item,
            SharedTradeItemDefinition definition)
        {
            ItemId = item.ItemId;
            Quantity = item.Quantity;
            SavedItem = item.SavedItem;
            Definition = definition;
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

        public IReadOnlyList<CaravanSavedCargoPresentationItem> CreatePresentation(
            CaravanSaveData caravan,
            ISharedGameDataProvider sharedGameData)
        {
            CaravanSavedCargoSnapshot snapshot = CreateSnapshot(caravan);
            if (snapshot.Items.Count == 0)
                return Array.Empty<CaravanSavedCargoPresentationItem>();

            var result = new CaravanSavedCargoPresentationItem[snapshot.Items.Count];
            for (int index = 0; index < snapshot.Items.Count; index++)
            {
                CaravanSavedCargoItem item = snapshot.Items[index];
                SharedTradeItemDefinition definition = null;
                if (sharedGameData != null && sharedGameData.IsLoaded)
                    sharedGameData.TryGetTradeItem(item.ItemId, out definition);

                result[index] = new CaravanSavedCargoPresentationItem(item, definition);
            }

            return result;
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }
}
