using System;
using System.Collections.Generic;
using System.Linq;
using ND.Framework;
using ND.UI.InGame.Warehouse;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.CargoSell
{
    /// <summary>
    /// Presents CargoSellViewData and owns only transient UI state. Saving, market mutation, and
    /// Journey transitions remain the responsibility of the arrival-sale application controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CargoSellPopupController : MonoBehaviour
    {
        private readonly CargoSellDraft draft = new CargoSellDraft();
        private readonly List<CargoSellCargoSlotView> slots = new List<CargoSellCargoSlotView>();
        private CargoSellViewData source;
        private CargoSellCargoItemViewData selected;
        private int selectedQuantity;
        private Slider quantitySlider;   // 수량 슬라이더(WarehouseQuantityModal과 동일)

        private CargoSellPendingSaleListView pendingList;
        private Transform cargoContent;
        private GameObject priceGroupModal;
        private Transform priceRowsContent;
        private GameObject priceRowTemplate;
        private TMP_Text priceSelectedItemNameText;
        private TMP_Text priceSelectedItemTotalText;
        private Image priceSelectedItemIcon;
        // Data rows are cloned from the prefab-authored template only when the pool must grow.
        // Subsequent opens rebind and toggle these rows instead of creating/destroying UI objects.
        private readonly List<GameObject> priceRowPool = new List<GameObject>();
        private GameObject quantityModal;
        private TMP_Text quantitySourceText;
        private TMP_Text quantityDestinationText;
        private TMP_Text selectedQuantityText;
        private TMP_Text titleText;
        private TMP_Text cargoTitleText;
        [SerializeField] private TMP_Text loadText;
        [SerializeField] private GameObject itemTooltip;
        [SerializeField] private TMP_Text tooltipNameText;
        [SerializeField] private TMP_Text tooltipPriceText;
        [SerializeField] private TMP_Text tooltipDescriptionText;
        private TMP_Text messageText;
        private TMP_Text confirmSaleButtonText;
        private Button closeButton;
        private Button backdropButton;
        private Button clearButton;
        private Button confirmSaleButton;
        private Button minusButton;
        private Button plusButton;
        private Button maxButton;
        private Button cancelQuantityButton;
        private Button cancelPriceGroupButton;
        private Button confirmQuantityButton;
        private bool wired;
        private bool submitting;
        private string submissionMessage = string.Empty;

        public string CaravanId => source?.caravanId ?? string.Empty;
        public string TradeId => source?.tradeId ?? string.Empty;
        public bool HasDraft => !draft.IsEmpty;
        public bool IsSubmitting => submitting;

        public event Action<IReadOnlyList<CargoSellPendingSaleRowViewData>> ConfirmRequested;
        public event Action CloseRequested;

        private void Awake()
        {
            ResolveReferences();
            WireInteractions();
            CloseSelection();
        }

        public bool Open(CargoSellViewData value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.caravanId)
                || string.IsNullOrWhiteSpace(value.tradeId))
                return false;

            ResolveReferences();
            WireInteractions();
            source = value;
            draft.Restore(value.pendingItems, value.cargoItems);
            submitting = false;
            submissionMessage = string.Empty;
            selected = null;
            selectedQuantity = 0;
            Render();
            CloseSelection();
            gameObject.SetActive(true);
            return true;
        }

        public void Refresh(CargoSellViewData value)
        {
            if (value == null
                || !string.Equals(CaravanId, value.caravanId, StringComparison.Ordinal)
                || !string.Equals(TradeId, value.tradeId, StringComparison.Ordinal))
                return;

            CargoSellPendingSaleRowViewData[] previous = draft.Snapshot();
            source = value;
            draft.Restore(previous, value.cargoItems);
            Render();
        }

        public CargoSellPendingSaleRowViewData[] GetDraftSnapshot() => draft.Snapshot();

        private void Render()
        {
            if (source == null) return;
            if (titleText != null)
                titleText.text = string.IsNullOrWhiteSpace(source.destinationTownName)
                    ? "화물 판매"
                    : source.destinationTownName + " 화물 판매";
            if (cargoTitleText != null)
                cargoTitleText.text = string.IsNullOrWhiteSpace(source.caravanDisplayName)
                    ? "Cargo"
                    : source.caravanDisplayName + " Cargo";
            if (messageText != null)
                messageText.text = string.IsNullOrWhiteSpace(submissionMessage)
                    ? source.message ?? string.Empty
                    : submissionMessage;
            if (loadText != null)
                loadText.text = $"적재량 {FormatLoad(source.currentLoad)} / {FormatLoad(source.maximumLoad)}";

            CargoSellCargoItemViewData[] cargo = BuildItemSlots(
                source.cargoItems ?? Array.Empty<CargoSellCargoItemViewData>());
            ResolveSlots();
            for (int index = 0; index < slots.Count; index++)
            {
                CargoSellCargoItemViewData item = index < cargo.Length ? cargo[index] : null;
                slots[index].Bind(item, OpenItem, ShowTooltip, HideTooltip);
            }

            pendingList?.Render(draft.Snapshot());
            if (confirmSaleButton != null)
                confirmSaleButton.interactable = source.canConfirm && !submitting;
            if (confirmSaleButtonText != null)
                confirmSaleButtonText.text = draft.IsEmpty ? "판매 없이 정산" : "판매 후 정산";
            if (clearButton != null)
                clearButton.interactable = !submitting && !draft.IsEmpty;
            if (closeButton != null) closeButton.interactable = !submitting;
            if (backdropButton != null) backdropButton.interactable = !submitting;
        }

        private static string FormatLoad(float value) =>
            float.IsPositiveInfinity(value) ? "∞" : Math.Max(0f, value).ToString("0.##");

        /// <summary>
        /// Physical Cargo slots aggregate by item ID. Exact purchase-price groups stay in the
        /// source snapshot and are selected in PriceGroupModal instead of becoming duplicate icons.
        /// </summary>
        private static CargoSellCargoItemViewData[] BuildItemSlots(
            IEnumerable<CargoSellCargoItemViewData> priceGroups)
        {
            return (priceGroups ?? Enumerable.Empty<CargoSellCargoItemViewData>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.itemId))
                .GroupBy(item => item.itemId, StringComparer.Ordinal)
                .Select(group =>
                {
                    CargoSellCargoItemViewData first = group.First();
                    return new CargoSellCargoItemViewData
                    {
                        itemId = first.itemId,
                        purchaseUnitPrice = first.purchaseUnitPrice,
                        displayName = first.displayName,
                        description = first.description,
                        icon = first.icon,
                        cargoQuantity = group.Sum(item => Math.Max(0, item.cargoQuantity)),
                        selectedSellQuantity = group.Sum(item => Math.Max(0, item.selectedSellQuantity)),
                        sellUnitPrice = first.sellUnitPrice,
                        unitWeight = first.unitWeight
                    };
                })
                .ToArray();
        }

        private void ShowTooltip(CargoSellCargoSlotView slot, CargoSellCargoItemViewData item)
        {
            if (itemTooltip == null || item == null) return;
            if (tooltipNameText != null) tooltipNameText.text = item.displayName;
            if (tooltipPriceText != null)
            {
                int groupCount = GetPriceGroups(item.itemId).Length;
                tooltipPriceText.text = groupCount > 1
                    ? $"구매가 묶음 {groupCount}종"
                    : $"구매 단가 {Math.Max(0L, item.purchaseUnitPrice)}G";
            }
            if (tooltipDescriptionText != null) tooltipDescriptionText.text = item.description;
            itemTooltip.SetActive(true);
            Canvas.ForceUpdateCanvases();
            PositionTooltipBesideSlot(slot.transform as RectTransform);
        }

        private void PositionTooltipBesideSlot(RectTransform slot)
        {
            RectTransform tooltipRect = itemTooltip != null ? itemTooltip.transform as RectTransform : null;
            RectTransform bounds = tooltipRect != null ? tooltipRect.parent as RectTransform : null;
            if (slot == null || tooltipRect == null || bounds == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            slot.GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bounds, RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]), eventCamera,
                out Vector2 bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bounds, RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]), eventCamera,
                out Vector2 topRight);

            const float gap = 16f;
            float width = tooltipRect.rect.width;
            float height = tooltipRect.rect.height;
            bool placeRight = topRight.x + gap + width <= bounds.rect.xMax;
            tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(.5f, .5f);
            tooltipRect.pivot = new Vector2(placeRight ? 0f : 1f, .5f);
            float x = placeRight ? topRight.x + gap : bottomLeft.x - gap;
            float y = Mathf.Clamp((bottomLeft.y + topRight.y) * .5f,
                bounds.rect.yMin + height * .5f,
                bounds.rect.yMax - height * .5f);
            tooltipRect.anchoredPosition = new Vector2(x, y);
        }

        private void HideTooltip()
        {
            if (itemTooltip != null) itemTooltip.SetActive(false);
        }

        /// <summary>Enters purchase-price selection before quantity whenever an item has multiple groups.</summary>
        private void OpenItem(CargoSellCargoItemViewData item)
        {
            if (submitting || item == null) return;

            CargoSellCargoItemViewData[] groups = GetPriceGroups(item.itemId);
            if (groups.Length == 0) return;
            if (groups.Length == 1)
            {
                OpenQuantity(groups[0]);
                return;
            }

            PopulatePriceGroups(item, groups);
            if (priceGroupModal != null) priceGroupModal.SetActive(true);
            if (quantityModal != null) quantityModal.SetActive(false);
        }

        private CargoSellCargoItemViewData[] GetPriceGroups(string itemId)
        {
            // SaveData may hold one physical item in several acquisition-price groups. Keep zero
            // price production goods as a real selectable group instead of treating 0G as missing.
            return (source?.cargoItems ?? Array.Empty<CargoSellCargoItemViewData>())
                .Where(item => item != null
                    && string.Equals(item.itemId, itemId, StringComparison.Ordinal)
                    && item.cargoQuantity > 0)
                .OrderBy(item => Math.Max(0L, item.purchaseUnitPrice))
                .ToArray();
        }

        private void PopulatePriceGroups(
            CargoSellCargoItemViewData aggregate,
            IReadOnlyList<CargoSellCargoItemViewData> groups)
        {
            if (priceSelectedItemNameText != null)
                priceSelectedItemNameText.text = aggregate.displayName;
            if (priceSelectedItemTotalText != null)
                priceSelectedItemTotalText.text = $"총 보유 {groups.Sum(group => Math.Max(0, group.cargoQuantity))}개";
            if (priceSelectedItemIcon != null)
            {
                priceSelectedItemIcon.sprite = aggregate.icon;
                priceSelectedItemIcon.enabled = aggregate.icon != null;
            }

            if (priceRowsContent == null || priceRowTemplate == null) return;
            // The template is authored in the prefab. Instantiate only when the reusable pool is
            // too small, then rebind existing rows on later opens to avoid UI churn.
            while (priceRowPool.Count < groups.Count)
            {
                GameObject pooledRow = Instantiate(priceRowTemplate, priceRowsContent);
                pooledRow.name = $"PriceGroupRow_{priceRowPool.Count}";
                pooledRow.SetActive(false);
                priceRowPool.Add(pooledRow);
            }

            for (int index = 0; index < priceRowPool.Count; index++)
            {
                GameObject row = priceRowPool[index];
                bool active = index < groups.Count;
                row.SetActive(active);
                if (!active) continue;

                CargoSellCargoItemViewData group = groups[index];
                row.name = $"PriceGroup_{Math.Max(0L, group.purchaseUnitPrice)}";
                WarehousePriceGroupRowView view = row.GetComponent<WarehousePriceGroupRowView>();
                view?.Bind(
                    new WarehousePriceGroup(
                        group.itemId,
                        group.purchaseUnitPrice,
                        group.cargoQuantity),
                    OpenQuantity);
            }
        }

        private void OpenQuantity(WarehousePriceGroup group)
        {
            // WarehousePriceGroupRowView is shared with Warehouse UI; translate its selection back
            // to the exact Cargo sale row before opening the common quantity modal.
            CargoSellCargoItemViewData item = (source?.cargoItems
                ?? Array.Empty<CargoSellCargoItemViewData>()).FirstOrDefault(candidate =>
                    candidate != null
                    && string.Equals(candidate.itemId, group.ItemId, StringComparison.Ordinal)
                    && Math.Max(0L, candidate.purchaseUnitPrice) == group.PurchaseUnitPrice);
            OpenQuantity(item);
        }

        private void OpenQuantity(CargoSellCargoItemViewData item)
        {
            if (submitting || item == null) return;
            selected = item;
            selectedQuantity = draft.GetQuantity(item.itemId, item.purchaseUnitPrice);
            if (quantitySlider != null)
            {
                quantitySlider.minValue = 0;
                quantitySlider.maxValue = Math.Max(0, item.cargoQuantity);
            }
            if (quantitySourceText != null)
            {
                quantitySourceText.text = string.IsNullOrWhiteSpace(source?.caravanDisplayName)
                    ? "Caravan"
                    : source.caravanDisplayName.Trim();
            }
            if (quantityDestinationText != null) quantityDestinationText.text = "판매 대기";
            if (priceGroupModal != null) priceGroupModal.SetActive(false);
            SetQuantityModalActive(true);
            UpdateQuantityText();
        }

        private void OpenQuantity(CargoSellPendingSaleRowViewData pending)
        {
            if (pending == null || source?.cargoItems == null) return;
            CargoSellCargoItemViewData item = source.cargoItems.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.itemId, pending.ItemId, StringComparison.Ordinal)
                && Math.Max(0L, candidate.purchaseUnitPrice) == Math.Max(0L, pending.PurchaseUnitPrice));
            OpenQuantity(item);
        }

        private void ChangeQuantity(int delta)
        {
            if (selected == null) return;
            selectedQuantity = Mathf.Clamp(
                selectedQuantity + delta,
                0,
                Math.Max(0, selected.cargoQuantity));
            UpdateQuantityText();
        }

        private void SelectMaximum()
        {
            if (selected == null) return;
            selectedQuantity = Math.Max(0, selected.cargoQuantity);
            UpdateQuantityText();
        }

        /// <summary>슬라이더 드래그 → 수량 반영(정수, 0~보유수량으로 clamp).</summary>
        private void SetQuantityFromSlider(float value)
        {
            if (selected == null) return;
            selectedQuantity = Mathf.Clamp(Mathf.RoundToInt(value), 0, Math.Max(0, selected.cargoQuantity));
            UpdateQuantityText();
        }

        private void ConfirmQuantity()
        {
            if (selected == null || !draft.SetQuantity(selected, selectedQuantity)) return;
            CloseQuantity();
            Render();
        }

        private void RemovePending(CargoSellPendingSaleRowViewData pending)
        {
            if (pending == null) return;
            draft.Remove(pending.ItemId, pending.PurchaseUnitPrice);
            Render();
        }

        private void ClearDraft()
        {
            if (submitting) return;
            draft.Clear();
            CloseSelection();
            Render();
        }

        private void RequestConfirm()
        {
            if (source == null || !source.canConfirm || submitting) return;
            if (ConfirmRequested == null)
            {
                SetSubmissionResult(false, "판매 연결을 찾을 수 없습니다.");
                return;
            }

            submitting = true;
            submissionMessage = string.Empty;
            Render();
            ConfirmRequested.Invoke(draft.Snapshot());
        }

        private void RequestClose()
        {
            if (submitting) return;
            CloseSelection();
            draft.Clear();
            source = null;
            submissionMessage = string.Empty;
            gameObject.SetActive(false);
            CloseRequested?.Invoke();
        }

        /// <summary>Unlocks the transient UI after a rejected or failed application command.</summary>
        public void SetSubmissionResult(bool success, string message)
        {
            submitting = false;
            if (success)
            {
                draft.Clear();
                source = null;
                submissionMessage = string.Empty;
                gameObject.SetActive(false);
                return;
            }

            submissionMessage = message ?? string.Empty;
            Render();
        }

        private void CloseQuantity()
        {
            selected = null;
            selectedQuantity = 0;
            SetQuantityModalActive(false);
        }

        private void ClosePriceGroups()
        {
            if (priceGroupModal != null) priceGroupModal.SetActive(false);
        }

        private void CloseSelection()
        {
            // Closing the parent popup or clearing its draft must also close either nested step.
            CloseQuantity();
            ClosePriceGroups();
        }

        private void SetQuantityModalActive(bool active)
        {
            if (quantityModal != null) quantityModal.SetActive(active);
        }

        private void UpdateQuantityText()
        {
            if (selectedQuantityText != null && selected != null)
                selectedQuantityText.text = selectedQuantity + " / " + Math.Max(0, selected.cargoQuantity);
            if (quantitySlider != null)
                quantitySlider.SetValueWithoutNotify(selectedQuantity);
        }

        private void ResolveSlots()
        {
            if (slots.Count > 0 || cargoContent == null) return;
            foreach (Transform child in cargoContent.Cast<Transform>())
            {
                CargoSellCargoSlotView slot = child.GetComponent<CargoSellCargoSlotView>()
                    ?? child.gameObject.AddComponent<CargoSellCargoSlotView>();
                slots.Add(slot);
            }
        }

        private void ResolveReferences()
        {
            if (pendingList != null) return;
            pendingList = GetComponentInChildren<CargoSellPendingSaleListView>(true);
            Transform cargoScroll = Find("CargoScrollView");
            cargoContent = FindWithin(FindWithin(cargoScroll, "Viewport"), "Content");
            Transform priceModalTransform = Find("PriceGroupModal");
            priceGroupModal = priceModalTransform?.gameObject;
            priceRowsContent = FindWithin(priceModalTransform, "Content");
            priceRowTemplate = priceRowsContent != null && priceRowsContent.childCount > 0
                ? priceRowsContent.GetChild(0).gameObject
                : null;
            priceSelectedItemNameText = FindWithin(priceModalTransform, "SelectedItemNameText")
                ?.GetComponent<TMP_Text>();
            priceSelectedItemTotalText = FindWithin(priceModalTransform, "SelectedItemTotalQuantityText")
                ?.GetComponent<TMP_Text>();
            priceSelectedItemIcon = FindWithin(priceModalTransform, "ItemIcon")?.GetComponent<Image>();
            cancelPriceGroupButton = FindWithin(priceModalTransform, "CancelButton")?.GetComponent<Button>();
            quantityModal = Find("QuantityModal")?.gameObject;
            quantitySourceText = FindWithin(Find("QuantityModal"), "SourceText")?.GetComponent<TMP_Text>();
            quantityDestinationText = FindWithin(Find("QuantityModal"), "DestinationText")?.GetComponent<TMP_Text>();
            selectedQuantityText = FindWithin(
                FindWithin(Find("QuantityModal"), "SelectedQuantity"),
                "QuantityText")?.GetComponent<TMP_Text>();
            titleText = Find("Title")?.GetComponent<TMP_Text>();
            cargoTitleText = Find("CargoTitle")?.GetComponent<TMP_Text>();
            messageText = Find("Message")?.GetComponent<TMP_Text>();
            closeButton = Find("CloseButton")?.GetComponent<Button>();
            backdropButton = Find("Backdrop")?.GetComponent<Button>();
            clearButton = Find("ClearPendingButton")?.GetComponent<Button>();
            confirmSaleButton = Find("ConfirmSaleButton")?.GetComponent<Button>();
            confirmSaleButtonText = confirmSaleButton?.GetComponentInChildren<TMP_Text>(true);
            minusButton = FindWithin(Find("QuantityModal"), "MinusButton")?.GetComponent<Button>();
            plusButton = FindWithin(Find("QuantityModal"), "PlusButton")?.GetComponent<Button>();
            maxButton = FindWithin(Find("QuantityModal"), "MaxButton")?.GetComponent<Button>();
            cancelQuantityButton = FindWithin(Find("QuantityModal"), "CancelButton")?.GetComponent<Button>();
            confirmQuantityButton = FindWithin(Find("QuantityModal"), "ConfirmTransferButton")?.GetComponent<Button>();
            quantitySlider = FindWithin(Find("QuantityModal"), "Slider")?.GetComponent<Slider>();
            pendingList?.SetInteractionCallbacks(OpenQuantity, RemovePending);
        }

        private void WireInteractions()
        {
            if (wired) return;
            wired = true;
            closeButton?.onClick.AddListener(RequestClose);
            backdropButton?.onClick.AddListener(RequestClose);
            clearButton?.onClick.AddListener(ClearDraft);
            confirmSaleButton?.onClick.AddListener(RequestConfirm);
            minusButton?.onClick.AddListener(() => ChangeQuantity(-1));
            plusButton?.onClick.AddListener(() => ChangeQuantity(1));
            maxButton?.onClick.AddListener(SelectMaximum);
            cancelPriceGroupButton?.onClick.AddListener(ClosePriceGroups);
            cancelQuantityButton?.onClick.AddListener(CloseQuantity);
            confirmQuantityButton?.onClick.AddListener(ConfirmQuantity);
            if (quantitySlider != null)
            {
                quantitySlider.wholeNumbers = true;                         // 정수 단위
                quantitySlider.onValueChanged.AddListener(SetQuantityFromSlider);
            }
        }

        private Transform Find(string objectName) =>
            GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);

        private static Transform FindWithin(Transform parent, string objectName)
        {
            return parent == null
                ? null
                : parent.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(child => child.name == objectName);
        }
    }
}
