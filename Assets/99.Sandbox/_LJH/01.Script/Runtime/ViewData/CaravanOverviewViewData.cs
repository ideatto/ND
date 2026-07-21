using System;

[System.Serializable]
public sealed class CaravanOverviewViewData
{
    // Identifies the Caravan currently focused for overview details, setting, or load editing.
    // This UI focus does not automatically select the Caravan for a departure Draft.
    public string focusedCaravanId = string.Empty;

    // Contains every slot supplied by the Caravan feature, including occupied, empty, and locked slots.
    // UI code displays this array without owning or recalculating the gameplay slot limit.
    public CaravanBlockViewData[] caravans = Array.Empty<CaravanBlockViewData>();
}
