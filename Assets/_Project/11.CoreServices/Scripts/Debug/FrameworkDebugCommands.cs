/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices debug 기능을 service 형태로 묶어 scene controller와 debug bridge에 제공한다.
 * - time scale 변경, 무역 즉시 완료 요청, load completed 강제 발행을 한 진입점으로 모은다.
 *
 * Main Features
 * - GameTimeService time scale 변경을 위임한다.
 * - CompleteTradeRequested 이벤트를 발행해 coordinator가 active trade를 즉시 완료하게 한다.
 * - LoadCompleted 이벤트를 강제로 발행한다.
 *
 * Usage for Team Members
 * - FrameworkRoot.DebugCommands를 통해 호출한다.
 * - runtime game flow 검증과 개발 편의 기능에 한정해 사용한다.
 *
 * Main Public APIs
 * - SetTimeScale(...): debug time scale을 적용한다.
 * - CompleteTradeImmediately(): active trade 즉시 완료 이벤트를 발행한다.
 * - ForceLoadCompleted(): 현재 저장 데이터로 load completed 이벤트를 발행한다.
 *
 * Important Notes
 * - 실제 상태 변경은 각 이벤트 구독자 또는 GameTimeService가 수행한다.
 */
namespace ND.Framework
{
    /// <summary>
    /// Framework debug 동작을 호출하기 위한 command service이다.
    /// </summary>
    public sealed class FrameworkDebugCommands
    {
        private readonly GameTimeService gameTimeService;

        /// <summary>
        /// debug command service를 생성한다.
        /// </summary>
        /// <param name="gameTimeService">time scale 변경을 수행할 GameTimeService.</param>
        public FrameworkDebugCommands(GameTimeService gameTimeService)
        {
            this.gameTimeService = gameTimeService;
        }

        /// <summary>
        /// Unity time scale을 변경한다.
        /// </summary>
        /// <param name="scale">적용할 time scale.</param>
        public void SetTimeScale(float scale)
        {
            // time scale 보정과 Unity 적용은 GameTimeService에 위임한다.
            gameTimeService.SetTimeScale(scale);
        }

        /// <summary>
        /// 현재 active trade를 즉시 완료하도록 요청한다.
        /// </summary>
        public void CompleteTradeImmediately()
        {
            // coordinator가 이벤트를 받아 실제 progress와 settlement 생성을 처리한다.
            FrameworkEvents.RaiseCompleteTradeRequested();
        }

        /// <summary>
        /// 현재 저장 데이터로 LoadCompleted 이벤트를 강제로 발행한다.
        /// </summary>
        public void ForceLoadCompleted()
        {
            // debug UI 갱신을 위해 현재 root의 SaveData 참조를 그대로 전달한다.
            FrameworkEvents.RaiseLoadCompleted(FrameworkRoot.Instance.CurrentSaveData);
        }
    }
}
