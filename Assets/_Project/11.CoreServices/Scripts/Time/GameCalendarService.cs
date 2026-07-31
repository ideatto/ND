using System;
using System.Collections.Generic;

namespace ND.Framework
{
    public readonly struct CalendarAdvanceResult
    {
        internal CalendarAdvanceResult(
            bool changed,
            long daysAdvanced,
            GameCalendarSnapshot previous,
            GameCalendarSnapshot current,
            bool monthChanged,
            bool yearChanged,
            bool seasonChanged,
            SaveResult saveResult)
        {
            Changed = changed;
            DaysAdvanced = daysAdvanced;
            Previous = previous;
            Current = current;
            MonthChanged = monthChanged;
            YearChanged = yearChanged;
            SeasonChanged = seasonChanged;
            SaveResult = saveResult;
        }

        public bool Changed { get; }
        public long DaysAdvanced { get; }
        public GameCalendarSnapshot Previous { get; }
        public GameCalendarSnapshot Current { get; }
        public bool MonthChanged { get; }
        public bool YearChanged { get; }
        public bool SeasonChanged { get; }
        public SaveResult SaveResult { get; }
    }

    /// <summary>
    /// Advances the authoritative game calendar from sampled UTC while keeping debug scaling local to the session.
    /// </summary>
    public sealed class GameCalendarService
    {
        public const long TicksPerGameDay = 120L * TimeSpan.TicksPerSecond;

        private readonly IGameTimeProvider timeProvider;
        private readonly MonthlyDisasterResolver disasterResolver;
        private readonly MonthlyDisasterPolicy disasterPolicy;
        private SaveData sessionSaveData;
        private long lastSampleUtcTicks;
        private long pendingGameTicks;
        private float debugScale = 1f;
        private bool hasCurrent;

        public GameCalendarService(IGameTimeProvider timeProvider)
            : this(timeProvider, new MonthlyDisasterResolver(), new MonthlyDisasterPolicy())
        {
        }

        public GameCalendarService(
            IGameTimeProvider timeProvider,
            MonthlyDisasterResolver disasterResolver,
            MonthlyDisasterPolicy disasterPolicy)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.disasterResolver = disasterResolver ?? throw new ArgumentNullException(nameof(disasterResolver));
            this.disasterPolicy = disasterPolicy ?? throw new ArgumentNullException(nameof(disasterPolicy));
        }

        public GameCalendarSnapshot Current { get; private set; }
        public float DebugScale => debugScale;

        /// <summary>
        /// Starts or replaces the active online session and repairs season/disaster caches in memory.
        /// Existing wall time since the day anchor becomes the initial pending progress; repair is not saved here.
        /// </summary>
        public bool BeginOnlineSession(SaveData saveData, DateTime currentUtc)
        {
            if (!TryGetCalendar(saveData, currentUtc, out var calendar, out var snapshot))
            {
                ResetSession();
                return false;
            }

            sessionSaveData = saveData;
            lastSampleUtcTicks = currentUtc.Ticks;
            pendingGameTicks = currentUtc.Ticks >= calendar.dayAnchorUtcTicks
                ? currentUtc.Ticks - calendar.dayAnchorUtcTicks
                : 0L;
            saveData.world.currentSeasonId = snapshot.SeasonId;
            saveData.world.currentDisasterId = disasterResolver.Resolve(
                saveData.world.worldSeed,
                snapshot.AbsoluteMonthIndex,
                snapshot.Season,
                disasterPolicy);
            snapshot = new GameCalendarSnapshot(
                GameCalendarDate.FromElapsedDays(calendar.totalElapsedDays),
                saveData.world.currentDisasterId);
            Current = snapshot;
            hasCurrent = true;
            return true;
        }

