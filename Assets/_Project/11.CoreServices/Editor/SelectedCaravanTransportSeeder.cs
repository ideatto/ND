#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ND.Framework;
using UnityEditor;
using UnityEngine;
using FrameworkAnimalSaveData = ND.Framework.AnimalSaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkWagonSaveData = ND.Framework.WagonSaveData;

/// <summary>Editor-only play-test utility. It is never included in player builds.</summary>
public static class SelectedCaravanTransportSeeder
{
    private const string MenuPath = "ND/Debug/Seed Selected Caravan Transport";
    private const string PreferredWagonId = "Wagon_M";
    private const string PreferredAnimalId = "Horse";

    [MenuItem(MenuPath)]
    private static void Seed()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (!Application.isPlaying || root?.CurrentSaveData == null
            || root.SaveService == null || root.SharedGameData == null
            || !root.SharedGameData.IsLoaded)
        {
            Debug.LogError("[Transport Seed] Enter Play Mode and wait for Framework shared data to load.");
            return;
        }

        FrameworkSaveData saveData = root.CurrentSaveData;
        if (!SaveDataLookup.TryGetSelectedCaravan(
                saveData, out FrameworkCaravanSaveData caravan))
        {
            Debug.LogError("[Transport Seed] The selected caravan was not found.");
            return;
        }

        SharedWagonDefinition wagon = ResolveWagon(root.SharedGameData);
        SharedDraftAnimalDefinition animal = ResolveAnimal(root.SharedGameData, wagon);
        if (wagon == null || animal == null)
        {
            Debug.LogError("[Transport Seed] A compatible wagon or draft animal was not found.");
            return;
        }

        caravan.wagon = new FrameworkWagonSaveData
        {
            instanceId = $"debug-wagon-{Guid.NewGuid():N}",
            wagonName = wagon.DisplayName,
            overLoad = Mathf.Max(0f, wagon.BaseEfficientLoad),
            maxLoad = Mathf.Max(1f, wagon.MaxLoad),
            minAnimals = Mathf.Max(1, wagon.MinRequireAnimals),
            maxAnimals = Mathf.Max(Mathf.Max(1, wagon.MinRequireAnimals), wagon.MaxPullAnimals),
            speedModifier = wagon.BaseMoveSpeed > 0f ? wagon.BaseMoveSpeed : 1f,
            maxDurability = Mathf.Max(1, wagon.MaxDurability),
            inventorySlotCount = Mathf.Max(1, wagon.InventorySlotCount)
        };

        int animalCount = Mathf.Clamp(
            Mathf.Max(1, caravan.wagon.minAnimals),
            1,
            caravan.wagon.maxAnimals);
        caravan.animals = new List<FrameworkAnimalSaveData>(animalCount);
        for (int i = 0; i < animalCount; i++)
        {
            Enum.TryParse(animal.AnimalType, true, out DraftAnimalType animalType);
            caravan.animals.Add(new FrameworkAnimalSaveData
            {
                instanceId = $"debug-animal-{Guid.NewGuid():N}",
                animalName = animal.DisplayName,
                speed = animal.BaseMoveSpeed > 0f ? animal.BaseMoveSpeed : 1f,
                foodPerKm = Mathf.Max(0f, animal.FoodConsumptionPerSecond),
                increaseOverLoad = Mathf.Max(0f, animal.AdditionalEfficientLoad),
                increaseMaxLoad = 0f,
                animalType = animalType
            });
        }

        caravan.currentDurability = caravan.wagon.maxDurability;
        SaveResult result = root.SaveService.Save(saveData);
        if (result == null || !result.Succeeded)
        {
            Debug.LogError($"[Transport Seed] Save failed: {result?.FailureReason}");
            return;
        }

        Debug.Log(
            $"[Transport Seed] Added wagon '{wagon.Id}' and {animalCount} '{animal.Id}' " +
            $"to caravan '{caravan.caravanId}'. MaxLoad={caravan.wagon.maxLoad}, " +
            $"Slots={caravan.wagon.inventorySlotCount}.");
    }

    private static SharedWagonDefinition ResolveWagon(ISharedGameDataProvider data)
    {
        if (data.TryGetWagon(PreferredWagonId, out SharedWagonDefinition preferred))
            return preferred;

        return data.WagonIds
            .Select(id => data.TryGetWagon(id, out SharedWagonDefinition value) ? value : null)
            .Where(value => value != null)
            .OrderByDescending(value => value.InventorySlotCount)
            .ThenByDescending(value => value.MaxLoad)
            .FirstOrDefault();
    }

    private static SharedDraftAnimalDefinition ResolveAnimal(
        ISharedGameDataProvider data,
        SharedWagonDefinition wagon)
    {
        if (data.TryGetDraftAnimal(PreferredAnimalId, out SharedDraftAnimalDefinition preferred)
            && IsCompatible(wagon, preferred))
        {
            return preferred;
        }

        return data.DraftAnimalIds
            .Select(id => data.TryGetDraftAnimal(id, out SharedDraftAnimalDefinition value) ? value : null)
            .FirstOrDefault(value => value != null && IsCompatible(wagon, value));
    }

    private static bool IsCompatible(
        SharedWagonDefinition wagon,
        SharedDraftAnimalDefinition animal)
    {
        return wagon?.EligibleAnimalTypes == null
            || wagon.EligibleAnimalTypes.Length == 0
            || wagon.EligibleAnimalTypes.Any(type =>
                string.Equals(type, animal.AnimalType, StringComparison.OrdinalIgnoreCase));
    }
}
#endif
