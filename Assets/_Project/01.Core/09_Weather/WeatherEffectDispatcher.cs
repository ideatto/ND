// =============================================================================
// WeatherEffectDispatcher — 날씨 이벤트 알림 → 효과 구현체들로 전달
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 이벤트 시스템
//
// [역할] WeatherEvents.Occurred(알림)를 구독해서, 자식에 붙은 모든 IWeatherEffect
//        구현체의 Apply를 호출한다. 트리거(감지)와 효과를 연결하는 중앙 허브.
//
// [사용] 이 컴포넌트가 붙은 오브젝트(또는 그 자식)에 IWeatherEffect 구현체(예:
//        WeatherNoticeEffect)를 두면, 이벤트 발생 시 자동으로 실행된다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>날씨 이벤트 알림을 받아 자식의 모든 IWeatherEffect에 전달하는 허브.</summary>
public class WeatherEffectDispatcher : MonoBehaviour
{
    [Tooltip("자식에서 IWeatherEffect를 자동 수집(끄면 effects 리스트만 사용)")]
    [SerializeField] private bool autoCollectChildren = true;
    [Tooltip("수동 지정 효과(MonoBehaviour면서 IWeatherEffect). autoCollect와 합쳐 사용")]
    [SerializeField] private MonoBehaviour[] manualEffects;

    private readonly List<IWeatherEffect> effects = new List<IWeatherEffect>();

    private void OnEnable()
    {
        RebuildEffects();
        Ensure();
    }

    private void OnDisable()
    {
        WeatherEvents.Occurred -= OnWeatherEvent;
    }

    // 핫 리로드/플레이 진입 타이밍으로 static 이벤트 구독이 풀려도 매 프레임 확실히 복구(중복 없이).
    private void Update()
    {
        Ensure();
        if (effects.Count == 0) RebuildEffects();
    }

    /// <summary>중복 없이 정확히 1회 구독 보장(먼저 제거 후 추가).</summary>
    private void Ensure()
    {
        WeatherEvents.Occurred -= OnWeatherEvent;
        WeatherEvents.Occurred += OnWeatherEvent;
    }

    /// <summary>효과 목록 재구성(자식 자동수집 + 수동지정).</summary>
    public void RebuildEffects()
    {
        effects.Clear();
        if (autoCollectChildren)
            foreach (var e in GetComponentsInChildren<IWeatherEffect>(true))
                if (e != null) effects.Add(e);
        if (manualEffects != null)
            foreach (var m in manualEffects)
                if (m is IWeatherEffect ie && !effects.Contains(ie)) effects.Add(ie);
    }

    private void OnWeatherEvent(WeatherEventOccurrence e)
    {
        for (int i = 0; i < effects.Count; i++)
            if (effects[i] != null) effects[i].Apply(e);
    }
}
