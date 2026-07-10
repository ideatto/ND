/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices에서 사용할 현재 UTC 시간과 Unity time scale 제어 기능을 제공한다.
 * - 무역 진행 시간 계산과 debug time scale 변경을 한 서비스에서 처리한다.
 *
 * Main Features
 * - UTC 현재 시각을 제공한다.
 * - Unity Time.timeScale을 0 이상 값으로 설정한다.
 * - 시작/종료 시각과 남은 시간을 계산하는 helper API를 제공한다.
 *
 * Usage for Team Members
 * - 시간 기반 진행률 계산에는 IGameTimeProvider.CurrentUtc를 사용한다.
 * - debug time scale 변경은 FrameworkDebugCommands를 통해 호출하는 것을 권장한다.
 *
 * Main Public APIs
 * - CurrentUtc: 현재 UTC 시각.
 * - SetTimeScale(...): Unity time scale을 변경한다.
 * - CalculateTradeEnd(...): 시작 시각과 기간으로 종료 시각을 계산한다.
 * - GetRemainingTime(...): 현재 UTC 기준 남은 시간을 계산한다.
 *
 * Important Notes
 * - SetTimeScale은 음수 값을 0으로 보정한다.
 * - TimeScale은 UnityEngine.Time.timeScale과 같은 값으로 유지된다.
 */
using System;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// framework 시간 조회와 Unity time scale 제어를 담당하는 서비스이다.
    /// </summary>
    public sealed class GameTimeService : IGameTimeProvider
    {
        /// <summary>
        /// 현재 Unity time scale 값이다.
        /// </summary>
        public float TimeScale { get; private set; } = 1f;

        /// <summary>
        /// 현재 UTC 시각을 반환한다.
        /// </summary>
        public DateTime CurrentUtc => DateTime.UtcNow;

        /// <summary>
        /// Unity time scale을 설정한다.
        /// </summary>
        /// <param name="scale">적용할 배율. 0보다 작으면 0으로 보정된다.</param>
        public void SetTimeScale(float scale)
        {
            // Unity time scale에 음수가 들어가지 않도록 framework 진입점에서 보정한다.
            TimeScale = Mathf.Max(0f, scale);
            Time.timeScale = TimeScale;
            FrameworkLog.Info($"Time scale changed: {TimeScale}");
        }

        /// <summary>
        /// 무역 시작 시각과 duration을 더해 예상 종료 시각을 계산한다.
        /// </summary>
        /// <param name="startUtc">UTC 기준 시작 시각.</param>
        /// <param name="duration">예상 진행 시간.</param>
        /// <returns>UTC 기준 예상 종료 시각.</returns>
        public DateTime CalculateTradeEnd(DateTime startUtc, TimeSpan duration)
        {
            return startUtc + duration;
        }

        /// <summary>
        /// 목표 종료 시각까지 남은 시간을 계산한다.
        /// </summary>
        /// <param name="endUtc">UTC 기준 종료 시각.</param>
        /// <returns>남은 시간이 있으면 해당 TimeSpan, 이미 지났으면 TimeSpan.Zero.</returns>
        public TimeSpan GetRemainingTime(DateTime endUtc)
        {
            // 이미 종료 시각이 지난 경우 음수 TimeSpan 대신 0을 반환해 UI 표시를 단순화한다.
            var remaining = endUtc - CurrentUtc;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
