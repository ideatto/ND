/*
 * Technical Ownership
 * - Responsible Area: UI & Title
 *
 * Script Purpose
 * - 물리적 저장 파일 존재 여부에 따라 Title 게임 플로우 버튼을 전환한다.
 * - 메뉴 입력과 저장 초기화 확인 흐름의 중복 실행을 차단한다.
 */
using ND.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Title
{
    public sealed class TitleMenuButtonStateController : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button resetSaveButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private TitleSceneController titleSceneController;
        [SerializeField] private TitleResetConfirmPopup resetConfirmPopup;

        private bool actionInProgress;

        private void OnEnable()
        {
            actionInProgress = false;
            RefreshButtonStates();
        }

        /// <summary>저장 파일 존재 여부를 다시 조회해 네 버튼의 표시 상태를 함께 갱신한다.</summary>
        public void RefreshButtonStates()
        {
            if (titleSceneController == null
                || newGameButton == null
                || continueButton == null
                || resetSaveButton == null
                || exitButton == null)
            {
                Debug.LogError("Title menu button state references are missing.", this);
                return;
            }

            var hasSaveData = titleSceneController.HasSaveData;
            newGameButton.gameObject.SetActive(!hasSaveData);
            continueButton.gameObject.SetActive(hasSaveData);
            resetSaveButton.gameObject.SetActive(hasSaveData);
            exitButton.gameObject.SetActive(true);
        }

        public void OnClickNewGame()
        {
            if (!CanHandleMenuInput())
            {
                return;
            }

            if (titleSceneController.HasSaveData)
            {
                RefreshButtonStates();
                return;
            }

            actionInProgress = true;
            titleSceneController.StartNewGame();
        }

        public void OnClickContinue()
        {
            if (!CanHandleMenuInput())
            {
                return;
            }

            if (!titleSceneController.HasSaveData)
            {
                RefreshButtonStates();
                return;
            }

            actionInProgress = true;
            titleSceneController.ContinueGame();
        }

        public void OnClickResetSave()
        {
            if (!CanHandleMenuInput())
            {
                return;
            }

            if (!titleSceneController.HasSaveData)
            {
                RefreshButtonStates();
                return;
            }

            if (resetConfirmPopup == null)
            {
                Debug.LogError("Title reset confirmation popup reference is missing.", this);
                return;
            }

            resetConfirmPopup.Show(ConfirmResetSave);
        }

        public void OnClickExit()
        {
            if (!CanHandleMenuInput())
            {
                return;
            }

            actionInProgress = true;
            titleSceneController.ExitGame();
        }

        private bool CanHandleMenuInput()
        {
            return !actionInProgress
                && titleSceneController != null
                && (resetConfirmPopup == null || !resetConfirmPopup.IsOpen);
        }

        private void ConfirmResetSave()
        {
            if (actionInProgress || titleSceneController == null)
            {
                return;
            }

            actionInProgress = true;
            try
            {
                titleSceneController.ResetSaveData();
                RefreshButtonStates();
            }
            finally
            {
                actionInProgress = false;
            }
        }
    }
}
