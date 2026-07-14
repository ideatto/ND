/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices에서 사용할 현재 UTC 시간, Unity time scale, 인게임 시간 배율을 제공한다.
 * - 무역 진행 시간 계산과 debug time scale 변경을 한 서비스에서 처리한다.
 *
 * Main Features
 * - UTC 현재 시각을 제공한다.
 * - Unity Time.timeScale을 0 이상 값으로 설정한다. (연출/debug 전용)
 * - inGameTimeMultiplier와 pause를 gameplay 시간 축으로 관리한다.
 * - 현실 UTC 경과를 인게임 경과로 변환하는 API를 제공한다.
 *
 * Usage for Team Members
 * - 시간 기반 진행률 계산에는 IGameTimeProvider.CurrentUtc를 사용한다.
 * - 인게임 경과 시간·배율·pause는 IInGameTimeProvider를 사용한다.
 * - debug time scale 변경은 FrameworkDebugCommands를 통해 호출하는 것을 권장한다.
 *
 * Main Public APIs
 * - CurrentUtc: 현재 UTC 시각.
 * - SetTimeScale(...): Unity time scale을 변경한다.
 * - InGameTimeMultiplier: 현재 인게임 시간 배율.
 * - PauseGameTime() / ResumeGameTime(): 인게임 진행 정지/재개.
 * - GetElapsedInGameSeconds(...): UTC 구간의 인게임 경과 초.
 * - GetOfflineElapsedInGameSeconds(...): 오프라인 복구용 인게임 경과 초.
 * - TryResolveOfflineEvaluationUtc(...): 역행 감지와 최대 오프라인 상한 clamp.
 *
 * Important Notes
 * - SetTimeScale은 inGameTimeMultiplier를 변경하지 않는다.
 * - Release 빌드에서는 SetInGameTimeMultiplier가 무시된다.
 * - pause 중에는 인게임 경과 시간이 증가하지 않는다.
 * - Related Documentation: Docs/Personal_Documents/CSU/0712_m3-offline-progress-pipeline.md
 */
