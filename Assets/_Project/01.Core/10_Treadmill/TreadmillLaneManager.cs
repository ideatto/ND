// =============================================================================
// TreadmillLaneManager — 마차별 독립 레인 생성·전환 관리
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 · 마차별 독립 레인 구조
//
// [역할] "마차N" 버튼/미니맵으로 어떤 마차를 열면, 그 마차 '전용 레인'을 보여준다.
//        - 각 마차는 자기 레인(Camera+Stage+Road)을 따로 가진다 → 서로 상태가 안 섞인다.
//        - 레인은 필요할 때 처음 한 번만 만든다(첫 마차=씬의 원본 레인 재사용, 이후=복제).
//        - 복제 레인은 옆으로 멀찍이(laneGap) 떨어뜨려, 각 카메라가 자기 레인만 찍게 격리.
//        - 성능: '지금 보이는 레인의 카메라'만 켜서 공유 RT(TreadmillRT)에 렌더 → 그리기 1배.
//          (안 보이는 레인은 카메라 off. 단 진행도 로직은 계속 돌아 위치가 항상 최신.)
//
// [연결] TreadmillPanel.Open() → 이 매니저의 Show(caravanId) 호출.
// [부착] 트레드밀 프리뷰 씬에 빈 오브젝트 하나 만들어 붙이고, prototypeLane에 원본 레인을 지정.
//        (비워두면 씬에서 TreadmillLane 하나를 자동으로 원본으로 잡는다.)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>마차별 트레드밀 레인을 만들고 활성 레인만 공유 RT로 렌더한다.</summary>
public class TreadmillLaneManager : MonoBehaviour
{
    [Tooltip("원본(프로토타입) 레인. 첫 마차는 이걸 그대로 쓰고, 이후 마차는 이걸 복제한다. 비면 자동 탐색.")]
    [SerializeField] private TreadmillLane prototypeLane;
    [Tooltip("레인끼리 떨어뜨릴 거리(m). 카메라가 이웃 레인을 안 찍도록 충분히 크게.")]
    [SerializeField] private float laneGap = 1000f;
    [Tooltip("레인을 쌓는 방향(월드). 기본 아래(-Y)로 세로 정렬 → 씬에서 찾기 쉬움.")]
    [SerializeField] private Vector3 laneStackDir = new Vector3(0f, -1f, 0f);

    private RenderTexture displayRT;                 // 패널이 보는 공유 RT(원본 카메라의 타겟)
    private readonly Dictionary<string, TreadmillLane> lanes = new Dictionary<string, TreadmillLane>();
    private bool prototypeUsed;
    private TreadmillLane active;

    private static TreadmillLaneManager instance;
    /// <summary>씬의 매니저(패널 등에서 접근). 없으면 null(→ 단일 인스턴스 폴백).</summary>
    public static TreadmillLaneManager Instance => instance;

    private void Awake()
    {
        instance = this;
        if (prototypeLane == null) prototypeLane = Object.FindFirstObjectByType<TreadmillLane>(FindObjectsInactive.Include);
        if (prototypeLane != null)
        {
            prototypeLane.Resolve();
            // 원본 카메라가 지금 그리고 있는 RT = 패널이 보는 공유 RT. 이걸 활성 레인에 물려준다.
            if (prototypeLane.cam != null) displayRT = prototypeLane.cam.targetTexture;
        }
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    /// <summary>지정 마차의 레인을 화면(공유 RT)에 보인다. 없으면 만들고, 그 카메라만 켠다.</summary>
    public void Show(string caravanId)
    {
        if (string.IsNullOrEmpty(caravanId) || prototypeLane == null) return;
        TreadmillLane lane = GetOrCreateLane(caravanId);
        if (lane == null) return;

        // 활성 레인만 공유 RT로 렌더, 나머지 카메라는 끔(그리기 1배 유지).
        foreach (var kv in lanes)
        {
            var l = kv.Value; if (l == null || l.cam == null) continue;
            bool on = (l == lane);
            if (!on) { l.cam.enabled = false; if (l.cam.targetTexture == displayRT) l.cam.targetTexture = null; }
        }
        if (lane.cam != null) { lane.cam.targetTexture = displayRT; lane.cam.enabled = true; }
        active = lane;
    }

    /// <summary>이미 생성된 해당 Caravan 레인을 최신 저장 데이터의 마차·동물 편성으로 다시 구성한다.</summary>
    public void RefreshCaravan(string caravanId)
    {
        if (string.IsNullOrEmpty(caravanId)
            || !lanes.TryGetValue(caravanId, out TreadmillLane lane)
            || lane == null
            || lane.stage == null) return;

        lane.stage.ShowCaravan(caravanId);
    }

    // 마차 전용 레인을 얻거나(있으면) 만든다(없으면). 첫 마차=원본 재사용, 이후=복제.
    private TreadmillLane GetOrCreateLane(string caravanId)
    {
        if (lanes.TryGetValue(caravanId, out var exist) && exist != null) return exist;

        TreadmillLane lane;
        if (!prototypeUsed)
        {
            lane = prototypeLane;          // 첫 마차: 씬 원본 레인을 그대로 사용(추가 비용 0)
            prototypeUsed = true;
        }
        else
        {
            // 이후 마차: 원본을 복제해 정해진 방향(기본 아래)으로 멀찍이 떨어뜨림(카메라가 자기 레인만 보게).
            // lanes.Count(=1,2,3…)를 곱해 원본과 안 겹치게 순서대로 쌓는다 → 씬에서 세로로 나란히.
            var go = Instantiate(prototypeLane.gameObject, prototypeLane.transform.parent);
            go.name = "TreadmillLane_" + Short(caravanId);
            Vector3 dir = laneStackDir.sqrMagnitude > 0.0001f ? laneStackDir.normalized : Vector3.down;
            go.transform.position = prototypeLane.transform.position + dir * (laneGap * lanes.Count);
            lane = go.GetComponent<TreadmillLane>();
        }

        lane.Resolve();
        lane.caravanId = caravanId;                 // 이 레인은 이 마차 전담(ProgressSync가 이걸 몬다)
        if (lane.stage != null) lane.stage.ShowCaravan(caravanId);   // 마차+동물 세우기(1회)
        if (lane.rain != null) lane.rain.SetCaravan(caravanId);      // 비도 이 마차 날씨로
        if (lane.cam != null) { lane.cam.enabled = false; lane.cam.targetTexture = null; }  // 기본 off(Show가 켬)

        lanes[caravanId] = lane;
        return lane;
    }

    private static string Short(string id)
        => string.IsNullOrEmpty(id) ? "?" : (id.Length > 8 ? id.Substring(0, 8) : id);
}
