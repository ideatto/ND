/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Title scene UI에서 새 게임, 이어하기, 저장 초기화, 종료 요청을 FrameworkRoot로 전달한다.
 * - Title scene 버튼과 CoreServices game flow 사이의 얇은 controller 역할을 한다.
 *
 * Main Features
 * - 저장 데이터 존재 여부를 UI에 제공한다.
 * - 새 게임/이어하기/저장 초기화/게임 종료 버튼용 public method를 제공한다.
 *
 * Usage for Team Members
 * - Title scene UI Button 이벤트에 public method를 연결한다.
 * - 이어하기 버튼 활성화 여부는 HasSaveData를 참고한다.
 *
 * Main Public APIs
 * - HasSaveData: 저장 데이터 존재 여부.
 * - StartNewGame(): 새 게임 flow를 시작한다.
 * - ContinueGame(): 이어하기 flow를 시작한다.
 * - ResetSaveData(): 저장 파일을 삭제한다.
 * - ExitGame(): Application.Quit을 요청한다.
 *
 * Important Notes
 * - FrameworkRoot.Instance와 SaveService가 초기화된 상태를 전제로 한다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Title scene UI 입력을 FrameworkRoot game flow로 전달하는 controller이다.
    /// </summary>
    public sealed class TitleSceneController : MonoBehaviour
    {
        /// <summary>
        /// 현재 저장 데이터가 존재하는지 반환한다.
        /// </summary>
        public bool HasSaveData => FrameworkRoot.Instance.SaveService.HasSaveData();

        /// <summary>
        /// 새 게임 flow를 시작한다.
        /// </summary>
        public void StartNewGame()
        {
            // 새 게임 생성과 loading scene 이동은 FrameworkRoot가 일관된 순서로 처리한다.
            FrameworkRoot.Instance.StartNewGame();
        }

        /// <summary>
        /// 저장 데이터를 로드해 이어하기 flow를 시작한다.
        /// </summary>
        public void ContinueGame()
        {
            // 저장 로드와 loading scene 이동은 FrameworkRoot에 위임한다.
            FrameworkRoot.Instance.ContinueGame();
        }

        /// <summary>
        /// 현재 저장 데이터를 삭제한다.
        /// </summary>
        public void ResetSaveData()
        {
            // Title scene에서 테스트나 새 시작을 위해 저장 파일을 제거한다.
            FrameworkRoot.Instance.SaveService.ResetSaveData();
        }

        /// <summary>
        /// 애플리케이션 종료를 요청한다.
        /// </summary>
        public void ExitGame()
        {
            // 종료 요청은 플랫폼에 따라 즉시 반영되지 않을 수 있어 로그를 남긴다.
            FrameworkLog.Info("Exit requested.");
            Application.Quit();
        }
    }
}
