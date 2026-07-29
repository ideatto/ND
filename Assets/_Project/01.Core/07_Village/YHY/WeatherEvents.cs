// =============================================================================
// WeatherEvents — 날씨 이벤트 발생 알림 채널(우리 자체)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 이벤트 시스템
//
// [역할] 프레임워크 FrameworkEvents(RouteEventForced 등)의 '날씨판'. 날씨 이벤트가
//        발생하면 여기로 알린다. UI·연출·기록 등 다른 시스템이 구독해서 반응한다.
//        (효과 적용을 여기 넣지 않는다 — 알림만. 효과는 구독자/효과 구현체 몫.)
// =============================================================================

using System;
using UnityEngine;

/// <summary>발생한 날씨 이벤트 1건의 정보(결정론 판정 결과).</summary>
public struct WeatherEventOccurrence
{
    public string caravanId;    // 대상 캐러밴
    public string tradeId;      // 진행 중 무역 ID(결정론 축)
    public string eventId;      // WeatherEventData.id (예: "rain")
    public int cellRow;         // 발생 셀
    public int cellCol;
    public int checkIndex;      // 몇 번째 셀 체크에서 발생했나(결정론 축)
    public float severity;      // 심각도(표시/연출)
    public float foodPenaltyRate; // 효과 파라미터(데이터 — 적용은 구독자/협의)
    public float delayRate;
}

/// <summary>날씨 이벤트 알림 채널. 구독은 static event, 발행은 Raise.</summary>
public static class WeatherEvents
{
    /// <summary>캐러밴에 날씨 이벤트가 발생했을 때. 구독자는 비활성화 시 반드시 해제할 것.</summary>
    public static event Action<WeatherEventOccurrence> Occurred;

    /// <summary>날씨 이벤트 1건 발행(+디버그 로그).</summary>
    public static void Raise(WeatherEventOccurrence o)
    {
        Debug.Log("[날씨이벤트] '" + o.eventId + "' → 캐러밴 " + o.caravanId
                  + "  셀(" + o.cellRow + "," + o.cellCol + ")  check#" + o.checkIndex
                  + "  심각도 " + o.severity.ToString("F1"));
        Occurred?.Invoke(o);
    }
}
