using System;

[System.Serializable]
public enum CaravanSlotState
{
    // Indicates that the Provider did not initialize this slot correctly.
    Unknown = 0,

    // Indicates that the player cannot currently use this slot.
    Locked = 1,

    // Indicates an unlocked slot where a new Caravan can be created.
    Empty = 2,

    // Indicates a slot containing an existing Caravan.
    Occupied = 3
}

[System.Serializable]
public sealed class CaravanBlockViewData
{
    // Identifies the fixed zero-based UI slot supplied by the Caravan feature.
    // The slot index remains valid even when no Caravan is assigned.
    public int slotIndex;

    // Determines whether this slot is locked, empty, or occupied.
    // Unknown exposes a missing Provider initialization instead of silently creating a valid-looking slot.
    public CaravanSlotState slotState = CaravanSlotState.Unknown;

    // Explains the action or requirement that unlocks this slot.
    // UI displays this hint through NoticeUI when the locked overlay is selected.
    public string unlockHintText = string.Empty;

    // Identifies the Caravan assigned to this slot.
    // This value must remain empty unless slotState is Occupied.
    public string caravanId = string.Empty;

    // Provides the user-facing name shown at the top of the block.
    public string displayName = string.Empty;

    // Determines the state text and state icon shown by the UI.
    // This value must be ignored unless slotState is Occupied.
    public JourneyState state = JourneyState.Prepare;

    // Resolves the wagon icon through the UI content catalog.
    // An empty value means that no wagon is currently assigned.
    public string wagonContentId = string.Empty;

    // Contains display-only animal icon summaries for the overview block.
    // The Caravan setting panel receives its editable data from CaravanSettingViewData.
    public AnimalIconViewData[] animalIcons = Array.Empty<AnimalIconViewData>();

    // Contains display-only cargo icon summaries for the overview block.
    // The load setting panel receives its editable data from CaravanLoadSettingViewData.
    public CargoIconViewData[] cargoIcons = Array.Empty<CargoIconViewData>();

}

[System.Serializable]
public sealed class AnimalIconViewData
{
    // Resolves the animal icon through the UI content catalog.
    public string animalContentId = string.Empty;

    // Shows how many animals of this type are assigned.
    public int quantity;
}

[System.Serializable]
public sealed class CargoIconViewData
{
    // Resolves the trade-item icon through the UI content catalog.
    public string itemId = string.Empty;

    // Shows the Provider-selected summary quantity for the current Caravan state.
    // This display value does not purchase, reserve, or commit inventory.
    public int quantity;
}
