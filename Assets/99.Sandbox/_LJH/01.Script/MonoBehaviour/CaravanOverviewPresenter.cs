using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads Caravan Overview snapshots through the provider contract and distributes them by slotIndex.
/// Replacing a test Provider with a production Provider does not require changes to this Presenter.
/// </summary>
[DisallowMultipleComponent]
public sealed class CaravanOverviewPresenter : MonoBehaviour
{
    [Tooltip("Assign a MonoBehaviour implementing ICaravanOverviewViewDataProvider.")]
    [SerializeField] private MonoBehaviour providerBehaviour;

    [Tooltip("Assign every fixed Caravan slot once. Array order is ignored; SlotIndex performs routing.")]
    [SerializeField] private CaravanSlotView[] slotViews = Array.Empty<CaravanSlotView>();

    [Tooltip("Displays Provider-owned unlock guidance when a locked slot is selected.")]
    [SerializeField] private NoticeUI noticeUI;

    private ICaravanOverviewViewDataProvider provider;
    private bool hasRuntimeProviderOverride;
    private readonly HashSet<int> createPendingSlots = new HashSet<int>();

    // The Presenter forwards intent without owning gameplay commands or SaveData mutations.
    public event Action<string> SettingRequested;
    public event Action<string> CargoRequested;
    public event Action<int> CreateRequested;

    private void OnEnable()
    {
        SubscribeToSlotViews();
        if (!hasRuntimeProviderOverride)
        {
            ResolveSerializedProvider();
        }

        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromSlotViews();
    }

    /// <summary>
    /// Supports runtime dependency injection when the production Provider is not a scene component.
    /// Passing null returns every slot to the safe Unknown state.
    /// </summary>
    public void SetProvider(ICaravanOverviewViewDataProvider runtimeProvider)
    {
        // Preserve explicit runtime injection across disable/enable cycles.
        // This also supports a deliberate null Provider that keeps every slot fail-closed.
        hasRuntimeProviderOverride = true;
        provider = runtimeProvider;
        Refresh();
    }

    /// <summary>Returns provider resolution to the scene-assigned MonoBehaviour.</summary>
    public void UseSerializedProvider()
    {
        hasRuntimeProviderOverride = false;
        ResolveSerializedProvider();
        Refresh();
    }

    /// <summary>Queries the current Provider and redraws all fixed slots.</summary>
    public void Refresh()
    {
        ShowEverySlotAsUnknown();

        if (provider == null)
        {
            return;
        }

        CaravanOverviewViewData overview;
        try
        {
            overview = provider.GetOverview();
        }
        catch (Exception exception)
        {
            // A Provider failure must leave the UI fail-closed instead of exposing active placeholder buttons.
            Debug.LogError($"Caravan Overview Provider failed: {exception}", this);
            return;
        }

        if (overview == null || overview.caravans == null)
        {
            Debug.LogError(
                "Caravan Overview Provider returned a null snapshot or slot collection.",
                this);
            return;
        }

        bool[] boundViews = new bool[slotViews != null ? slotViews.Length : 0];
        for (int blockIndex = 0; blockIndex < overview.caravans.Length; blockIndex++)
        {
            CaravanBlockViewData block = overview.caravans[blockIndex];
            if (block == null)
            {
                Debug.LogError("Caravan Overview Provider returned a null slot entry.", this);
                continue;
            }

            int viewArrayIndex = FindViewArrayIndex(block.slotIndex);
            if (viewArrayIndex < 0)
            {
                Debug.LogError(
                    $"No CaravanSlotView is assigned for Provider slotIndex {block.slotIndex}.",
                    this);
                continue;
            }

            if (boundViews[viewArrayIndex])
            {
                Debug.LogError(
                    $"Caravan Overview Provider returned duplicate slotIndex {block.slotIndex}.",
                    this);
                continue;
            }

            CaravanSlotView slotView = slotViews[viewArrayIndex];
            slotView.Bind(block);

            if (createPendingSlots.Contains(block.slotIndex))
            {
                if (block.slotState == CaravanSlotState.Empty)
                {
                    slotView.SetCreatePending(true);
                }
                else
                {
                    // A successful Provider refresh replaces the transient Empty state with real data.
                    createPendingSlots.Remove(block.slotIndex);
                }
            }

            boundViews[viewArrayIndex] = true;
        }
    }

