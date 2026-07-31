using System;
using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>Reports the calendar mutation prepared from one accepted offline interval.</summary>
    public sealed class CalendarRestoreResult
    {
        private static readonly IReadOnlyList<MonthlyWorldState> EmptyMonths =
            Array.Empty<MonthlyWorldState>();

        internal CalendarRestoreResult(
            GameCalendarSnapshot previous,
            GameCalendarSnapshot current,
            IReadOnlyList<MonthlyWorldState> passedMonths,
            long daysAdvanced,
            bool changed)
        {
            Previous = previous;
            Current = current;
            PassedMonths = passedMonths ?? EmptyMonths;
            DaysAdvanced = daysAdvanced;
            Changed = changed;
        }

        public GameCalendarSnapshot Previous { get; }
        public GameCalendarSnapshot Current { get; }
        public IReadOnlyList<MonthlyWorldState> PassedMonths { get; }
        public long DaysAdvanced { get; }
        public bool Changed { get; }
    }
}