        /// <summary>
        /// Applies whole game days from the shared accepted UTC endpoint without saving or publishing events.
        /// The online sample is rebased to load UTC so the restored interval cannot be counted again.
        /// </summary>
        public CalendarRestoreResult RestoreOffline(
            SaveData saveData,
            OfflineRestoreContext context)
        {
            var world = saveData?.world;
            var calendar = world?.calendar;
            if (calendar == null || calendar.totalElapsedDays < 0L || calendar.dayAnchorUtcTicks <= 0L)
            {
                FrameworkLog.Warning("Game calendar offline restore was skipped because save state is invalid.");
                ResetSession();
                return null;
            }

            GameCalendarSnapshot previous;
            try
            {
                previous = new GameCalendarSnapshot(
                    GameCalendarDate.FromElapsedDays(calendar.totalElapsedDays),
                    world.currentDisasterId);
            }
            catch (Exception exception) when (
                exception is OverflowException || exception is ArgumentOutOfRangeException)
            {
                FrameworkLog.Warning($"Game calendar offline restore was skipped: {exception.Message}");
                ResetSession();
                return null;
            }

            var daysAdvanced = 0L;
            var nextTotalDays = calendar.totalElapsedDays;
            var nextAnchorTicks = calendar.dayAnchorUtcTicks;
            if (!context.ClockRollbackDetected
                && context.EvaluationUtc.Ticks >= calendar.dayAnchorUtcTicks)
            {
                var elapsedTicks = context.EvaluationUtc.Ticks - calendar.dayAnchorUtcTicks;
                daysAdvanced = elapsedTicks / TicksPerGameDay;
                try
                {
                    nextTotalDays = checked(calendar.totalElapsedDays + daysAdvanced);
                    nextAnchorTicks = checked(
                        calendar.dayAnchorUtcTicks + checked(daysAdvanced * TicksPerGameDay));
                }
                catch (OverflowException exception)
                {
                    FrameworkLog.Warning($"Game calendar offline restore was skipped: {exception.Message}");
                    daysAdvanced = 0L;
                    nextTotalDays = calendar.totalElapsedDays;
                    nextAnchorTicks = calendar.dayAnchorUtcTicks;
                }
            }

            GameCalendarDate finalDate;
            try
            {
                finalDate = GameCalendarDate.FromElapsedDays(nextTotalDays);
            }
            catch (Exception exception) when (
                exception is OverflowException || exception is ArgumentOutOfRangeException)
            {
                FrameworkLog.Warning($"Game calendar offline restore was skipped: {exception.Message}");
                finalDate = GameCalendarDate.FromElapsedDays(calendar.totalElapsedDays);
                daysAdvanced = 0L;
                nextTotalDays = calendar.totalElapsedDays;
                nextAnchorTicks = calendar.dayAnchorUtcTicks;
            }

            var finalDisasterId = disasterResolver.Resolve(
                world.worldSeed,
                finalDate.AbsoluteMonthIndex,
                finalDate.Season,
                disasterPolicy);
            var current = new GameCalendarSnapshot(finalDate, finalDisasterId);
            var passedMonths = BuildPassedMonths(
                world.worldSeed,
                previous.AbsoluteMonthIndex,
                finalDate.AbsoluteMonthIndex,
                daysAdvanced);
            var changed = calendar.totalElapsedDays != nextTotalDays
                || calendar.dayAnchorUtcTicks != nextAnchorTicks
                || !string.Equals(world.currentSeasonId, current.SeasonId, StringComparison.Ordinal)
                || !string.Equals(
                    world.currentDisasterId,
                    current.ActiveDisasterId,
                    StringComparison.Ordinal);

            calendar.totalElapsedDays = nextTotalDays;
            calendar.dayAnchorUtcTicks = nextAnchorTicks;
            world.currentSeasonId = current.SeasonId;
            world.currentDisasterId = current.ActiveDisasterId;
            sessionSaveData = saveData;
            lastSampleUtcTicks = context.LoadUtc.Ticks;
            pendingGameTicks = context.EvaluationUtc.Ticks >= nextAnchorTicks
                ? context.EvaluationUtc.Ticks - nextAnchorTicks
                : 0L;
            debugScale = 1f;
            Current = current;
            hasCurrent = true;

            return new CalendarRestoreResult(
                previous,
                current,
                passedMonths,
                daysAdvanced,
                changed);
        }

        private IReadOnlyList<MonthlyWorldState> BuildPassedMonths(
            uint worldSeed,
            long startingMonthIndex,
            long finalMonthIndex,
            long daysAdvanced)
        {
            if (finalMonthIndex <= startingMonthIndex || daysAdvanced <= 0L)
            {
                return Array.Empty<MonthlyWorldState>();
            }

            var maximumEntries = checked(daysAdvanced / 30L + 2L);
            var requestedEntries = finalMonthIndex - startingMonthIndex;
            if (requestedEntries > maximumEntries || requestedEntries > int.MaxValue)
            {
                FrameworkLog.Warning("Game calendar offline month timeline exceeded its safe bound.");
                return Array.Empty<MonthlyWorldState>();
            }

            var months = new List<MonthlyWorldState>((int)requestedEntries);
            for (var monthIndex = startingMonthIndex + 1L;
                 monthIndex <= finalMonthIndex;
                 monthIndex++)
            {
                var date = GameCalendarDate.FromElapsedDays(checked(monthIndex * 30L));
                months.Add(new MonthlyWorldState(
                    monthIndex,
                    date.Year,
                    date.Month,
                    date.Season,
                    disasterResolver.Resolve(
                        worldSeed,
                        monthIndex,
                        date.Season,
                        disasterPolicy)));
            }

            return months.AsReadOnly();
        }

