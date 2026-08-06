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
        private NoticeUI noticeUI;
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
                // 화면에 남은 건물 블록이 최신 저장 상태와 어긋난 경우에도 실패 이유를 먼저 안내한다.
                ShowNotice(ResolveWarehouseOpenFailure(root?.CurrentSaveData, CurrentState));
                gameObject.SetActive(false);
                return false;
            }

            gameObject.SetActive(true);
            ResetTransientState();
            RefreshAll();
            return true;
        }

        /// <summary>
        /// UnityEvent(마을 건물 클릭 라우터 등)에서 인벤토리 패널을 여는 void 래퍼.
        /// TryOpen()이 bool을 반환해 UnityEvent 연결이 번거로워, 반환값 없는 진입점을 제공한다.
        /// </summary>
        public void OpenPanel() => TryOpen();

        public static bool IsEligible(ND.Framework.CaravanSaveData caravan, string baseTownId)
        {
            return caravan != null
                && !string.IsNullOrWhiteSpace(caravan.caravanId)
                && caravan.state == JourneyState.Prepare
                && string.Equals(caravan.currentTownId, baseTownId, StringComparison.Ordinal);
        }

        public bool SelectCaravan(string caravanId)
        {
            if (!AcceptInput()) return false;
            if (string.IsNullOrWhiteSpace(caravanId))
            {
                ShowNotice("Caravan 슬롯 식별자가 유효하지 않습니다.");
                return false;
            }
            ND.Framework.SaveData save = CurrentSave();
            ND.Framework.CaravanSaveData caravan = FindCaravan(save, caravanId);
            string baseTownId = WarehouseFunction.BaseTownId;
            if (!IsEligible(caravan, baseTownId))
            {
                ShowNotice(ResolveCaravanSelectionFailure(caravan));
                return false;
            }

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

            CaravanSlotValidationResult validation = CaravanSlotValidation.Validate(save.caravans);
            string baseTownId = WarehouseFunction.BaseTownId;
            for (int slotIndex = 0; slotIndex < CaravanSlotValidation.SlotCount; slotIndex++)
            {
                if (validation.IsConflicted(slotIndex))
                {
                    // Render an explicit disabled row so duplicate data is visible, but never selectable.
                    var conflicted = new ND.Framework.CaravanSaveData
                    {
                        slotIndex = slotIndex,
                        caravanId = string.Empty
                    };
                    GameObject conflictInstance = Instantiate(caravanSlotPrefab, caravanContent);
                    conflictInstance.name = "CaravanSlotConflict_" + slotIndex;
                    WarehouseCaravanSlotView conflictView =
                        conflictInstance.GetComponent<WarehouseCaravanSlotView>()
                        ?? conflictInstance.AddComponent<WarehouseCaravanSlotView>();
                    conflictView.Bind(
                        conflicted,
                        false,
                        "Caravan 슬롯 데이터가 중복되었거나 식별자가 유효하지 않습니다.",
                        _ => { },
                        true);
                    continue;
                }

                ND.Framework.CaravanSaveData caravan = validation.GetCaravanAt(slotIndex);
                if (caravan == null)
                {
                    bool unlocked = save.world?.unlockedCaravanSlotIndices != null
                        && save.world.unlockedCaravanSlotIndices.Contains(slotIndex);
                    if (!unlocked) continue;

                    GameObject emptyInstance = Instantiate(caravanSlotPrefab, caravanContent);
                    emptyInstance.name = "CaravanSlotEmpty_" + slotIndex;
                    WarehouseCaravanSlotView emptyView =
                        emptyInstance.GetComponent<WarehouseCaravanSlotView>()
                        ?? emptyInstance.AddComponent<WarehouseCaravanSlotView>();
                    emptyView.Bind(
                        null,
                        false,
                        "Caravan 데이터가 없습니다.",
                        _ => { },
                        true);
                    continue;
                }

                bool eligible = IsEligible(caravan, baseTownId);
                string reason = eligible ? string.Empty
                    : caravan.state != JourneyState.Prepare ? "준비 상태가 아닙니다."
                    : !string.Equals(caravan.currentTownId, baseTownId, StringComparison.Ordinal)
                        ? "BaseCamp에 없습니다." : "선택할 수 없습니다.";
                GameObject instance = Instantiate(caravanSlotPrefab, caravanContent);
                instance.name = "Caravan_" + caravan.caravanId;
                WarehouseCaravanSlotView view = instance.GetComponent<WarehouseCaravanSlotView>()
                    ?? instance.AddComponent<WarehouseCaravanSlotView>();
                view.Bind(
                    caravan,
                    eligible,
                    reason,
                    id => SelectCaravan(id),
                    false,
                    BuildCaravanLocationText(caravan));
            }

            if (validation.HasInvalidEntries)
                Debug.LogWarning("Invalid Caravan slot data was excluded from Warehouse selection.", this);
        }

        private static string BuildCaravanLocationText(ND.Framework.CaravanSaveData caravan)
        {
            if (caravan == null) return string.Empty;

            string townId = caravan.currentTownId?.Trim() ?? string.Empty;
            string townName = townId;
            ISharedGameDataProvider shared = FrameworkRoot.Instance?.SharedGameData;
            if (!string.IsNullOrEmpty(townId)
                && shared != null
                && shared.TryGetTown(townId, out SharedTownDefinition town)
                && town != null
                && !string.IsNullOrWhiteSpace(town.DisplayName))
            {
                townName = town.DisplayName.Trim();
            }

            return string.IsNullOrEmpty(townName) ? "현재 위치 없음" : townName;
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
            if (presenter.Panel != WarehouseSelectionPanel.None || tooltip == null || slot == null) return;
            WarehouseItemTooltipViewData data = WarehouseInventoryViewDataBuilder.BuildTooltip(
                itemId, FrameworkRoot.Instance != null ? FrameworkRoot.Instance.SharedGameData : null);
            if (tooltipNameText != null) tooltipNameText.text = data.DisplayName;
            if (tooltipPriceText != null)
                tooltipPriceText.text = $"기본 구매가  {data.BaseBuyPrice} G\n무게  {data.Weight:0.##}";
            if (tooltipDescriptionText != null) tooltipDescriptionText.text = data.Description;
            SetActive(tooltipLayer, true);
            SetActive(tooltip, true);
            PositionTooltipBesideSlot(slot);
        }

        /// <summary>
        /// Places the tooltip beside the hovered slot, flips it to the left at the right edge,
        /// and clamps its vertical center so the complete tooltip stays inside its overlay.
        /// </summary>
        private void PositionTooltipBesideSlot(RectTransform slot)
        {
            RectTransform layerRect = tooltipLayer != null ? tooltipLayer.transform as RectTransform : null;
            RectTransform tooltipRect = tooltip != null ? tooltip.transform as RectTransform : null;
            if (layerRect == null || tooltipRect == null || slot == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var slotWorldCorners = new Vector3[4];
            slot.GetWorldCorners(slotWorldCorners);
            Vector2 bottomLeft;
            Vector2 topRight;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                layerRect,
                RectTransformUtility.WorldToScreenPoint(eventCamera, slotWorldCorners[0]),
                eventCamera,
                out bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                layerRect,
                RectTransformUtility.WorldToScreenPoint(eventCamera, slotWorldCorners[2]),
                eventCamera,
                out topRight);

            const float gap = 16f;
            float tooltipWidth = tooltipRect.rect.width;
            float tooltipHeight = tooltipRect.rect.height;
            bool placeRight = topRight.x + gap + tooltipWidth <= layerRect.rect.xMax;

            tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRect.pivot = new Vector2(placeRight ? 0f : 1f, 0.5f);

            float x = placeRight ? topRight.x + gap : bottomLeft.x - gap;
            float y = (bottomLeft.y + topRight.y) * 0.5f;
            y = Mathf.Clamp(
                y,
                layerRect.rect.yMin + tooltipHeight * 0.5f,
                layerRect.rect.yMax - tooltipHeight * 0.5f);
            tooltipRect.anchoredPosition = new Vector2(x, y);
        }


        private void HideTooltip()
        {
            SetActive(tooltip, false);
            SetActive(tooltipLayer, false);
        }

        /// <summary>Builds price groups from the directional source and enters the required selection panel.</summary>
        private void SelectItem(string itemId, WarehouseTransferDirection direction)
        {
            if (!AcceptInput()) return;

            ND.Framework.SaveData save = CurrentSave();
            ND.Framework.CaravanSaveData caravan = FindCaravan(save, SelectedCaravanId);
            if (direction == WarehouseTransferDirection.HomeToCargo
                && !IsEligible(caravan, WarehouseFunction.BaseTownId))
            {
                ShowNotice(ResolveCaravanSelectionFailure(caravan));
                return;
            }

            IEnumerable<CargoEntrySaveData> source = direction == WarehouseTransferDirection.HomeToCargo
                ? save?.player?.homeInventory
                : caravan?.cargo;
            selectedDirection = direction;
            selectedItemId = itemId ?? string.Empty;
            HideTooltip();

            WarehousePriceGroupModalViewData data = presenter.SelectItem(
                source, selectedItemId,
                FrameworkRoot.Instance != null ? FrameworkRoot.Instance.SharedGameData : null);
            if (data == null || data.Groups.Count == 0)
            {
                ShowNotice("선택한 아이템에서 이동 가능한 가격 묶음을 찾을 수 없습니다.");
                presenter.CancelSelection();
                return;
            }

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

            // Revalidate immediately before mutation. A duplicate or out-of-range slot that appeared
            // after selection must invalidate the stale caravanId instead of redirecting Cargo.
            if (FindCaravan(CurrentSave(), SelectedCaravanId) == null)
            {
                ResetItemSelectionState();
                CloseSelection();
                ShowFailure(WarehouseTransferFailure.InvalidCaravan);
                return;
            }

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
            string message;
            switch (failure)
            {
                case WarehouseTransferFailure.InvalidFramework:
                    message = "저장 데이터가 아직 준비되지 않아 창고를 이용할 수 없습니다.";
                    break;
                case WarehouseTransferFailure.InvalidCaravan:
                    message = "선택한 Caravan을 찾을 수 없거나 슬롯 데이터가 유효하지 않습니다.";
                    break;
                case WarehouseTransferFailure.NotAtBaseCamp:
                    message = "BaseCamp에 있는 Caravan만 창고를 이용할 수 있습니다.";
                    break;
                case WarehouseTransferFailure.CaravanBusy:
                    message = "준비 상태인 Caravan만 창고를 이용할 수 있습니다.";
                    break;
                case WarehouseTransferFailure.CargoFull:
                    message = "Cargo 슬롯에 여유가 없습니다.";
                    break;
                case WarehouseTransferFailure.CargoOverweight:
                    message = "Cargo 최대 적재량이 부족합니다. 배정된 Wagon 정보를 확인해 주세요.";
                    break;
                case WarehouseTransferFailure.WarehouseFull:
                    message = "Warehouse 슬롯에 여유가 없습니다.";
                    break;
                case WarehouseTransferFailure.InsufficientSource:
                    message = "선택한 가격 묶음의 수량이 부족합니다.";
                    break;
                case WarehouseTransferFailure.InvalidItem:
                    message = "선택한 아이템 정보가 유효하지 않습니다.";
                    break;
                case WarehouseTransferFailure.SaveFailed:
                    message = "저장에 실패하여 아이템 이동을 취소했습니다.";
                    break;
                default:
                    message = "아이템을 이동할 수 없습니다: " + failure;
                    break;
            }

            if (guideText != null) guideText.text = message;
            ShowNotice(message);
        }

        private void ShowNotice(string message)
        {
            if (noticeUI == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                noticeUI = canvas != null ? canvas.GetComponentInChildren<NoticeUI>(true) : null;
            }

            if (noticeUI != null)
                noticeUI.Show(message);
            else if (guideText != null)
                guideText.text = message;
        }

        private static string ResolveCaravanSelectionFailure(ND.Framework.CaravanSaveData caravan)
        {
            if (caravan == null)
                return "먼저 유효한 Caravan을 선택해 주세요.";
            if (caravan.state != JourneyState.Prepare)
                return "준비 상태인 Caravan만 창고를 이용할 수 있습니다.";
            if (!string.Equals(caravan.currentTownId, WarehouseFunction.BaseTownId, StringComparison.Ordinal))
                return "BaseCamp에 있는 Caravan만 창고를 이용할 수 있습니다.";
            return "현재 Caravan은 Cargo 이동에 사용할 수 없습니다.";
        }

        /// <summary>
        /// 창고 진입 규칙은 WarehouseFunction에 유지하고, 실패 상태만 사용자 문구로 변환한다.
        /// </summary>
        private static string ResolveWarehouseOpenFailure(
            ND.Framework.SaveData save,
            WarehouseState state)
        {
            if (save?.player == null)
                return "저장 데이터가 아직 준비되지 않아 창고를 열 수 없습니다.";
            if (!string.Equals(save.player.currentTownId, WarehouseFunction.BaseTownId, StringComparison.Ordinal))
                return "BaseCamp에서만 창고를 이용할 수 있습니다.";
            if (state.Level <= 0)
                return "창고를 건설한 뒤 이용할 수 있습니다.";
            return "현재 창고를 열 수 없습니다.";
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
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            noticeUI = rootCanvas != null ? rootCanvas.GetComponentInChildren<NoticeUI>(true) : null;
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

private static ND.Framework.CaravanSaveData FindCaravan(
            ND.Framework.SaveData save,
            string caravanId)
        {
            if (save?.caravans == null) return null;

            // Keep caravanId as final identity, but only resolve IDs that survive slot validation.
            CaravanSlotValidationResult validation = CaravanSlotValidation.Validate(save.caravans);
            return validation.TryGetCaravan(caravanId, out ND.Framework.CaravanSaveData caravan)
                ? caravan
                : null;
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
