namespace ND.Framework
{
    public interface ISettlementView
    {
        void ShowSettlement(SettlementViewData viewData);
        void ShowNoSettlement(string reason);
        void SetClaimInteractable(bool interactable);
    }
}
