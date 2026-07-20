using System;

[System.Serializable]
public sealed class CaravanOverviewViewData
{
    // Contains every slot supplied by the Caravan feature, including occupied, empty, and locked slots.
    // UI code displays this array without owning or recalculating the gameplay slot limit.
    public CaravanBlockViewData[] caravans = Array.Empty<CaravanBlockViewData>();
}

[System.Serializable]
public sealed class CaravanBlockViewData
{
    // Identifies the fixed zero-based UI slot supplied by the Caravan feature.
    // The slot index remains valid even when no Caravan is assigned.
    public int slotIndex;

    // Indicates whether this slot currently contains an existing Caravan.
    // When false, caravan-specific fields such as caravanId and cargo must remain empty.
    public bool hasCaravan;

    // Indicates whether the player can use this slot.
    // An unlocked empty slot can open Caravan creation, while a locked slot cannot be selected.
    public bool isUnlocked;

    // Provides the user-facing reason shown when this slot is locked.
    // This value should remain empty while isUnlocked is true.
    public string lockedReason = string.Empty;

    // Identifies the Caravan assigned to this slot.
    // This value must remain empty when hasCaravan is false.
    public string caravanId = string.Empty;

    // Provides the user-facing name shown at the top of the block.
    public string displayName = string.Empty;

    // Determines the state text and state icon shown by the UI.
    // This value must be ignored when hasCaravan is false.
    public JourneyState state = JourneyState.Prepare;

    // Resolves the wagon icon through the UI content catalog.
    // An empty value means that no wagon is currently assigned.
    public string wagonContentId = string.Empty;

    // Contains grouped animal types and their assigned quantities.
    public AnimalIconViewData[] animals = Array.Empty<AnimalIconViewData>();

    // Contains the cargo currently committed to this Caravan.
    // Preparation Draft items must not be included.
    public CargoIconViewData[] cargo = Array.Empty<CargoIconViewData>();
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

    // Shows the quantity currently committed to the Caravan.
    public int quantity;
}
