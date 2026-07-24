public interface ICaravanOverviewViewDataProvider
{
    // Supplies a complete display snapshot without exposing SaveData or mutable runtime Caravans to UI code.
    // Implementations must return a non-null ViewData whose collection fields are also non-null.
    CaravanOverviewViewData GetOverview();
}
