/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - InGame scene의 preparation/traveling/settlement panel 전환과 settlement 표시를 수동 검증하는 테스트 view이다.
 * - ISettlementView 구현체 예시로 SettlementUiDataAdapter와 연결된다.
 *
 * Main Features
 * - InGameScreenChanged 이벤트를 받아 panel active 상태를 전환한다.
 * - SettlementViewData의 trade ID, 등급, 실패 원인, 순이익을 TMP text에 표시한다.
 * - claim 버튼 interactable 상태를 갱신한다.
 *
 * Usage for Team Members
 * - 테스트 UI GameObject에 연결하고 panel, text, button reference를 Inspector에서 지정한다.
 * - SettlementUiDataAdapter의 settlementViewBehaviour에 이 component를 연결할 수 있다.
 *
 * Main Public APIs
 * - ShowSettlement(...): 정산 표시 데이터를 UI에 반영한다.
 * - ShowNoSettlement(...): 정산 결과 없음 상태를 표시한다.
 * - SetClaimInteractable(...): claim 버튼 활성 상태를 변경한다.
 *
 * Important Notes
 * - showAllPanelsForDebug가 true이면 화면 상태와 관계없이 모든 panel을 표시한다.
 * - 이 스크립트는 테스트 view이며 최종 UI 구현체를 대체하지 않을 수 있다.
 */
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Framework
{
    /// <summary>
    /// settlement UI와 인게임 화면 전환을 검증하기 위한 테스트 view이다.
    /// </summary>
    public sealed class InGameSettlementTestView : MonoBehaviour, ISettlementView
    {
        [Header("Panels")]
        [SerializeField] private GameObject preparationPanel;
        [SerializeField] private GameObject travelingPanel;
        [SerializeField] private GameObject settlementPanel;
        [SerializeField] private bool showAllPanelsForDebug;

        [Header("Settlement Text")]
        [SerializeField] private TMP_Text tradeIdText;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private TMP_Text failureReasonText;
        [SerializeField] private TMP_Text netProfitText;

        [Header("Actions")]
        [SerializeField] private Button claimSettlementButton;

        /// <summary>
        /// 정산 결과를 테스트 UI text와 claim 버튼 상태에 반영한다.
        /// </summary>
        /// <param name="viewData">표시할 settlement view data.</param>
        public void ShowSettlement(SettlementViewData viewData)
        {
            // view data가 없으면 이전 정산 값이 남지 않도록 결과 없음 상태로 전환한다.
            if (viewData == null)
            {
                ShowNoSettlement("No settlement view data.");
                return;
            }

            SetText(tradeIdText, viewData.TradeId);
            SetText(gradeText, viewData.Grade.ToString());
            SetText(failureReasonText, viewData.FailureReason.ToString());
            SetText(netProfitText, viewData.NetProfit.ToString());
            SetClaimInteractable(viewData.CanClaim);
        }

        /// <summary>
        /// 표시할 정산 결과가 없는 상태를 테스트 UI에 반영한다.
        /// </summary>
        /// <param name="reason">결과가 없는 이유.</param>
        public void ShowNoSettlement(string reason)
        {
            // 결과 없음 상태에서는 trade ID 영역에 사유를 표시하고 상세 값은 비운다.
            SetText(tradeIdText, reason ?? "No settlement.");
            SetText(gradeText, string.Empty);
            SetText(failureReasonText, string.Empty);
            SetText(netProfitText, string.Empty);
            SetClaimInteractable(false);
        }

        /// <summary>
        /// claim 버튼의 상호작용 가능 여부를 변경한다.
        /// </summary>
        /// <param name="interactable">버튼을 누를 수 있으면 true.</param>
        public void SetClaimInteractable(bool interactable)
        {
            // 버튼 reference가 비어 있어도 테스트 view가 예외를 만들지 않도록 null을 허용한다.
            if (claimSettlementButton != null)
            {
                claimSettlementButton.interactable = interactable;
            }
        }

        private void OnEnable()
        {
            // 화면 상태 이벤트를 받아 테스트 panel 표시를 즉시 동기화한다.
            FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
            RefreshCurrentScreen();
        }

        private void OnDisable()
        {
            // 비활성 테스트 view가 화면 이벤트를 계속 받지 않도록 구독을 해제한다.
            FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
        }

        private void HandleScreenChanged(InGameScreenState screenState)
        {
            ApplyScreenState(screenState);
        }

        private void RefreshCurrentScreen()
        {
            var root = FrameworkRoot.Instance;
            // router가 아직 준비되지 않은 초기 상태에서는 preparation 화면을 기본값으로 표시한다.
            if (root == null || root.InGameScreenRouter == null)
            {
                ApplyScreenState(InGameScreenState.Preparation);
                return;
            }

            ApplyScreenState(root.InGameScreenRouter.CurrentScreenState);
        }

        private void ApplyScreenState(InGameScreenState screenState)
        {
            // panel 배치 확인이 필요할 때는 모든 panel을 동시에 표시한다.
            if (showAllPanelsForDebug)
            {
                SetActive(preparationPanel, true);
                SetActive(travelingPanel, true);
                SetActive(settlementPanel, true);
                return;
            }

            // 현재 인게임 화면 상태에 해당하는 panel만 활성화한다.
            SetActive(preparationPanel, screenState == InGameScreenState.Preparation);
            SetActive(travelingPanel, screenState == InGameScreenState.Traveling);
            SetActive(settlementPanel, screenState == InGameScreenState.Settlement);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }
    }
}
