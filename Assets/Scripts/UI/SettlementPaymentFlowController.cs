using UnityEngine;

/// <summary>
/// Claims a pending settlement after the payment UI completes.
/// Presentation panels only raise completion events and never mutate Framework or SaveData.
/// </summary>
public sealed class SettlementPaymentFlowController : MonoBehaviour
{
    private TradeSettlementPanelController settlementPanel;
    private ND.Framework.SettlementUiDataAdapter settlementAdapter;
    private bool wired;

    public void Configure(
        TradeSettlementPanelController panel,
        ND.Framework.SettlementUiDataAdapter adapter)
    {
        Unwire();
        settlementPanel = panel;
        settlementAdapter = adapter;
        Wire();
    }

    private void OnEnable()
    {
        Wire();
    }

    private void OnDisable()
    {
        Unwire();
    }

    private void Wire()
    {
        if (wired || settlementPanel == null || settlementAdapter == null)
            return;

        settlementPanel.PaymentCompleted.AddListener(HandlePaymentCompleted);
        wired = true;
    }

    private void Unwire()
    {
        if (wired && settlementPanel != null)
            settlementPanel.PaymentCompleted.RemoveListener(HandlePaymentCompleted);

        wired = false;
    }

    private void HandlePaymentCompleted()
    {
        settlementAdapter.OnClickClaimSettlement();
    }
}
