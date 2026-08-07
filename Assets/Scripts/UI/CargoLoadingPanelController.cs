using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// S4 적재 화면의 렌더링과 사용자 선택 초안만 소유한다.
/// 저장 Cargo, 시장 예약, 통화의 단일 원본은 Runtime/Framework 계층에 있으며 이 UI는 완전한 선택 Snapshot을 전달한다.
/// </summary>
public sealed class CargoLoadingPanelController : MonoBehaviour
{
    /// <summary>
    /// Carries one item's final S4 quantity to the Runtime Binding.
    /// Only ID and quantity are exposed so Runtime Draft does not depend on UI slot internals.
    /// </summary>
    [Serializable]
    public struct CargoSelection
    {
        public string itemId;
        public int quantity;
    }

    public sealed class CargoChangeSnapshot
    {
        public string caravanId = string.Empty;
        public string marketId = string.Empty;
        public IReadOnlyList<CargoSelection> items = Array.Empty<CargoSelection>();
    }

    /// <summary>
    /// Raised after S4 cargo changes. A complete snapshot is sent so items removed from
    /// the UI can also be cleared from Runtime Draft by assigning quantity zero.
    /// </summary>
    public event Action<CargoChangeSnapshot> LoadChanged;
    public event Action<string> CargoConfirmed;

    /// <summary>
    /// Optional integration boundary invoked before S4 advances to the mercenary step.
    /// The UI owns only its draft; the assigned command must atomically persist cargo,
    /// market stock, and currency. Returning false keeps this panel open.
    /// </summary>
    public Func<bool> TryCommitCargoTransaction { get; set; }

    /// <summary>
    /// Optional detached Caravan S4 handler. When assigned, Continue confirms the Caravan-owned
    /// cargo plan and never advances into the normal market/mercenary flow.
    /// </summary>
    public Func<bool> TryCommitDetachedCargoPlan { get; set; }

    /// <summary>Optional authoritative validation for the current market draft.</summary>
    public Func<bool> CanCommitCargoTransaction { get; set; }

    /// <summary>Optional projected currency after the current market draft.</summary>
    public Func<long> ProjectedCurrencyAfterCargoTransaction { get; set; }

    /// <summary>
    /// Optional integration boundary used when the whole preparation is cancelled.
    /// It must discard presentation draft only and must not mutate saved cargo.
    /// </summary>
    public Action CancelCargoTransactionDraft { get; set; }

    [Header("상점 데이터")]
    [SerializeField] private TradeItemData[] shopItems = Array.Empty<TradeItemData>();
    [SerializeField] private int[] initialStocks = Array.Empty<int>();
    private long[] marketBuyUnitPrices = Array.Empty<long>();

    [Header("무역 준비 값")]
    [SerializeField] private WagonData selectedWagon;
    [SerializeField] private RouteData selectedRoute;
    [SerializeField] private DraftAnimalData[] selectedDraftAnimals = Array.Empty<DraftAnimalData>();
    [SerializeField, Min(0)] private long currentGold = 2000;
    [SerializeField, Min(0f)] private float maximumLoad = 100f;
    [Tooltip("Fallback used only when no RouteData is selected.")]
    [SerializeField, Min(0)] private int requiredFood = 5;

    [Header("단계 전환 대상 (선택)")]
    [SerializeField] private GameObject previousStepPanel;
    [SerializeField] private GameObject mercenaryStepPanel;

    [Header("단계 이벤트")]
    [SerializeField] private UnityEvent onBackRequested = new UnityEvent();
    [SerializeField] private UnityEvent onTradeCancelled = new UnityEvent();
    [SerializeField] private UnityEvent onMercenaryHireRequested = new UnityEvent();

    private sealed class LoadedLine
    {
        public TradeItemData Item;
        public TradeItemViewData SavedItem;
        public int Quantity;
        public long UnitPrice;
        public bool IsSaved;

        public string ItemId => Item != null ? Item.ItemId : SavedItem?.itemId ?? string.Empty;
        public Sprite Icon => Item != null ? Item.Icon : SavedItem?.icon;
        public float Weight => Item != null ? Item.Weight : Mathf.Max(0f, SavedItem?.unitWeight ?? 0f);
        public TradeItemCategory Category => Item != null ? Item.Category : SavedItem?.category ?? default;
        public bool CanStack => Item != null ? Item.CanStack : true;
        public int MaxCount => Item != null ? Item.MaxCount : int.MaxValue;
        public long BaseSellPrice => Item != null ? Item.BaseSellPrice : Math.Max(0L, SavedItem?.sellPrice ?? 0L);
        public bool HasItem => !string.IsNullOrWhiteSpace(ItemId);
    }

    /// <summary>
    /// Presentation-only projection. Persisted Cargo and Market reservations stay as separate
    /// LoadedLine records, while equal item IDs occupy one visible slot with a combined quantity.
    /// </summary>
    private sealed class LoadedSlotView
    {
        public string ItemId;
        public LoadedLine PresentationLine;
        public int Quantity;
    }

    private readonly List<LoadedLine> loadedLines = new List<LoadedLine>();
    private int[] remainingStocks = Array.Empty<int>();
    private string marketDraftCaravanId = string.Empty;
    private string marketDraftMarketId = string.Empty;

    private RectTransform panelRect;
    private RectTransform shopGrid;
    private RectTransform loadedGrid;
    private GameObject popupOverlay;
    private RectTransform popupRect;
    private TMP_Text popupTitleText;
    private TMP_Text popupInfoText;
    private TMP_Text popupCountText;
    private Button popupMinusButton;
    private Button popupPlusButton;
    private Button popupMinButton;
    private Button popupMaxButton;
    private Button popupLoadButton;
    [SerializeField] private Slider purchaseSlider;   // 적재 수량 슬라이더(최대 = 허용 최대 구매수). 인스펙터에서 연결

    private TMP_Text currentLoadText;
    private TMP_Text currentMoneyText;
    private TMP_Text pendingCostText;
    private TMP_Text feedConsumptionText;
    private TMP_Text loadedFoodText;
    private TMP_Text foodWarningText;
    private Button nextButton;
    private Button closeButton;
    private Button backButton;
    private MercenaryHirePanelController mercenaryHireController;

    private RectTransform[] shopSlots = Array.Empty<RectTransform>();
    private RectTransform[] loadedSlots = Array.Empty<RectTransform>();
    private int detachedInventorySlotLimit = -1;
    private ScrollRect shopScrollRect;
    private ScrollRect loadedScrollRect;

    private int selectedShopIndex = -1;
    private int selectedPurchaseCount = 1;
    private long pendingPurchaseCost;
    private string cargoTransactionError = string.Empty;
    private bool cargoEditingEnabled = true;
    private Coroutine panelAnimation;
    private Coroutine popupAnimation;

    public long CurrentGold => currentGold;
    public long PendingPurchaseCost => pendingPurchaseCost;
    public WagonData SelectedWagon => selectedWagon;
    public RouteData SelectedRoute => selectedRoute;
    public IReadOnlyList<DraftAnimalData> SelectedDraftAnimals => selectedDraftAnimals;
    public float TotalFeedConsumption => selectedDraftAnimals == null
        ? 0f
        : selectedDraftAnimals
            .Where(animal => animal != null)
            .Sum(animal => animal.FeedConsumption);
    public int RequiredFood => selectedRoute == null
        ? requiredFood
        : selectedRoute.BaseRequiredFoodQuantity;
    public int InventorySlotLimit => detachedInventorySlotLimit >= 0
        ? detachedInventorySlotLimit
        : selectedWagon == null
            ? loadedSlots.Length
            : selectedWagon.InventorySlotCount;
    public float CurrentLoad => loadedLines.Sum(line => !line.HasItem ? 0f : line.Weight * line.Quantity);
    public float SavedLoad => loadedLines.Where(line => line.IsSaved && line.HasItem)
        .Sum(line => line.Weight * line.Quantity);
    public float ReservedLoad => loadedLines.Where(line => !line.IsSaved && line.HasItem)
        .Sum(line => line.Weight * line.Quantity);
    public float MaximumLoad => GetCoreMaximumLoad();
    public int LoadedFood => loadedLines
        .Where(line => line.HasItem && IsFood(line))
        .Sum(line => line.Quantity);
    public bool CanProceed => (TryCommitDetachedCargoPlan != null
            ? cargoEditingEnabled
            : (CanCommitCargoTransaction?.Invoke() ?? pendingPurchaseCost <= currentGold))
        && CurrentLoad <= MaximumLoad + 0.0001f
        && LoadedSlotCount <= InventorySlotLimit;
    private int LoadedSlotCount => CalculateProjectedSlotCount();

