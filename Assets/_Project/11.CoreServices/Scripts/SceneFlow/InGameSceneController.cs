/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - InGame scene 진입 시 저장 데이터 기반 화면 상태를 refresh하고 debug/scene 버튼 요청을 FrameworkRoot로 전달한다.
 * - 인게임 UI 버튼과 CoreServices service 사이의 scene controller 역할을 한다.
 *
 * Main Features
 * - Start에서 현재 저장 데이터 기준 화면 상태를 강제 알림한다.
 * - title 복귀, time scale 변경, 무역 즉시 완료 버튼용 API를 제공한다.
 *
 * Usage for Team Members
 * - InGame scene의 controller GameObject에 연결하고 UI Button 이벤트에 public method를 연결한다.
 * - 화면 패널은 InGameScreenStateRouter 이벤트를 구독해 갱신한다.
 *
 * Main Public APIs
 * - RefreshCurrentScreen(): 현재 저장 데이터 기준 화면 상태를 다시 발행한다.
 * - ReturnToTitle(): title scene으로 복귀한다.
 * - SetTimeScale(...): debug time scale을 변경한다.
 * - CompleteTradeImmediately(): debug 무역 즉시 완료를 요청한다.
 *
 * Important Notes
 * - debug 성격의 time scale과 즉시 완료 API는 실제 release UI 노출 여부를 별도로 관리해야 한다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// InGame scene UI 입력과 화면 상태 refresh를 FrameworkRoot service에 연결하는 controller이다.
    /// </summary>
    public sealed class InGameSceneController : MonoBehaviour
    {
        private void Start()
        {
            // scene 진입 직후 기존 저장 상태를 UI가 다시 받을 수 있도록 강제 refresh한다.
            RefreshCurrentScreen();
        }

        /// <summary>
        /// 현재 저장 데이터 기준 인게임 화면 상태를 다시 발행한다.
        /// </summary>
        public void RefreshCurrentScreen()
        {
            var root = FrameworkRoot.Instance;
            // FrameworkRoot가 아직 준비되지 않은 경우 scene controller가 직접 복구하지 않고 조용히 대기한다.
            if (root == null || root.InGameScreenRouter == null)
            {
                return;
            }

            root.InGameScreenRouter.RefreshFromSaveData(root.CurrentSaveData, true);
        }

        /// <summary>
        /// 현재 저장 데이터를 저장하고 title scene으로 복귀한다.
        /// </summary>
        public void ReturnToTitle()
        {
            // 저장 후 scene 이동 순서를 FrameworkRoot에 위임한다.
            FrameworkRoot.Instance.ReturnToTitle();
        }

        /// <summary>
        /// Unity time scale을 debug command를 통해 변경한다.
        /// </summary>
        /// <param name="scale">적용할 time scale. 음수는 GameTimeService에서 0으로 보정된다.</param>
        public void SetTimeScale(float scale)
        {
            // scene UI가 GameTimeService를 직접 조작하지 않도록 debug command를 경유한다.
            FrameworkRoot.Instance.DebugCommands.SetTimeScale(scale);
        }

        /// <summary>
        /// gameplay 인게임 시간 배율을 debug command를 통해 변경한다.
        /// </summary>
        /// <param name="multiplier">현실 1초당 인게임 N초.</param>
        public void SetInGameTimeMultiplier(float multiplier)
        {
            FrameworkRoot.Instance.DebugCommands.SetInGameTimeMultiplier(multiplier);
        }

        /// <summary>
        /// 인게임 시간 진행을 일시정지한다.
        /// </summary>
        public void PauseGameTime()
        {
            FrameworkRoot.Instance.DebugCommands.PauseGameTime();
        }

        /// <summary>
        /// 인게임 시간 진행을 재개한다.
        /// </summary>
        public void ResumeGameTime()
        {
            FrameworkRoot.Instance.DebugCommands.ResumeGameTime();
        }

        /// <summary>
        /// 현재 active trade의 즉시 완료를 요청한다.
        /// </summary>
        public void CompleteTradeImmediately()
        {
            // 실제 완료 처리는 event를 구독한 TradeProgressCoordinator가 수행한다.
            FrameworkRoot.Instance.DebugCommands.CompleteTradeImmediately();
        }
    }
}
