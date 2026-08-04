using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>Defines which persisted inventory is the source and which is the destination.</summary>
    public enum WarehouseTransferDirection { HomeToCargo, CargoToHome }
    /// <summary>Stable failure reasons that UI maps to localized Notice messages.</summary>
    public enum WarehouseTransferFailure
    {
        None, InvalidFramework, InvalidCaravan, NotAtBaseCamp, CaravanBusy,
        InvalidItem, InsufficientSource, WarehouseFull, CargoFull, CargoOverweight, SaveFailed
    }

    /// <summary>
    /// A UI aggregation key derived from itemId and purchaseUnitPrice. This is deliberately
    /// not a persistent lot identity; equal-price acquisitions are displayed and moved together.
    /// </summary>
    public readonly struct WarehousePriceGroup
    {
        public WarehousePriceGroup(string itemId, long purchaseUnitPrice, int quantity)
        {
            ItemId = itemId ?? string.Empty;
            PurchaseUnitPrice = Math.Max(0L, purchaseUnitPrice);
            Quantity = Math.Max(0, quantity);
        }
        public string ItemId { get; }
        public long PurchaseUnitPrice { get; }
        public int Quantity { get; }
    }

    /// <summary>
    /// Immutable transfer intent. Explicit caravan identity prevents UI selection state from
    /// silently redirecting a transfer to another Caravan.
    /// </summary>
    public readonly struct WarehouseTransferRequest
    {
        public WarehouseTransferRequest(string caravanId, string baseTownId, string itemId,
            long purchaseUnitPrice, int quantity, WarehouseTransferDirection direction,
            int warehouseSlotLimit, int cargoSlotLimit, float cargoWeightLimit)
        {
            CaravanId = caravanId ?? string.Empty;
            BaseTownId = baseTownId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            PurchaseUnitPrice = Math.Max(0L, purchaseUnitPrice);
            Quantity = quantity;
            Direction = direction;
            WarehouseSlotLimit = Math.Max(0, warehouseSlotLimit);
            CargoSlotLimit = Math.Max(0, cargoSlotLimit);
            CargoWeightLimit = Math.Max(0f, cargoWeightLimit);
        }
        public string CaravanId { get; }
        public string BaseTownId { get; }
        public string ItemId { get; }
        public long PurchaseUnitPrice { get; }
        public int Quantity { get; }
        public WarehouseTransferDirection Direction { get; }
        public int WarehouseSlotLimit { get; }
        public int CargoSlotLimit { get; }
        public float CargoWeightLimit { get; }
    }

    /// <summary>Maximum currently valid quantity plus the reason for an unavailable transfer.</summary>
    public readonly struct WarehouseTransferCapacity
    {
        public WarehouseTransferCapacity(int maxTransfer, WarehouseTransferFailure failure)
        { MaxTransfer = Math.Max(0, maxTransfer); Failure = failure; }
        public int MaxTransfer { get; }
        public WarehouseTransferFailure Failure { get; }
    }

    /// <summary>Builds read-only price rows without owning or mutating SaveData.</summary>
    public static class WarehousePriceGroupResolver
    {
        public static IReadOnlyList<WarehousePriceGroup> Resolve(
            IEnumerable<CargoEntrySaveData> entries, string itemId)
        {
            if (entries == null || string.IsNullOrWhiteSpace(itemId))
                return Array.Empty<WarehousePriceGroup>();

            return entries
                .Where(e => e?.item != null && e.quantity > 0 &&
                            string.Equals(e.item.itemId, itemId, StringComparison.Ordinal))
                .GroupBy(e => Math.Max(0L, e.item.purchaseUnitPrice))
                .Select(g => new WarehousePriceGroup(itemId, g.Key, g.Sum(e => e.quantity)))
                .OrderBy(g => g.PurchaseUnitPrice)
                .ToArray();
        }
    }

    /// <summary>
    /// Pure capacity rules shared by quantity preview and final confirmation. Slot usage is
    /// itemId-based; purchase price affects transfer identity but never consumes extra slots.
    /// </summary>
    public static class WarehouseTransferCapacityCalculator
    {
        public static WarehouseTransferCapacity Calculate(
            SaveData saveData, CaravanSaveData caravan, WarehouseTransferRequest request)
        {
            if (saveData?.player == null || caravan == null)
                return new WarehouseTransferCapacity(0, WarehouseTransferFailure.InvalidFramework);

            List<CargoEntrySaveData> source = request.Direction == WarehouseTransferDirection.HomeToCargo
                ? saveData.player.homeInventory : caravan.cargo;
            List<CargoEntrySaveData> target = request.Direction == WarehouseTransferDirection.HomeToCargo
                ? caravan.cargo : saveData.player.homeInventory;
            int available = GroupQuantity(source, request.ItemId, request.PurchaseUnitPrice);
            if (available <= 0)
                return new WarehouseTransferCapacity(0, WarehouseTransferFailure.InsufficientSource);

            TradeItemSaveData item = FindGroupItem(source, request.ItemId, request.PurchaseUnitPrice);
            if (item == null || item.maxCount <= 0 || item.weight < 0f)
                return new WarehouseTransferCapacity(0, WarehouseTransferFailure.InvalidItem);

            int slotLimit = request.Direction == WarehouseTransferDirection.HomeToCargo
                ? request.CargoSlotLimit : request.WarehouseSlotLimit;
            int bySlots = MaxAdditionalBySlots(target, item, slotLimit);
            if (bySlots <= 0)
                return new WarehouseTransferCapacity(0, request.Direction == WarehouseTransferDirection.HomeToCargo
                    ? WarehouseTransferFailure.CargoFull : WarehouseTransferFailure.WarehouseFull);

            int max = Math.Min(available, bySlots);
            if (request.Direction == WarehouseTransferDirection.HomeToCargo)
            {
                float currentWeight = CargoWeight(caravan);
                float remaining = request.CargoWeightLimit - currentWeight;
                int byWeight = item.weight <= 0f ? int.MaxValue : Math.Max(0, (int)Math.Floor(remaining / item.weight));
                max = Math.Min(max, byWeight);
                if (max <= 0)
                    return new WarehouseTransferCapacity(0, WarehouseTransferFailure.CargoOverweight);
            }
            return new WarehouseTransferCapacity(max, WarehouseTransferFailure.None);
        }

        internal static int GroupQuantity(IEnumerable<CargoEntrySaveData> entries, string id, long price)
            => entries?.Where(e => e?.item != null && e.quantity > 0 &&
                e.item.itemId == id && Math.Max(0L, e.item.purchaseUnitPrice) == Math.Max(0L, price))
                .Sum(e => e.quantity) ?? 0;

        internal static TradeItemSaveData FindGroupItem(IEnumerable<CargoEntrySaveData> entries, string id, long price)
            => entries?.FirstOrDefault(e => e?.item != null && e.quantity > 0 &&
                e.item.itemId == id && Math.Max(0L, e.item.purchaseUnitPrice) == Math.Max(0L, price))?.item;

private static int MaxAdditionalBySlots(
            IEnumerable<CargoEntrySaveData> entries,
            TradeItemSaveData item,
            int slotLimit)
        {
            if (slotLimit <= 0)
                return 0;

            // Materialize once because callers may provide a one-shot enumerable. Physical
            // slots intentionally aggregate every purchase-price group by itemId.
            List<CargoEntrySaveData> validEntries = (entries ?? Enumerable.Empty<CargoEntrySaveData>())
                .Where(entry => entry?.item != null && entry.quantity > 0)
                .ToList();
            Dictionary<string, int> quantities = validEntries
                .GroupBy(entry => entry.item.itemId ?? string.Empty)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.quantity), StringComparer.Ordinal);

            int usedByOtherItems = 0;
            foreach (KeyValuePair<string, int> pair in quantities)
            {
                if (string.Equals(pair.Key, item.itemId, StringComparison.Ordinal))
                    continue;

                TradeItemSaveData sample = validEntries.First(entry =>
                    string.Equals(entry.item.itemId, pair.Key, StringComparison.Ordinal)).item;
                usedByOtherItems += DivideCeiling(pair.Value, Math.Max(1, sample.maxCount));
            }

            int slotsAvailableForItem = Math.Max(0, slotLimit - usedByOtherItems);
            int existingQuantity = quantities.TryGetValue(item.itemId ?? string.Empty, out int quantity)
                ? quantity
                : 0;
            return Math.Max(0,
                checked(slotsAvailableForItem * Math.Max(1, item.maxCount) - existingQuantity));
        }

        private static float CargoWeight(CaravanSaveData caravan)
        {
            double total = Math.Max(0, caravan.foodAmount) * Math.Max(0f, caravan.foodUnitWeight);
            if (caravan.cargo != null)
                foreach (CargoEntrySaveData e in caravan.cargo)
                    if (e?.item != null && e.quantity > 0)
                        total += Math.Max(0f, e.item.weight) * e.quantity;
            return total >= float.MaxValue ? float.PositiveInfinity : (float)total;
        }

        private static int DivideCeiling(int value, int divisor) => value <= 0 ? 0 : (value - 1) / divisor + 1;
    }

    /// <summary>
    /// The only Warehouse authority allowed to mutate both inventories, persist them, roll back
    /// a failed save, and publish refresh events after success.
    /// </summary>
    public static class WarehouseInventoryTransferService
    {
        public static bool TryTransfer(SaveData saveData, ISaveService saveService,
            WarehouseTransferRequest request, out WarehouseTransferFailure failure)
        {
            failure = WarehouseTransferFailure.None;
            if (saveData?.player == null || saveService == null)
                return Fail(WarehouseTransferFailure.InvalidFramework, out failure);
            CaravanSlotValidationResult slotValidation = CaravanSlotValidation.Validate(saveData.caravans);
            if (!slotValidation.TryGetCaravan(request.CaravanId, out CaravanSaveData caravan))
                return Fail(WarehouseTransferFailure.InvalidCaravan, out failure);
            if (!string.Equals(request.BaseTownId, WarehouseFunction.BaseTownId, StringComparison.Ordinal)
                || !string.Equals(saveData.player.currentTownId, WarehouseFunction.BaseTownId, StringComparison.Ordinal)
                || !string.Equals(caravan.currentTownId, WarehouseFunction.BaseTownId, StringComparison.Ordinal))
                return Fail(WarehouseTransferFailure.NotAtBaseCamp, out failure);
            if (caravan.state != JourneyState.Prepare)
                return Fail(WarehouseTransferFailure.CaravanBusy, out failure);
            if (SaveDataLookup.TryGetTradeProgress(saveData, request.CaravanId, out TradeProgressSaveData progress) &&
                (progress.state == TradeProgressState.Traveling ||
                 progress.state == TradeProgressState.SettlementPending))
                return Fail(WarehouseTransferFailure.CaravanBusy, out failure);
            if (string.IsNullOrWhiteSpace(request.ItemId) || request.Quantity <= 0)
                return Fail(WarehouseTransferFailure.InvalidItem, out failure);

            saveData.player.homeInventory ??= new List<CargoEntrySaveData>();
            caravan.cargo ??= new List<CargoEntrySaveData>();
            WarehouseTransferCapacity capacity = WarehouseTransferCapacityCalculator.Calculate(saveData, caravan, request);
            if (capacity.Failure != WarehouseTransferFailure.None || request.Quantity > capacity.MaxTransfer)
                return Fail(capacity.Failure == WarehouseTransferFailure.None
                    ? WarehouseTransferFailure.InsufficientSource : capacity.Failure, out failure);

            List<CargoEntrySaveData> source = request.Direction == WarehouseTransferDirection.HomeToCargo
                ? saveData.player.homeInventory : caravan.cargo;
            List<CargoEntrySaveData> target = request.Direction == WarehouseTransferDirection.HomeToCargo
                ? caravan.cargo : saveData.player.homeInventory;
            TradeItemSaveData item = WarehouseTransferCapacityCalculator.FindGroupItem(
                source, request.ItemId, request.PurchaseUnitPrice);
            // Snapshot the complete object because both source and destination must roll back atomically.
            string snapshot = JsonUtility.ToJson(saveData);
            RemoveGroup(source, request.ItemId, request.PurchaseUnitPrice, request.Quantity);
            AddGroup(target, CloneItem(item), request.Quantity);

            try
            {
                SaveResult result = saveService.Save(saveData);
                if (result == null || !result.Succeeded)
                {
                    JsonUtility.FromJsonOverwrite(snapshot, saveData);
                    return Fail(WarehouseTransferFailure.SaveFailed, out failure);
                }
            }
            catch (Exception)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                return Fail(WarehouseTransferFailure.SaveFailed, out failure);
            }

            // Events are invalidation signals, not data payloads; subscribers re-read the saved state.
            FrameworkEvents.RaiseHomeInventoryChanged();
            FrameworkEvents.RaiseCaravanCargoChanged(
                request.CaravanId,
                CaravanCargoChangeSource.WarehouseTransfer);
            return true;
        }

        private static void RemoveGroup(List<CargoEntrySaveData> entries, string id, long price, int quantity)
        {
            for (int i = entries.Count - 1; i >= 0 && quantity > 0; i--)
            {
                CargoEntrySaveData e = entries[i];
                if (e?.item == null || e.item.itemId != id ||
                    Math.Max(0L, e.item.purchaseUnitPrice) != Math.Max(0L, price)) continue;
                int removed = Math.Min(quantity, Math.Max(0, e.quantity));
                e.quantity -= removed;
                quantity -= removed;
                if (e.quantity <= 0) entries.RemoveAt(i);
            }
        }

        private static void AddGroup(List<CargoEntrySaveData> entries, TradeItemSaveData item, int quantity)
        {
            CargoEntrySaveData e = entries.FirstOrDefault(x => x?.item != null &&
                x.item.itemId == item.itemId &&
                Math.Max(0L, x.item.purchaseUnitPrice) == Math.Max(0L, item.purchaseUnitPrice));
            if (e == null)
            {
                entries.Add(new CargoEntrySaveData { item = item, quantity = quantity });
                return;
            }
            e.quantity = checked(e.quantity + quantity);
        }

        private static TradeItemSaveData CloneItem(TradeItemSaveData source) => new TradeItemSaveData
        {
            itemId = source.itemId, itemName = source.itemName, weight = source.weight,
            purchaseUnitPrice = source.purchaseUnitPrice, basePrice = source.basePrice, maxCount = source.maxCount
        };

        private static bool Fail(WarehouseTransferFailure reason, out WarehouseTransferFailure failure)
        { failure = reason; return false; }
    }
}