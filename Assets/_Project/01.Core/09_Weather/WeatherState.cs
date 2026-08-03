// =============================================================================
// WeatherState — 현재 날씨 '연속 상태' 게시 채널(비 세기)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 시스템 ↔ 트레드밀 연출 연결
//
// [역할] WeatherEvents(이산 '발생' 알림)와 달리, 매 프레임 갱신되는 '지금 얼마나 비가
//        오는가'(연속 세기)를 게시한다. 날씨 감지기(MinimapWeatherEventDetector)가 캐러밴별
//        비 세기를 Report로 올리고, 트레드밀 비 연출(TreadmillRain) 등 다른 씬/시스템이
//        폴링해서 읽는다. 씬이 서로 달라도(월드맵 ↔ InGame 애디티브) 정적 채널로 느슨하게 연결.
//
// [안전] 신선도(staleness) 판정 내장 — 게시자가 없으면(예: 감지기 미로딩) 세기 0으로 취급해
//        비가 안 오게 한다. 오래된 값이 남아 '유령 비'가 내리지 않도록.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>매 프레임 갱신되는 캐러밴별 비 세기(연속)를 게시/조회하는 정적 채널.</summary>
public static class WeatherState
{
    // 게시 후 이 시간(초)이 지나면 '오래된 값'으로 보고 무시(게시자 사라지면 비 멈춤).
    private const float StaleSeconds = 0.5f;

    private struct Entry { public float value; public float time; }
    private static readonly Dictionary<string, Entry> map = new Dictionary<string, Entry>();

    /// <summary>한 캐러밴의 현재 비 세기를 게시(0=비 없음). 감지기가 매 프레임 호출.</summary>
    public static void Report(string caravanId, float intensity)
    {
        if (string.IsNullOrEmpty(caravanId)) return;
        map[caravanId] = new Entry { value = Mathf.Max(0f, intensity), time = Time.unscaledTime };
    }

    /// <summary>특정 캐러밴의 현재 비 세기(오래됐거나 없으면 0).</summary>
    public static float Get(string caravanId)
    {
        if (!string.IsNullOrEmpty(caravanId) && map.TryGetValue(caravanId, out var e)
            && Time.unscaledTime - e.time < StaleSeconds)
            return e.value;
        return 0f;
    }

    /// <summary>지금 가장 세게 비 맞는 캐러밴의 세기(신선한 값만). 단일 플레이어 캐러밴 뷰용 편의값.</summary>
    public static float Max
    {
        get
        {
            float now = Time.unscaledTime, m = 0f;
            foreach (var kv in map)
                if (now - kv.Value.time < StaleSeconds && kv.Value.value > m) m = kv.Value.value;
            return m;
        }
    }

    // ── 번개 펄스(순간 이벤트) ──
    // 번개가 칠 때 시각(unscaledTime)을 갱신. 구독자는 이 값이 '바뀌면' 새 번개로 보고 반응.
    private static float lastLightning = -999f;

    /// <summary>번개 발생 통지(번개 시스템이 칠 때 호출).</summary>
    public static void ReportLightning() => lastLightning = Time.unscaledTime;

    /// <summary>마지막 번개 시각(unscaledTime). 값이 바뀌면 새 번개.</summary>
    public static float LastLightningTime => lastLightning;
}
