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

            timeText.text = $"KST {koreaTime:yyyy-MM-dd HH:mm:ss} (UTC+09:00)\nTime Scale x{gameTime.TimeScale:0.##}";
        }
    }
}
