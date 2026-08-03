/*
 * Technical Ownership
 * - Responsible Area: UI & Title
 *
 * Script Purpose
 * - 저장 데이터 초기화 전에 확인을 받고 중복 입력을 차단한다.
 */
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Title
{
    public sealed class TitleResetConfirmPopup : MonoBehaviour
    {
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        private Action onConfirmed;
        private bool isProcessing;

        public bool IsOpen => popupRoot != null && popupRoot.activeSelf;

        private void Awake()
        {
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Cancel);
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(Confirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Cancel);
            }
        }

        /// <summary>현재 확인 콜백을 교체하고 초기화 확인창을 연다.</summary>
        public void Show(Action confirmationCallback)
        {
            if (popupRoot == null || confirmButton == null || cancelButton == null)
            {
                Debug.LogError("Title reset confirmation popup references are missing.", this);
                return;
            }

            onConfirmed = confirmationCallback;
            isProcessing = false;
            confirmButton.interactable = true;
            cancelButton.interactable = true;
            popupRoot.SetActive(true);
        }

        /// <summary>확인 콜백을 제거하고 팝업 입력 차단을 해제한다.</summary>
        public void Hide()
        {
            onConfirmed = null;
            isProcessing = false;

            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }
        }

        private void Confirm()
        {
            if (isProcessing)
            {
                return;
            }

            isProcessing = true;
            confirmButton.interactable = false;
            cancelButton.interactable = false;

            var confirmationCallback = onConfirmed;
            onConfirmed = null;
            try
            {
                confirmationCallback?.Invoke();
            }
            finally
            {
                Hide();
            }
        }

        private void Cancel()
        {
            if (!isProcessing)
            {
                Hide();
            }
        }
    }
}
