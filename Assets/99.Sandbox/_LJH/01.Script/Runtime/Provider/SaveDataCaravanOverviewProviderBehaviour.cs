using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkSaveData = ND.Framework.SaveData;

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
    public const int SlotCount = 4;

    private FrameworkSaveData saveDataOverrideForTests;
    private bool hasSaveDataOverrideForTests;

    public CaravanOverviewViewData GetOverview()
    {
        FrameworkSaveData saveData = ResolveSaveData();
        if (saveData?.caravans == null)
            return CreateUnknownOverview();

        var slots = new CaravanBlockViewData[SlotCount];
        var claimedSlots = new bool[SlotCount];
        for (int index = 0; index < saveData.caravans.Count; index++)
        {
            FrameworkCaravanSaveData caravan = saveData.caravans[index];
            if (caravan == null)
                continue;

            // slotIndex is persistent identity. SaveData list order is storage detail and must not
            // be allowed to reroute an existing caravan to another visible slot.
            int slotIndex = caravan.slotIndex;
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                Debug.LogWarning(
                    $"Caravan '{caravan.caravanId}' has an unsupported slotIndex ({slotIndex}).",
                    this);
                continue;
            }

            if (claimedSlots[slotIndex])
            {
                // Normalization should prevent duplicates. If damaged data reaches the UI anyway,
                // fail this slot closed instead of choosing one caravan by list order.
                slots[slotIndex] = CreateUnknownBlock(slotIndex);
                continue;
            }

            claimedSlots[slotIndex] = true;
            slots[slotIndex] = CreateOccupiedBlock(caravan, slotIndex);
        }

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

    private static CaravanBlockViewData CreateOccupiedBlock(
        FrameworkCaravanSaveData caravan,
        int slotIndex)
    {
        return new CaravanBlockViewData
        {
            slotIndex = slotIndex,
            slotState = CaravanSlotState.Occupied,
            caravanId = caravan.caravanId ?? string.Empty,
            displayName = $"Caravan {slotIndex + 1}",
            state = caravan.state,
            wagonContentId = caravan.wagon != null ? caravan.wagon.wagonName ?? string.Empty : string.Empty,
            animalIcons = CreateAnimalIcons(caravan),
            cargoIcons = CreateCargoIcons(caravan)
        };
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