        internal void RebuildRuntimeAfterFailedRestore(SaveData saveData, DateTime loadUtc)
        {
            var calendar = saveData?.world?.calendar;
            if (calendar == null || calendar.totalElapsedDays < 0L || calendar.dayAnchorUtcTicks <= 0L)
            {
                ResetSession();
                return;
            }

            try
            {
                Current = new GameCalendarSnapshot(
                    GameCalendarDate.FromElapsedDays(calendar.totalElapsedDays),
                    saveData.world.currentDisasterId);
                sessionSaveData = saveData;
                lastSampleUtcTicks = loadUtc.Ticks;
                pendingGameTicks = 0L;
                debugScale = 1f;
                hasCurrent = true;
            }
            catch (Exception exception) when (
                exception is OverflowException || exception is ArgumentOutOfRangeException)
            {
                FrameworkLog.Warning($"Game calendar runtime rollback failed: {exception.Message}");
                ResetSession();
            }
        }

        /// <summary>
        /// Samples the injected UTC provider and persists at most one calendar mutation for all elapsed whole days.
        /// A failed save restores authoritative fields and the query snapshot; pending progress remains retryable.
        /// </summary>
        public CalendarAdvanceResult TickOnline(SaveData saveData, ISaveService saveService)
        {
            return TickOnline(saveData, timeProvider.CurrentUtc, saveService);
        }

        internal CalendarAdvanceResult TickOnline(
            SaveData saveData,
            DateTime currentUtc,
            ISaveService saveService)
        {
            if (!hasCurrent || !ReferenceEquals(sessionSaveData, saveData))
            {
                if (!BeginOnlineSession(saveData, currentUtc))
                {
                    return default;
                }
            }

            var currentUtcTicks = currentUtc.Ticks;
            if (currentUtcTicks < lastSampleUtcTicks)
            {
                FrameworkLog.Warning(
                    $"Game calendar UTC rollback ignored. Current: {currentUtcTicks}, LastSample: {lastSampleUtcTicks}.");
                return Unchanged();
            }

            var rawDelta = currentUtcTicks - lastSampleUtcTicks;
            lastSampleUtcTicks = currentUtcTicks;
            if (!TryAddScaledDelta(rawDelta))
            {
                FrameworkLog.Warning("Game calendar tick was ignored because scaled UTC accumulation overflowed.");
                return Unchanged();
            }

            return AdvancePendingWholeDays(saveData, currentUtcTicks, saveService);
        }

        /// <summary>
        /// Accepts exactly 0x, 1x, 2x, or 4x for this service instance. The value is never persisted.
        /// </summary>
        public bool TrySetDebugScale(float scale)
        {
            if (scale != 0f && scale != 1f && scale != 2f && scale != 4f)
            {
                return false;
            }

            debugScale = scale;
            return true;
        }