    private int SavedSlotCount => loadedLines
        .Where(line => line.IsSaved && line.HasItem && line.Quantity > 0)
        .GroupBy(line => line.ItemId, StringComparer.Ordinal)
        .Sum(group => RequiredSlots(group.Sum(line => line.Quantity), group.First().CanStack, group.First().MaxCount));

    private int CalculateProjectedSlotCount()
    {
        return loadedLines
            .Where(line => line.HasItem && line.Quantity > 0)
            .GroupBy(line => line.ItemId, StringComparer.Ordinal)
            .Sum(group =>
            {
                TradeItemData definition = FindShopItem(group.Key);
                bool canStack = definition != null ? definition.CanStack : group.First().CanStack;
                int maxCount = definition != null ? definition.MaxCount : group.First().MaxCount;
                return RequiredSlots(group.Sum(line => line.Quantity), canStack, maxCount);
            });
    }


    private static int RequiredSlots(int quantity, bool canStack, int maxCount)
    {
        int stack = canStack ? Mathf.Max(1, maxCount) : 1;
        return quantity <= 0 ? 0 : (quantity + stack - 1) / stack;
    }

    public long MercenaryBudget => ProjectedCurrencyAfterCargoTransaction != null
        ? Math.Max(0L, ProjectedCurrencyAfterCargoTransaction())
        : Math.Max(0L, currentGold - pendingPurchaseCost);

    private float GetCoreMaximumLoad()
    {
        if (selectedWagon == null)
            return maximumLoad;

        var caravan = new CaravanData
        {
            wagon = new imsiWagonData
            {
                maxLoad = selectedWagon.MaxLoad
            }
        };

        if (selectedDraftAnimals != null)
        {
            foreach (DraftAnimalData animal in selectedDraftAnimals)
            {
                if (animal == null)
                    continue;

                caravan.animals.Add(new imsiAnimalData
                {
                    increaseMaxLoad = animal.IncreaseMaxLoad
                });
            }
        }

        return CaravanCalculator.GetMaxLoad(caravan);
    }

    private void Awake()
    {
        CacheHierarchy();
        EnsureDynamicSlots();
        EnsureGridScrollViews();
        EnsureActionButtons();
        // [하드코딩 비활성화] 레이아웃/색상 팔레트를 매 초기화마다 강제로 덮어써서,
        //   캔버스(프리팹)에서 수정한 값이 계속 초기화됐음. → 이제 프리팹 편집값을 그대로 사용.
        //   (원래 하드코딩 스타일로 되돌리려면 아래 한 줄의 주석을 해제하세요.)
        // ApplySection9LayoutAndPalette();
        EnsureMercenaryHirePanel();
        BuildPurchasePopup();
        InitializeState();
        WireInteractions();
        RefreshAll();
        ClosePurchasePopupImmediate();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        panelRect ??= transform as RectTransform;
        panelRect.localScale = Vector3.zero;

        if (panelAnimation != null)
            StopCoroutine(panelAnimation);

        panelAnimation = StartCoroutine(ScaleRoutine(panelRect, Vector3.one, 0.22f, null));
    }

    public void Configure(
        long availableGold,
        float maxLoad,
        int foodRequirement,
        TradeItemData[] items,
        int[] stocks)
    {
        Configure(availableGold, maxLoad, foodRequirement, items, stocks, (long[])null);
    }

    public void Configure(
        long availableGold,
        float maxLoad,
        int foodRequirement,
        TradeItemData[] items,
        int[] stocks,
        long[] buyUnitPrices)
    {
        currentGold = Math.Max(0, availableGold);
        maximumLoad = Mathf.Max(0f, maxLoad);
        requiredFood = Mathf.Max(0, foodRequirement);
        shopItems = items ?? Array.Empty<TradeItemData>();
        initialStocks = stocks ?? Array.Empty<int>();
        marketBuyUnitPrices = buyUnitPrices ?? Array.Empty<long>();

        EnsureDynamicSlots();
        WireSlotInteractions();
        InitializeState();
        RefreshAll();
    }

    /// <summary>
    /// Restores cargo quantities already held by the authoritative preparation draft.
    /// Configure clears presentation state first, so returning to S4 must rebuild it.
    /// </summary>
    /// <summary>
    /// Restores persisted Cargo from Provider-owned ViewData. Saved lines remain read-only
    /// until the detached command commits the combined final snapshot.
    /// </summary>
    public void RestoreSavedCargo(
        IReadOnlyList<TradeItemViewData> savedItems,
        bool notifyChange = false)
    {
        if (savedItems == null)
            return;

        foreach (TradeItemViewData item in savedItems)
        {
            if (item == null || item.ownedAmount <= 0 || string.IsNullOrWhiteSpace(item.itemId))
                continue;

            TryAddSavedQuantity(item, item.ownedAmount);
        }

        RefreshAll();
        if (notifyChange)
            NotifyLoadChanged();
    }
    public void RestoreSelectedCargo(
        IReadOnlyList<TradeItemViewData> selectedItems,
        bool useOwnedCargo = false,
        bool notifyChange = true)
    {
        if (selectedItems == null || shopItems == null)
            return;

        foreach (TradeItemViewData selected in selectedItems)
        {
            int quantityToLoad = useOwnedCargo ? selected?.ownedAmount ?? 0 : selected?.selectedBuyAmount ?? 0;
            if (selected == null || quantityToLoad <= 0 || string.IsNullOrEmpty(selected.itemId))
                continue;

            int shopIndex = Array.FindIndex(
                shopItems,
                item => item != null && string.Equals(item.ItemId, selected.itemId, StringComparison.Ordinal));
            if (shopIndex < 0 || shopIndex >= remainingStocks.Length)
                continue;

            int quantity = useOwnedCargo
                ? quantityToLoad
                : Mathf.Min(quantityToLoad, remainingStocks[shopIndex]);
            long unitPrice = useOwnedCargo ? 0L : GetMarketBuyUnitPrice(shopIndex);
            if (quantity <= 0 || !TryAddLoadedQuantity(
                    shopItems[shopIndex],
                    quantity,
                    unitPrice,
                    ignoreSlotCapacity: useOwnedCargo,
                    isSaved: useOwnedCargo))
                continue;

            if (!useOwnedCargo)
            {
                remainingStocks[shopIndex] -= quantity;
                pendingPurchaseCost += GetMarketBuyUnitPrice(shopIndex) * quantity;
            }
        }

        RefreshAll();
        if (notifyChange)
            NotifyLoadChanged();
    }

    public void SetCargoEditingEnabled(bool enabled)
    {
        cargoEditingEnabled = enabled;
        ClosePurchasePopupImmediate();
    }

    public void SetDetachedInventorySlotLimit(int slotLimit)
    {
        detachedInventorySlotLimit = Mathf.Max(0, slotLimit);
        EnsureDynamicSlots();
        WireSlotInteractions();
        RefreshAll();
    }

    public void ClearDetachedInventorySlotLimit()
    {
        detachedInventorySlotLimit = -1;
        RefreshAll();
    }

