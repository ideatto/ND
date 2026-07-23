using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TradePrepareDraft
{
    // Identifies the Caravan selected inside this departure Draft before later choices are made.
    // Overview focus is separate and never preselects this value automatically.
    public string departureCaravanId;

    public string currentTownId;

    public string selectedDestinationTownId;
    public string selectedRouteId;

    public string selectedWagonId;
    // True when S3 supplied and validated this Caravan-specific owned-instance composition.
    public bool hasAuthoritativeCaravanComposition;
    public int selectedWagonCurrentDurability;
    public List<DraftAnimalSelectionData> selectedAnimals = new List<DraftAnimalSelectionData>();

    // True when S4 supplied the complete cargo plan; it replaces legacy single-Caravan SaveData cargo.
    public bool hasAuthoritativeCargoPlan;
    public List<TradeItemBundle> selectedBuyItems = new List<TradeItemBundle>();

    [SerializeField]
    private List<string> selectedMercenaryIds = new List<string>();

    public IReadOnlyList<string> SelectedMercenaryIds => selectedMercenaryIds;

    public bool SelectMercenary(string mercenaryId)
    {
        if (string.IsNullOrEmpty(mercenaryId) || selectedMercenaryIds.Contains(mercenaryId))
        {
            return false;
        }

        selectedMercenaryIds.Add(mercenaryId);
        return true;
    }

    public bool DeselectMercenary(string mercenaryId)
    {
        return !string.IsNullOrEmpty(mercenaryId) && selectedMercenaryIds.Remove(mercenaryId);
    }

    public bool IsMercenarySelected(string mercenaryId)
    {
        return !string.IsNullOrEmpty(mercenaryId) && selectedMercenaryIds.Contains(mercenaryId);
    }

    public void ClearMercenaries()
    {
        selectedMercenaryIds.Clear();
    }

    public TradePrepareDraft CreateSnapshot()
    {
        var snapshot = new TradePrepareDraft
        {
            departureCaravanId = departureCaravanId,
            currentTownId = currentTownId,
            selectedDestinationTownId = selectedDestinationTownId,
            selectedRouteId = selectedRouteId,
            selectedWagonId = selectedWagonId,
            hasAuthoritativeCaravanComposition = hasAuthoritativeCaravanComposition,
            selectedWagonCurrentDurability = selectedWagonCurrentDurability,
            hasAuthoritativeCargoPlan = hasAuthoritativeCargoPlan
        };

        if (selectedAnimals != null)
        {
            for (var index = 0; index < selectedAnimals.Count; index++)
            {
                var selection = selectedAnimals[index];
                if (selection == null)
                {
                    continue;
                }

                snapshot.selectedAnimals.Add(new DraftAnimalSelectionData
                {
                    draftAnimalId = selection.draftAnimalId,
                    quantity = selection.quantity
                });
            }
        }

        CopyItemSelections(selectedBuyItems, snapshot.selectedBuyItems);

        for (var index = 0; index < selectedMercenaryIds.Count; index++)
        {
            snapshot.SelectMercenary(selectedMercenaryIds[index]);
        }

        return snapshot;
    }

    private static void CopyItemSelections(List<TradeItemBundle> source, List<TradeItemBundle> target)
    {
        if (source == null)
        {
            return;
        }

        for (var index = 0; index < source.Count; index++)
        {
            var selection = source[index];
            if (selection == null)
            {
                continue;
            }

            target.Add(new TradeItemBundle
            {
                itemId = selection.itemId,
                quantity = selection.quantity,
                purchaseUnitPrice = selection.purchaseUnitPrice,
                sellUnitPrice = selection.sellUnitPrice
            });
        }
    }
}
