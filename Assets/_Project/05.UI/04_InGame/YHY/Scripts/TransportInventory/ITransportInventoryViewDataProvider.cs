namespace ND.UI.InGame.TransportInventory
{
    /// <summary>Read-only boundary supplied by the scene composition layer.</summary>
    public interface ITransportInventoryViewDataProvider
    {
        TransportInventoryPopupViewData GetViewData();
    }
}
