using System;
using System.Collections.Generic;

/// <summary>
/// Stores the temporary wagon and animal choices for one Caravan setting edit session.
/// </summary>
/// <remarks>
/// This UI-owned Draft never mutates SaveData. Framework receives a snapshot only when the
/// user confirms the edit and remains responsible for validation, persistence, and rollback.
/// Owned assets use instance IDs because two assets may share the same content definition.
/// </remarks>
[Serializable]
public sealed class CaravanSettingDraft
{
    // Identifies the only Caravan that may consume this edit session.
    public string caravanId = string.Empty;

    // Identifies one owned wagon instance rather than its shared content definition.
    public string selectedWagonInstanceId = string.Empty;

    // Keeps individual owned animal IDs so Framework asset-lock validation remains possible.
    private readonly List<string> selectedAnimalInstanceIds = new List<string>();

    public IReadOnlyList<string> SelectedAnimalInstanceIds => selectedAnimalInstanceIds;

    public bool SelectAnimal(string animalInstanceId)
    {
        string normalizedId = NormalizeId(animalInstanceId);
        if (string.IsNullOrEmpty(normalizedId) || selectedAnimalInstanceIds.Contains(normalizedId))
        {
            return false;
        }

        selectedAnimalInstanceIds.Add(normalizedId);
        return true;
    }

    public bool DeselectAnimal(string animalInstanceId)
    {
        string normalizedId = NormalizeId(animalInstanceId);
        return !string.IsNullOrEmpty(normalizedId) && selectedAnimalInstanceIds.Remove(normalizedId);
    }

    public void ClearAnimals()
    {
        selectedAnimalInstanceIds.Clear();
    }

    // Returns a deep copy so panel code cannot mutate the Store's authoritative edit session.
    public CaravanSettingDraft CreateSnapshot()
    {
        var snapshot = new CaravanSettingDraft
        {
            caravanId = NormalizeId(caravanId),
            selectedWagonInstanceId = NormalizeId(selectedWagonInstanceId)
        };

        for (var index = 0; index < selectedAnimalInstanceIds.Count; index++)
        {
            snapshot.SelectAnimal(selectedAnimalInstanceIds[index]);
        }

        return snapshot;
    }

    internal static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}

/// <summary>Stores one detached S4 cargo plan without mutating saved Caravan cargo.</summary>
[Serializable]
public sealed class CaravanLoadSettingDraft
{
    public string caravanId = string.Empty;
    public List<CaravanLoadItemDraft> items = new List<CaravanLoadItemDraft>();

    public CaravanLoadSettingDraft CreateSnapshot()
    {
        var snapshot = new CaravanLoadSettingDraft
        {
            caravanId = CaravanSettingDraft.NormalizeId(caravanId)
        };

        if (items == null)
        {
            return snapshot;
        }

        for (int index = 0; index < items.Count; index++)
        {
            CaravanLoadItemDraft item = items[index];
            if (item == null)
            {
                continue;
            }

            snapshot.items.Add(new CaravanLoadItemDraft
            {
                itemId = CaravanSettingDraft.NormalizeId(item.itemId),
                quantity = Math.Max(0, item.quantity)
            });
        }

        return snapshot;
    }
}

[Serializable]
public sealed class CaravanLoadItemDraft
{
    public string itemId = string.Empty;
    public int quantity;
}
