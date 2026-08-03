namespace ND.Framework
{
    /// <summary>소비자가 보유해도 저장 데이터가 변경되지 않는 달력 읽기 스냅샷이다.</summary>
    public readonly struct GameCalendarSnapshot
    {
        public GameCalendarSnapshot(GameCalendarDate date, string activeDisasterId)
        {
            TotalElapsedDays = date.TotalElapsedDays;
            Year = date.Year;
            Month = date.Month;
            Day = date.Day;
            AbsoluteMonthIndex = date.AbsoluteMonthIndex;
            Season = date.Season;
            SeasonId = date.SeasonId;
            ActiveDisasterId = activeDisasterId ?? string.Empty;
        }

        public long TotalElapsedDays { get; }
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }
        public long AbsoluteMonthIndex { get; }
        public GameSeason Season { get; }
        public string SeasonId { get; }
        public string ActiveDisasterId { get; }
    }
}
