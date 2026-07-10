/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Loading scene에서 framework loading 완료를 FrameworkRoot에 알린다.
 * - 저장 데이터 기반 초기화 후 in-game scene으로 진입하는 버튼/자동 호출 지점을 제공한다.
 *
 * Main Features
 * - Start 시 설정값에 따라 loading 완료 처리를 자동 호출한다.
 * - 수동 호출 가능한 CompleteLoading API를 제공한다.
 *
 * Usage for Team Members
 * - Loading scene GameObject에 연결하고 completeLoadingOnStart로 자동 진입 여부를 조정한다.
 * - 로딩 UI 연출이 끝난 시점에 CompleteLoading()을 호출할 수 있다.
 *
 * Main Public APIs
 * - CompleteLoading(): 저장 데이터 기반 초기화를 완료하고 in-game scene으로 이동한다.
 *
 * Important Notes
 * - 실제 저장 데이터 준비와 scene 이동 순서는 FrameworkRoot.CompleteLoadingAndEnterGame()이 담당한다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Loading scene 완료 시점을 FrameworkRoot game flow로 전달하는 controller이다.
    /// </summary>
    public sealed class LoadingSceneController : MonoBehaviour
    {
        [SerializeField] private bool completeLoadingOnStart = true;

        private void Start()
        {
            // 별도 로딩 연출이 없는 경우 Start에서 즉시 in-game 진입 flow를 실행한다.
            if (completeLoadingOnStart)
            {
                CompleteLoading();
            }
        }

        /// <summary>
        /// framework loading 완료 처리를 실행하고 in-game scene 진입을 요청한다.
        /// </summary>
        public void CompleteLoading()
        {
            // 저장 데이터 확인, 화면 router 갱신, scene 전환 순서는 FrameworkRoot가 보장한다.
            FrameworkRoot.Instance.CompleteLoadingAndEnterGame();
        }
    }
}
