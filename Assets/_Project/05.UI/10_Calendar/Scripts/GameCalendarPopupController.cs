/*
 * Technical Ownership
 * - Responsible Area: UI & Calendar
 *
 * Script Purpose
 * - Inspector에서 연결한 Calendar Popup Root의 표시 상태만 제어한다.
 */
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Calendar
{
    public enum GameCalendarValueKind
    {
        YearMonth,
        Season,
        Disaster
    }

    public sealed class GameCalendarPopupController : MonoBehaviour
    {
        [SerializeField, Tooltip("열고 닫을 Calendar Popup 전체 루트입니다.")]
        private GameObject popupRoot;

        [SerializeField, Tooltip("달력의 연월, 계절, 재해 값을 순서대로 확인할 때 누르는 패널입니다.")]
        private Button valueInspectionButton;

        private int nextInspectionIndex;
        private SlidePanel slidePanel;
        private bool wasOpen;

        public event Action Opened;
        public event Action Closed;
        public event Action<GameCalendarValueKind> ValueInspected;

        /// <summary>연결된 Popup Root가 현재 활성화되어 있는지 반환한다.</summary>
        public bool IsOpen
        {
            get
            {
                SlidePanel panel = ResolveSlidePanel();
                return panel != null
                    ? panel.IsOpen
                    : popupRoot != null && popupRoot.activeSelf;
            }
        }

        private void OnEnable()
        {
            if (valueInspectionButton != null)
                valueInspectionButton.onClick.AddListener(InspectNextValue);
        }

        private void Start()
        {
            wasOpen = IsOpen;
        }

        private void Update()
        {
            bool isOpen = IsOpen;
            if (isOpen == wasOpen)
                return;

            wasOpen = isOpen;
            nextInspectionIndex = 0;
            if (isOpen)
                Opened?.Invoke();
            else
                Closed?.Invoke();
        }

        private void OnDisable()
        {
            if (valueInspectionButton != null)
                valueInspectionButton.onClick.RemoveListener(InspectNextValue);
        }

        /// <summary>Popup Root가 연결되어 있고 닫혀 있을 때 Calendar Popup을 연다.</summary>
        public void OpenCalendar()
        {
            SlidePanel panel = ResolveSlidePanel();
            if (panel != null)
            {
                panel.SetOpen(true);
                return;
            }

            if (!TryGetPopupRoot(out GameObject root) || root.activeSelf)
            {
                return;
            }

            nextInspectionIndex = 0;
            root.SetActive(true);
        }

        /// <summary>Popup Root가 연결되어 있고 열려 있을 때 Calendar Popup을 닫는다.</summary>
        public void CloseCalendar()
        {
            SlidePanel panel = ResolveSlidePanel();
            if (panel != null)
            {
                panel.SetOpen(false);
                return;
            }

            if (!TryGetPopupRoot(out GameObject root) || !root.activeSelf)
            {
                return;
            }

            root.SetActive(false);
        }

        /// <summary>Popup Root가 연결되어 있으면 현재 활성 상태를 반전한다.</summary>
        public void ToggleCalendar()
        {
            SlidePanel panel = ResolveSlidePanel();
            if (panel != null)
            {
                panel.Toggle();
                return;
            }

            if (!TryGetPopupRoot(out GameObject root))
            {
                return;
            }

            if (root.activeSelf)
            {
                CloseCalendar();
                return;
            }

            OpenCalendar();
        }

        private void InspectNextValue()
        {
            if (!IsOpen || nextInspectionIndex > (int)GameCalendarValueKind.Disaster)
                return;

            GameCalendarValueKind value = (GameCalendarValueKind)nextInspectionIndex;
            nextInspectionIndex++;
            ValueInspected?.Invoke(value);
        }

        private SlidePanel ResolveSlidePanel()
        {
            if (slidePanel != null)
                return slidePanel;

            if (popupRoot != null)
                slidePanel = popupRoot.GetComponentInChildren<SlidePanel>(true);

            if (slidePanel == null)
                slidePanel = GetComponentInChildren<SlidePanel>(true);

            return slidePanel;
        }

        private bool TryGetPopupRoot(out GameObject root)
        {
            root = popupRoot;
            if (root != null)
            {
                return true;
            }

            Debug.LogWarning("[Calendar UI] Popup Root is not assigned.", this);
            return false;
        }
    }
}
