/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Core와 UI가 사용할 인게임 시간 배율·pause·변환 API 계약을 정의한다.
 *
 * Important Notes
 * - 이 계약은 Unity Time.timeScale을 다루지 않는다.
 * - 무역 진행 중 elapsed 계산은 저장된 출발 시점 배율을 사용한다.
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// 인게임 시간 배율, pause 상태, 경과 인게임 시간 변환을 제공하는 계약이다.
    /// </summary>
    public interface IInGameTimeProvider
    {
        /// <summary>
        /// 현재 runtime 인게임 시간 배율이다. Release에서는 config 초기값만 유지된다.
        /// </summary>
        float InGameTimeMultiplier { get; }

        /// <summary>
        /// 인게임 시간 진행이 일시정지되었는지 여부이다.
        /// </summary>
        bool IsGameTimePaused { get; }

        /// <summary>
        /// 경과 인게임 시간 UI 표시 단위이다.
        /// </summary>
        InGameTimeUnit ElapsedTimeDisplayUnit { get; }

        /// <summary>
        /// 견인 동물 식량 소모율 해석 단위이다.
        /// </summary>
        InGameTimeUnit FoodConsumptionUnit { get; }

        /// <summary>
        /// UTC 구간의 인게임 경과 초를 계산한다.
        /// </summary>
        double GetElapsedInGameSeconds(DateTime startUtc, DateTime endUtc, float multiplier);

        /// <summary>
        /// active trade 저장 데이터와 endUtc를 사용해 인게임 경과 초를 계산한다.
        /// </summary>
        double GetElapsedInGameSecondsForActiveTrade(TradeProgressSaveData progress, DateTime endUtc);

        /// <summary>
        /// 인게임 경과 초를 선택한 단위 문자열로 포맷한다.
        /// </summary>
        string FormatInGameDuration(double inGameSeconds, InGameTimeUnit unit);

        /// <summary>
        /// raw 소모율을 인게임 초당 소모율로 정규화한다.
        /// </summary>
        float ToConsumptionPerInGameSecond(float rawRate);
    }
}
