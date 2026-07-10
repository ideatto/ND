/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices의 Unity scene 전환을 담당한다.
 * - scene load 중복 요청을 차단하고 완료 시 framework event를 발행한다.
 *
 * Main Features
 * - scene 이름 기반 비동기 load를 수행한다.
 * - Title, Loading, InGame scene으로 이동하는 convenience API를 제공한다.
 * - load 완료 시 SceneChanged 이벤트를 발행한다.
 *
 * Usage for Team Members
 * - FrameworkRoot.SceneFlow를 통해 scene 전환을 요청한다.
 * - 직접 scene 이름을 넘길 때는 SceneNames 상수를 우선 사용한다.
 *
 * Main Public APIs
 * - IsLoading: 현재 비동기 scene load 진행 여부.
 * - LoadScene(...): 지정한 scene을 비동기로 로드한다.
 * - GoToTitle(), GoToLoading(), GoToInGame(): framework 주요 scene으로 이동한다.
 *
 * Important Notes
 * - IsLoading이 true인 동안 추가 load 요청은 무시된다.
 * - scene 이름이 비어 있거나 LoadSceneAsync 시작에 실패하면 warning/error 로그만 남기고 반환한다.
 */
using System;
using UnityEngine.SceneManagement;

namespace ND.Framework
{
    /// <summary>
    /// Unity scene load 요청과 완료 이벤트 발행을 담당하는 서비스이다.
    /// </summary>
    public sealed class SceneFlowService
    {
        /// <summary>
        /// 현재 비동기 scene load가 진행 중인지 나타낸다.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// 지정한 Unity scene을 비동기로 로드한다.
        /// </summary>
        /// <param name="sceneName">로드할 scene 이름. null, 빈 문자열, 공백이면 요청을 무시한다.</param>
        /// <remarks>
        /// load 완료 시 FrameworkEvents.RaiseSceneChanged를 호출한다.
        /// </remarks>
        public void LoadScene(string sceneName)
        {
            // 빈 scene 이름은 Unity load 오류로 이어지므로 요청 단계에서 차단한다.
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                FrameworkLog.Warning("Scene load was skipped because scene name is empty.");
                return;
            }

            // 동시에 여러 scene load가 진행되면 완료 순서가 꼬일 수 있으므로 중복 요청을 막는다.
            if (IsLoading)
            {
                FrameworkLog.Warning($"Scene load ignored while another load is running: {sceneName}");
                return;
            }

            try
            {
                // 비동기 작업 시작 직전에 loading flag를 세워 같은 frame의 추가 요청도 차단한다.
                IsLoading = true;
                FrameworkLog.Info($"Loading scene: {sceneName}");

                var operation = SceneManager.LoadSceneAsync(sceneName);
                // Unity가 operation을 만들지 못한 경우 flag를 되돌려 이후 재시도를 허용한다.
                if (operation == null)
                {
                    IsLoading = false;
                    FrameworkLog.Error($"Scene load failed to start: {sceneName}");
                    return;
                }

                operation.completed += _ =>
                {
                    // 완료 콜백에서 flag를 해제한 뒤 scene 변경 이벤트를 발행한다.
                    IsLoading = false;
                    FrameworkEvents.RaiseSceneChanged(sceneName);
                };
            }
            catch (Exception exception)
            {
                // load 요청 중 예외가 발생하면 loading 상태를 복구해 framework가 멈추지 않게 한다.
                IsLoading = false;
                FrameworkLog.Error($"Scene load failed: {sceneName}, {exception.Message}");
            }
        }

        /// <summary>
        /// title scene으로 이동한다.
        /// </summary>
        public void GoToTitle()
        {
            LoadScene(SceneNames.Title);
        }

        /// <summary>
        /// loading scene으로 이동한다.
        /// </summary>
        public void GoToLoading()
        {
            LoadScene(SceneNames.Loading);
        }

        /// <summary>
        /// in-game scene으로 이동한다.
        /// </summary>
        public void GoToInGame()
        {
            LoadScene(SceneNames.InGame);
        }
    }
}
