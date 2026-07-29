// =============================================================================
// WeatherEventData — 날씨 이벤트 정의(ScriptableObject)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 이벤트 시스템(루트 이벤트와 별개, 위치/날씨 기반)
//
// [역할] 프레임워크 루트 이벤트의 SharedRouteEventDefinition에 대응하는 '날씨판'.
//        어떤 날씨(셀 상태)에서 어떤 이벤트가, 얼마 확률로, 어떤 효과로 발생하는지를
//        데이터(에셋)로 정의한다. 콘텐츠 담당이 코드 없이 이벤트를 늘릴 수 있게.
//
// [경계] 효과 파라미터는 '데이터만' 담는다. 실제 게임플레이 효과 적용(식량↓·지연)은
//        효과 구현체(IWeatherEffect) 또는 프레임워크 협의 몫. 지금은 표시/기록.
// =============================================================================

using UnityEngine;

/// <summary>날씨 이벤트가 발생하는 셀 조건.</summary>
public enum WeatherCondition
{
    DarkCloud,   // 먹구름(비) 아래 — 현재 지원
    // 확장 여지: Storm(폭풍), Snow(눈), Clear(맑음 보너스) ...
}

/// <summary>날씨 이벤트 한 종류의 정의(SO). 조건·확률·효과 파라미터를 데이터로 보관.</summary>
[CreateAssetMenu(fileName = "WeatherEvent", menuName = "ND/Weather Event", order = 0)]
public class WeatherEventData : ScriptableObject
{
    [Header("기본")]
    [Tooltip("이벤트 식별자(결정론 hash·기록에 사용). 고유해야 함")]
    public string id = "rain";
    public string displayName = "비";
    [TextArea(2, 4)] public string description;

    [Header("발생 조건")]
    [Tooltip("이 셀 상태일 때 발생 후보가 된다")]
    public WeatherCondition condition = WeatherCondition.DarkCloud;
    [Range(0f, 1f)]
    [Tooltip("조건 충족 시 실제 발생 확률. 결정론 hash(tradeId,checkIndex,cellId)로 판정 → 같은 여행이면 같은 결과")]
    public float chance = 1f;

    [Header("효과 파라미터 (데이터만 — 실제 적용은 효과 구현/협의)")]
    [Tooltip("식량 추가 소모율(예시). 지금은 표시/기록만")]
    public float foodPenaltyRate = 0.05f;
    [Tooltip("이동 지연율(예시). 지금은 표시/기록만")]
    public float delayRate = 0.1f;
    [Range(0f, 3f)]
    [Tooltip("심각도(표시·연출 강도)")]
    public float severity = 1f;
}