    public void SetDetachedPresentation(bool detached)
    {
        Transform loadUiBadge = FindDeepChild(transform, "LoadUiBadge");
        if (loadUiBadge != null)
            loadUiBadge.gameObject.SetActive(!detached);

        if (foodWarningText != null)
            foodWarningText.gameObject.SetActive(!detached);

        TMP_Text nextLabel = nextButton == null
            ? null
            : nextButton.GetComponentInChildren<TMP_Text>(true);
        if (nextLabel != null)
            nextLabel.text = "구매";

        // [위치 강제 중단] 캔버스에서 옮긴 NextButton 위치가 런타임에 덮어써지지 않도록 비활성화.
        //   (캔버스에서 직접 배치한 위치·크기를 그대로 유지)
        /*
        if (nextButton != null)
        {
            RectTransform nextRect = nextButton.transform as RectTransform;
            nextRect.sizeDelta = detached ? new Vector2(152f, 36f) : new Vector2(180f, 44f);
            nextRect.anchoredPosition = detached ? new Vector2(392f, -128f) : new Vector2(364f, -116f);
        }
        */

        // [위치 강제 중단] 캔버스에서 옮긴 LoadedInventoryLabel 위치가 런타임에 덮어써지지 않도록 비활성화.
        //   (NextButton과 동일 이유: 캔버스에서 직접 배치한 위치·크기를 그대로 유지)
        /*
        TMP_Text loadedInventoryLabel = FindText("LoadedInventoryLabel");
        if (loadedInventoryLabel != null)
        {
            RectTransform labelRect = loadedInventoryLabel.rectTransform;
            labelRect.anchoredPosition = detached ? new Vector2(12f, -10f) : new Vector2(12f, -12f);
            labelRect.sizeDelta = detached ? new Vector2(220f, 30f) : new Vector2(156f, 44f);
        }
        */

        if (loadedGrid != null)
        {
            loadedGrid.anchoredPosition = detached ? new Vector2(20f, -12f) : new Vector2(20f, -52f);
            GridLayoutGroup layout = loadedGrid.GetComponent<GridLayoutGroup>();
            if (layout != null)
            {
                layout.padding = detached
                    ? new RectOffset(12, 0, 40, 4)
                    : new RectOffset(0, 0, 0, 0);
                layout.spacing = detached ? new Vector2(12f, 12f) : new Vector2(20f, 12f);
            }
        }

        if (loadedFoodText != null)
        {
            RectTransform loadedFoodRect = loadedFoodText.rectTransform;
            loadedFoodRect.anchoredPosition = new Vector2(loadedFoodRect.anchoredPosition.x, -26f);
        }

        UpdateGridContentHeight(loadedGrid, InventorySlotLimit);
        RefreshLoadedSlots();
        RefreshStatus();
    }

    public void SetCargoTransactionError(string errorCode)
    {
        cargoTransactionError = errorCode ?? string.Empty;
        RefreshStatus();
    }

    public void Configure(
        long availableGold,
        float maxLoad,
        int foodRequirement,
        TradeItemData[] items,
        int[] stocks,
        WagonData wagon)
    {
        selectedWagon = wagon;
        Configure(availableGold, maxLoad, foodRequirement, items, stocks);
    }

    public bool SetSelectedWagon(WagonData wagon)
    {
        int nextSlotLimit = wagon == null
            ? loadedSlots.Length
            : wagon.InventorySlotCount;
        int usedSlotCount = loadedLines.Count(line => line.HasItem && line.Quantity > 0);

        if (usedSlotCount > nextSlotLimit)
        {
            Debug.LogWarning(
                $"Cannot change wagon: cargo uses {usedSlotCount} slots but the selected wagon allows {nextSlotLimit}.",
                this);
            return false;
        }

        selectedWagon = wagon;
        EnsureDynamicSlots();
        WireSlotInteractions();
        RefreshAll();
        return true;
    }

    public void SetSelectedRoute(RouteData route)
    {
        selectedRoute = route;
        RefreshAll();
    }

    public void SetSelectedDraftAnimals(IEnumerable<DraftAnimalData> animals)
    {
        selectedDraftAnimals = animals?
            .Where(animal => animal != null)
            .ToArray()
            ?? Array.Empty<DraftAnimalData>();
        RefreshAll();
    }

    public void Configure(
        long availableGold,
        float maxLoad,
        TradeItemData[] items,
        int[] stocks,
        RouteData route,
        WagonData wagon)
    {
        selectedRoute = route;
        selectedWagon = wagon;
        Configure(
            availableGold,
            maxLoad,
            route == null ? requiredFood : route.BaseRequiredFoodQuantity,
            items,
            stocks);
    }

    public TradeItemBundle[] BuildTradeItemBundles()
    {
        return loadedLines
            .Where(line => line.HasItem && line.Quantity > 0)
            .GroupBy(line => line.ItemId, StringComparer.Ordinal)
            .Select(group => new TradeItemBundle
            {
                itemId = group.Key,
                quantity = group.Sum(line => line.Quantity),
                purchaseUnitPrice = group.First().UnitPrice,
                sellUnitPrice = group.First().BaseSellPrice
            })
            .ToArray();
    }

    /// <summary>Returns an ID-only snapshot for the detached Caravan cargo-plan command.</summary>
    public CargoSelection[] BuildCargoSelections()
    {
        return loadedLines
            .Where(line => line.HasItem && line.Quantity > 0)
            .GroupBy(line => line.ItemId)
            .Select(group => new CargoSelection
            {
                itemId = group.Key ?? string.Empty,
                quantity = group.Sum(line => line.Quantity)
            })
            .ToArray();
    }

    public void SetMarketDraftContext(string caravanId, string marketId)
    {
        marketDraftCaravanId = caravanId ?? string.Empty;
        marketDraftMarketId = marketId ?? string.Empty;
    }

    public void BackToPreviousStep()
    {
        // Returning to wagon/animal selection can change cargo capacity, so discard only the
        // uncommitted market draft. Saved cargo and previously committed trades remain intact.
        CancelCargoTransactionDraft?.Invoke();
        ClosePurchasePopupImmediate();
        HidePanel(() =>
        {
            if (previousStepPanel != null)
                previousStepPanel.SetActive(true);

            onBackRequested.Invoke();
        });
    }

    public void CancelTradePreparation()
    {
        CancelCargoTransactionDraft?.Invoke();
        ResetCargo();
        HidePanel(() => onTradeCancelled.Invoke());
    }

    public void ContinueToMercenaryHire()
    {
        if (TryCommitDetachedCargoPlan != null)
        {
            if (TryCommitDetachedCargoPlan.Invoke())
            {
                CargoConfirmed?.Invoke(marketDraftCaravanId);
            }
            return;
        }

        if (!CanProceed)
            return;

        // The Cargo purchase UI is independent. Commit first, then close this panel only.
        if (TryCommitCargoTransaction != null && !TryCommitCargoTransaction())
            return;

        CargoConfirmed?.Invoke(marketDraftCaravanId);

        ClosePurchasePopupImmediate();
        HidePanel(null);
    }

    public void ReturnFromMercenaryHire()
    {
        gameObject.SetActive(true);
    }

    public void ResetAfterTradeCompleted()
    {
        ResetCargo();
    }

    public void CancelTradeFromMercenaryHire()
    {
        CancelCargoTransactionDraft?.Invoke();
        ResetCargo();
        onTradeCancelled.Invoke();
    }

    public void DecrementLoadedSlot(int slotIndex)
    {
        if (!cargoEditingEnabled) return;
        LoadedLine line = GetVisibleReservedLine(slotIndex);
        if (line == null) return;
        ReturnToShop(line, 1);
        RefreshAll();
        NotifyLoadChanged();
    }

    public void ClearLoadedSlot(int slotIndex)
    {
        if (!cargoEditingEnabled) return;
        LoadedSlotView slot = GetVisibleLoadedSlot(slotIndex);
        if (slot == null) return;

        // Warehouse/persisted Cargo is read-only in the Market screen. A right click cancels
        // every purchase reservation for this item ID and leaves the saved baseline untouched.
        LoadedLine[] reservations = loadedLines
            .Where(line => !line.IsSaved
                && line.Quantity > 0
                && string.Equals(line.ItemId, slot.ItemId, StringComparison.Ordinal))
            .ToArray();
        foreach (LoadedLine reservation in reservations)
            ReturnToShop(reservation, reservation.Quantity);
        RefreshAll();
        NotifyLoadChanged();
    }

