/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 인게임 scene에서 표시할 주요 화면 상태를 정의한다.
 * - 저장 데이터의 무역 진행 상태를 UI panel 전환 상태로 변환하는 기준을 제공한다.
 *
 * Main Features
 * - 준비, 이동, 정산, 마을 화면 상태를 enum으로 제공한다.
 *
 * Usage for Team Members
 * - InGameScreenStateRouter를 통해 상태를 변경하고 FrameworkEvents.InGameScreenChanged를 구독해 UI를 갱신한다.
 *
 * Main Public APIs
 * - InGameScreenState: 인게임 화면 상태 enum.
 *
 * Important Notes
 * - enum 값 추가 시 InGameScreenStateRouter.MapFromTradeProgressState와 관련 UI panel 처리를 함께 검토해야 한다.
 */
namespace ND.Framework
{
    /// <summary>
    /// 인게임 UI가 표시해야 하는 주요 화면 상태를 나타낸다.
    /// </summary>
    public enum InGameScreenState
    {
        /// <summary>
        /// 무역 출발 전 준비 화면이다.
        /// </summary>
        Preparation,

        /// <summary>
        /// 무역 이동 중 화면이다.
        /// </summary>
        Traveling,

        /// <summary>
        /// 무역 정산 결과 확인 화면이다.
        /// </summary>
        Settlement,

        /// <summary>
        /// 정산 claim과 목적지 위치 저장이 완료된 마을 화면이다.
        /// </summary>
        Town,

        /// <summary>
        /// Town에서 진입하는 일시적인 시장 UI 화면이다. SaveData에는 저장하지 않는다.
        /// </summary>
        Market
    }
}
