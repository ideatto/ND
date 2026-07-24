using System;
using System.Collections.Generic;

public sealed class TradePrepareDraftStore
{
    private TradePrepareDraft current = new TradePrepareDraft();

    public TradePrepareDraft Current => current.CreateSnapshot();

    public event Action<TradePrepareDraft> DraftChanged;

    // Opens a fresh preparation session without inheriting the Caravan focused in Overview.
    public void Reset(string currentTownId)
    {
        current = new TradePrepareDraft
        {
            currentTownId = NormalizeId(currentTownId)
        };

        NotifyChanged();
    }

    // Selects the departure Caravan inside TradePrepareUI and discards choices owned by the previous preset.
    // This prevents route, cargo, equipment, or mercenary data from leaking between Caravan Drafts.
    public void SelectDepartureCaravan(string caravanId)
    {
        var normalizedCaravanId = NormalizeId(caravanId);
        if (current.departureCaravanId == normalizedCaravanId)
        {
            return;
        }

        current.departureCaravanId = normalizedCaravanId;
        current.selectedDestinationTownId = string.Empty;
        current.selectedRouteId = string.Empty;
        current.selectedWagonId = string.Empty;
        current.hasAuthoritativeCaravanComposition = false;
        current.selectedWagonCurrentDurability = 0;
        current.selectedAnimals.Clear();
        current.hasAuthoritativeCargoPlan = false;
        current.selectedBuyItems.Clear();
        current.ClearMercenaries();
        NotifyChanged();
    }

    public void SelectDestination(string townId)
    {
        var normalizedTownId = NormalizeId(townId);
        if (current.selectedDestinationTownId == normalizedTownId)
        {
            return;
        }

        current.selectedDestinationTownId = normalizedTownId;
        current.selectedRouteId = string.Empty;
        NotifyChanged();
    }

    public void SelectRoute(string routeId)
    {
        SetId(ref current.selectedRouteId, routeId);
    }

    public void SelectWagon(string wagonId)
    {
        var normalizedWagonId = NormalizeId(wagonId);
        if (current.selectedWagonId == normalizedWagonId)
        {
            return;
        }

        current.selectedWagonId = normalizedWagonId;
        current.hasAuthoritativeCaravanComposition = false;
        current.selectedWagonCurrentDurability = 0;
        current.selectedAnimals.Clear();
        current.selectedBuyItems.Clear();
        NotifyChanged();
    }

    public void SetAnimalQuantity(string draftAnimalId, int quantity)
    {
        var normalizedAnimalId = NormalizeId(draftAnimalId);
        if (string.IsNullOrEmpty(normalizedAnimalId))
        {
            return;
        }

        var selectionIndex = current.selectedAnimals.FindIndex(
            selection => selection != null && selection.draftAnimalId == normalizedAnimalId);

        if (quantity <= 0)
        {
            if (selectionIndex >= 0)
            {
                current.selectedAnimals.RemoveAt(selectionIndex);
                NotifyChanged();
            }

            return;
        }

        if (selectionIndex >= 0)
        {
            if (current.selectedAnimals[selectionIndex].quantity == quantity)
            {
                return;
            }

            current.selectedAnimals[selectionIndex].quantity = quantity;
        }
        else
        {
            current.selectedAnimals.Add(new DraftAnimalSelectionData
            {
                draftAnimalId = normalizedAnimalId,
                quantity = quantity
            });
        }

        NotifyChanged();
    }

    public void SetBuyItemQuantity(string itemId, int quantity)
    {
        if (SetItemQuantity(current.selectedBuyItems, itemId, quantity))
        {
            NotifyChanged();
        }
    }

    public void ClearCargo()
    {
        if (current.selectedBuyItems.Count == 0)
        {
            return;
        }

        current.selectedBuyItems.Clear();
        NotifyChanged();
    }

    public void ReplaceCaravanComposition(
        string wagonId,
        IReadOnlyList<DraftAnimalSelectionData> animals,
        int currentWagonDurability)
    {
        current.selectedWagonId = NormalizeId(wagonId);
        current.hasAuthoritativeCaravanComposition = true;
        current.selectedWagonCurrentDurability = string.IsNullOrEmpty(current.selectedWagonId)
            ? 0
            : Math.Max(0, currentWagonDurability);
        current.selectedAnimals.Clear();

        if (animals != null)
        {
            for (int index = 0; index < animals.Count; index++)
            {
                DraftAnimalSelectionData selection = animals[index];
                string animalId = selection != null
                    ? NormalizeId(selection.draftAnimalId)
                    : string.Empty;
                if (string.IsNullOrEmpty(animalId) || selection.quantity <= 0)
                    continue;

                current.selectedAnimals.Add(new DraftAnimalSelectionData
                {
                    draftAnimalId = animalId,
                    quantity = selection.quantity
                });
            }
        }

        // S3 Command already validates the new composition against planned cargo capacity, so
        // restoring S3 here must not clear the independently saved S4 plan.
        NotifyChanged();
    }

