/*
 * Technical Ownership
 * - Responsible Area: UI & Calendar
 *
 * Script Purpose
 * - Inspector에서 연결한 Calendar Popup Root의 표시 상태만 제어한다.
 */
using UnityEngine;

namespace ND.UI.Calendar
{
    public sealed class GameCalendarPopupController : MonoBehaviour
    {
        [SerializeField, Tooltip("열고 닫을 Calendar Popup 전체 루트입니다.")]
        private GameObject popupRoot;

        /// <summary>연결된 Popup Root가 현재 활성화되어 있는지 반환한다.</summary>
        public bool IsOpen => popupRoot != null && popupRoot.activeSelf;

        /// <summary>Popup Root가 연결되어 있고 닫혀 있을 때 Calendar Popup을 연다.</summary>
        public void OpenCalendar()
        {
            if (!TryGetPopupRoot(out GameObject root) || root.activeSelf)
            {
                return;
            }

            root.SetActive(true);
        }

        /// <summary>Popup Root가 연결되어 있고 열려 있을 때 Calendar Popup을 닫는다.</summary>
        public void CloseCalendar()
        {
            if (!TryGetPopupRoot(out GameObject root) || !root.activeSelf)
            {
                return;
            }

            root.SetActive(false);
        }

        /// <summary>Popup Root가 연결되어 있으면 현재 활성 상태를 반전한다.</summary>
        public void ToggleCalendar()
        {
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
