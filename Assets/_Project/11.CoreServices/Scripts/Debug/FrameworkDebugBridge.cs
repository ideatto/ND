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
 *
 * Usage for Team Members
 * - debug용 GameObject에 component로 추가하고 ContextMenu 항목을 실행한다.
 * - debugTimeScale 값을 Inspector에서 조정할 수 있다.
 *
 * Main Public APIs
 * - SetDebugTimeScale(): debugTimeScale 값을 적용한다.
 * - ResetTimeScale(): time scale을 1로 되돌린다.
 * - CompleteTradeImmediately(): active trade 즉시 완료를 요청한다.
 * - ForceLoadCompleted(): LoadCompleted 이벤트를 강제로 발행한다.
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
        /// time scale을 기본값 1로 되돌린다.
        /// </summary>
        [ContextMenu("Framework/Reset Time Scale")]
        public void ResetTimeScale()
        {
            // 테스트 후 game speed를 정상 값으로 복구한다.
            FrameworkRoot.Instance.DebugCommands.SetTimeScale(1f);
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
    }
}