using System;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// framework 시간 조회, Unity time scale 제어, 인게임 시간 배율을 담당하는 서비스이다.
    /// </summary>
    public sealed class GameTimeService : IGameTimeProvider, IInGameTimeProvider
    {
        private readonly InGameTimePolicyConfig policyConfig;
        private readonly InGameTimeConversionPolicy conversionPolicy;
        private float inGameTimeMultiplier;
        private bool isGameTimePaused;

        /// <summary>
        /// policy config 없이 기본값으로 서비스를 생성한다.
        /// </summary>
        public GameTimeService()
            : this(null)
        {
        }

        /// <summary>
        /// policy config를 주입받아 서비스를 생성한다.
        /// </summary>
        /// <param name="policyConfig">기본 배율과 단위 정책. null이면 코드 기본값을 사용한다.</param>
        public GameTimeService(InGameTimePolicyConfig policyConfig)
        {
            this.policyConfig = policyConfig;
            conversionPolicy = new InGameTimeConversionPolicy();
            inGameTimeMultiplier = policyConfig != null ? policyConfig.DefaultInGameTimeMultiplier : 1f;
        }

        /// <summary>
        /// 현재 Unity time scale 값이다. 인게임 시간 배율과 분리된다.
        /// </summary>
        public float TimeScale { get; private set; } = 1f;

        /// <summary>
        /// 현재 runtime 인게임 시간 배율이다.
        /// </summary>
        public float InGameTimeMultiplier => inGameTimeMultiplier;

        /// <summary>
        /// 인게임 시간 진행이 일시정지되었는지 여부이다.
        /// </summary>
        public bool IsGameTimePaused => isGameTimePaused;

        /// <summary>
        /// 경과 인게임 시간 UI 표시 단위이다.
        /// </summary>
        public InGameTimeUnit ElapsedTimeDisplayUnit =>
            policyConfig != null ? policyConfig.ElapsedTimeDisplayUnit : InGameTimeUnit.Hour;

        /// <summary>
        /// 견인 동물 식량 소모율 해석 단위이다.
        /// </summary>
        public InGameTimeUnit FoodConsumptionUnit =>
            policyConfig != null ? policyConfig.FoodConsumptionUnit : InGameTimeUnit.Hour;

        /// <summary>
        /// 오프라인 복구에 인정하는 최대 현실 경과 시간이다. 단위: 초.
        /// </summary>
        public double MaxOfflineRealSeconds =>
            policyConfig != null
                ? policyConfig.MaxOfflineRealSeconds
                : InGameTimePolicyConfig.DefaultMaxOfflineRealSeconds;

        /// <summary>
        /// 현재 UTC 시각을 반환한다.
        /// </summary>
        public DateTime CurrentUtc => DateTime.UtcNow;

        /// <summary>
        /// Unity time scale을 설정한다. inGameTimeMultiplier에는 영향을 주지 않는다.
        /// </summary>
        /// <param name="scale">적용할 배율. 0보다 작으면 0으로 보정된다.</param>
        public void SetTimeScale(float scale)
        {
            TimeScale = Mathf.Max(0f, scale);
            Time.timeScale = TimeScale;
            FrameworkLog.Info($"Time scale changed: {TimeScale}");
        }

        /// <summary>
        /// runtime 인게임 시간 배율을 설정한다.
        /// </summary>
        /// <param name="multiplier">현실 1초당 인게임 N초. 0보다 작으면 0으로 보정된다.</param>
        /// <returns>Editor 또는 Development Build에서 적용되면 true, Release에서는 false.</returns>
        public bool TrySetInGameTimeMultiplier(float multiplier)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            inGameTimeMultiplier = Mathf.Max(0f, multiplier);
            FrameworkLog.Info($"In-game time multiplier changed: {inGameTimeMultiplier}");
            return true;
#else
            FrameworkLog.Warning("In-game time multiplier change is ignored in release builds.");
            return false;
#endif
        }

        /// <summary>
        /// 인게임 시간 진행을 일시정지한다.
        /// </summary>
        public void PauseGameTime()
        {
            isGameTimePaused = true;
            FrameworkLog.Info("In-game time paused.");
        }

        /// <summary>
        /// 인게임 시간 진행을 재개한다.
        /// </summary>
        public void ResumeGameTime()
        {
            isGameTimePaused = false;
            FrameworkLog.Info("In-game time resumed.");
        }

        /// <summary>
        /// config 기본 배율로 runtime 배율을 되돌린다.
        /// </summary>
        public void ResetInGameTimeMultiplier()
        {
            inGameTimeMultiplier = policyConfig != null ? policyConfig.DefaultInGameTimeMultiplier : 1f;
            FrameworkLog.Info($"In-game time multiplier reset: {inGameTimeMultiplier}");
        }

        /// <summary>
        /// UTC 구간의 인게임 경과 초를 계산한다.
        /// </summary>
        public double GetElapsedInGameSeconds(DateTime startUtc, DateTime endUtc, float multiplier)
        {
            return conversionPolicy.GetElapsedInGameSeconds(startUtc, endUtc, multiplier);
        }

        /// <summary>
        /// active trade 저장 데이터를 기준으로 인게임 경과 초를 계산한다.
        /// </summary>
        public double GetElapsedInGameSecondsForActiveTrade(TradeProgressSaveData progress, DateTime endUtc)
        {
            if (progress == null || progress.tradeStartUtcTick <= 0)
            {
                return 0d;
            }

            var startUtc = new DateTime(progress.tradeStartUtcTick, DateTimeKind.Utc);
            var multiplier = NormalizeStoredMultiplier(progress.inGameTimeMultiplierAtStart);
            return GetElapsedInGameSeconds(startUtc, endUtc, multiplier);
        }

        /// <summary>
        /// 인게임 경과 초를 선택한 단위 문자열로 포맷한다.
        /// </summary>
        public string FormatInGameDuration(double inGameSeconds, InGameTimeUnit unit)
        {
            return conversionPolicy.FormatInGameDuration(inGameSeconds, unit);
        }

        /// <summary>
        /// raw 소모율을 인게임 초당 소모율로 정규화한다.
        /// </summary>
        public float ToConsumptionPerInGameSecond(float rawRate)
        {
            return conversionPolicy.ToConsumptionPerInGameSecond(rawRate, FoodConsumptionUnit);
        }

        /// <summary>
        /// 오프라인 복구용 인게임 경과 초를 계산한다.
        /// </summary>
        public double GetOfflineElapsedInGameSeconds(
            DateTime tradeStartUtc,
            DateTime loadUtc,
            float multiplierAtStart)
        {
            return conversionPolicy.GetOfflineElapsedInGameSeconds(tradeStartUtc, loadUtc, multiplierAtStart);
        }

        /// <summary>
        /// 오프라인 복구에 사용할 evaluationUtc를 결정한다.
        /// </summary>
        /// <returns>시간 역행이면 true. 이 경우 evaluationUtc는 loadUtc이며 호출자는 적용을 건너뛴다.</returns>
        public bool TryResolveOfflineEvaluationUtc(
            long lastSavedUtcTicks,
            DateTime loadUtc,
            out DateTime evaluationUtc)
        {
            return conversionPolicy.TryResolveOfflineEvaluationUtc(
                lastSavedUtcTicks,
                loadUtc,
                MaxOfflineRealSeconds,
                out evaluationUtc);
        }

        /// <summary>
        /// 무역 시작 시각과 duration을 더해 예상 종료 시각을 계산한다.
        /// </summary>
        public DateTime CalculateTradeEnd(DateTime startUtc, TimeSpan duration)
        {
            return startUtc + duration;
        }

        /// <summary>
        /// 목표 종료 시각까지 남은 시간을 계산한다.
        /// </summary>
        public TimeSpan GetRemainingTime(DateTime endUtc)
        {
            var remaining = endUtc - CurrentUtc;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private static float NormalizeStoredMultiplier(float multiplier)
        {
            return multiplier <= 0f ? 1f : multiplier;
        }
    }
}
