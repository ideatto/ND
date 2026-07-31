using ND.Framework;
using UnityEngine;

namespace ND.UI.Calendar
{
    /// <summary>Connects Framework calendar queries and events to the display-only calendar view.</summary>
    public sealed class GameCalendarPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameCalendarPanelView view;
        private bool subscribed;

        private void OnEnable()
        {
            Subscribe();
            RefreshFromCurrentState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            FrameworkEvents.CalendarRestored += HandleCalendarRestored;
            FrameworkEvents.YearChanged += HandleCalendarChanged;
            FrameworkEvents.MonthChanged += HandleCalendarChanged;
            FrameworkEvents.SeasonChanged += HandleCalendarChanged;
            FrameworkEvents.DisasterChanged += HandleCalendarChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            FrameworkEvents.CalendarRestored -= HandleCalendarRestored;
            FrameworkEvents.YearChanged -= HandleCalendarChanged;
            FrameworkEvents.MonthChanged -= HandleCalendarChanged;
            FrameworkEvents.SeasonChanged -= HandleCalendarChanged;
            FrameworkEvents.DisasterChanged -= HandleCalendarChanged;
            subscribed = false;
        }

        private void RefreshFromCurrentState()
        {
            GameCalendarService calendar = FrameworkRoot.Instance?.GameCalendar;
            if (calendar != null && calendar.TryGetCurrent(out GameCalendarSnapshot snapshot))
            {
                view?.Refresh(snapshot);
                return;
            }

            view?.HideUntilInitialized();
        }

        private void HandleCalendarRestored(CalendarRestoreResult result)
        {
            if (result != null)
            {
                view?.Refresh(result.Current);
            }
        }

        private void HandleCalendarChanged(GameCalendarSnapshot previous, GameCalendarSnapshot current)
        {
            view?.Refresh(current);
        }
    }
}
