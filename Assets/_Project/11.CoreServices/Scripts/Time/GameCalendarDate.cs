using System;

namespace ND.Framework
{
    /// <summary>30일 고정 월과 3월 시작 epoch에서 날짜와 계절을 순수 계산한다.</summary>
    public readonly struct GameCalendarDate
    {
        public const string SpringId = "spring";
        public const string SummerId = "summer";
        public const string AutumnId = "autumn";
        public const string WinterId = "winter";

        public GameCalendarDate(
            long totalElapsedDays,
            int year,
            int month,
            int day,
            long absoluteMonthIndex,
            GameSeason season)
        {
            TotalElapsedDays = totalElapsedDays;
            Year = year;
            Month = month;
            Day = day;
            AbsoluteMonthIndex = absoluteMonthIndex;
            Season = season;
        }

        public long TotalElapsedDays { get; }
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }
        /// <summary>epoch인 Year 1 March를 0으로 하는 단조 증가 월 인덱스다.</summary>
        public long AbsoluteMonthIndex { get; }
        public GameSeason Season { get; }
        public string SeasonId => ToSeasonId(Season);

        public static GameCalendarDate FromElapsedDays(long totalElapsedDays)
        {
            var normalizedDays = Math.Max(0L, totalElapsedDays);
            var monthOffset = normalizedDays / 30L;
            var calendarMonthOffset = checked(monthOffset + 2L);
            var yearValue = checked(1L + calendarMonthOffset / 12L);
            if (yearValue > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(totalElapsedDays), "Derived calendar year exceeds Int32 range.");
            }

            var month = (int)(calendarMonthOffset % 12L) + 1;
            var day = (int)(normalizedDays % 30L) + 1;
            return new GameCalendarDate(
                normalizedDays,
                (int)yearValue,
                month,
                day,
                monthOffset,
                FromMonth(month));
        }

        public static GameSeason FromMonth(int month)
        {
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month));
            }

            if (month <= 2 || month == 12) return GameSeason.Winter;
            if (month <= 5) return GameSeason.Spring;
            if (month <= 8) return GameSeason.Summer;
            return GameSeason.Autumn;
        }

        public static string ToSeasonId(GameSeason season)
        {
            switch (season)
            {
                case GameSeason.Spring: return SpringId;
                case GameSeason.Summer: return SummerId;
                case GameSeason.Autumn: return AutumnId;
                case GameSeason.Winter: return WinterId;
                default: throw new ArgumentOutOfRangeException(nameof(season));
            }
        }
    }
}
