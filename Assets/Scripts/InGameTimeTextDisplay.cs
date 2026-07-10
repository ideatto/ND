using System;
using TMPro;
using UnityEngine;

namespace ND.Framework
{
    public sealed class InGameTimeTextDisplay : MonoBehaviour
    {
        private static readonly TimeSpan KoreaUtcOffset = TimeSpan.FromHours(9);

        [SerializeField] private TMP_Text timeText;
        [SerializeField] private float refreshIntervalSeconds = 0.25f;

        private float elapsedSinceRefresh;

        private void Awake()
        {
            if (timeText == null)
            {
                timeText = GetComponent<TMP_Text>();
            }
        }

        private void OnEnable()
        {
            elapsedSinceRefresh = refreshIntervalSeconds;
            Refresh();
        }

        private void Update()
        {
            elapsedSinceRefresh += Time.unscaledDeltaTime;
            if (elapsedSinceRefresh < refreshIntervalSeconds)
            {
                return;
            }

            elapsedSinceRefresh = 0f;
            Refresh();
        }

        private void Refresh()
        {
            if (timeText == null || FrameworkRoot.Instance == null || FrameworkRoot.Instance.GameTime == null)
            {
                return;
            }

            var gameTime = FrameworkRoot.Instance.GameTime;
            var koreaTime = gameTime.CurrentUtc + KoreaUtcOffset;
            var saveData = FrameworkRoot.Instance.CurrentSaveData;
            var elapsedInGameText = BuildElapsedInGameText(gameTime, saveData);
            var pauseText = gameTime.IsGameTimePaused ? "\nPaused" : string.Empty;

            timeText.text =
                $"KST {koreaTime:yyyy-MM-dd HH:mm:ss} (UTC+09:00)\n" +
                $"Time Scale x{gameTime.TimeScale:0.##}\n" +
                $"In-Game Multiplier x{gameTime.InGameTimeMultiplier:0.##}\n" +
                $"{elapsedInGameText}{pauseText}";
        }

        private static string BuildElapsedInGameText(GameTimeService gameTime, SaveData saveData)
        {
            if (saveData?.tradeProgress == null
                || saveData.tradeProgress.state != TradeProgressState.Traveling
                || saveData.tradeProgress.tradeStartUtcTick <= 0)
            {
                return "Elapsed In-Game: -";
            }

            var elapsedInGameSeconds = gameTime.GetElapsedInGameSecondsForActiveTrade(
                saveData.tradeProgress,
                gameTime.CurrentUtc);
            var formatted = gameTime.FormatInGameDuration(
                elapsedInGameSeconds,
                gameTime.ElapsedTimeDisplayUnit);
            var unitLabel = InGameTimeUnitExtensions.ToDisplayLabel(gameTime.ElapsedTimeDisplayUnit);
            return $"Elapsed In-Game: {formatted} ({unitLabel})";
        }
    }
}
