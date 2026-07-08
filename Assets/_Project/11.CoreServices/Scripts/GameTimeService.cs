using System;
using UnityEngine;

namespace ND.Framework
{
    public sealed class GameTimeService : IGameTimeProvider
    {
        public float TimeScale { get; private set; } = 1f;
        public DateTime CurrentUtc => DateTime.UtcNow;

        public void SetTimeScale(float scale)
        {
            TimeScale = Mathf.Max(0f, scale);
            Time.timeScale = TimeScale;
            FrameworkLog.Info($"Time scale changed: {TimeScale}");
        }

        public DateTime CalculateTradeEnd(DateTime startUtc, TimeSpan duration)
        {
            return startUtc + duration;
        }

        public TimeSpan GetRemainingTime(DateTime endUtc)
        {
            var remaining = endUtc - CurrentUtc;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
