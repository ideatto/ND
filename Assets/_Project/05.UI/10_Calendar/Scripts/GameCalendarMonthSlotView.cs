using TMPro;
using UnityEngine;

namespace ND.UI.Calendar
{
    /// <summary>Displays one fixed month number and its current-month visual state.</summary>
    public sealed class GameCalendarMonthSlotView : MonoBehaviour
    {
        [SerializeField, Tooltip("이 슬롯이 표시하는 월(1~12)입니다.")]
        private int month;
        [SerializeField] private TMP_Text monthText;
        [SerializeField] private GameObject currentHighlight;

        public int Month => month;
        public bool IsCurrent => currentHighlight != null && currentHighlight.activeSelf;

        public void SetMonth(int value)
        {
            month = value;
            if (monthText != null)
            {
                monthText.text = value.ToString();
            }
        }

        public void SetCurrent(bool isCurrent)
        {
            if (currentHighlight != null)
            {
                currentHighlight.SetActive(isCurrent);
            }
        }
    }
}
