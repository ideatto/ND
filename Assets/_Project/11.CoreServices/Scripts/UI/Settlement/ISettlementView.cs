/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Settlement UI가 CoreServices settlement adapter와 통신하기 위한 view 계약을 정의한다.
 * - UI 구현체가 settlement 결과 표시와 claim 버튼 상태 갱신을 일관되게 처리하도록 한다.
 *
 * Main Features
 * - settlement 결과 표시, 결과 없음 표시, claim 상호작용 상태 변경 API를 제공한다.
 *
 * Usage for Team Members
 * - SettlementUiDataAdapter의 settlementViewBehaviour에는 이 interface를 구현한 MonoBehaviour를 연결한다.
 * - 구현체는 전달된 SettlementViewData를 표시 전용 데이터로 취급해야 한다.
 *
 * Main Public APIs
 * - ShowSettlement(...): 정산 결과를 표시한다.
 * - ShowNoSettlement(...): 표시할 정산 결과가 없는 상태를 표시한다.
 * - SetClaimInteractable(...): claim 조작 가능 여부를 반영한다.
 *
 * Important Notes
 * - 이 계약은 UI 표시만 담당하며 저장 데이터 변경이나 정산 claim 처리를 직접 수행하지 않는다.
 */
namespace ND.Framework
{
    /// <summary>
    /// Settlement 결과 화면이 adapter로부터 표시 데이터를 받기 위한 UI 계약이다.
    /// </summary>
    public interface ISettlementView
    {
        /// <summary>
        /// 정산 결과 표시 데이터를 화면에 반영한다.
        /// </summary>
        /// <param name="viewData">표시할 trade ID, 결과 등급, 손익, claim 가능 여부를 담은 데이터.</param>
        void ShowSettlement(SettlementViewData viewData);

        /// <summary>
        /// 표시할 정산 결과가 없거나 bridge가 준비되지 않은 상태를 화면에 반영한다.
        /// </summary>
        /// <param name="reason">결과가 없는 이유를 설명하는 메시지.</param>
        void ShowNoSettlement(string reason);

        /// <summary>
        /// 정산 claim 조작 가능 여부를 화면 컨트롤에 반영한다.
        /// </summary>
        /// <param name="interactable">사용자가 claim을 요청할 수 있으면 true.</param>
        void SetClaimInteractable(bool interactable);
    }
}
