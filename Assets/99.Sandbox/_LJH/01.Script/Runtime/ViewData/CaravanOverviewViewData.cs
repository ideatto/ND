using System;

[System.Serializable]
public sealed class CaravanOverviewViewData
{
    // Identifies the occupied Caravan currently selected by the player.
    // UI code forwards this ID to Framework commands and never creates or derives it.
    public string selectedCaravanId = string.Empty;

    // Contains every slot supplied by the Caravan feature, including occupied, empty, and locked slots.
    // UI code displays this array without owning or recalculating the gameplay slot limit.
    public CaravanBlockViewData[] caravans = Array.Empty<CaravanBlockViewData>();
}
