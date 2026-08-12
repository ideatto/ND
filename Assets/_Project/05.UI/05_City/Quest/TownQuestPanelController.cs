using System;
using System.Collections.Generic;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Quest
{
    /// <summary>
    /// Inspector-wired Quest UI. The caller decides where the list can be opened
    /// and supplies that town ID through OpenTownQuestList.
    /// </summary>
    public sealed class TownQuestPanelController : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject caravanSelectionPanel;
        [SerializeField] private GameObject listPanel;
        [SerializeField] private GameObject offerPanel;
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private GameObject paymentPanel;

        [Header("Caravan Selection")]
        [SerializeField] private Transform caravanSelectionContainer;
        [SerializeField] private Button caravanSelectionRowTemplate;
        [SerializeField] private TMP_Text emptyCaravanSelectionText;
        [SerializeField] private Button caravanSelectionCloseButton;

        [Header("Quest List")]
        [SerializeField] private TMP_Text questListContextText;
        [SerializeField] private Transform questListContainer;
        [SerializeField] private Button questRowTemplate;
        [SerializeField] private TMP_Text emptyListText;
        [SerializeField] private Button listCloseButton;

        [Header("Offer")]
        [SerializeField] private TMP_Text offerTitleText;
        [SerializeField] private TMP_Text offerDescriptionText;
        [SerializeField] private TMP_Text offerConditionText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button rejectButton;

        [Header("Progress")]
        [SerializeField] private TMP_Text progressTitleText;
        [SerializeField] private TMP_Text progressDescriptionText;
        [SerializeField] private TMP_Text progressConditionText;
        [SerializeField] private Button progressCloseButton;

        [Header("Payment")]
        [SerializeField] private TMP_Text paymentTitleText;
        [SerializeField] private TMP_Text paymentConditionText;
        [SerializeField] private Transform caravanListContainer;
        [SerializeField] private Button caravanRowTemplate;
        [SerializeField] private TMP_Text selectedCaravanText;
        [SerializeField] private TMP_Text paymentErrorText;
        [SerializeField] private Button tradingCurrencyPaymentButton;
        [SerializeField] private Button allItemsPaymentButton;
        [SerializeField] private Button paymentCancelButton;

        [Header("Back (뒤로 가기 — 이전 단계로)")]
        [SerializeField] private Button listBackButton;      // 목록 → 마차 선택
        [SerializeField] private Button offerBackButton;     // 상세(수락) → 목록
        [SerializeField] private Button progressBackButton;  // 상세(진행) → 목록
        [SerializeField] private Button paymentBackButton;   // 상세(결제) → 목록

        private readonly List<Button> questRows = new List<Button>();
        private readonly List<Button> caravanSelectionRows = new List<Button>();
        private readonly List<Button> caravanRows = new List<Button>();
        private QuestPanelRuntimeBridge bridge;
        private TownQuestViewModel currentQuest;
        private TownQuestCaravanView selectedCaravan;
        private string currentTownId = string.Empty;

        public event Action QuestMenuOpened;
        public event Action QuestMenuClosed;
        public event Action<string, int> QuestCaravanContextSelected;
        public event Action<string, TownQuestPanelKind> QuestPanelChanged;
        public event Action<string> QuestAccepted;
        public event Action<string> PaymentCaravanSelected;

        private void Awake()
        {
            SetAllPanels(false);
            if (caravanSelectionRowTemplate != null)
                caravanSelectionRowTemplate.gameObject.SetActive(false);
            if (questRowTemplate != null)
                questRowTemplate.gameObject.SetActive(false);
            if (caravanRowTemplate != null)
                caravanRowTemplate.gameObject.SetActive(false);

            Button backdropButton = GetComponent<Button>();
            backdropButton?.onClick.AddListener(CloseAllPanels);
            acceptButton?.onClick.AddListener(AcceptQuest);
            // 닫기/취소/거절은 '전 단계로'가 아니라 퀘스트 UI를 통째로 닫는다(뒤로 버튼과 역할 분리).
            rejectButton?.onClick.AddListener(CloseAllPanels);
            progressCloseButton?.onClick.AddListener(CloseAllPanels);
            paymentCancelButton?.onClick.AddListener(CloseAllPanels);
            caravanSelectionCloseButton?.onClick.AddListener(
                CloseCaravanSelection);
            listCloseButton?.onClick.AddListener(CloseList);
            tradingCurrencyPaymentButton?.onClick.AddListener(
                () => Complete(QuestPaymentMode.TradingCurrency));
            allItemsPaymentButton?.onClick.AddListener(
                () => Complete(QuestPaymentMode.CaravanItems));

            // 뒤로 가기(이전 단계로) — 목록→마차선택, 상세→목록
            listBackButton?.onClick.AddListener(BackToCaravanSelection);
            offerBackButton?.onClick.AddListener(BackToList);
            progressBackButton?.onClick.AddListener(BackToList);
            paymentBackButton?.onClick.AddListener(BackToList);
        }

        private void OnEnable()
        {
            TryBindBridge();
        }

        private void Start()
        {
            TryBindBridge();
        }

        private void OnDisable()
        {
            UnbindBridge();
        }

        public void OpenTownQuestList(string townId)
        {
            QuestMenuOpened?.Invoke();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (!TryBindBridge() || string.IsNullOrWhiteSpace(townId))
            {
                gameObject.SetActive(false);
                return;
            }

            currentTownId = townId;
            SetAllPanels(false);
            SetActive(listPanel, true);
            bridge.RequestTownQuestList(townId);
        }

        public void OpenCaravanSelection()
        {
            QuestMenuOpened?.Invoke();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (!TryBindBridge())
            {
                gameObject.SetActive(false);
                return;
            }

            currentTownId = string.Empty;
            SetAllPanels(false);
            SetActive(caravanSelectionPanel, true);
            RenderCaravanSelection();
        }

        public void RefreshOpenTownQuestList()
        {
            if (bridge != null && !string.IsNullOrWhiteSpace(currentTownId))
                bridge.RequestTownQuestList(currentTownId);
        }

        private bool TryBindBridge()
        {
            QuestPanelRuntimeBridge next = FrameworkRoot.Instance?.QuestPanelBridge;
            if (next == null) return false;
            if (ReferenceEquals(bridge, next)) return true;
            UnbindBridge();
            bridge = next;
            bridge.ListChanged += RenderList;
            bridge.PanelRequested += RenderPanel;
            return true;
        }

        private void UnbindBridge()
        {
            if (bridge == null) return;
            bridge.ListChanged -= RenderList;
            bridge.PanelRequested -= RenderPanel;
            bridge = null;
        }

        private void RenderList(IReadOnlyList<TownQuestViewModel> quests)
        {
            ClearRows(questRows);
            int count = quests?.Count ?? 0;
            SetActive(emptyListText?.gameObject, count == 0);
            if (questRowTemplate == null || questListContainer == null) return;

            for (int i = 0; i < count; i++)
            {
                TownQuestViewModel quest = quests[i];
                Button row = Instantiate(questRowTemplate, questListContainer);
                row.gameObject.SetActive(true);
                SetButtonText(row, BuildQuestRowText(quest));
                string questId = quest.QuestId;
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(() => bridge?.RequestOpenQuest(questId));
                questRows.Add(row);
            }
        }

        private void RenderCaravanSelection()
        {
            ClearRows(caravanSelectionRows);
            ND.Framework.SaveData saveData =
                FrameworkRoot.Instance?.CurrentSaveData;
            int count = saveData?.caravans?.Count ?? 0;
            SetActive(emptyCaravanSelectionText?.gameObject, count == 0);
            if (caravanSelectionRowTemplate == null ||
                caravanSelectionContainer == null)
                return;

            for (int i = 0; i < count; i++)
            {
                ND.Framework.CaravanSaveData caravan = saveData.caravans[i];
                if (caravan == null) continue;

                Button row = Instantiate(
                    caravanSelectionRowTemplate,
                    caravanSelectionContainer);
                row.gameObject.SetActive(true);
                string town = string.IsNullOrWhiteSpace(caravan.currentTownId)
                    ? "위치 미상"
                    : caravan.currentTownId.Trim();
                SetButtonText(
                    row,
                    $"{ResolveCaravanDisplayName(caravan)}  —  {town}");
                row.interactable = !string.IsNullOrWhiteSpace(
                    caravan.currentTownId);
                string townId = caravan.currentTownId;
                string caravanDisplayName = ResolveCaravanDisplayName(caravan);
                int slotIndex = caravan.slotIndex;
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(
                    () => SelectQuestCaravan(townId, slotIndex, caravanDisplayName));
                caravanSelectionRows.Add(row);
            }
        }

        private void SelectQuestCaravan(
            string townId,
            int slotIndex,
            string caravanDisplayName)
        {
            if (string.IsNullOrWhiteSpace(townId)) return;

            currentTownId = townId.Trim();
            FrameworkRoot root = FrameworkRoot.Instance;
            root?.RefreshTownQuestsOnVisit(currentTownId);

            SetAllPanels(false);
            SetActive(listPanel, true);
            SetText(
                questListContextText,
                $"{NormalizeCaravanDisplayName(caravanDisplayName, slotIndex)}  /  {currentTownId}");
            bridge?.RequestTownQuestList(currentTownId);
            QuestCaravanContextSelected?.Invoke(currentTownId, slotIndex);
        }

        // Rename 결과를 모든 퀘스트 화면에서 사용하고, 구버전 저장 데이터만 슬롯 기본명으로 보완한다.
        private static string ResolveCaravanDisplayName(ND.Framework.CaravanSaveData caravan)
        {
            if (caravan == null) return string.Empty;
            return NormalizeCaravanDisplayName(caravan.displayName, caravan.slotIndex);
        }

        private static string NormalizeCaravanDisplayName(string displayName, int slotIndex)
        {
            string normalized = displayName?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(normalized)
                ? $"Caravan {slotIndex + 1}"
                : normalized;
        }

        private void RenderPanel(TownQuestOpenResult result)
        {
            if (result == null || result.PanelKind == TownQuestPanelKind.None)
            {
                QuestPanelChanged?.Invoke(string.Empty, TownQuestPanelKind.None);
                CloseDetailPanels();
                return;
            }

            currentQuest = result.Quest;
            selectedCaravan = null;
            QuestPanelChanged?.Invoke(currentQuest?.QuestId ?? string.Empty, result.PanelKind);
            SetActive(offerPanel, result.PanelKind == TownQuestPanelKind.Offer);
            SetActive(progressPanel, result.PanelKind == TownQuestPanelKind.Progress);
            SetActive(paymentPanel, result.PanelKind == TownQuestPanelKind.Payment);

            string conditions = BuildConditionText(currentQuest);
            if (result.PanelKind == TownQuestPanelKind.Offer)
            {
                SetText(offerTitleText, currentQuest.DisplayName);
                SetText(offerDescriptionText, currentQuest.Description);
                SetText(offerConditionText, conditions);
            }
            else if (result.PanelKind == TownQuestPanelKind.Progress)
            {
                SetText(progressTitleText, currentQuest.DisplayName);
                SetText(progressDescriptionText,
                    "퀘스트 발행 마을에 조건을 완납할 수 있는 캐러반이 도착해야 합니다.");
                SetText(progressConditionText, conditions);
            }
            else
            {
                SetText(paymentTitleText, currentQuest.DisplayName);
                SetText(paymentConditionText, conditions);
                SetText(paymentErrorText, string.Empty);
                RenderCaravans(currentQuest.EligibleCaravans);
                UpdatePaymentButtons();
            }
        }

        private void RenderCaravans(
            IReadOnlyList<TownQuestCaravanView> caravans)
        {
            ClearRows(caravanRows);
            if (caravanRowTemplate == null || caravanListContainer == null)
                return;
            for (int i = 0; i < caravans.Count; i++)
            {
                TownQuestCaravanView caravan = caravans[i];
                Button row = Instantiate(caravanRowTemplate, caravanListContainer);
                row.gameObject.SetActive(true);
                // GUID는 결제 요청에만 사용하고, 결제 패널에는 플레이어가 지정한 이름을 표시한다.
                SetButtonText(row, caravan.DisplayName);
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(() => SelectCaravan(caravan));
                caravanRows.Add(row);
            }
        }

        private void SelectCaravan(TownQuestCaravanView caravan)
        {
            selectedCaravan = caravan;
            PaymentCaravanSelected?.Invoke(caravan?.CaravanId ?? string.Empty);
            SetText(selectedCaravanText,
                $"선택: {caravan.DisplayName}");
            SetText(paymentErrorText, string.Empty);
            UpdatePaymentButtons();
        }

        private void UpdatePaymentButtons()
        {
            bool selected = selectedCaravan != null;
            SetActive(tradingCurrencyPaymentButton?.gameObject,
                currentQuest?.ShowTradingCurrencyPayment == true);
            SetActive(allItemsPaymentButton?.gameObject,
                currentQuest?.ShowItemPayment == true);
            if (tradingCurrencyPaymentButton != null)
                tradingCurrencyPaymentButton.interactable =
                    selected && selectedCaravan.CanPayTradingCurrency;
            if (allItemsPaymentButton != null)
                allItemsPaymentButton.interactable =
                    selected && selectedCaravan.CanPayAllItems;
            if (!selected)
                SetText(selectedCaravanText, "결제할 캐러반을 선택하세요.");
        }

        private void AcceptQuest()
        {
            string questId = currentQuest?.QuestId ?? string.Empty;
            QuestMutationResult result = bridge?.AcceptOpenQuest();
            if (result != null && result.Succeeded)
            {
                QuestAccepted?.Invoke(questId);
                return;
            }

            if (result == null || !result.Succeeded)
                SetText(offerConditionText, "퀘스트 수락 저장에 실패했습니다.");
        }

        private void Complete(QuestPaymentMode mode)
        {
            if (selectedCaravan == null)
            {
                SetText(paymentErrorText, "결제할 캐러반을 선택하세요.");
                return;
            }

            QuestCompletionResult result = bridge?.CompleteOpenQuest(
                selectedCaravan.CaravanId, mode);
            if (result == null || !result.Succeeded)
            {
                SetText(paymentErrorText,
                    BuildCompletionError(result?.FailureReason ??
                        QuestCompletionFailureReason.InvalidInput));
                return;
            }
            RefreshOpenTownQuestList();
        }

        /// <summary>상세(offer/progress/payment) → 이전 단계인 퀘스트 목록으로.</summary>
        private void BackToList()
        {
            SetAllPanels(false);
            SetActive(listPanel, true);
            RefreshOpenTownQuestList();   // 현재 마을 목록 다시 채움
        }

        /// <summary>퀘스트 목록 → 이전 단계인 마차 선택으로.</summary>
        private void BackToCaravanSelection()
        {
            OpenCaravanSelection();
        }

        private void CloseDetailPanels()
        {
            currentQuest = null;
            selectedCaravan = null;
            SetActive(offerPanel, false);
            SetActive(progressPanel, false);
            SetActive(paymentPanel, false);
        }

        private void CloseList()
        {
            CloseAllPanels();
        }

        private void CloseCaravanSelection()
        {
            CloseAllPanels();
        }

        private void SetAllPanels(bool active)
        {
            SetActive(caravanSelectionPanel, active);
            SetActive(listPanel, active);
            SetActive(offerPanel, active);
            SetActive(progressPanel, active);
            SetActive(paymentPanel, active);
        }

        private static string BuildQuestRowText(TownQuestViewModel quest)
        {
            string state = quest.Phase == QuestRuntimePhase.Offered
                ? "제안"
                : quest.HasEligibleCaravan ? "완료 가능" : "진행 중";
            return $"{quest.DisplayName}\n[{state}]";
        }

        private static string BuildConditionText(TownQuestViewModel quest)
        {
            if (quest == null) return string.Empty;
            var lines = new List<string>();
            if (quest.ShowTradingCurrencyPayment)
                lines.Add($"골드 결제: {quest.RequiredTradingCurrency:N0} G");
            if (quest.ShowItemPayment)
            {
                lines.Add("아이템 전체 제출:");
                for (int i = 0; i < quest.RequiredItems.Count; i++)
                {
                    TownQuestItemView item = quest.RequiredItems[i];
                    lines.Add($"- {item.DisplayName} × {item.RequiredQuantity:N0}");
                }
            }
            return string.Join("\n", lines);
        }

        private static string BuildCompletionError(
            QuestCompletionFailureReason reason)
        {
            switch (reason)
            {
                case QuestCompletionFailureReason.CaravanNotAtSubmissionTown:
                    return "선택한 캐러반이 퀘스트 발행 마을에 없습니다.";
                case QuestCompletionFailureReason.CaravanUnavailable:
                    return "선택한 캐러반은 현재 결제할 수 없는 상태입니다.";
                case QuestCompletionFailureReason.InsufficientTradingCurrency:
                    return "골드가 부족합니다.";
                case QuestCompletionFailureReason.InsufficientItems:
                    return "조건 아이템 수량이 부족합니다.";
                case QuestCompletionFailureReason.SaveFailed:
                    return "저장에 실패했습니다. 다시 시도하세요.";
                default:
                    return $"퀘스트 완료에 실패했습니다. ({reason})";
            }
        }

        private static void ClearRows(List<Button> rows)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null) Destroy(rows[i].gameObject);
            rows.Clear();
        }

        private static void SetButtonText(Button button, string value)
        {
            SetText(button?.GetComponentInChildren<TMP_Text>(true), value);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

        public void CloseAllPanels()
        {
            bridge?.CancelPanel();
            ClearRows(caravanSelectionRows);
            CloseDetailPanels();
            SetActive(caravanSelectionPanel, false);
            SetActive(listPanel, false);
            currentTownId = string.Empty;
            QuestMenuClosed?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
