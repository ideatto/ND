using System;
using System.Collections.Generic;
using System.Linq;
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

        private CargoSellPendingSaleListView pendingList;
        private Transform cargoContent;
        private GameObject quantityModal;
        private TMP_Text selectedQuantityText;
        private TMP_Text titleText;
        private TMP_Text cargoTitleText;
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
            CloseQuantity();
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
            CloseQuantity();
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

            CargoSellCargoItemViewData[] cargo = source.cargoItems
                ?? Array.Empty<CargoSellCargoItemViewData>();
            ResolveSlots();
            for (int index = 0; index < slots.Count; index++)
            {
                CargoSellCargoItemViewData item = index < cargo.Length ? cargo[index] : null;
                slots[index].Bind(item, OpenQuantity);
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

        private void OpenQuantity(CargoSellCargoItemViewData item)
        {
            if (submitting || item == null) return;
            selected = item;
            selectedQuantity = draft.GetQuantity(item.itemId, item.purchaseUnitPrice);
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
            CloseQuantity();
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
            CloseQuantity();
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

        private void SetQuantityModalActive(bool active)
        {
            if (quantityModal != null) quantityModal.SetActive(active);
        }

        private void UpdateQuantityText()
        {
            if (selectedQuantityText != null && selected != null)
                selectedQuantityText.text = selectedQuantity + " / " + Math.Max(0, selected.cargoQuantity);
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
            quantityModal = Find("QuantityModal")?.gameObject;
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
            cancelQuantityButton?.onClick.AddListener(CloseQuantity);
            confirmQuantityButton?.onClick.AddListener(ConfirmQuantity);
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
