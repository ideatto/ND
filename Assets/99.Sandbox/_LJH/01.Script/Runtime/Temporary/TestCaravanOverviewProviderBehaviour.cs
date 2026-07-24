using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;

/// <summary>
/// Exposes the temporary in-memory fixture as a scene-assignable Provider component.
/// Replace this component reference with a production Provider; Presenter code remains unchanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class TestCaravanOverviewProviderBehaviour : MonoBehaviour, ICaravanOverviewViewDataProvider
{
    public CaravanOverviewViewData GetOverview()
    {
        var saveData = FrameworkRoot.Instance != null
            ? FrameworkRoot.Instance.CurrentSaveData
            : null;
        if (saveData?.caravans == null || saveData.caravans.Count == 0)
        {
            // Editor smoke tests without a bootstrapped Framework retain the fixed fixture.
            return new TestCaravanOverviewViewDataProvider().GetOverview();
        }

        var slots = new CaravanBlockViewData[TestCaravanOverviewViewDataProvider.SlotCount];
        int occupiedCount = Mathf.Min(saveData.caravans.Count, slots.Length);
        for (int index = 0; index < occupiedCount; index++)
        {
            slots[index] = CreateOccupiedBlock(saveData.caravans[index], index);
        }

        // Slot unlocking is still temporary: expose one creation slot and keep the rest locked.
        for (int index = occupiedCount; index < slots.Length; index++)
        {
            bool firstAvailable = index == occupiedCount;
            slots[index] = new CaravanBlockViewData
            {
                slotIndex = index,
                slotState = firstAvailable ? CaravanSlotState.Empty : CaravanSlotState.Locked,
                unlockHintText = firstAvailable
                    ? string.Empty
                    : "Complete the required quest to unlock this Caravan slot."
            };
        }

        return new CaravanOverviewViewData { caravans = slots };
    }

    private static CaravanBlockViewData CreateOccupiedBlock(
        FrameworkCaravanSaveData caravan,
        int slotIndex)
    {
        return new CaravanBlockViewData
        {
            slotIndex = slotIndex,
            slotState = CaravanSlotState.Occupied,
            caravanId = caravan?.caravanId ?? string.Empty,
            displayName = $"Caravan {slotIndex + 1}",
            state = caravan != null ? caravan.state : JourneyState.Prepare,
            wagonContentId = caravan?.wagon != null ? caravan.wagon.wagonName ?? string.Empty : string.Empty,
            animalIcons = CreateAnimalIcons(caravan),
            cargoIcons = CreateCargoIcons(caravan)
        };
    }

    private static AnimalIconViewData[] CreateAnimalIcons(FrameworkCaravanSaveData caravan)
    {
        var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        if (caravan?.animals != null)
        {
            foreach (var animal in caravan.animals)
            {
                string id = animal?.animalName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id)) continue;
                quantities.TryGetValue(id, out int quantity);
                quantities[id] = quantity + 1;
            }
        }

        var result = new List<AnimalIconViewData>();
        foreach (var pair in quantities)
            result.Add(new AnimalIconViewData { animalContentId = pair.Key, quantity = pair.Value });
        return result.ToArray();
    }

    private static CargoIconViewData[] CreateCargoIcons(FrameworkCaravanSaveData caravan)
    {
        var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        if (caravan?.cargo != null)
        {
            foreach (var cargo in caravan.cargo)
            {
                string id = cargo?.item?.itemId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || cargo.quantity <= 0) continue;
                quantities.TryGetValue(id, out int quantity);
                quantities[id] = quantity + cargo.quantity;
            }
        }

        var result = new List<CargoIconViewData>();
        foreach (var pair in quantities)
            result.Add(new CargoIconViewData { itemId = pair.Key, quantity = pair.Value });
        return result.ToArray();
    }
}
