using System;
using System.Collections.Generic;
using System.Linq;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.InGame.Warehouse
{
    /// <summary>
    /// MainUI와 분리된 Warehouse Popup runtime 경계다.
    /// View는 사용자 의도만 전달하고, 이 Controller가 매 단계 최신 SaveData를 다시 읽는다.
    /// </summary>
    public sealed class WarehouseInventoryPopupController : MonoBehaviour
    {
        [Header("Warehouse-only prefabs")]
        [SerializeField] private GameObject inventorySlotPrefab;
        [SerializeField] private GameObject caravanSlotPrefab;
        [SerializeField] private GameObject priceGroupRowPrefab;
        [SerializeField, Min(0.1f)] private float repeatedInputGuardSeconds = 0.15f;

        private readonly WarehouseInventoryPopupPresenter presenter = new WarehouseInventoryPopupPresenter();
        private GameObject caravanSelectionPanel;
        private GameObject cargoPanel;
        private GameObject selectionModalLayer;
        private GameObject tooltipLayer;
        private GameObject tooltip;
        private GameObject priceGroupModal;
        private GameObject quantityModal;
        private Transform warehouseContent;
        private Transform cargoContent;
        private Transform caravanContent;
        private Transform priceRowsContent;
        private TMP_Text warehouseCapacityText;
        private TMP_Text cargoCapacityText;
        private TMP_Text loadText;
        private TMP_Text maxLoadText;
        private TMP_Text guideText;
        private TMP_Text cargoTitleText;
        private TMP_Text tooltipNameText;
        private TMP_Text tooltipPriceText;
        private TMP_Text tooltipDescriptionText;
        private TMP_Text selectedItemNameText;
        private TMP_Text selectedItemTotalText;
        private Image selectedItemIcon;
        private TMP_Text selectedQuantityText;
        private TMP_Text quantityDirectionText;
        private TMP_Text quantitySourceText;
        private TMP_Text quantityDestinationText;
        private Button closeButton;
        private Button backdropButton;
        private Button modalBlockerButton;
        private Button backToCaravanButton;
        private Button priceCancelButton;
        private Button quantityCancelButton;
        private Button minusButton;
        private Button plusButton;
        private Button minButton;
        private Button maxButton;
        private Button confirmButton;

        private float nextAcceptedInputTime;
        private WarehouseTransferDirection selectedDirection;
        private string selectedItemId = string.Empty;
        private int selectedQuantity = 1;
        
        private bool suppressEventRefresh;
private int selectedMaxQuantity;

        public WarehouseState CurrentState { get; private set; }
        public string SelectedCaravanId { get; private set; } = string.Empty;

        private void Awake()
        {
            ResolveReferences();
            WireButtons();
        }

        private void OnEnable()
        {
            FrameworkEvents.LoadCompleted += OnSaveLoaded;
            FrameworkEvents.HomeInventoryChanged += RefreshAll;
            
            FrameworkEvents.CaravanCreated += OnCaravanCreated;
FrameworkEvents.CaravanCargoChanged += OnCaravanCargoChanged;
        }

        private void OnDisable()
        {
            FrameworkEvents.LoadCompleted -= OnSaveLoaded;
            FrameworkEvents.HomeInventoryChanged -= RefreshAll;
            
            FrameworkEvents.CaravanCreated -= OnCaravanCreated;
FrameworkEvents.CaravanCargoChanged -= OnCaravanCargoChanged;
        }

        /// <summary>
        /// MainUI 후속 연결은 이 메서드 하나만 호출한다. Lv.0은 건물 미보유이므로 열지 않는다.
        /// </summary>
        public bool TryOpen()
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            CurrentState = WarehouseFunction.Evaluate(root != null ? root.CurrentSaveData : null);
            if (!CurrentState.CanOpen
                || !string.Equals(root?.CurrentSaveData?.player?.currentTownId,
                    WarehouseFunction.BaseTownId, StringComparison.Ordinal))
            {
                gameObject.SetActive(false);
                return false;
            }

            gameObject.SetActive(true);
            ResetTransientState();
            RefreshAll();
            return true;
        }

        public static bool IsEligible(ND.Framework.CaravanSaveData caravan, string baseTownId)
        {
            return caravan != null
                && !string.IsNullOrWhiteSpace(caravan.caravanId)
                && caravan.state == JourneyState.Prepare
                && string.Equals(caravan.currentTownId, baseTownId, StringComparison.Ordinal);
        }

        public bool SelectCaravan(string caravanId)
        {
            if (!AcceptInput() || string.IsNullOrWhiteSpace(caravanId)) return false;
            ND.Framework.SaveData save = CurrentSave();
            ND.Framework.CaravanSaveData caravan = FindCaravan(save, caravanId);
            string baseTownId = WarehouseFunction.BaseTownId;
            if (!IsEligible(caravan, baseTownId)) return false;

            // index가 아닌 영속 ID를 보관해야 Caravan 목록 정렬이 바뀌어도 다른 Cargo로 이동하지 않는다.
            SelectedCaravanId = caravan.caravanId;
            SetActive(caravanSelectionPanel, false);
            SetActive(cargoPanel, true);
            CloseSelection();
            RefreshInventories();
            return true;
        }

        public void ResetTransientState()
        {
            SelectedCaravanId = string.Empty;
            selectedItemId = string.Empty;
            selectedQuantity = 1;
            selectedMaxQuantity = 0;
            nextAcceptedInputTime = 0f;
            presenter.CancelSelection();
            SetActive(caravanSelectionPanel, true);
            SetActive(cargoPanel, false);
            CloseSelection();
            RefreshCaravans();
        }

        /// <summary>Revalidates Warehouse and Caravan authority before rebuilding the visible state.</summary>
        private void RefreshAll()
        {
            ND.Framework.SaveData save = CurrentSave();
            CurrentState = WarehouseFunction.Evaluate(save);
            if (!CurrentState.CanOpen
                || !string.Equals(save?.player?.currentTownId,
                    WarehouseFunction.BaseTownId, StringComparison.Ordinal))
            {
                gameObject.SetActive(false);
                return;
            }

            RefreshCaravans();
            if (!string.IsNullOrEmpty(SelectedCaravanId))
            {
                ND.Framework.CaravanSaveData caravan = FindCaravan(save, SelectedCaravanId);
                string baseTownId = WarehouseFunction.BaseTownId;
                if (!IsEligible(caravan, baseTownId))
                {
                    ResetTransientState();
                    return;
                }
            }
            RefreshInventories();
        }

        /// <summary>Rebuilds selection rows with persistent caravan IDs rather than list positions.</summary>
        private void RefreshCaravans()
        {
            if (caravanContent == null || caravanSlotPrefab == null) return;
            ClearGenerated(caravanContent);
            ND.Framework.SaveData save = CurrentSave();
            if (save?.caravans == null) return;
            string baseTownId = WarehouseFunction.BaseTownId;

            foreach (ND.Framework.CaravanSaveData caravan in save.caravans.OrderBy(c => c?.slotIndex ?? int.MaxValue))
            {
                if (caravan == null) continue;
                bool eligible = IsEligible(caravan, baseTownId);
                string reason = eligible ? string.Empty
                    : caravan.state != JourneyState.Prepare ? "준비 상태가 아닙니다."
                    : !string.Equals(caravan.currentTownId, baseTownId, StringComparison.Ordinal)
                        ? "BaseCamp에 없습니다." : "선택할 수 없습니다.";
                GameObject instance = Instantiate(caravanSlotPrefab, caravanContent);
                instance.name = "Caravan_" + caravan.caravanId;
                WarehouseCaravanSlotView view = instance.GetComponent<WarehouseCaravanSlotView>()
                    ?? instance.AddComponent<WarehouseCaravanSlotView>();
                view.Bind(caravan, eligible, reason, id => SelectCaravan(id));
            }
        }

        /// <summary>Reads the latest SaveData and refreshes both inventories, capacity, title, and load.</summary>
        private void RefreshInventories()
        {
            ND.Framework.SaveData save = CurrentSave();
            ISharedGameDataProvider catalog = FrameworkRoot.Instance != null
                ? FrameworkRoot.Instance.SharedGameData : null;

            IReadOnlyList<WarehouseInventorySlotViewData> home = WarehouseInventoryViewDataBuilder.BuildSlots(
                save?.player?.homeInventory, catalog);
            PopulateSlots(warehouseContent, home, CurrentState.SlotCount, WarehouseTransferDirection.HomeToCargo);
            if (warehouseCapacityText != null)
                warehouseCapacityText.text = home.Count + " / " + CurrentState.SlotCount;

            ND.Framework.CaravanSaveData caravan = FindCaravan(save, SelectedCaravanId);
            if (cargoTitleText != null)
                cargoTitleText.text = caravan != null
                    ? "Caravan " + (Math.Max(0, caravan.slotIndex) + 1) + " Cargo"
                    : "Cargo";

            int cargoSlots = Math.Max(0, caravan?.wagon?.inventorySlotCount ?? 0);
            IReadOnlyList<WarehouseInventorySlotViewData> cargo = WarehouseInventoryViewDataBuilder.BuildSlots(
                caravan?.cargo, catalog);
            PopulateSlots(cargoContent, cargo, cargoSlots, WarehouseTransferDirection.CargoToHome);
            if (cargoCapacityText != null) cargoCapacityText.text = cargo.Count + " / " + cargoSlots;

            float currentLoad = CalculateCargoWeight(caravan);
            float maxLoad = Math.Max(0f, caravan?.wagon?.maxLoad ?? 0f);
            if (loadText != null) loadText.text = "현재 적재량: " + currentLoad.ToString("0.##");
            if (maxLoadText != null) maxLoadText.text = "최대 적재량: " + maxLoad.ToString("0.##");
        }

        /// <summary>Renders exactly the authoritative slot limit, including empty slots.</summary>
        private void PopulateSlots(
            Transform content,
            IReadOnlyList<WarehouseInventorySlotViewData> data,
            int slotLimit,
            WarehouseTransferDirection direction)
        {
            if (content == null || inventorySlotPrefab == null) return;
            ClearGenerated(content);
            int renderCount = Math.Max(0, slotLimit);
            for (int i = 0; i < renderCount; i++)
            {
                WarehouseInventorySlotViewData item = i < data.Count ? data[i] : null;
                GameObject instance = Instantiate(inventorySlotPrefab, content);
                instance.name = item != null ? "Item_" + item.ItemId + "_" + item.StackIndex : "Empty_" + i;
                WarehouseInventorySlotView view = instance.GetComponent<WarehouseInventorySlotView>()
                    ?? instance.AddComponent<WarehouseInventorySlotView>();
                view.Bind(item,
                    itemId => SelectItem(itemId, direction),
                    ShowTooltip,
                    HideTooltip);
            }
        }

        private void ShowTooltip(string itemId, RectTransform slot)
        {
            if (presenter.Panel != WarehouseSelectionPanel.None || tooltip == null) return;
            WarehouseItemTooltipViewData data = WarehouseInventoryViewDataBuilder.BuildTooltip(
                itemId, FrameworkRoot.Instance != null ? FrameworkRoot.Instance.SharedGameData : null);
            if (tooltipNameText != null) tooltipNameText.text = data.DisplayName;
            if (tooltipPriceText != null) tooltipPriceText.text = "기본 구매가  " + data.BaseBuyPrice + " G";
            if (tooltipDescriptionText != null) tooltipDescriptionText.text = data.Description;
            SetActive(tooltipLayer, true);
            SetActive(tooltip, true);
        }

        private void HideTooltip()
        {
            SetActive(tooltip, false);
            SetActive(tooltipLayer, false);
        }

        /// <summary>Builds price groups from the directional source and enters the required selection panel.</summary>
        private void SelectItem(string itemId, WarehouseTransferDirection direction)
        {
            // Caravan 선택 화면에서도 Player item 정보 hover는 허용한다.
            // 다만 목적지 Cargo가 확정되기 전에는 가격/수량 선택을 시작하지 않아
            // InvalidCaravan으로 Modal이 조용히 닫히는 잘못된 흐름을 차단한다.
            if (direction == WarehouseTransferDirection.HomeToCargo)
            {
                ND.Framework.CaravanSaveData selectedCaravan = FindCaravan(CurrentSave(), SelectedCaravanId);
                if (!IsEligible(selectedCaravan, WarehouseFunction.BaseTownId)) return;
            }
            if (!AcceptInput()) return;
            ND.Framework.SaveData save = CurrentSave();
            ND.Framework.CaravanSaveData caravan = FindCaravan(save, SelectedCaravanId);
            IEnumerable<CargoEntrySaveData> source = direction == WarehouseTransferDirection.HomeToCargo
                ? save?.player?.homeInventory : caravan?.cargo;
            selectedDirection = direction;
            selectedItemId = itemId ?? string.Empty;
            HideTooltip();

            WarehousePriceGroupModalViewData data = presenter.SelectItem(
                source, selectedItemId,
                FrameworkRoot.Instance != null ? FrameworkRoot.Instance.SharedGameData : null);
            if (data == null || data.Groups.Count == 0) return;

            BindSelectedItemSummary(data);
            if (data.Groups.Count == 1)
            {
                OpenQuantity(data.Groups[0]);
                return;
            }

            PopulatePriceRows(data.Groups);
            SetActive(selectionModalLayer, true);
            SetActive(priceGroupModal, true);
            SetActive(quantityModal, false);
        }

        private void BindSelectedItemSummary(WarehousePriceGroupModalViewData data)
        {
            if (selectedItemNameText != null) selectedItemNameText.text = data.DisplayName;
            if (selectedItemTotalText != null) selectedItemTotalText.text = "총 보유 " + data.TotalQuantity + "개";
            if (selectedItemIcon != null)
            {
                selectedItemIcon.sprite = data.Icon;
                selectedItemIcon.enabled = data.Icon != null;
            }
        }

        private void PopulatePriceRows(IReadOnlyList<WarehousePriceGroup> groups)
        {
            if (priceRowsContent == null || priceGroupRowPrefab == null) return;
            ClearGenerated(priceRowsContent);
            foreach (WarehousePriceGroup group in groups)
            {
                GameObject instance = Instantiate(priceGroupRowPrefab, priceRowsContent);
                WarehousePriceGroupRowView view = instance.GetComponent<WarehousePriceGroupRowView>()
                    ?? instance.AddComponent<WarehousePriceGroupRowView>();
                view.Bind(group, OpenQuantity);
            }
        }

        /// <summary>Derives the current maximum from the chosen group and destination capacity.</summary>
        private void OpenQuantity(WarehousePriceGroup group)
        {
            if (presenter.Panel == WarehouseSelectionPanel.PriceGroup)
                presenter.SelectPriceGroup(group);

            WarehouseTransferRequest probe = BuildRequest(group, 1);
            ND.Framework.CaravanSaveData caravan = FindCaravan(CurrentSave(), SelectedCaravanId);
            WarehouseTransferCapacity capacity = WarehouseTransferCapacityCalculator.Calculate(CurrentSave(), caravan, probe);
            selectedMaxQuantity = Math.Min(group.Quantity, capacity.MaxTransfer);
            if (selectedMaxQuantity <= 0)
            {
                ShowFailure(capacity.Failure);
                presenter.CancelSelection();
                CloseSelection();
                return;
            }

            selectedQuantity = 1;
            UpdateQuantityView();
            SetActive(selectionModalLayer, true);
            SetActive(priceGroupModal, false);
            SetActive(quantityModal, true);
        }

        private void UpdateQuantityView()
        {
            if (selectedQuantityText != null)
                selectedQuantityText.text = selectedQuantity + " / " + selectedMaxQuantity;
            if (quantityDirectionText != null)
                quantityDirectionText.text = "→";
            if (quantitySourceText != null)
                quantitySourceText.text = selectedDirection == WarehouseTransferDirection.HomeToCargo
                    ? "Player Inventory" : "Cargo";
            if (quantityDestinationText != null)
                quantityDestinationText.text = selectedDirection == WarehouseTransferDirection.HomeToCargo
                    ? "Cargo" : "Player Inventory";
        }

/// <summary>Delegates final validation, mutation, save, and rollback to the transfer service.</summary>
        private void ConfirmTransfer()
        {
            if (!AcceptInput() || presenter.Panel != WarehouseSelectionPanel.Quantity) return;
            presenter.BeginTransfer();
            SetButtonsInteractable(false);

            WarehouseTransferRequest request = BuildRequest(presenter.SelectedGroup, selectedQuantity);
            FrameworkRoot root = FrameworkRoot.Instance;
            WarehouseTransferFailure failure = WarehouseTransferFailure.InvalidFramework;
            bool succeeded = false;
            try
            {
                // Service가 발행하는 동기 inventory event가 transfer 완료 전 View를 두 번 생성하지 않게 한다.
                suppressEventRefresh = true;
                succeeded = root != null && WarehouseInventoryTransferService.TryTransfer(
                    root.CurrentSaveData, root.SaveService, request, out failure);
            }
            finally
            {
                suppressEventRefresh = false;
                presenter.EndTransfer();
                SetButtonsInteractable(true);
            }

            CloseSelection();
            if (succeeded)
                RefreshAll();
            else
                ShowFailure(failure);
        }

        /// <summary>Converts transient UI state into an immutable transfer intent.</summary>
        private WarehouseTransferRequest BuildRequest(WarehousePriceGroup group, int quantity)
        {
            ND.Framework.SaveData save = CurrentSave();
            ND.Framework.CaravanSaveData caravan = FindCaravan(save, SelectedCaravanId);
            string baseTownId = WarehouseFunction.BaseTownId;
            return new WarehouseTransferRequest(
                SelectedCaravanId,
                baseTownId,
                selectedItemId,
                group.PurchaseUnitPrice,
                quantity,
                selectedDirection,
                CurrentState.SlotCount,
                Math.Max(0, caravan?.wagon?.inventorySlotCount ?? 0),
                Math.Max(0f, caravan?.wagon?.maxLoad ?? 0f));
        }

        private void ChangeQuantity(int delta)
        {
            if (!AcceptInput() || presenter.Panel != WarehouseSelectionPanel.Quantity) return;
            selectedQuantity = Mathf.Clamp(selectedQuantity + delta, 1, selectedMaxQuantity);
            UpdateQuantityView();
        }

        private void SetQuantityTo(int value)
        {
            if (!AcceptInput() || presenter.Panel != WarehouseSelectionPanel.Quantity) return;
            selectedQuantity = Mathf.Clamp(value, 1, selectedMaxQuantity);
            UpdateQuantityView();
        }

private void CancelSelection()
        {
            if (!AcceptInput() || presenter.Panel == WarehouseSelectionPanel.Busy) return;
            ResetItemSelectionState();
            CloseSelection();
        }

        private void CloseSelection()
        {
            HideTooltip();
            SetActive(priceGroupModal, false);
            SetActive(quantityModal, false);
            SetActive(selectionModalLayer, false);
        }

        private void BackToCaravanSelection()
        {
            if (!AcceptInput()) return;
            ResetTransientState();
        }

        private void ClosePopup()
        {
            if (!AcceptInput()) return;
            presenter.CancelSelection();
            CloseSelection();
            gameObject.SetActive(false);
        }

        /// <summary>Prevents one pointer sequence or rapid repeat from reaching multiple UI layers.</summary>
        private bool AcceptInput()
        {
            if (Time.unscaledTime < nextAcceptedInputTime) return false;
            nextAcceptedInputTime = Time.unscaledTime + repeatedInputGuardSeconds;
            return true;
        }

        private void ShowFailure(WarehouseTransferFailure failure)
        {
            if (guideText != null)
                guideText.text = "이동할 수 없습니다: " + failure;
        }

        private void OnSaveLoaded(ND.Framework.SaveData _)
        {
            if (!suppressEventRefresh) RefreshAll();
        }

        /// <summary>Refreshes only when the changed cargo belongs to the selected Caravan.</summary>
        private void OnCaravanCargoChanged(string caravanId)
        {
            if (!suppressEventRefresh
                && string.Equals(caravanId, SelectedCaravanId, StringComparison.Ordinal))
                RefreshAll();
        }

private void OnCaravanCreated(string _, int __)
        {
            // 새 Caravan의 payload를 별도 캐시하지 않고 권위 SaveData 목록을 다시 읽는다.
            if (!suppressEventRefresh) RefreshAll();
        }


        /// <summary>Resolves the prefab naming contract in one place so missing bindings are visible.</summary>
        private void ResolveReferences()
        {
            caravanSelectionPanel = Find("CaravanSelectionPanel")?.gameObject;
            cargoPanel = Find("CargoPanel")?.gameObject;
            selectionModalLayer = Find("SelectionModalLayer")?.gameObject;
            tooltipLayer = Find("TooltipLayer")?.gameObject;
            tooltip = Find("SharedItemTooltip")?.gameObject;
            priceGroupModal = Find("PriceGroupModal")?.gameObject;
            quantityModal = Find("WarehouseQuantityModal")?.gameObject;
            warehouseContent = FindWithin(Find("WarehouseInventoryPanel"), "Content");
            cargoContent = FindWithin(Find("CargoPanel"), "Content");
            caravanContent = Find("CaravanGrid");
            priceRowsContent = FindWithin(Find("PriceGroupModal"), "Content");
            warehouseCapacityText = FindWithin(Find("WarehouseInventoryPanel"), "CapacityText")?.GetComponent<TMP_Text>();
            cargoCapacityText = FindWithin(Find("CargoPanel"), "CapacityText")?.GetComponent<TMP_Text>();
            loadText = Find("LoadText")?.GetComponent<TMP_Text>();
            maxLoadText = Find("MaxLoadText")?.GetComponent<TMP_Text>();
            guideText = Find("GuideText")?.GetComponent<TMP_Text>();
            cargoTitleText = Find("CargoTitle")?.GetComponent<TMP_Text>();
            tooltipNameText = FindWithin(Find("SharedItemTooltip"), "DisplayNameText")?.GetComponent<TMP_Text>();
            tooltipPriceText = FindWithin(Find("SharedItemTooltip"), "BasePriceText")?.GetComponent<TMP_Text>();
            tooltipDescriptionText = FindWithin(Find("SharedItemTooltip"), "DescriptionText")?.GetComponent<TMP_Text>();
            selectedItemNameText = FindWithin(Find("PriceGroupModal"), "SelectedItemNameText")?.GetComponent<TMP_Text>();
            selectedItemTotalText = FindWithin(Find("PriceGroupModal"), "SelectedItemTotalQuantityText")?.GetComponent<TMP_Text>();
            selectedItemIcon = FindWithin(Find("PriceGroupModal"), "ItemIcon")?.GetComponent<Image>();
            selectedQuantityText = FindWithin(
                FindWithin(Find("WarehouseQuantityModal"), "SelectedQuantity"), "QuantityText")?.GetComponent<TMP_Text>();
            quantityDirectionText = FindWithin(Find("WarehouseQuantityModal"), "DirectionText")?.GetComponent<TMP_Text>();
            quantitySourceText = FindWithin(Find("WarehouseQuantityModal"), "SourceText")?.GetComponent<TMP_Text>();
            quantityDestinationText = FindWithin(Find("WarehouseQuantityModal"), "DestinationText")?.GetComponent<TMP_Text>();
            closeButton = Find("CloseButton")?.GetComponent<Button>();
            backdropButton = Find("BackdropButton")?.GetComponent<Button>();
            modalBlockerButton = Find("ModalBlocker")?.GetComponent<Button>();
            backToCaravanButton = Find("BackToCaravanSelectButton")?.GetComponent<Button>();
            priceCancelButton = FindWithin(Find("PriceGroupModal"), "CancelButton")?.GetComponent<Button>();
            quantityCancelButton = FindWithin(Find("WarehouseQuantityModal"), "CancelButton")?.GetComponent<Button>();
            minusButton = FindWithin(Find("WarehouseQuantityModal"), "MinusButton")?.GetComponent<Button>();
            plusButton = FindWithin(Find("WarehouseQuantityModal"), "PlusButton")?.GetComponent<Button>();
            minButton = FindWithin(Find("WarehouseQuantityModal"), "MinButton")?.GetComponent<Button>();
            maxButton = FindWithin(Find("WarehouseQuantityModal"), "MaxButton")?.GetComponent<Button>();
            confirmButton = FindWithin(Find("WarehouseQuantityModal"), "ConfirmTransferButton")?.GetComponent<Button>();
        }

        private void WireButtons()
        {
            closeButton?.onClick.AddListener(ClosePopup);
            backdropButton?.onClick.AddListener(ClosePopup);
            modalBlockerButton?.onClick.AddListener(CancelSelection);
            backToCaravanButton?.onClick.AddListener(BackToCaravanSelection);
            priceCancelButton?.onClick.AddListener(CancelSelection);
            quantityCancelButton?.onClick.AddListener(CancelSelection);
            minusButton?.onClick.AddListener(() => ChangeQuantity(-1));
            plusButton?.onClick.AddListener(() => ChangeQuantity(1));
            minButton?.onClick.AddListener(() => SetQuantityTo(1));
            maxButton?.onClick.AddListener(() => ChangeQuantity(99));
            confirmButton?.onClick.AddListener(ConfirmTransfer);
        }

        private void SetButtonsInteractable(bool value)
        {
            if (confirmButton != null) confirmButton.interactable = value;
            if (quantityCancelButton != null) quantityCancelButton.interactable = value;
            if (minusButton != null) minusButton.interactable = value;
            if (plusButton != null) plusButton.interactable = value;
            if (minButton != null) minButton.interactable = value;
            if (maxButton != null) maxButton.interactable = value;
        }

        private ND.Framework.SaveData CurrentSave()
        {
            return FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null;
        }

        private static ND.Framework.CaravanSaveData FindCaravan(ND.Framework.SaveData save, string caravanId)
        {
            return save?.caravans?.FirstOrDefault(
                c => c != null && string.Equals(c.caravanId, caravanId, StringComparison.Ordinal));
        }

        private static float CalculateCargoWeight(ND.Framework.CaravanSaveData caravan)
        {
            if (caravan?.cargo == null) return 0f;
            float result = 0f;
            foreach (CargoEntrySaveData entry in caravan.cargo)
                if (entry?.item != null && entry.quantity > 0)
                    result += Math.Max(0f, entry.item.weight) * entry.quantity;
            return result;
        }

/// <summary>Preserves the template while detaching generated views before deferred destruction.</summary>
        private void ClearGenerated(Transform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                // Prefab 안의 비활성 RowTemplate은 작성용 기준이므로 runtime 생성물과 함께 표시하지 않는다.
                if (child.name == "RowTemplate")
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                // Destroy는 frame 끝에 실행된다. 먼저 Layout 계층에서 분리해야 같은 frame의 재갱신에도
                // 오래된 슬롯과 새 슬롯이 동시에 슬롯 수로 계산되지 않는다.
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private Transform Find(string objectName)
        {
            return GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
        }

        private static Transform FindWithin(Transform root, string objectName)
        {
            return root == null ? null
                : root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    

private void ResetItemSelectionState()
        {
            // Backdrop/취소는 선택한 Caravan은 유지하되 진행 중이던 item·가격·수량만 폐기한다.
            presenter.CancelSelection();
            selectedItemId = string.Empty;
            selectedQuantity = 1;
            selectedMaxQuantity = 0;
        }
}
}
