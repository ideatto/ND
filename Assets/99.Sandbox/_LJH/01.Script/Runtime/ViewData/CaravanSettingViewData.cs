using System;

[System.Serializable]
public sealed class CaravanSettingViewData
{
    // Identifies the Caravan whose wagon and draft animals are being displayed.
    public string caravanId = string.Empty;

    // Provides the user-facing Caravan name shown by the setting panel.
    public string caravanDisplayName = string.Empty;

    // Describes the current journey state without requiring the panel to query runtime data.
    public JourneyState state = JourneyState.Prepare;

    // Indicates whether the player may modify the wagon and draft-animal configuration.
    // Non-Preparation states can still open this panel in read-only mode.
    public bool canEdit;

    // Explains why Caravan setting changes are unavailable in the current state.
    // This value must remain empty while canEdit is true.
    public string editBlockedReason = string.Empty;

    // Identifies the owned wagon instance currently assigned to this Caravan.
    // This is intentionally different from WagonViewData.wagonId, which identifies shared content.
    public string selectedWagonInstanceId = string.Empty;

    // Identifies each owned animal instance currently assigned to this Caravan.
    // Individual IDs preserve asset-lock ownership when multiple animals share one content definition.
    public string[] selectedAnimalInstanceIds = Array.Empty<string>();

    // Contains the wagons displayed by the extracted legacy S3 selection UI.
    public WagonViewData[] wagons = Array.Empty<WagonViewData>();

    // Contains the draft animals displayed by the extracted legacy S3 selection UI.
    public DraftAnimalViewData[] draftAnimals = Array.Empty<DraftAnimalViewData>();
}

[System.Serializable]
public sealed class CaravanLoadSettingViewData
{
    // Identifies the Caravan whose planned cargo is being displayed.
    public string caravanId = string.Empty;

    // Provides the user-facing Caravan name shown by the load setting panel.
    public string caravanDisplayName = string.Empty;

    // Identifies the town whose market inventory and prices must be shown for this Caravan.
    public string currentTownId = string.Empty;

    // Describes the current journey state without requiring the panel to query runtime data.
    public JourneyState state = JourneyState.Prepare;

    // Indicates whether the player may modify the planned cargo configuration.
    // Non-Preparation states can still open this panel in read-only mode.
    public bool canEdit;

    // Explains why cargo editing is unavailable in the current state.
    // This value must remain empty while canEdit is true.
    public string editBlockedReason = string.Empty;

    // Contains the items displayed by the extracted legacy S4 selection UI.
    public TradeItemViewData[] availableItems = Array.Empty<TradeItemViewData>();

    // Contains the next-departure cargo plan while the Caravan is in Preparation.
    // These values remain a plan until the departure command commits inventory and currency changes.
    public CargoItemViewData[] plannedItems = Array.Empty<CargoItemViewData>();

    // Provides display-ready load and slot projections without UI-side calculation.
    public float currentLoad;
    public float overloadLimit;
    public float maxLoad;
    public int usedInventorySlotCount;
    public int maxInventorySlotCount;

    // Provides the projected purchase values for the current cargo plan.
    // Displaying these values does not mean that a purchase has already been committed.
    public long totalPlannedPurchaseCost;
    public long estimatedCurrencyAfterPurchase;
}
