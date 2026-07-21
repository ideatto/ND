/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 저장 데이터의 무역 진행 상태를 인게임 UI 화면 상태로 변환하고 변경 이벤트를 발행한다.
 * - preparation, traveling, settlement, town 화면 전환의 단일 진입점을 제공한다.
 *
 * Main Features
 * - SaveData 또는 TradeProgressState에서 InGameScreenState를 계산한다.
 * - 중복 화면 상태 이벤트 발행을 방지한다.
 * - 강제 알림 옵션으로 scene 진입 시 현재 상태를 다시 전파할 수 있다.
 *
 * Usage for Team Members
 * - 화면 상태 변경은 RequestScreen(...) 또는 RefreshFromSaveData(...)를 통해 요청한다.
 * - UI는 FrameworkEvents.InGameScreenChanged를 구독해 panel을 갱신한다.
 *
 * Main Public APIs
 * - CurrentScreenState: 마지막으로 요청된 화면 상태.
 * - RefreshFromSaveData(...): 저장 데이터 기준 화면 상태를 요청한다.
 * - RequestScreen(...): 명시적인 화면 상태 변경을 요청한다.
 * - MapFromSaveData(...): 저장 데이터에서 화면 상태를 계산한다.
 *
 * Important Notes
 * - forceNotify가 false이면 같은 상태에 대한 중복 이벤트는 발행하지 않는다.
 * - 완료/실패 상태는 목적지 위치 반영과 pending/commit 정리가 확인된 경우에만 town으로 매핑된다.
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// 무역 진행 상태를 인게임 화면 상태로 변환하고 화면 변경 이벤트를 발행하는 router이다.
    /// </summary>
    public sealed class InGameScreenStateRouter
    {
        private bool hasCurrentScreenState;

        /// <summary>
        /// 마지막으로 적용된 인게임 화면 상태이다.
        /// </summary>
        public InGameScreenState CurrentScreenState { get; private set; }

        /// <summary>
        /// 저장 데이터의 무역 진행 상태를 기준으로 현재 화면 상태를 갱신한다.
        /// </summary>
        /// <param name="saveData">화면 상태 계산에 사용할 저장 데이터.</param>
        /// <param name="forceNotify">같은 상태여도 이벤트를 다시 발행하려면 true.</param>
        public void RefreshFromSaveData(SaveData saveData, bool forceNotify = false)
        {
            RequestScreen(MapFromSaveData(saveData), forceNotify);
        }

        /// <summary>
        /// 지정한 인게임 화면 상태를 적용하고 변경 이벤트를 발행한다.
        /// </summary>
        /// <param name="screenState">적용할 화면 상태.</param>
        /// <param name="forceNotify">현재 상태와 같아도 이벤트를 발행하려면 true.</param>
        public void RequestScreen(InGameScreenState screenState, bool forceNotify = false)
        {
            // 같은 화면 상태를 반복 발행하면 UI refresh가 불필요하게 중복되므로 기본적으로 차단한다.
            if (hasCurrentScreenState && CurrentScreenState == screenState && !forceNotify)
            {
                return;
            }

            // 상태를 먼저 갱신한 뒤 이벤트를 발행해 구독자가 CurrentScreenState를 즉시 조회할 수 있게 한다.
            hasCurrentScreenState = true;
            CurrentScreenState = screenState;
            FrameworkEvents.RaiseInGameScreenChanged(screenState);
        }

        /// <summary>
        /// 저장 데이터에서 인게임 화면 상태를 계산한다.
        /// </summary>
        /// <param name="saveData">무역 진행 상태를 포함한 저장 데이터.</param>
        /// <returns>저장 데이터에 대응되는 인게임 화면 상태.</returns>
        public static InGameScreenState MapFromSaveData(SaveData saveData)
        {
            // 저장 데이터가 없으면 사용자가 다시 준비 화면에서 상태를 회복할 수 있도록 preparation으로 보낸다.
            if (saveData == null || saveData.tradeProgress == null)
            {
                return InGameScreenState.Preparation;
            }

            var progressState = saveData.tradeProgress.state;
            if (progressState == TradeProgressState.Completed || progressState == TradeProgressState.Failed)
            {
                var pendingCleared = saveData.pendingSettlement == null || !saveData.pendingSettlement.hasResult;
                var commitCleared = saveData.tradePreparationCommit == null || !saveData.tradePreparationCommit.hasCommit;
                var hasCurrentTown = saveData.player != null
                    && !string.IsNullOrWhiteSpace(saveData.player.currentTownId);
                return pendingCleared && commitCleared && hasCurrentTown
                    ? InGameScreenState.Town
                    : InGameScreenState.Preparation;
            }

            return MapFromTradeProgressState(progressState);
        }

        /// <summary>
        /// 저장 데이터의 무역 진행 상태를 인게임 화면 상태로 변환한다.
        /// </summary>
        /// <param name="progressState">저장 데이터 기준 무역 진행 상태.</param>
        /// <returns>UI가 표시할 인게임 화면 상태.</returns>
        public static InGameScreenState MapFromTradeProgressState(TradeProgressState progressState)
        {
            // 이동 중과 정산 대기만 별도 화면으로 유지하고 나머지 상태는 다음 출발을 위한 준비 화면으로 돌린다.
            switch (progressState)
            {
                case TradeProgressState.Traveling:
                    return InGameScreenState.Traveling;
                case TradeProgressState.SettlementPending:
                    return InGameScreenState.Settlement;
                case TradeProgressState.None:
                case TradeProgressState.Preparing:
                case TradeProgressState.Completed:
                case TradeProgressState.Failed:
                default:
                    return InGameScreenState.Preparation;
            }
        }
    }
}
