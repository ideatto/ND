/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 현실 UTC 경과 시간을 인게임 경과 시간으로 변환하는 정책을 한 곳에 모은다.
 * - 오프라인 복구 공식 변경 시 이 클래스만 수정하면 된다.
 *
 * Important Notes
 * - 온라인·오프라인·식량 계산은 동일한 변환 규칙을 사용한다.
 * - 음수 현실 경과는 0으로 보정한다.
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// 현실 시간과 인게임 시간 사이의 변환 규칙을 제공한다.
    /// </summary>
    public sealed class InGameTimeConversionPolicy
    {
        /// <summary>
        /// 현실 경과 시간을 인게임 경과 시간으로 변환한다.
        /// </summary>
        /// <param name="realElapsed">UTC wall-clock 기준 현실 경과 시간.</param>
        /// <param name="multiplier">현실 1초당 인게임 N초를 나타내는 배율.</param>
        public TimeSpan ConvertRealElapsedToInGame(TimeSpan realElapsed, float multiplier)
        {
            if (realElapsed < TimeSpan.Zero)
            {
                realElapsed = TimeSpan.Zero;
            }

            var safeMultiplier = NormalizeMultiplier(multiplier);
            var inGameSeconds = realElapsed.TotalSeconds * safeMultiplier;
            return TimeSpan.FromSeconds(inGameSeconds);
        }

        /// <summary>
        /// UTC 구간의 인게임 경과 초를 계산한다.
        /// </summary>
        public double GetElapsedInGameSeconds(DateTime startUtc, DateTime endUtc, float multiplier)
        {
            if (endUtc < startUtc)
            {
                return 0d;
            }

            var realElapsed = endUtc - startUtc;
            return ConvertRealElapsedToInGame(realElapsed, multiplier).TotalSeconds;
        }

        /// <summary>
        /// 오프라인 복구용 인게임 경과 초를 계산한다.
        /// </summary>
        /// <remarks>
        /// 기본 공식: (loadUtc - tradeStartUtc) × multiplierAtStart
        /// 공식 변경이 필요하면 이 메서드만 수정한다.
        /// </remarks>
        public double GetOfflineElapsedInGameSeconds(
            DateTime tradeStartUtc,
            DateTime loadUtc,
            float multiplierAtStart)
        {
            return GetElapsedInGameSeconds(tradeStartUtc, loadUtc, multiplierAtStart);
        }

        /// <summary>
        /// 단위 기준 raw 소모율을 인게임 초당 소모율로 정규화한다.
        /// </summary>
        public float ToConsumptionPerInGameSecond(float rawConsumptionRate, InGameTimeUnit unit)
        {
            if (rawConsumptionRate <= 0f)
            {
                return 0f;
            }

            var secondsPerUnit = InGameTimeUnitExtensions.SecondsPerUnit(unit);
            return secondsPerUnit <= 0d ? 0f : rawConsumptionRate / (float)secondsPerUnit;
        }

        /// <summary>
        /// 인게임 경과 초를 선택한 단위 기준 문자열로 포맷한다.
        /// </summary>
        public string FormatInGameDuration(double inGameSeconds, InGameTimeUnit unit)
        {
            var unitValue = InGameTimeUnitExtensions.ToUnitValue(inGameSeconds, unit);
            var label = InGameTimeUnitExtensions.ToDisplayLabel(unit);
            return $"{unitValue:0.##} {label}";
        }

        private static float NormalizeMultiplier(float multiplier)
        {
            return multiplier <= 0f ? 0f : multiplier;
        }
    }
}
