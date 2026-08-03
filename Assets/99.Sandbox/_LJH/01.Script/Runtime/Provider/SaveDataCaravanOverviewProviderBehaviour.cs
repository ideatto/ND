using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeProgressSaveData = ND.Framework.TradeProgressSaveData;
using FrameworkTradeProgressState = ND.Framework.TradeProgressState;

/// <summary>
/// Adapts Framework SaveData into immutable Caravan Overview snapshots owned by the UI layer.
/// </summary>
/// <remarks>
/// This production-facing provider never falls back to fixture Caravans. Until Framework SaveData
/// is available, every fixed slot remains Unknown so the Presenter fails closed.
/// </remarks>
[DisallowMultipleComponent]
public sealed class SaveDataCaravanOverviewProviderBehaviour :
    MonoBehaviour,
    ICaravanOverviewViewDataProvider
{
    public const int SlotCount = CaravanSlotValidation.SlotCount;

    private FrameworkSaveData saveDataOverrideForTests;
    private bool hasSaveDataOverrideForTests;

public CaravanOverviewViewData GetOverview()
    {
        FrameworkSaveData saveData = ResolveSaveData();
        if (saveData?.caravans == null)
            return CreateUnknownOverview();

        CaravanSlotValidationResult validation = CaravanSlotValidation.Validate(saveData.caravans);
        var slots = new CaravanBlockViewData[SlotCount];
        for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            // A damaged slot is shown as an error block. We never choose a Caravan by list order.
            if (validation.IsConflicted(slotIndex))
            {
                slots[slotIndex] = CreateUnknownBlock(slotIndex);
                continue;
            }

            FrameworkCaravanSaveData caravan = validation.GetCaravanAt(slotIndex);
            if (caravan != null)
                slots[slotIndex] = CreateOccupiedBlock(saveData, caravan, slotIndex);
        }

        if (validation.HasInvalidEntries)
            Debug.LogWarning("Invalid Caravan slot data was excluded from the Main UI.", this);

        // Unlock ownership remains in persistent progression data. Creating a Caravan changes only
        // occupancy and must never unlock the next slot by itself.
        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index] != null)
                continue;

            bool isUnlocked =
                saveData.world?.unlockedCaravanSlotIndices?.Contains(index) == true;
            slots[index] = new CaravanBlockViewData
            {
                slotIndex = index,
                slotState = isUnlocked
                    ? CaravanSlotState.Empty
                    : CaravanSlotState.Locked,
                unlockHintText = isUnlocked
                    ? string.Empty
                    : "Complete the required quest to unlock this Caravan slot."
            };
        }

        return new CaravanOverviewViewData { caravans = slots };
    }

    internal void SetSaveDataForTests(FrameworkSaveData saveData)
    {
        saveDataOverrideForTests = saveData;
        hasSaveDataOverrideForTests = true;
    }

    private FrameworkSaveData ResolveSaveData()
    {
        return hasSaveDataOverrideForTests
            ? saveDataOverrideForTests
            : FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null;
    }

    private static CaravanOverviewViewData CreateUnknownOverview()
    {
        var slots = new CaravanBlockViewData[SlotCount];
        for (int index = 0; index < slots.Length; index++)
            slots[index] = CreateUnknownBlock(index);
        return new CaravanOverviewViewData { caravans = slots };
    }

    private static CaravanBlockViewData CreateUnknownBlock(int slotIndex)
    {
        return new CaravanBlockViewData
        {
            slotIndex = slotIndex,
            slotState = CaravanSlotState.Unknown
        };
    }

    private CaravanBlockViewData CreateOccupiedBlock(
        FrameworkSaveData saveData,
        FrameworkCaravanSaveData caravan,
        int slotIndex)
    {
        bool canOpenArrivalSale = TryResolveArrivalSaleTradeId(
            saveData,
            caravan.caravanId,
            out string arrivalSaleTradeId);
        return new CaravanBlockViewData
        {
            slotIndex = slotIndex,
            slotState = CaravanSlotState.Occupied,
            caravanId = caravan.caravanId ?? string.Empty,
            arrivalSaleTradeId = arrivalSaleTradeId,
            canOpenArrivalSale = canOpenArrivalSale,
            displayName = $"Caravan {slotIndex + 1}",
            state = caravan.state,
            wagonContentId = caravan.wagon != null ? caravan.wagon.wagonName ?? string.Empty : string.Empty,
            animalIcons = CreateAnimalIcons(caravan),
            cargoIcons = CreateCargoIcons(caravan)
        };
    }

    /// <summary>
    /// Resolves one exact, eligible Arrival Sale Pending from the row Caravan identity.
    /// Duplicate progress or Pending entries fail closed through the canonical lookup contract.
    /// </summary>
    private bool TryResolveArrivalSaleTradeId(
        FrameworkSaveData saveData,
        string caravanId,
        out string tradeId)
    {
        tradeId = string.Empty;
        if (string.IsNullOrWhiteSpace(caravanId)
            || !SaveDataLookup.TryGetTradeProgress(
                saveData,
                caravanId,
                out FrameworkTradeProgressSaveData progress)
            || progress.state != FrameworkTradeProgressState.SettlementPending
            || string.IsNullOrWhiteSpace(progress.activeTradeId))
        {
            return false;
        }

        string requestedTradeId = progress.activeTradeId;
        if (!SaveDataLookup.TryGetPendingSettlement(
                saveData,
                caravanId,
                requestedTradeId,
                out PendingSettlementSaveData pending)
            || pending == null
            || !pending.hasResult
            || pending.grade == JourneyResultGrade.Failed)
        {
            Debug.LogWarning(
                $"Arrival Sale row disabled. CaravanId={caravanId}, TradeId={requestedTradeId}, Reason=ExactPendingInvalid",
                this);
            return false;
        }

        tradeId = requestedTradeId;
        return true;
    }

    private static AnimalIconViewData[] CreateAnimalIcons(FrameworkCaravanSaveData caravan)
    {
        var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (caravan.animals != null)
        {
            for (int index = 0; index < caravan.animals.Count; index++)
            {
                AnimalSaveData animal = caravan.animals[index];
                string contentId = animal?.animalName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(contentId))
                    continue;

                quantities.TryGetValue(contentId, out int quantity);
                quantities[contentId] = checked(quantity + 1);
            }
        }

        var result = new AnimalIconViewData[quantities.Count];
        int resultIndex = 0;
        foreach (KeyValuePair<string, int> pair in quantities)
        {
            result[resultIndex++] = new AnimalIconViewData
            {
                animalContentId = pair.Key,
                quantity = pair.Value
            };
        }
        return result;
    }

    private static CargoIconViewData[] CreateCargoIcons(FrameworkCaravanSaveData caravan)
    {
        var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (caravan.cargo != null)
        {
            for (int index = 0; index < caravan.cargo.Count; index++)
            {
                CargoEntrySaveData cargo = caravan.cargo[index];
                string itemId = cargo?.item?.itemId?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(itemId) || cargo.quantity <= 0)
                    continue;

                quantities.TryGetValue(itemId, out int quantity);
                quantities[itemId] = checked(quantity + cargo.quantity);
            }
        }

        var result = new CargoIconViewData[quantities.Count];
        int resultIndex = 0;
        foreach (KeyValuePair<string, int> pair in quantities)
        {
            result[resultIndex++] = new CargoIconViewData
            {
                itemId = pair.Key,
                quantity = pair.Value
            };
        }
        return result;
    }
}
