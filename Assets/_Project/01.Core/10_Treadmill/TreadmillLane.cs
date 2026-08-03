// =============================================================================
// TreadmillLane — '마차 1대 전용' 트레드밀 레인(카메라+스테이지+길 한 묶음)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 · 마차별 독립 레인 구조
//
// [왜] 예전엔 트레드밀이 딱 하나(카메라·길·스테이지 1개)라, 마차를 바꿀 때마다 그 하나를
//      '재구성'했다. 한 군데라도 안 갈리면 마차2에 마차1 상태가 남는 교차 오염 버그가 잦았다.
//      → 마차마다 '전용 레인'을 따로 두면 서로 섞일 수가 없다(구조적으로 안전).
//
// [무엇] 이 컴포넌트는 한 레인(Camera + TreadmillStage + TreadmillRoad + Rain/TownArrival/Sync)의
//        루트에 붙어, 그 레인 내부 부품들을 캐싱하고 '이 레인이 담당하는 caravanId'를 보관한다.
//        레인 안의 각 부품(Stage/Road/Sync/TownArrival/Dodge)은 전역 검색 대신 이 레인에서
//        형제 부품을 찾는다(GetComponentInParent<TreadmillLane>) → 다른 레인과 안 엉킨다.
//
// [관리] 여러 레인의 생성·배치·활성 카메라 전환은 TreadmillLaneManager가 담당.
// =============================================================================

using UnityEngine;

/// <summary>마차 1대 전용 트레드밀 레인. 내부 부품 캐싱 + 담당 caravanId 보관.</summary>
public class TreadmillLane : MonoBehaviour
{
    [Tooltip("이 레인이 담당하는 캐러밴 id(런타임에 매니저가 지정). 비면 미배정.")]
    public string caravanId = "";

    // 레인 내부 부품(런타임 캐싱 — 씬 직렬화 안 함)
    [System.NonSerialized] public TreadmillStage stage;
    [System.NonSerialized] public TreadmillRoad road;
    [System.NonSerialized] public TreadmillProgressSync sync;
    [System.NonSerialized] public TreadmillTownArrival arrival;
    [System.NonSerialized] public TreadmillRain rain;
    [System.NonSerialized] public TreadmillWaitingRoom waitingRoom;
    [System.NonSerialized] public Camera cam;

    private void Awake() => Resolve();

    /// <summary>레인 내부 부품을 (없으면) 한 번 찾아 캐싱한다. 어느 부품이든 안전하게 호출 가능.</summary>
    public void Resolve()
    {
        if (stage == null) stage = GetComponentInChildren<TreadmillStage>(true);
        if (road == null) road = GetComponentInChildren<TreadmillRoad>(true);
        if (sync == null) sync = GetComponentInChildren<TreadmillProgressSync>(true);
        if (arrival == null) arrival = GetComponentInChildren<TreadmillTownArrival>(true);
        if (rain == null) rain = GetComponentInChildren<TreadmillRain>(true);
        if (waitingRoom == null) waitingRoom = GetComponentInChildren<TreadmillWaitingRoom>(true);
        if (cam == null) cam = GetComponentInChildren<Camera>(true);
    }

    /// <summary>이 컴포넌트가 속한 레인을 찾는다(부품들이 형제 참조를 얻을 때 사용). 없으면 null.</summary>
    public static TreadmillLane Of(Component c)
        => c != null ? c.GetComponentInParent<TreadmillLane>(true) : null;
}