    private void CacheHierarchy()
    {
        panelRect = transform as RectTransform;
        shopGrid = FindDeepChild(transform, "ShopGrid") as RectTransform;
        // 깊이 탐색으로 변경: HLG 컨테이너 등으로 계층이 한 겹 더 감싸져도 안전하게 찾음
        // (기존 transform.Find는 직계 경로만 찾아 LoadedInventoryArea가 중첩되면 null → 적재 그리드 유실)
        loadedGrid = FindDeepChild(transform, "LoadedInventoryGrid") as RectTransform;
        popupOverlay = FindDeepChild(transform, "PurchasePopupOverlay")?.gameObject;
        popupRect = FindDeepChild(transform, "PurchaseAmountPopup") as RectTransform;

        currentLoadText = FindText("CurrentLoadText");
        currentMoneyText = FindText("CurrentMoneyText");
        pendingCostText = FindText("PendingCostText");
        feedConsumptionText = FindText("FeedConsumptionText") ?? FindText("RequiredFoodText");
        loadedFoodText = FindText("LoadedFoodText");
        foodWarningText = FindText("FoodWarningText");

        // S4 is saved before a Route is selected. Only loaded DraftAnimalsFood belongs here;
        // route-dependent requirement and shortage are presented in the departure Summary.
        if (feedConsumptionText != null)
            feedConsumptionText.gameObject.SetActive(false);

        nextButton = FindDeepChild(transform, "NextButton")?.GetComponent<Button>();
        closeButton = FindDeepChild(transform, "CloseButton")?.GetComponent<Button>();

        shopSlots = shopGrid == null
            ? Array.Empty<RectTransform>()
            : shopGrid.Cast<Transform>().OfType<RectTransform>().ToArray();

        loadedSlots = loadedGrid == null
            ? Array.Empty<RectTransform>()
            : loadedGrid.Cast<Transform>().OfType<RectTransform>().ToArray();
    }


    private void EnsureDynamicSlots()
    {
        int requiredShopSlots = Mathf.Max(1, shopItems?.Length ?? 0);
        int requiredLoadedSlots = Mathf.Max(1, InventorySlotLimit);

        EnsureSlotCapacity(shopGrid, requiredShopSlots, "ShopItemSlot");
        EnsureSlotCapacity(loadedGrid, requiredLoadedSlots, "LoadedItemSlot");

        shopSlots = shopGrid == null
            ? Array.Empty<RectTransform>()
            : shopGrid.Cast<Transform>().OfType<RectTransform>().ToArray();
        loadedSlots = loadedGrid == null
            ? Array.Empty<RectTransform>()
            : loadedGrid.Cast<Transform>().OfType<RectTransform>().ToArray();
    }

    private static void EnsureSlotCapacity(RectTransform grid, int requiredCount, string slotNamePrefix)
    {
        if (grid == null || requiredCount <= grid.childCount || grid.childCount == 0)
            return;

        GameObject template = grid.GetChild(0).gameObject;
        for (int index = grid.childCount; index < requiredCount; index++)
        {
            GameObject clone = Instantiate(template, grid, false);
            clone.name = $"{slotNamePrefix}_{index + 1}";
            clone.SetActive(true);
            StyleSection9Slot(clone.transform as RectTransform, 14f);
        }
    }

    private void EnsureGridScrollViews()
    {
        shopScrollRect = EnsureVerticalScroll(shopGrid, "ShopScrollbar");
        loadedScrollRect = EnsureVerticalScroll(loadedGrid, "LoadedInventoryScrollbar");
    }

    private static ScrollRect EnsureVerticalScroll(RectTransform content, string scrollbarName)
    {
        if (content == null || !(content.parent is RectTransform viewport))
            return null;

        ScrollRect scrollRect = viewport.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = viewport.gameObject.AddComponent<ScrollRect>();

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        Scrollbar scrollbar = FindDeepChild(viewport, scrollbarName)?.GetComponent<Scrollbar>();
        if (scrollbar == null)
            scrollbar = CreateVerticalScrollbar(viewport, scrollbarName);

        scrollRect.content = content;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 32f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = -4f;
        return scrollRect;
    }