        private CalendarAdvanceResult AdvancePendingWholeDays(
            SaveData saveData,
            long currentUtcTicks,
            ISaveService saveService)
        {
            var daysAdvanced = pendingGameTicks / TicksPerGameDay;
            if (daysAdvanced <= 0L)
            {
                return Unchanged();
            }

            var world = saveData?.world;
            var calendar = world?.calendar;
            if (calendar == null || calendar.totalElapsedDays < 0 || calendar.dayAnchorUtcTicks <= 0)
            {
                FrameworkLog.Warning("Game calendar tick was ignored because save calendar state is invalid.");
                return Unchanged();
            }

            long newTotalElapsedDays;
            long consumedTicks;
            long remainderTicks;
            long newAnchorUtcTicks;
            GameCalendarSnapshot nextSnapshot;
            try
            {
                newTotalElapsedDays = checked(calendar.totalElapsedDays + daysAdvanced);
                consumedTicks = checked(daysAdvanced * TicksPerGameDay);
                remainderTicks = pendingGameTicks - consumedTicks;
                newAnchorUtcTicks = checked(currentUtcTicks - remainderTicks);
                if (newAnchorUtcTicks <= 0L || newAnchorUtcTicks > DateTime.MaxValue.Ticks)
                {
                    throw new OverflowException("Derived calendar anchor is outside the DateTime tick range.");
                }

                var date = GameCalendarDate.FromElapsedDays(newTotalElapsedDays);
                var disasterId = Current.AbsoluteMonthIndex != date.AbsoluteMonthIndex
                    ? disasterResolver.Resolve(
                        world.worldSeed,
                        date.AbsoluteMonthIndex,
                        date.Season,
                        disasterPolicy)
                    : world.currentDisasterId;
                nextSnapshot = new GameCalendarSnapshot(date, disasterId);
            }
            catch (Exception exception) when (
                exception is OverflowException || exception is ArgumentOutOfRangeException)
            {
                FrameworkLog.Warning($"Game calendar tick was ignored because advancement is out of range: {exception.Message}");
                return Unchanged();
            }

            var previous = Current;
            var previousDays = calendar.totalElapsedDays;
            var previousAnchor = calendar.dayAnchorUtcTicks;
            var previousSeasonId = world.currentSeasonId;
            var previousDisasterId = world.currentDisasterId;

            calendar.totalElapsedDays = newTotalElapsedDays;
            calendar.dayAnchorUtcTicks = newAnchorUtcTicks;
            world.currentSeasonId = nextSnapshot.SeasonId;
            world.currentDisasterId = nextSnapshot.ActiveDisasterId;
            Current = nextSnapshot;

            SaveResult saveResult = null;
            try
            {
                saveResult = saveService?.Save(saveData);
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Game calendar save threw an exception: {exception.Message}");
            }

            if (saveResult == null || !saveResult.Succeeded)
            {
                calendar.totalElapsedDays = previousDays;
                calendar.dayAnchorUtcTicks = previousAnchor;
                world.currentSeasonId = previousSeasonId;
                world.currentDisasterId = previousDisasterId;
                Current = previous;
                return new CalendarAdvanceResult(
                    false, 0L, previous, previous, false, false, false, saveResult);
            }

            pendingGameTicks = remainderTicks;
            return new CalendarAdvanceResult(
                true,
                daysAdvanced,
                previous,
                nextSnapshot,
                previous.AbsoluteMonthIndex != nextSnapshot.AbsoluteMonthIndex,
                previous.Year != nextSnapshot.Year,
                previous.Season != nextSnapshot.Season,
                saveResult);
        }

        private bool TryAddScaledDelta(long rawDelta)
        {
            try
            {
                var scale = (long)debugScale;
                pendingGameTicks = checked(pendingGameTicks + checked(rawDelta * scale));
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private CalendarAdvanceResult Unchanged()
        {
            return new CalendarAdvanceResult(
                false, 0L, Current, Current, false, false, false, null);
        }

        private static bool TryGetCalendar(
            SaveData saveData,
            DateTime currentUtc,
            out GameCalendarSaveData calendar,
            out GameCalendarSnapshot snapshot)
        {
            calendar = saveData?.world?.calendar;
            snapshot = default;
            if (calendar == null || calendar.totalElapsedDays < 0 || calendar.dayAnchorUtcTicks <= 0)
            {
                FrameworkLog.Warning("Game calendar session could not start because save calendar state is invalid.");
                return false;
            }

            if (currentUtc.Ticks < calendar.dayAnchorUtcTicks)
            {
                FrameworkLog.Warning(
                    $"Game calendar UTC rollback ignored. Current: {currentUtc.Ticks}, Anchor: {calendar.dayAnchorUtcTicks}.");
            }

            try
            {
                var date = GameCalendarDate.FromElapsedDays(calendar.totalElapsedDays);
                snapshot = new GameCalendarSnapshot(date, saveData.world.currentDisasterId);
                return true;
            }
            catch (Exception exception) when (
                exception is OverflowException || exception is ArgumentOutOfRangeException)
            {
                FrameworkLog.Warning($"Game calendar session could not start: {exception.Message}");
                return false;
            }
        }

        private void ResetSession()
        {
            sessionSaveData = null;
            lastSampleUtcTicks = 0L;
            pendingGameTicks = 0L;
            Current = default;
            hasCurrent = false;
        }
    }
}
