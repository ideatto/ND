using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Atomically transfers cargo from one caravan into the shared base-camp inventory.
    /// UI supplies an explicit caravan ID and never mutates either inventory list directly.
    /// </summary>
    public static class BaseCampInventoryTransferService
    {
        public const string ErrorInvalidFramework = "INVALID_FRAMEWORK";
        public const string ErrorInvalidCaravan = "INVALID_CARAVAN";
        public const string ErrorNotAtBaseCamp = "NOT_AT_BASE_CAMP";
        public const string ErrorCaravanBusy = "CARAVAN_BUSY";
        public const string ErrorInvalidItem = "INVALID_ITEM";
        public const string ErrorInsufficientCargo = "INSUFFICIENT_CARGO";
        public const string ErrorSaveFailed = "SAVE_FAILED";

        public static bool TryDeposit(
            SaveData saveData,
            ISaveService saveService,
            string caravanId,
            string baseTownId,
            string itemId,
            int quantity,
            out string error)
        {
            error = string.Empty;
            if (saveData == null || saveService == null || saveData.player == null)
                return Fail(ErrorInvalidFramework, out error);
            if (!SaveDataLookup.TryGetCaravan(saveData, caravanId, out CaravanSaveData caravan))
                return Fail(ErrorInvalidCaravan, out error);
            if (string.IsNullOrWhiteSpace(baseTownId)
                || !string.Equals(caravan.currentTownId, baseTownId, StringComparison.Ordinal))
                return Fail(ErrorNotAtBaseCamp, out error);
            if (SaveDataLookup.TryGetTradeProgress(saveData, caravanId, out TradeProgressSaveData progress)
                && (progress.state == TradeProgressState.Traveling
                    || progress.state == TradeProgressState.SettlementPending))
                return Fail(ErrorCaravanBusy, out error);
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
                return Fail(ErrorInvalidItem, out error);

            caravan.cargo ??= new List<CargoEntrySaveData>();
            saveData.player.homeInventory ??= new List<CargoEntrySaveData>();
            if (GetQuantity(caravan.cargo, itemId) < quantity)
                return Fail(ErrorInsufficientCargo, out error);

            string snapshot = JsonUtility.ToJson(saveData);
            TradeItemSaveData item = FindItem(caravan.cargo, itemId);
            RemoveQuantity(caravan.cargo, itemId, quantity);
            AddQuantity(saveData.player.homeInventory, item, quantity);

            SaveResult saveResult = saveService.Save(saveData);
            if (saveResult == null || !saveResult.Succeeded)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                return Fail(ErrorSaveFailed, out error);
            }

            return true;
        }

        private static int GetQuantity(List<CargoEntrySaveData> entries, string itemId)
        {
            int total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                CargoEntrySaveData entry = entries[i];
                if (entry?.item != null && entry.item.itemId == itemId && entry.quantity > 0)
                    total += entry.quantity;
            }
            return total;
        }

        private static TradeItemSaveData FindItem(List<CargoEntrySaveData> entries, string itemId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                CargoEntrySaveData entry = entries[i];
                if (entry?.item != null && entry.item.itemId == itemId && entry.quantity > 0)
                    return entry.item;
            }
            return null;
        }

        private static void RemoveQuantity(List<CargoEntrySaveData> entries, string itemId, int quantity)
        {
            for (int i = entries.Count - 1; i >= 0 && quantity > 0; i--)
            {
                CargoEntrySaveData entry = entries[i];
                if (entry?.item == null || entry.item.itemId != itemId || entry.quantity <= 0)
                    continue;

                int removed = Math.Min(entry.quantity, quantity);
                entry.quantity -= removed;
                quantity -= removed;
                if (entry.quantity <= 0)
                    entries.RemoveAt(i);
            }
        }

        private static void AddQuantity(
            List<CargoEntrySaveData> entries,
            TradeItemSaveData item,
            int quantity)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                CargoEntrySaveData entry = entries[i];
                if (entry?.item == null || entry.item.itemId != item.itemId)
                    continue;

                entry.quantity += quantity;
                return;
            }

            entries.Add(new CargoEntrySaveData
            {
                item = item,
                quantity = quantity
            });
        }

        private static bool Fail(string failure, out string error)
        {
            error = failure;
            return false;
        }
    }
}
