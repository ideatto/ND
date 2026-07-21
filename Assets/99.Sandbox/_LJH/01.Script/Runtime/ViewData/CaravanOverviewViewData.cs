using System;

[System.Serializable]
public sealed class CaravanOverviewViewData
{
    // Contains every slot supplied by the Caravan feature, including occupied, empty, and locked slots.
    // UI code displays this array without owning or recalculating the gameplay slot limit.
    public CaravanBlockViewData[] caravans = Array.Empty<CaravanBlockViewData>();
}
