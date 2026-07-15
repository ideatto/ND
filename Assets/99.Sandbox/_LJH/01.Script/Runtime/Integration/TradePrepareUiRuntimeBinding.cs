using System;
using UnityEngine;

/// <summary>
/// Connects the production trade-prepare runtime context to the existing S1 town/route panel.
/// UI input is sent through the provider so the UI never edits the preparation draft directly.
/// </summary>
public sealed class TradePrepareUiRuntimeBinding : MonoBehaviour
{
    [Header("Runtime source")]
    [SerializeField] private TradePrepareRuntimeContextProvider runtimeContext;

    [Header("S1 view")]
    [SerializeField] private TownRoutePanel townRoutePanel;

    private void OnEnable()
    {
        if (runtimeContext != null)
        {
            runtimeContext.ViewDataChanged += HandleViewDataChanged;

            // The context can initialize while TradePrepareUI is inactive, so apply its latest
            // snapshot immediately when the UI is opened for the first time.
            HandleViewDataChanged(runtimeContext.CurrentViewData);
        }

        if (townRoutePanel != null)
            townRoutePanel.OnRouteSelected += HandleRouteSelected;
    }

    private void OnDisable()
    {
        if (runtimeContext != null)
            runtimeContext.ViewDataChanged -= HandleViewDataChanged;

        if (townRoutePanel != null)
            townRoutePanel.OnRouteSelected -= HandleRouteSelected;
    }

    private void HandleViewDataChanged(TradePrepareViewData viewData)
    {
        if (townRoutePanel != null && viewData != null)
            townRoutePanel.Populate(viewData);
    }

    private void HandleRouteSelected(string destinationTownId, string routeId, float distance)
    {
        if (runtimeContext == null || !CanSelectRoute(runtimeContext.CurrentViewData, routeId))
            return;

        // Provider commands update the draft and rebuild ViewData; the panel only supplies IDs.
        runtimeContext.SelectDestination(destinationTownId);
        runtimeContext.SelectRoute(routeId);
    }

    private static bool CanSelectRoute(TradePrepareViewData viewData, string routeId)
    {
        if (viewData == null || viewData.routes == null || string.IsNullOrWhiteSpace(routeId))
            return false;

        foreach (RouteViewData route in viewData.routes)
        {
            if (route != null &&
                string.Equals(route.routeId, routeId, StringComparison.Ordinal) &&
                route.isUnlocked &&
                route.canSelect)
            {
                return true;
            }
        }

        return false;
    }
}
