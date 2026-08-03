namespace ND.Framework
{
    /// <summary>Describes the deterministic world state of one month entered during restoration.</summary>
    public readonly struct MonthlyWorldState
    {
        public MonthlyWorldState(
            long absoluteMonthIndex,
            int year,
            int month,
            GameSeason season,
            string disasterId)
        {
            AbsoluteMonthIndex = absoluteMonthIndex;
            Year = year;
            Month = month;
            Season = season;
            DisasterId = disasterId ?? string.Empty;
        }

        public long AbsoluteMonthIndex { get; }
        public int Year { get; }
        public int Month { get; }
        public GameSeason Season { get; }
        public string DisasterId { get; }
    }
}
