/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Unity Inspector ContextMenu에서 FrameworkDebugCommands를 호출할 수 있게 하는 debug bridge이다.
 * - scene에 배치된 MonoBehaviour를 통해 개발 중 framework flow를 수동 검증한다.
 *
 * Main Features
 * - debug time scale 적용과 reset을 제공한다.
 * - 무역 즉시 완료와 load completed 강제 발행 ContextMenu를 제공한다.
 * - 공용 데이터 로드 요약을 ContextMenu에서 출력한다.
 * - ForceSeason / ForceDisaster / ForceRouteEvent ContextMenu를 제공한다.
 *
 * Usage for Team Members
 * - debug용 GameObject에 component로 추가하고 ContextMenu 항목을 실행한다.
 * - debugTimeScale, debugSeasonId 등 값을 Inspector에서 조정할 수 있다.
 *
 * Main Public APIs
 * - SetDebugTimeScale(): debugTimeScale 값을 적용한다.
 * - ResetTimeScale(): time scale을 1로 되돌린다.
 * - CompleteTradeImmediately(): active trade 즉시 완료를 요청한다.
 * - ForceLoadCompleted(): LoadCompleted 이벤트를 강제로 발행한다.
 * - LogSharedGameDataSummary(): SharedGameData 요약을 로그로 출력한다.
 * - ForceSeason() / ForceDisaster() / ForceRouteEvent(): M2 월드 Force* debug API를 호출한다.
 *
 * Important Notes
 * - FrameworkRoot.Instance가 준비되어 있어야 정상 동작한다.
 * - runtime gameplay UI가 아닌 개발 편의용 진입점이다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Inspector ContextMenu로 framework debug command를 실행하는 MonoBehaviour이다.
    /// </summary>
    public sealed class FrameworkDebugBridge : MonoBehaviour
    {
        [SerializeField] private float debugTimeScale = 10f;
        [SerializeField] private float debugInGameTimeMultiplier = 60f;

        [Tooltip("ForceSeason ContextMenu에 사용할 계절 ID입니다.")]
        [SerializeField] private string debugSeasonId = "winter";

        [Tooltip("ForceDisaster ContextMenu에 사용할 재난 ID입니다. 빈 문자열이면 재난 없음을 의미합니다.")]
        [SerializeField] private string debugDisasterId = "drought";

        [Tooltip("ForceRouteEvent ContextMenu에 사용할 route event ID입니다. Traveling trade가 필요합니다.")]
        [SerializeField] private string debugRouteEventId = "debug_route_event_001";

        /// <summary>
        /// Inspector에 설정된 debug time scale을 적용한다.
        /// </summary>
        [ContextMenu("Framework/Set Debug Time Scale")]
        public void SetDebugTimeScale()
        {
            // 실제 time scale 적용은 FrameworkDebugCommands를 통해 수행한다.
            FrameworkRoot.Instance.DebugCommands.SetTimeScale(debugTimeScale);
        }

        /// <summary>
        /// Inspector에 설정된 인게임 시간 배율을 적용한다.
        /// </summary>
        [ContextMenu("Framework/Set In-Game Time Multiplier")]
        public void SetDebugInGameTimeMultiplier()
        {
            FrameworkRoot.Instance.DebugCommands.SetInGameTimeMultiplier(debugInGameTimeMultiplier);
        }

        /// <summary>
        /// time scale을 기본값 1로 되돌린다.
        /// </summary>
        [ContextMenu("Framework/Reset Time Scale")]
        public void ResetTimeScale()
        {
            // 테스트 후 game speed를 정상 값으로 복구한다.
            FrameworkRoot.Instance.DebugCommands.SetTimeScale(1f);
        }

        /// <summary>
        /// 인게임 시간 배율을 config 기본값으로 되돌린다.
        /// </summary>
        [ContextMenu("Framework/Reset In-Game Time Multiplier")]
        public void ResetInGameTimeMultiplier()
        {
            FrameworkRoot.Instance.DebugCommands.ResetInGameTimeMultiplier();
        }

        /// <summary>
        /// 인게임 시간 진행을 일시정지한다.
        /// </summary>
        [ContextMenu("Framework/Pause In-Game Time")]
        public void PauseInGameTime()
        {
            FrameworkRoot.Instance.DebugCommands.PauseGameTime();
        }

        /// <summary>
        /// 인게임 시간 진행을 재개한다.
        /// </summary>
        [ContextMenu("Framework/Resume In-Game Time")]
        public void ResumeInGameTime()
        {
            FrameworkRoot.Instance.DebugCommands.ResumeGameTime();
        }

        /// <summary>
        /// 현재 active trade의 즉시 완료를 요청한다.
        /// </summary>
        [ContextMenu("Framework/Complete Trade Immediately")]
        public void CompleteTradeImmediately()
        {
            // 실제 완료 처리는 이벤트를 구독한 coordinator가 수행한다.
            FrameworkRoot.Instance.DebugCommands.CompleteTradeImmediately();
        }

        /// <summary>
        /// 현재 저장 데이터로 LoadCompleted 이벤트를 강제로 발행한다.
        /// </summary>
        [ContextMenu("Framework/Force Load Completed")]
        public void ForceLoadCompleted()
        {
            // load 완료 이벤트 구독 UI와 서비스의 반응을 수동 검증할 때 사용한다.
            FrameworkRoot.Instance.DebugCommands.ForceLoadCompleted();
        }

        /// <summary>
        /// 현재 로드된 공용 데이터 수량과 주요 상태를 로그로 출력한다.
        /// </summary>
        [ContextMenu("Framework/Log Shared Game Data Summary")]
        public void LogSharedGameDataSummary()
        {
            // M0 통합 중 UI 없이도 공용 데이터 로드 여부를 확인할 수 있게 한다.
            FrameworkRoot.Instance.DebugCommands.LogSharedGameDataSummary();
        }

        /// <summary>
        /// Inspector의 debugSeasonId로 WorldSaveData.currentSeasonId를 강제 변경한다.
        /// </summary>
        [ContextMenu("Framework/Force Season")]
        public void ForceSeason()
        {
            if (!TryGetDebugCommands(out var commands))
            {
                return;
            }

            commands.ForceSeason(debugSeasonId);
        }

        /// <summary>
        /// Inspector의 debugDisasterId로 WorldSaveData.currentDisasterId를 강제 변경한다.
        /// </summary>
        [ContextMenu("Framework/Force Disaster")]
        public void ForceDisaster()
        {
            if (!TryGetDebugCommands(out var commands))
            {
                return;
            }

            commands.ForceDisaster(debugDisasterId);
        }

        /// <summary>
        /// Inspector의 debugRouteEventId로 Traveling trade route event 주입 hook을 등록한다.
        /// </summary>
        [ContextMenu("Framework/Force Route Event")]
        public void ForceRouteEvent()
        {
            if (!TryGetDebugCommands(out var commands))
            {
                return;
            }

            commands.ForceRouteEvent(debugRouteEventId);
        }

        private static bool TryGetDebugCommands(out FrameworkDebugCommands commands)
        {
            commands = null;
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.DebugCommands == null)
            {
                FrameworkLog.Warning("Debug bridge command skipped because FrameworkRoot.DebugCommands is not ready.");
                return false;
            }

            commands = FrameworkRoot.Instance.DebugCommands;
            return true;
        }
    }
}
