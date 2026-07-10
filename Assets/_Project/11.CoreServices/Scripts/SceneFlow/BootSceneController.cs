/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Boot scene 진입 후 title scene으로 이동하는 초기 scene controller이다.
 * - FrameworkRoot 자동 생성 이후 첫 scene flow를 시작한다.
 *
 * Main Features
 * - Start 시 설정값에 따라 title scene load를 요청한다.
 *
 * Usage for Team Members
 * - Boot scene의 GameObject에 연결하고 loadTitleOnStart로 자동 이동 여부를 제어한다.
 *
 * Main Public APIs
 * - 없음. Unity lifecycle Start에서 동작한다.
 *
 * Important Notes
 * - FrameworkRoot.Instance가 준비되어 있어야 scene 전환을 요청할 수 있다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Boot scene에서 title scene으로 자동 이동을 수행하는 controller이다.
    /// </summary>
    public sealed class BootSceneController : MonoBehaviour
    {
        [SerializeField] private bool loadTitleOnStart = true;

        private void Start()
        {
            // 디버그나 수동 테스트에서는 자동 title 이동을 끌 수 있도록 serialized flag를 확인한다.
            if (!loadTitleOnStart)
            {
                return;
            }

            // FrameworkRoot가 보유한 scene flow를 통해 title scene으로 이동한다.
            FrameworkRoot.Instance.SceneFlow.GoToTitle();
        }
    }
}