    // TODO(PRODUCTION): selectedBuyItems is temporarily reused as the S4 planned-cargo container.
    // Replace it with a dedicated Caravan cargo-plan collection (or an authoritative SaveData cargo
    // snapshot) once the Framework Caravan command owns S4 persistence. Market purchases and saved
    // Caravan cargo must then remain separate to prevent duplicate stock/currency settlement.
    // Replaces the departure Draft cargo with one Provider-owned Caravan plan in a single update.
    // Invalid rows are ignored and duplicate IDs are merged so a malformed snapshot cannot leak
    // duplicate bundles into departure validation.
    public void ReplaceCargoPlan(CargoItemViewData[] plannedItems)
    {
        current.hasAuthoritativeCargoPlan = true;
        plannedItems = plannedItems ?? Array.Empty<CargoItemViewData>();
        var replacements = new List<TradeItemBundle>();
        var indexesById = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int index = 0; index < plannedItems.Length; index++)
        {
            CargoItemViewData item = plannedItems[index];
            string itemId = item != null ? NormalizeId(item.itemId) : string.Empty;
            int quantity = item != null ? Math.Max(0, item.quantity) : 0;
            if (string.IsNullOrEmpty(itemId) || quantity == 0)
                continue;

            if (indexesById.TryGetValue(itemId, out int existingIndex))
            {
                TradeItemBundle existing = replacements[existingIndex];
                existing.quantity = existing.quantity > int.MaxValue - quantity
                    ? int.MaxValue
                    : existing.quantity + quantity;
                continue;
            }

            indexesById[itemId] = replacements.Count;
            replacements.Add(new TradeItemBundle
            {
                itemId = itemId,
                quantity = quantity,
                purchaseUnitPrice = Math.Max(0L, item.purchaseUnitPrice),
                sellUnitPrice = Math.Max(0L, item.estimatedSellUnitPrice)
            });
        }

        if (AreSameCargo(current.selectedBuyItems, replacements))
            return;

        current.selectedBuyItems.Clear();
        current.selectedBuyItems.AddRange(replacements);
        NotifyChanged();
    }

    public void SelectMercenary(string mercenaryId)
    {
        if (current.SelectMercenary(NormalizeId(mercenaryId)))
        {
            NotifyChanged();
        }
    }

    public void DeselectMercenary(string mercenaryId)
    {
        if (current.DeselectMercenary(NormalizeId(mercenaryId)))
        {
            NotifyChanged();
        }
    }

    public void ClearMercenaries()
    {
        if (current.SelectedMercenaryIds.Count == 0)
        {
            return;
        }

        current.ClearMercenaries();
        NotifyChanged();
    }

    public void Cancel()
    {
        current = new TradePrepareDraft();
        NotifyChanged();
    }

    private void SetId(ref string targetId, string value)
    {
        var normalizedId = NormalizeId(value);
        if (targetId == normalizedId)
        {
            return;
        }

        targetId = normalizedId;
        NotifyChanged();
    }

    private static bool SetItemQuantity(List<TradeItemBundle> selections, string itemId, int quantity)
    {
        var normalizedItemId = NormalizeId(itemId);
        if (string.IsNullOrEmpty(normalizedItemId))
        {
            return false;
        }

        var selectionIndex = selections.FindIndex(
            selection => selection != null && selection.itemId == normalizedItemId);

        if (quantity <= 0)
        {
            if (selectionIndex < 0)
            {
                return false;
            }

            selections.RemoveAt(selectionIndex);
            return true;
        }

        if (selectionIndex >= 0)
        {
            if (selections[selectionIndex].quantity == quantity)
            {
                return false;
            }

            selections[selectionIndex].quantity = quantity;
            return true;
        }

        selections.Add(new TradeItemBundle
        {
            itemId = normalizedItemId,
            quantity = quantity
        });
        return true;
    }

    private static bool AreSameCargo(
        IReadOnlyList<TradeItemBundle> currentItems,
        IReadOnlyList<TradeItemBundle> replacements)
    {
        if (currentItems == null || currentItems.Count != replacements.Count)
            return false;

        for (int index = 0; index < currentItems.Count; index++)
        {
            TradeItemBundle currentItem = currentItems[index];
            TradeItemBundle replacement = replacements[index];
            if (currentItem == null
                || currentItem.itemId != replacement.itemId
                || currentItem.quantity != replacement.quantity
                || currentItem.purchaseUnitPrice != replacement.purchaseUnitPrice
                || currentItem.sellUnitPrice != replacement.sellUnitPrice)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }

    private void NotifyChanged()
    {
        DraftChanged?.Invoke(current.CreateSnapshot());
    }
}