    private static Scrollbar CreateVerticalScrollbar(RectTransform parent, string objectName)
    {
        GameObject scrollbarObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Scrollbar));
        scrollbarObject.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(-4f, 0f);
        scrollbarRect.sizeDelta = new Vector2(14f, -8f);
        scrollbarObject.GetComponent<Image>().color = new Color(0.16f, 0.14f, 0.14f, 0.45f);

        GameObject handleObject = new GameObject(
            "Handle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        handleObject.transform.SetParent(scrollbarRect, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(2f, 2f);
        handleRect.offsetMax = new Vector2(-2f, -2f);
        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.78f, 0.72f, 0.68f, 0.95f);

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private void EnsureMercenaryHirePanel()
    {
        if (mercenaryStepPanel == null)
        {
            Transform existing = transform.parent == null
                ? null
                : transform.parent.Find("MercenaryHirePanel");

            mercenaryHireController = existing == null
                ? MercenaryHirePanelController.CreateForCargo(this)
                : existing.GetComponent<MercenaryHirePanelController>();

            if (mercenaryHireController != null)
                mercenaryStepPanel = mercenaryHireController.gameObject;
        }
        else
        {
            mercenaryHireController = mercenaryStepPanel.GetComponent<MercenaryHirePanelController>();
            if (mercenaryHireController == null)
                mercenaryHireController = mercenaryStepPanel.AddComponent<MercenaryHirePanelController>();

            mercenaryHireController.Initialize(this);
        }
    }

    private void ApplySection9LayoutAndPalette()
    {
        if (panelRect == null)
            return;

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1176f, 740f);

        SetRect("Header", new Vector2(1176f, 104f), Vector2.zero);
        SetRect("ShopArea", new Vector2(1116f, 412f), new Vector2(32f, -104f));
        SetRect("LoadedInventoryArea", new Vector2(516f, 172f), new Vector2(32f, -536f));
        SetRect("LoadStatusArea", new Vector2(568f, 172f), new Vector2(580f, -536f));

        // [색 하드코딩 비활성화] 패널·영역·버튼·텍스트 색을 캔버스에서 정하도록 강제 색 지정 중단.
        /*
        SetImageColor(transform, new Color32(184, 177, 177, 255));
        SetImageColor("Header", new Color32(184, 177, 177, 255));
        SetImageColor("TitleBackground", new Color32(248, 246, 243, 255));
        SetImageColor("ShopArea", new Color32(105, 94, 93, 255));
        SetImageColor("LoadedInventoryArea", new Color32(242, 239, 237, 255));
        SetImageColor("LoadStatusArea", new Color32(242, 239, 237, 255));
        SetImageColor("ShopAreaLabel", new Color32(105, 94, 93, 255));
        SetImageColor("LoadUiBadge", new Color32(69, 142, 39, 255));
        SetImageColor("CloseButton", new Color32(190, 87, 87, 255));
        SetImageColor("NextButton", new Color32(255, 232, 70, 255));
        SetImageColor("BackButton", new Color32(105, 94, 93, 255));

        SetTextColor("TitleText", new Color32(35, 31, 30, 255));
        SetTextColor("CurrentLoadText", new Color32(45, 40, 39, 255));
        SetTextColor("CurrentMoneyText", new Color32(45, 40, 39, 255));
        SetTextColor("PendingCostText", new Color32(45, 40, 39, 255));
        SetTextColor("RequiredFoodText", new Color32(45, 40, 39, 255));
        SetTextColor("LoadedFoodText", new Color32(45, 40, 39, 255));
        */

        StyleLabel("ShopAreaLabel", "상점", 22f, Color.white);
        StyleLabel("LoadedInventoryLabel", "적재 인벤토리", 22f, Section9TextColor());
        StyleLabel("DataLabel", "적재 상태", 22f, Section9TextColor());
        StyleLabel("CurrentLoadText", null, 22f, Section9TextColor());
        StyleLabel("CurrentMoneyText", null, 22f, Section9TextColor());
        StyleLabel("PendingCostText", null, 22f, Section9TextColor());
        StyleLabel("RequiredFoodText", null, 19f, Section9TextColor());
        StyleLabel("LoadedFoodText", null, 19f, Section9TextColor());
        StyleLabel("FoodWarningText", null, 18f, new Color32(176, 54, 54, 255));

        foreach (RectTransform slot in shopSlots)
            StyleSection9Slot(slot, 14f);
        foreach (RectTransform slot in loadedSlots)
            StyleSection9Slot(slot, 14f);
    }

    private static Color Section9TextColor()
    {
        return new Color32(45, 40, 39, 255);
    }

    private void StyleLabel(string objectName, string replacement, float size, Color color)
    {
        TMP_Text text = FindText(objectName);
        if (text == null)
            return;

        if (!string.IsNullOrEmpty(replacement))
            text.text = replacement;
        text.fontSize = size;
        text.color = color;
    }

    private static void StyleSection9Slot(RectTransform slot, float minimumFontSize)
    {
        if (slot == null)
            return;

        Image background = slot.GetComponent<Image>();
        if (background == null)
            background = slot.gameObject.AddComponent<Image>();
        // [색 하드코딩 비활성화] 슬롯 배경색은 캔버스에서 지정
        // background.color = new Color32(248, 246, 243, 255);
        background.raycastTarget = true;

        Outline outline = slot.GetComponent<Outline>();
        if (outline == null)
            outline = slot.gameObject.AddComponent<Outline>();
        // [색 하드코딩 비활성화] 테두리 색은 캔버스에서 지정
        // outline.effectColor = new Color32(91, 80, 79, 255);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        foreach (TMP_Text text in slot.GetComponentsInChildren<TMP_Text>(true))
        {
            text.fontSize = Mathf.Max(text.fontSize, minimumFontSize);
            // [색 하드코딩 비활성화] 슬롯 글자색은 캔버스에서 지정
            // text.color = new Color32(45, 40, 39, 255);
        }
    }


    private void SetRect(string objectName, Vector2 size, Vector2 position)
    {
        RectTransform rect = FindDeepChild(transform, objectName) as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void SetImageColor(string objectName, Color color)
    {
        Transform target = FindDeepChild(transform, objectName);
        SetImageColor(target, color);
    }

    private static void SetImageColor(Transform target, Color color)
    {
        Image image = target == null ? null : target.GetComponent<Image>();
        if (image != null)
            image.color = color;
    }

    private void SetTextColor(string objectName, Color color)
    {
        TMP_Text text = FindText(objectName);
        if (text != null)
            text.color = color;
    }

    private void EnsureActionButtons()
    {
        Transform header = FindDeepChild(transform, "Header");
        if (header == null)
            return;

        // [뒤로 버튼 제거] 씬에서 지운 뒤로 버튼을 코드가 Awake에서 다시 만들지 않도록 생성 중단.
        //   있으면 참조만 하고, 없으면 null (WireInteractions에서 null 체크되어 안전).
        Transform existing = FindDeepChild(header, "BackButton");
        backButton = existing != null ? existing.GetComponent<Button>() : null;

        // [하드코딩 비활성화] 뒤로/타이틀/구매/닫기 버튼의 위치·크기·글자를 매번 덮어써서
        //   캔버스에서 수정이 안 됐음. → 아래 블록을 비활성화해 프리팹 편집값을 그대로 사용.
        //   (위의 backButton 생성/할당은 그대로라 '뒤로가기' 클릭 연결은 정상 작동)
        //   ※ 주의: 이 값들이 사라지므로, 버튼 위치·글자("무역 물품 적재"/"구매"/"X")는
        //           프리팹에서 직접 설정해 주세요. 되돌리려면 아래 블록 주석을 해제하면 됩니다.
        /*
        if (backButton != null)
        {
            RectTransform backRect = backButton.transform as RectTransform;
            backRect.anchorMin = new Vector2(0.5f, 0.5f);
            backRect.anchorMax = new Vector2(0.5f, 0.5f);
            backRect.pivot = new Vector2(0.5f, 0.5f);
            backRect.sizeDelta = new Vector2(112f, 56f);
            backRect.anchoredPosition = new Vector2(-510f, 0f);
        }

        TMP_Text title = FindText("TitleText");
        if (title != null)
        {
            title.text = "무역 물품 적재";
            title.fontSize = 34f;
        }

        TMP_Text nextLabel = nextButton == null ? null : nextButton.GetComponentInChildren<TMP_Text>(true);
        if (nextLabel != null)
        {
            nextLabel.text = "구매";
            nextLabel.fontSize = 22f;
        }

        if (nextButton != null)
        {
            RectTransform nextRect = nextButton.transform as RectTransform;
            nextRect.sizeDelta = new Vector2(180f, 44f);
            nextRect.anchoredPosition = new Vector2(364f, -116f);
        }

        TMP_Text closeLabel = closeButton == null ? null : closeButton.GetComponentInChildren<TMP_Text>(true);
        if (closeLabel != null)
        {
            closeLabel.text = "X";
            closeLabel.fontSize = 28f;
            closeLabel.fontStyle = FontStyles.Bold;
        }
        */
    }

    private void BuildPurchasePopup()
    {
        if (popupOverlay == null || popupRect == null)
            return;

        Image overlayImage = popupOverlay.GetComponent<Image>();
        if (overlayImage == null)
            overlayImage = popupOverlay.AddComponent<Image>();

        // [색 하드코딩 비활성화] 오버레이 어두운색은 캔버스에서 지정
        // overlayImage.color = new Color(0f, 0f, 0f, 0.62f);
        overlayImage.raycastTarget = true;

        Button overlayButton = popupOverlay.GetComponent<Button>();
        if (overlayButton == null)
            overlayButton = popupOverlay.AddComponent<Button>();

        overlayButton.transition = Selectable.Transition.None;
        overlayButton.onClick.RemoveAllListeners();
        overlayButton.onClick.AddListener(ClosePurchasePopup);

        if (popupRect.GetComponent<CargoPopupClickBlocker>() == null)
            popupRect.gameObject.AddComponent<CargoPopupClickBlocker>();

        // [캔버스 유지] 팝업 판의 위치·크기·색을 캔버스에서 정하도록 강제 배치 비활성화.
        // popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        // popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        // popupRect.pivot = new Vector2(0.5f, 0.5f);
        // popupRect.anchoredPosition = Vector2.zero;
        // popupRect.sizeDelta = new Vector2(560f, 420f);

        Image popupImage = popupRect.GetComponent<Image>();
        if (popupImage == null)
            popupImage = popupRect.gameObject.AddComponent<Image>();

        // popupImage.color = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        popupImage.raycastTarget = true;

        popupTitleText = EnsureText(popupRect, "PopupTitleText", "물품 구매", 28f,
            new Vector2(500f, 44f), new Vector2(0f, 172f), TextAlignmentOptions.Center, FontStyles.Bold);

        popupInfoText = EnsureText(popupRect, "PopupInfoText", string.Empty, 19f,
            new Vector2(490f, 180f), new Vector2(0f, 55f), TextAlignmentOptions.TopLeft, FontStyles.Normal);

        popupCountText = EnsureText(popupRect, "PopupCountText", "1", 25f,
            new Vector2(86f, 48f), new Vector2(0f, -78f), TextAlignmentOptions.Center, FontStyles.Bold);

        popupMinButton = EnsureButton(popupRect, "PopupMinButton", "최소",
            new Vector2(72f, 46f), new Vector2(-184f, -78f));
        popupMinusButton = EnsureButton(popupRect, "PopupMinusButton", "-1",
            new Vector2(72f, 46f), new Vector2(-98f, -78f));
        popupPlusButton = EnsureButton(popupRect, "PopupPlusButton", "+1",
            new Vector2(72f, 46f), new Vector2(98f, -78f));
        popupMaxButton = EnsureButton(popupRect, "PopupMaxButton", "최대",
            new Vector2(72f, 46f), new Vector2(184f, -78f));
        popupLoadButton = EnsureButton(popupRect, "PopupLoadButton", "적재",
            new Vector2(220f, 54f), new Vector2(0f, -154f));

        Button popupClose = EnsureButton(popupRect, "PopupCloseButton", "×",
            new Vector2(44f, 40f), new Vector2(246f, 186f));
        popupClose.onClick.RemoveAllListeners();
        popupClose.onClick.AddListener(ClosePurchasePopup);
    }

    private void InitializeState()
    {
        loadedLines.Clear();
        pendingPurchaseCost = 0;
        selectedShopIndex = -1;
        selectedPurchaseCount = 1;

        remainingStocks = new int[shopItems?.Length ?? 0];
        for (int i = 0; i < remainingStocks.Length; i++)
        {
            int configured = i < initialStocks.Length ? initialStocks[i] : 10;
            remainingStocks[i] = Mathf.Max(0, configured);
        }
    }

    private void WireInteractions()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CancelTradePreparation);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(BackToPreviousStep);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(ContinueToMercenaryHire);
        }

        WireSlotInteractions();

        popupMinusButton?.onClick.AddListener(() => SetPopupCount(selectedPurchaseCount - 1));
        popupPlusButton?.onClick.AddListener(() => SetPopupCount(selectedPurchaseCount + 1));
        popupMinButton?.onClick.AddListener(() => SetPopupCount(1));
        popupMaxButton?.onClick.AddListener(() => SetPopupCount(GetMaximumPurchaseCount()));
        popupLoadButton?.onClick.AddListener(ConfirmPurchase);

        // 슬라이더 → 수량 연결(정수). 드래그하면 SetPopupCount로 반영(-1/+1/최소/최대 버튼과 같은 경로)
        if (purchaseSlider != null)
        {
            purchaseSlider.wholeNumbers = true;
            purchaseSlider.onValueChanged.AddListener(v => SetPopupCount((int)v));
        }
    }

    private void WireSlotInteractions()
    {
        for (int i = 0; i < shopSlots.Length; i++)
        {
            int slotIndex = i;
            Button button = EnsureSlotButton(shopSlots[i]);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OpenPurchasePopup(slotIndex));
        }

        for (int i = 0; i < loadedSlots.Length; i++)
        {
            CargoLoadedSlotClickHandler handler = loadedSlots[i].GetComponent<CargoLoadedSlotClickHandler>();
            if (handler == null)
                handler = loadedSlots[i].gameObject.AddComponent<CargoLoadedSlotClickHandler>();

            handler.Initialize(this, i);
        }
    }

    private void RefreshAll()
    {
        RefreshShopSlots();
        RefreshLoadedSlots();
        RefreshStatus();
        RefreshPopup();
    }

    private void RefreshShopSlots()
    {
        for (int i = 0; i < shopSlots.Length; i++)
        {
            bool hasItem = i < shopItems.Length && shopItems[i] != null;
            shopSlots[i].gameObject.SetActive(hasItem);
            if (!hasItem)
                continue;

            TradeItemData item = shopItems[i];
            Image icon = FindDeepChild(shopSlots[i], "IconArea")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = item.Icon;
                icon.color = item.Icon == null ? new Color(0.24f, 0.27f, 0.31f) : Color.white;
                icon.preserveAspect = true;
            }

            TMP_Text nameText = shopSlots[i].GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "ItemNameBackground");
            TMP_Text countText = FindDeepChild(shopSlots[i], "PriceOrCountText")?.GetComponent<TMP_Text>();
            TMP_Text priceText = shopSlots[i].GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "Text (TMP)");

            if (nameText != null)
                nameText.text = item.DisplayName;
            if (countText != null)
                countText.text = $"재고 {remainingStocks[i]}";
            if (priceText != null)
                priceText.text = $"{GetMarketBuyUnitPrice(i):N0} G";

            Button button = shopSlots[i].GetComponent<Button>();
            if (button != null)
                button.interactable = cargoEditingEnabled
                    && remainingStocks[i] > 0
                    && GetAffordableCount(i) > 0;
        }

        UpdateGridContentHeight(shopGrid, shopItems?.Length ?? 0);
    }

    private void RefreshLoadedSlots()
    {
        List<LoadedSlotView> visibleLines = BuildLoadedSlotViews();

        for (int i = 0; i < loadedSlots.Length; i++)
        {
            bool isAvailable = i < InventorySlotLimit;
            loadedSlots[i].gameObject.SetActive(isAvailable);
            if (!isAvailable)
                continue;

            bool hasLine = i < visibleLines.Count;
            LoadedSlotView slot = hasLine ? visibleLines[i] : null;
            LoadedLine line = slot?.PresentationLine;
            Image icon = FindDeepChild(loadedSlots[i], "ItemIcon")?.GetComponent<Image>();
            TMP_Text quantity = FindDeepChild(loadedSlots[i], "QuantityText")?.GetComponent<TMP_Text>();
            TMP_Text foodBadge = FindDeepChild(loadedSlots[i], "FoodBadge")?.GetComponent<TMP_Text>();

            if (icon != null)
            {
                icon.enabled = hasLine;
                icon.sprite = hasLine ? line.Icon : null;
                icon.color = hasLine && line.Icon == null
                    ? new Color(0.24f, 0.27f, 0.31f)
                    : Color.white;
                icon.preserveAspect = true;
            }

            bool detached = TryCommitDetachedCargoPlan != null;
            if (quantity != null)
                quantity.text = hasLine && !detached ? slot.Quantity.ToString() : string.Empty;

            if (foodBadge != null)
            {
                bool showBadge = hasLine && (detached || IsFood(line));
                foodBadge.gameObject.SetActive(showBadge);
                foodBadge.text = !showBadge
                    ? string.Empty
                    : detached ? slot.Quantity.ToString() : "먹이";
            }
        }

        UpdateGridContentHeight(loadedGrid, InventorySlotLimit);
    }


    private void UpdateGridContentHeight(RectTransform grid, int visibleSlotCount)
    {
        if (grid == null)
            return;

        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        if (layout == null)
            return;

        int columnCount = layout.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            ? Mathf.Max(1, layout.constraintCount)
            : Mathf.Max(1, visibleSlotCount);
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, visibleSlotCount) / (float)columnCount));
        float height = layout.padding.top
            + layout.padding.bottom
            + rowCount * layout.cellSize.y
            + Mathf.Max(0, rowCount - 1) * layout.spacing.y;

        if (grid == loadedGrid)
        {
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(0f, 1f);
            grid.pivot = new Vector2(0f, 1f);
            grid.anchoredPosition = new Vector2(20f, grid.anchoredPosition.y);
            grid.sizeDelta = new Vector2(476f, grid.sizeDelta.y);
        }
        else
        {
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.anchoredPosition = new Vector2(0f, grid.anchoredPosition.y);
            grid.sizeDelta = new Vector2(0f, grid.sizeDelta.y);
        }

        grid.sizeDelta = new Vector2(grid.sizeDelta.x, height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
    }

    private void RefreshStatus()
    {
        if (currentLoadText != null)
            currentLoadText.text = $"Load  {CurrentLoad:0.##} / {MaximumLoad:0.##}";

        if (currentMoneyText != null)
            currentMoneyText.text = $"소지 골드  {currentGold:N0} G";

        if (pendingCostText != null)
        {
            pendingCostText.text = $"구매 금액  {pendingPurchaseCost:N0} G";
            pendingCostText.color = pendingPurchaseCost > currentGold
                ? new Color(1f, 0.34f, 0.3f)
                : Color.white;
        }

        if (feedConsumptionText != null)
            feedConsumptionText.text = $"Feed Consumption  {TotalFeedConsumption:0.##}/s";

        if (loadedFoodText != null)
            loadedFoodText.text = $"적재 먹이  {LoadedFood}";

        if (foodWarningText != null)
        {
            if (!string.IsNullOrWhiteSpace(cargoTransactionError))
            {
                foodWarningText.text = $"Transaction failed: {cargoTransactionError}";
                foodWarningText.color = new Color(1f, 0.34f, 0.3f);
            }
            else if (CurrentLoad > MaximumLoad)
            {
                foodWarningText.text = "최대 적재량을 초과했습니다.";
                foodWarningText.color = new Color(1f, 0.34f, 0.3f);
            }
            else if (LoadedSlotCount > InventorySlotLimit)
            {
                foodWarningText.text = "적재 가능한 슬롯 수를 초과했습니다.";
                foodWarningText.color = new Color(1f, 0.34f, 0.3f);
            }
            else
            {
                foodWarningText.text = "용병 고용으로 진행할 수 있습니다.";
                foodWarningText.color = new Color(0.45f, 0.9f, 0.5f);
            }
        }

        if (nextButton != null)
            nextButton.interactable = CanProceed;
    }

    private void OpenPurchasePopup(int shopIndex)
    {
        if (!cargoEditingEnabled)
            return;

        if (shopIndex < 0 || shopIndex >= shopItems.Length || shopItems[shopIndex] == null)
            return;

        selectedShopIndex = shopIndex;
        selectedPurchaseCount = 1;

        if (GetMaximumPurchaseCount() < 1)
            return;

        popupOverlay.SetActive(true);
        popupRect.localScale = Vector3.zero;

        if (popupAnimation != null)
            StopCoroutine(popupAnimation);

        popupAnimation = StartCoroutine(ScaleRoutine(popupRect, Vector3.one, 0.18f, null));
        RefreshPopup();
    }

    private void ClosePurchasePopup()
    {
        if (popupOverlay == null || !popupOverlay.activeSelf)
            return;

        if (popupAnimation != null)
            StopCoroutine(popupAnimation);

        popupAnimation = StartCoroutine(ScaleRoutine(
            popupRect,
            Vector3.zero,
            0.14f,
            () => popupOverlay.SetActive(false)));
    }

    private void ClosePurchasePopupImmediate()
    {
        if (popupOverlay == null)
            return;

        if (popupAnimation != null)
            StopCoroutine(popupAnimation);

        popupRect.localScale = Vector3.zero;
        popupOverlay.SetActive(false);
        selectedShopIndex = -1;
    }

    private void RefreshPopup()
    {
        if (selectedShopIndex < 0 || selectedShopIndex >= shopItems.Length)
            return;

        TradeItemData item = shopItems[selectedShopIndex];
        if (item == null)
            return;

        int maximum = GetMaximumPurchaseCount();
        selectedPurchaseCount = Mathf.Clamp(selectedPurchaseCount, maximum > 0 ? 1 : 0, maximum);

        // 슬라이더 범위 = 허용 최대 구매수. SetValueWithoutNotify로 되먹임 루프 방지
        if (purchaseSlider != null)
        {
            purchaseSlider.minValue = maximum > 0 ? 1 : 0;
            purchaseSlider.maxValue = maximum;
            purchaseSlider.SetValueWithoutNotify(selectedPurchaseCount);
        }

        if (popupTitleText != null)
            popupTitleText.text = item.DisplayName;

        if (popupInfoText != null)
        {
            string specialty = item.LocalSpecialty ? "특산품" : "일반 상품";
            popupInfoText.text =
                $"{item.Description}\n\n" +
                $"분류  {item.Category}    등급  {item.Rarity}\n" +
                $"가격  {GetMarketBuyUnitPrice(selectedShopIndex):N0} G    무게  {item.Weight:0.##}\n" +
                $"{specialty}    재고  {remainingStocks[selectedShopIndex]}";
        }

        if (popupCountText != null)
            popupCountText.text = selectedPurchaseCount.ToString();

        popupMinusButton.interactable = selectedPurchaseCount > 1;
        popupPlusButton.interactable = selectedPurchaseCount < maximum;
        popupMinButton.interactable = maximum > 0 && selectedPurchaseCount != 1;
        popupMaxButton.interactable = maximum > 0 && selectedPurchaseCount != maximum;
        popupLoadButton.interactable = maximum > 0 && selectedPurchaseCount > 0;

        TMP_Text loadLabel = popupLoadButton.GetComponentInChildren<TMP_Text>(true);
        if (loadLabel != null)
            loadLabel.text = $"적재  {(GetMarketBuyUnitPrice(selectedShopIndex) * selectedPurchaseCount):N0} G";
    }

    private void SetPopupCount(int value)
    {
        int maximum = GetMaximumPurchaseCount();
        selectedPurchaseCount = Mathf.Clamp(value, maximum > 0 ? 1 : 0, maximum);
        RefreshPopup();
    }

    private int GetMaximumPurchaseCount()
    {
        if (selectedShopIndex < 0 || selectedShopIndex >= shopItems.Length)
            return 0;

        TradeItemData item = shopItems[selectedShopIndex];
        if (item == null)
            return 0;

        int byStock = remainingStocks[selectedShopIndex];
        int byMoney = GetAffordableCount(selectedShopIndex);
        int byWeight = item.Weight <= 0f
            ? byStock
            : Mathf.FloorToInt(Mathf.Max(0f, MaximumLoad - CurrentLoad) / item.Weight);

        int bySlotCapacity = GetAvailableSlotCapacity(item);
        return Mathf.Max(0, Mathf.Min(byStock, byMoney, byWeight, bySlotCapacity));
    }

    private int GetAvailableSlotCapacity(TradeItemData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
            return 0;

        int slotsUsedByOtherItems = loadedLines
            .Where(line => line.HasItem
                && line.Quantity > 0
                && !string.Equals(line.ItemId, item.ItemId, StringComparison.Ordinal))
            .GroupBy(line => line.ItemId, StringComparer.Ordinal)
            .Sum(group =>
            {
                TradeItemData definition = FindShopItem(group.Key);
                bool canStack = definition != null ? definition.CanStack : group.First().CanStack;
                int maxCount = definition != null ? definition.MaxCount : group.First().MaxCount;
                return RequiredSlots(group.Sum(line => line.Quantity), canStack, maxCount);
            });
        int slotsAvailableForItem = Mathf.Max(0, InventorySlotLimit - slotsUsedByOtherItems);
        int currentQuantity = loadedLines
            .Where(line => line.HasItem
                && string.Equals(line.ItemId, item.ItemId, StringComparison.Ordinal))
            .Sum(line => Mathf.Max(0, line.Quantity));

        int capacity = item.CanStack
            ? slotsAvailableForItem * Mathf.Max(1, item.MaxCount) - currentQuantity
            : slotsAvailableForItem - currentQuantity;
        return Mathf.Max(0, capacity);
    }

    private TradeItemData FindShopItem(string itemId)
    {
        return (shopItems ?? Array.Empty<TradeItemData>())
            .FirstOrDefault(item => item != null
                && string.Equals(item.ItemId, itemId, StringComparison.Ordinal));
    }


    private int GetAffordableCount(int shopIndex)
    {
        long available = TryCommitDetachedCargoPlan != null
            ? Math.Max(0L, currentGold - pendingPurchaseCost)
            : ProjectedCurrencyAfterCargoTransaction != null
            ? Math.Max(0L, ProjectedCurrencyAfterCargoTransaction())
            : Math.Max(0L, currentGold - pendingPurchaseCost);
        long unitPrice = GetMarketBuyUnitPrice(shopIndex);
        if (unitPrice <= 0)
            return int.MaxValue;

        return (int)Math.Min(int.MaxValue, available / unitPrice);
    }

    private long GetMarketBuyUnitPrice(int shopIndex)
    {
        if (shopIndex >= 0
            && shopIndex < marketBuyUnitPrices.Length
            && marketBuyUnitPrices[shopIndex] >= 0L)
        {
            return marketBuyUnitPrices[shopIndex];
        }

        return shopIndex >= 0 && shopIndex < shopItems.Length && shopItems[shopIndex] != null
            ? Math.Max(0L, shopItems[shopIndex].BaseBuyPrice)
            : 0L;
    }

    private void ConfirmPurchase()
    {
        if (!cargoEditingEnabled)
            return;

        if (selectedShopIndex < 0 || selectedPurchaseCount <= 0)
            return;

        TradeItemData item = shopItems[selectedShopIndex];
        int maximum = GetMaximumPurchaseCount();
        int purchaseCount = Mathf.Clamp(selectedPurchaseCount, 0, maximum);
        if (item == null || purchaseCount <= 0)
            return;

        long unitPrice = GetMarketBuyUnitPrice(selectedShopIndex);
        if (!TryAddLoadedQuantity(item, purchaseCount, unitPrice))
            return;

        remainingStocks[selectedShopIndex] -= purchaseCount;
        pendingPurchaseCost += unitPrice * purchaseCount;

        ClosePurchasePopup();
        RefreshAll();
        // Confirmed UI cargo is still a preparation choice; this reports it without charging currency.
        NotifyLoadChanged();
    }

    /// <summary>
    /// Combines quantities split across UI slots and publishes one final quantity per item ID.
    /// This method reports UI selection only and does not mutate currency or store inventory.
    /// </summary>
    private void NotifyLoadChanged()
    {
        cargoTransactionError = string.Empty;
        var quantityByItemId = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (LoadedLine line in loadedLines)
        {
            if (line == null || !line.HasItem || line.Quantity <= 0)
                continue;

            string itemId = line.ItemId;
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            int currentQuantity;
            if (quantityByItemId.TryGetValue(itemId, out currentQuantity))
                quantityByItemId[itemId] = currentQuantity + line.Quantity;
            else
                quantityByItemId.Add(itemId, line.Quantity);
        }

        var snapshot = new List<CargoSelection>(quantityByItemId.Count);
        foreach (KeyValuePair<string, int> pair in quantityByItemId)
        {
            snapshot.Add(new CargoSelection
            {
                itemId = pair.Key,
                quantity = pair.Value
            });
        }

        LoadChanged?.Invoke(new CargoChangeSnapshot
        {
            caravanId = marketDraftCaravanId,
            marketId = marketDraftMarketId,
            items = snapshot
        });
    }

    private bool TryAddLoadedQuantity(TradeItemData item, int quantity, long unitPrice, bool ignoreSlotCapacity = false, bool isSaved = false)
    {
        if (item == null || quantity <= 0 || (!ignoreSlotCapacity && GetAvailableSlotCapacity(item) < quantity)) return false;
        int remaining = quantity;
        int stackLimit = item.CanStack ? item.MaxCount : 1;
        if (item.CanStack)
        {
            foreach (LoadedLine line in loadedLines.Where(line => line.Item == item && line.IsSaved == isSaved && line.Quantity < stackLimit))
            {
                int added = Mathf.Min(stackLimit - line.Quantity, remaining);
                line.Quantity += added;
                remaining -= added;
                if (remaining <= 0) return true;
            }
        }
        while (remaining > 0)
        {
            int added = Mathf.Min(stackLimit, remaining);
            loadedLines.Add(new LoadedLine { Item = item, Quantity = added, UnitPrice = unitPrice, IsSaved = isSaved });
            remaining -= added;
        }
        return true;
    }
    private void TryAddSavedQuantity(TradeItemViewData item, int quantity)
    {
        if (item == null || quantity <= 0 || string.IsNullOrWhiteSpace(item.itemId))
            return;

        LoadedLine existing = loadedLines.FirstOrDefault(line =>
            line.IsSaved && string.Equals(line.ItemId, item.itemId, StringComparison.Ordinal));
        if (existing != null)
        {
            existing.Quantity += quantity;
            return;
        }

        loadedLines.Add(new LoadedLine
        {
            SavedItem = item,
            Quantity = quantity,
            UnitPrice = 0L,
            IsSaved = true
        });
    }


    private void ReturnToShop(LoadedLine line, int requestedCount)
    {
        int count = Mathf.Clamp(requestedCount, 0, line.Quantity);
        if (count <= 0)
            return;

        line.Quantity -= count;
        pendingPurchaseCost = Math.Max(0, pendingPurchaseCost - line.UnitPrice * count);

        int shopIndex = Array.IndexOf(shopItems, line.Item);
        if (shopIndex >= 0 && shopIndex < remainingStocks.Length)
            remainingStocks[shopIndex] += count;

        if (line.Quantity <= 0)
            loadedLines.Remove(line);
    }

    private List<LoadedSlotView> BuildLoadedSlotViews()
    {
        return loadedLines
            .Where(line => line.HasItem && line.Quantity > 0)
            .GroupBy(line => line.ItemId, StringComparer.Ordinal)
            .Select(group => new LoadedSlotView
            {
                ItemId = group.Key,
                PresentationLine = group.FirstOrDefault(line => line.IsSaved) ?? group.First(),
                Quantity = group.Sum(line => line.Quantity)
            })
            .ToList();
    }

    private LoadedSlotView GetVisibleLoadedSlot(int slotIndex)
    {
        if (slotIndex < 0)
            return null;
        List<LoadedSlotView> slots = BuildLoadedSlotViews();
        return slotIndex < slots.Count ? slots[slotIndex] : null;
    }

    private LoadedLine GetVisibleReservedLine(int slotIndex)
    {
        LoadedSlotView slot = GetVisibleLoadedSlot(slotIndex);
        return slot == null
            ? null
            : loadedLines.FirstOrDefault(line => !line.IsSaved
                && line.Quantity > 0
                && string.Equals(line.ItemId, slot.ItemId, StringComparison.Ordinal));
    }

    private void ResetCargo()
    {
        InitializeState();
        ClosePurchasePopupImmediate();
        RefreshAll();
        // An empty snapshot clears every cargo item from Runtime Draft after an explicit reset.
        NotifyLoadChanged();
    }

    private void HidePanel(Action afterHidden)
    {
        if (!gameObject.activeInHierarchy)
        {
            afterHidden?.Invoke();
            return;
        }

        if (panelAnimation != null)
            StopCoroutine(panelAnimation);

        panelAnimation = StartCoroutine(ScaleRoutine(panelRect, Vector3.zero, 0.18f, () =>
        {
            afterHidden?.Invoke();
            gameObject.SetActive(false);
        }));
    }

    private static bool IsFood(LoadedLine line)
    {
        return line != null && line.Category == TradeItemCategory.DraftAnimalsFood;
    }

    private TMP_Text FindText(string objectName)
    {
        return FindDeepChild(transform, objectName)?.GetComponent<TMP_Text>();
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (root.name == objectName)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeepChild(child, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Button EnsureSlotButton(RectTransform slot)
    {
        Image image = slot.GetComponent<Image>();
        if (image == null)
        {
            image = slot.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
        }

        image.raycastTarget = true;

        Button button = slot.GetComponent<Button>();
        if (button == null)
            button = slot.gameObject.AddComponent<Button>();

        button.targetGraphic = image;
        return button;
    }

    private static TMP_Text EnsureText(
        Transform parent,
        string objectName,
        string value,
        float fontSize,
        Vector2 size,
        Vector2 position,
        TextAlignmentOptions alignment,
        FontStyles fontStyle)
    {
        Transform existing = FindDeepChild(parent, objectName);
        TMP_Text text = existing == null ? null : existing.GetComponent<TMP_Text>();
        // [캔버스 유지] 이미 씬에 있으면 위치·크기·글자를 건드리지 않고 참조만 반환한다.
        if (text != null)
            return text;

        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            text = go.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button EnsureButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 size,
        Vector2 position)
    {
        Transform existing = FindDeepChild(parent, objectName);
        Button button = existing == null ? null : existing.GetComponent<Button>();
        // [캔버스 유지] 이미 씬에 있으면 글자·위치를 건드리지 않고 참조만 반환한다.
        if (button != null)
            return button;

        button = CreateButton(parent, objectName, label, size, position);
        return button;
    }

    private static Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 size,
        Vector2 position)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.82f, 0.61f, 0.18f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = EnsureText(go.transform, "Label", label, 20f, size, Vector2.zero,
            TextAlignmentOptions.Center, FontStyles.Bold);
        text.color = new Color(0.08f, 0.07f, 0.05f);

        return button;
    }

    private static IEnumerator ScaleRoutine(
        RectTransform target,
        Vector3 destination,
        float duration,
        Action completed)
    {
        if (target == null)
        {
            completed?.Invoke();
            yield break;
        }

        Vector3 start = target.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            target.localScale = Vector3.LerpUnclamped(start, destination, eased);
            yield return null;
        }

        target.localScale = destination;
        completed?.Invoke();
    }
}

public sealed class CargoLoadedSlotClickHandler : MonoBehaviour, IPointerClickHandler
{
    private CargoLoadingPanelController controller;
    private int slotIndex;

    public void Initialize(CargoLoadingPanelController owner, int index)
    {
        controller = owner;
        slotIndex = index;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
            controller.ClearLoadedSlot(slotIndex);
        else if (eventData.button == PointerEventData.InputButton.Left)
            controller.DecrementLoadedSlot(slotIndex);
    }
}

public sealed class CargoPopupClickBlocker : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
    }
}