    /// <summary>
    /// Starts or clears the UI-only in-flight state for one Empty slot.
    /// The Framework result handler calls this before refreshing Provider data.
    /// </summary>
    public bool SetCreatePending(int slotIndex, bool pending)
    {
        int viewArrayIndex = FindViewArrayIndex(slotIndex);
        if (viewArrayIndex < 0)
        {
            return false;
        }

        if (pending)
        {
            createPendingSlots.Add(slotIndex);
        }
        else
        {
            createPendingSlots.Remove(slotIndex);
        }

        slotViews[viewArrayIndex].SetCreatePending(pending);
        return true;
    }

    private void ResolveSerializedProvider()
    {
        provider = providerBehaviour as ICaravanOverviewViewDataProvider;
        if (providerBehaviour != null && provider == null)
        {
            Debug.LogError(
                $"{providerBehaviour.GetType().Name} must implement {nameof(ICaravanOverviewViewDataProvider)}.",
                this);
        }
    }

    private int FindViewArrayIndex(int slotIndex)
    {
        if (slotViews == null)
        {
            return -1;
        }

        for (int index = 0; index < slotViews.Length; index++)
        {
            CaravanSlotView slotView = slotViews[index];
            if (slotView != null && slotView.SlotIndex == slotIndex)
            {
                return index;
            }
        }

        return -1;
    }

    private void ShowEverySlotAsUnknown()
    {
        if (slotViews == null)
        {
            return;
        }

        for (int index = 0; index < slotViews.Length; index++)
        {
            slotViews[index]?.ShowUnknown();
        }
    }

    private void SubscribeToSlotViews()
    {
        if (slotViews == null)
        {
            return;
        }

        for (int index = 0; index < slotViews.Length; index++)
        {
            CaravanSlotView slotView = slotViews[index];
            if (slotView == null)
            {
                continue;
            }

            slotView.SettingRequested += HandleSettingRequested;
            slotView.CargoRequested += HandleCargoRequested;
            slotView.CreateRequested += HandleCreateRequested;
            slotView.UnlockHintRequested += HandleUnlockHintRequested;
        }
    }

    private void UnsubscribeFromSlotViews()
    {
        if (slotViews == null)
        {
            return;
        }

        for (int index = 0; index < slotViews.Length; index++)
        {
            CaravanSlotView slotView = slotViews[index];
            if (slotView == null)
            {
                continue;
            }

            slotView.SettingRequested -= HandleSettingRequested;
            slotView.CargoRequested -= HandleCargoRequested;
            slotView.CreateRequested -= HandleCreateRequested;
            slotView.UnlockHintRequested -= HandleUnlockHintRequested;
        }
    }

    private void HandleSettingRequested(string caravanId)
    {
        SettingRequested?.Invoke(caravanId);
    }

    private void HandleCargoRequested(string caravanId)
    {
        CargoRequested?.Invoke(caravanId);
    }

    private void HandleCreateRequested(int slotIndex)
    {
        // Preserve the pending lock across Provider refreshes until the Binding applies a result.
        SetCreatePending(slotIndex, true);

        // UI forwards intent only; Framework must create and persist the stable caravanId.
        CreateRequested?.Invoke(slotIndex);
    }

    private void HandleUnlockHintRequested(string unlockHintText)
    {
        // NoticeUI owns display duration and fading; the Presenter only routes Provider text.
        if (noticeUI != null && !string.IsNullOrWhiteSpace(unlockHintText))
        {
            noticeUI.Show(unlockHintText);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (providerBehaviour != null && !(providerBehaviour is ICaravanOverviewViewDataProvider))
        {
            Debug.LogError(
                $"{providerBehaviour.GetType().Name} must implement {nameof(ICaravanOverviewViewDataProvider)}.",
                this);
        }

        // Duplicate slot indices would make Provider routing ambiguous even when the array has four entries.
        if (slotViews == null)
        {
            return;
        }

        for (int firstIndex = 0; firstIndex < slotViews.Length; firstIndex++)
        {
            CaravanSlotView first = slotViews[firstIndex];
            if (first == null)
            {
                continue;
            }

            for (int secondIndex = firstIndex + 1; secondIndex < slotViews.Length; secondIndex++)
            {
                CaravanSlotView second = slotViews[secondIndex];
                if (second != null && first.SlotIndex == second.SlotIndex)
                {
                    Debug.LogError(
                        $"CaravanSlotView slotIndex {first.SlotIndex} is assigned more than once.",
                        this);
                }
            }
        }
    }
#endif
}
