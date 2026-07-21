using System;
using System.Collections.Generic;

public sealed class TradePrepareDraftStore
{
    private TradePrepareDraft current = new TradePrepareDraft();

    public TradePrepareDraft Current => current.CreateSnapshot();

    public event Action<TradePrepareDraft> DraftChanged;

    // Preserves legacy callers that begin preparation before choosing an explicit Caravan.
    public void Reset(string currentTownId)
    {
        ResetForCaravan(string.Empty, currentTownId);
    }

    // Starts the route and departure Draft for the Caravan selected in the overview flow.
    // Equipment and cargo settings remain owned by the selected Caravan and are not recreated here.
    public void ResetForCaravan(string caravanId, string currentTownId)
    {
        current = new TradePrepareDraft
        {
            selectedCaravanId = NormalizeId(caravanId),
            currentTownId = NormalizeId(currentTownId)
        };

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

    private static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }

    private void NotifyChanged()
    {
        DraftChanged?.Invoke(current.CreateSnapshot());
    }
}
