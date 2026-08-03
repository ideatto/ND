using System;
using ND.Framework;
using TMPro;
using UnityEngine;

namespace ND.UI.Calendar
{
    /// <summary>Applies an authoritative calendar snapshot to serialized UI references.</summary>
    public sealed class GameCalendarPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private TMP_Text yearMonthText;
        [SerializeField] private TMP_Text seasonText;
        [SerializeField] private GameObject disasterRow;
        [SerializeField] private TMP_Text disasterText;
        [SerializeField] private GameCalendarMonthSlotView[] monthSlots =
            Array.Empty<GameCalendarMonthSlotView>();

        public string YearMonthDisplay => yearMonthText != null ? yearMonthText.text : string.Empty;
        public string SeasonDisplay => seasonText != null ? seasonText.text : string.Empty;
        public string DisasterDisplay => disasterText != null ? disasterText.text : string.Empty;
        public bool IsDisasterVisible => disasterRow != null && disasterRow.activeSelf;
        public bool IsContentVisible => content != null && content.activeSelf;
        public GameCalendarMonthSlotView[] MonthSlots => monthSlots;

        public void HideUntilInitialized()
        {
            if (content != null)
            {
                content.SetActive(false);
            }
        }

        /// <summary>Refreshes all display fields without retaining or mutating the supplied snapshot.</summary>
        public void Refresh(GameCalendarSnapshot snapshot)
        {
            if (content != null)
            {
                content.SetActive(true);
            }

            if (yearMonthText != null)
            {
                yearMonthText.text = $"{snapshot.Year}년 {snapshot.Month}월";
            }

            if (seasonText != null)
            {
                seasonText.text = GetSeasonLabel(snapshot.Season);
            }

            string disasterLabel = GetDisasterLabel(snapshot.ActiveDisasterId);
            bool hasDisaster = !string.IsNullOrEmpty(snapshot.ActiveDisasterId);
            if (disasterRow != null)
            {
                disasterRow.SetActive(hasDisaster);
            }

            if (disasterText != null)
            {
                disasterText.text = disasterLabel;
            }

            for (int i = 0; i < monthSlots.Length; i++)
            {
                GameCalendarMonthSlotView slot = monthSlots[i];
                if (slot != null)
                {
                    slot.SetCurrent(snapshot.Month >= 1 && snapshot.Month <= 12
                        && slot.Month == snapshot.Month);
                }
            }
        }

        public static string GetSeasonLabel(GameSeason season)
        {
            switch (season)
            {
                case GameSeason.Spring: return "봄";
                case GameSeason.Summer: return "여름";
                case GameSeason.Autumn: return "가을";
                case GameSeason.Winter: return "겨울";
                default: return season.ToString();
            }
        }

        public static string GetDisasterLabel(string disasterId)
        {
            switch (disasterId)
            {
                case null:
                case "": return string.Empty;
                case "flood": return "홍수";
                case "drought": return "가뭄";
                default: return disasterId;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (monthSlots == null || monthSlots.Length == 0)
            {
                return;
            }

            if (monthSlots.Length != 12)
            {
                Debug.LogWarning("[Calendar UI] Exactly twelve month slots must be assigned.", this);
                return;
            }

            var seen = new bool[13];
            for (int i = 0; i < monthSlots.Length; i++)
            {
                GameCalendarMonthSlotView slot = monthSlots[i];
                if (slot == null || slot.Month < 1 || slot.Month > 12 || seen[slot.Month])
                {
                    Debug.LogWarning("[Calendar UI] Month slots must contain unique months 1 through 12.", this);
                    return;
                }

                seen[slot.Month] = true;
            }
        }
#endif
    }
}
