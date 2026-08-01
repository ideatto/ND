using System;

namespace ND.Framework
{
    /// <summary>
    /// Carries the single accepted wall-clock interval shared by all load-time restore participants.
    /// </summary>
    public readonly struct OfflineRestoreContext
    {
        public OfflineRestoreContext(
            DateTime lastSavedUtc,
            DateTime loadUtc,
            DateTime evaluationUtc,
            TimeSpan acceptedElapsed,
            bool clockRollbackDetected,
            bool wasClamped)
        {
            LastSavedUtc = lastSavedUtc;
            LoadUtc = loadUtc;
            EvaluationUtc = evaluationUtc;
            AcceptedElapsed = acceptedElapsed;
            ClockRollbackDetected = clockRollbackDetected;
            WasClamped = wasClamped;
        }

        public DateTime LastSavedUtc { get; }
        public DateTime LoadUtc { get; }
        public DateTime EvaluationUtc { get; }
        public TimeSpan AcceptedElapsed { get; }
        public bool ClockRollbackDetected { get; }
        public bool WasClamped { get; }
    }
}
