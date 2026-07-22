public enum TradePrepareConditionType
{
    Available,
    TradeAlreadyActive,
    NotEnoughMoney,
    NotEnoughDraftAnimalFood,
    NotEnoughMercenaryPower,
    OverloadWarning,
    LoadExceeded,
    RouteLocked,
    RouteNotSelected,
    WagonNotSelected,
    WagonNotOwned,
    BrokenWagon,
    NotEnoughDraftAnimals,
    TooManyDraftAnimals,
    InvalidDraftAnimalType,
    InvalidDraftAnimalSelection,
    MixedDraftAnimalType,
    InvalidCargoSelection,
    NoCargo,
    InventorySlotExceeded,
    InvalidMercenarySelection,
    // Appended to preserve the numeric values of existing validation states.
    DepartureCaravanNotSelected
}
