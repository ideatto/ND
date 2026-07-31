// =============================================================================
// TreadmillTownArrival — 트레드밀에 '마을 도착' 연출(건물 프리팹을 앞에서 스폰→다가와 정지)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [역할] 캐러밴이 목적지 마을에 다다르면, 그 마을의 건물 프리팹(TownWorldView가 지정)을
//        길 저 앞(+Z)에 스폰하고, 길이 흐르는 속도에 맞춰 캐러밴 쪽으로 다가오게 한 뒤,
//        정지 지점(stopZ)에 닿으면 길·동물을 멈춰(도착) 세운다.
//
// [연결] 건물 프리팹은 A안(자유 배치 마을) — TreadmillRouteSampler.TryGetArrivalTownPrefab이
//        경로 끝점에 가장 가까운 TownWorldView.TreadmillBuildingPrefab을 찾아준다.
//        정지는 TreadmillRoad.SetArrived(true) → IsScrolling=false로 길·걷기·덜컹까지 멈춤.
//
// [부착] TreadmillRoad와 같은 축(트레드밀 원점, +Z=앞)에 두면 된다. 보통 Road 오브젝트에 부착.
// =============================================================================

using UnityEngine;

/// <summary>목적지 마을 건물을 앞에서 스폰해 다가와 정지시키는 도착 연출.</summary>
public class TreadmillTownArrival : MonoBehaviour
{
    [SerializeField] private TreadmillRoad road;   // 비면 자동 검색
    [Tooltip("마을을 스폰할 앞쪽 거리(m). 여기서부터 다가온다.")]
    [SerializeField] private float aheadZ = 55f;
    [Tooltip("마을이 멈춰 서는(도착) 캐러밴 앞 거리(m).")]
    [SerializeField] private float stopZ = 6f;
    [Tooltip("건물 바닥 높이 보정(m).")]
    [SerializeField] private float yOffset = 0f;
    [Tooltip("건물 폭을 이 크기(m)에 맞춰 자동 스케일(마을마다 원본 크기 달라도 일정하게). 0이면 buildingScale 사용.")]
    [SerializeField] private float autoFitWidth = 16f;
    [Tooltip("autoFitWidth=0일 때 쓰는 수동 배율.")]
    [SerializeField] private float buildingScale = 1f;
    [Tooltip("건물 방향(도). 보통 캐러밴을 마주보게 180.")]
    [SerializeField] private float buildingYaw = 180f;

    private Transform town;     // 현재 스폰된 마을 인스턴스
    private bool approaching;   // 다가오는 중
    private float baseY;        // 지면(캐러밴 위치)에서의 바닥 로컬 y(커브 z=0 기준)

    // 길 커브를 반영한 지면 높이(z가 멀수록 아래로) — 마을이 그리드 위에 서게.
    private float GroundY(float z)
    {
        float curv = road != null ? road.Curvature : 0.0025f;
        return baseY - curv * z * z;
    }

    /// <summary>지금 마을이 떠 있는지(도착 포함).</summary>
    public bool HasTown => town != null;

    private void Awake()
    {
        if (road == null) road = Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);
    }

    /// <summary>[디버그] 마을을 앞에 스폰하고 스스로 다가오는 연출 시작(길 속도로).</summary>
    public void Arrive(GameObject prefab)
    {
        if (!EnsureTown(prefab)) return;
        var sp = town.localPosition; sp.z = aheadZ; sp.y = GroundY(aheadZ); town.localPosition = sp;
        approaching = true;
        if (road != null) road.SetArrived(false);
    }

    /// <summary>마을이 없으면 스폰(스케일·바닥스냅). 진행도 구동은 위치를 PlaceByRemaining이 정한다.</summary>
    public bool EnsureTown(GameObject prefab)
    {
        if (town != null) return true;
        if (prefab == null) return false;
        if (road == null) road = Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);
        Transform parent = road != null ? road.transform : transform;   // 길과 같은 축
        var go = Instantiate(prefab, parent);
        go.name = "TreadmillTown";
        go.transform.localRotation = Quaternion.Euler(0f, buildingYaw, 0f);
        go.transform.localPosition = new Vector3(0f, yOffset, aheadZ);
        town = go.transform;

        // 폭 자동 맞춤(마을마다 원본 크기 달라도 도로 폭에 맞게 일정) 또는 수동 배율
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float footprint = Mathf.Max(b.size.x, b.size.z);
            float scale = (autoFitWidth > 0.01f && footprint > 0.001f)
                ? autoFitWidth / footprint : Mathf.Max(0.01f, buildingScale);
            go.transform.localScale *= scale;
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float baseYworld = parent.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
            baseY = go.transform.localPosition.y + (yOffset - baseYworld);   // z=0에서 바닥이 yOffset에 앉는 로컬 y
        }
        else { go.transform.localScale *= Mathf.Max(0.01f, buildingScale); baseY = yOffset; }
        var sp = go.transform.localPosition; sp.y = GroundY(aheadZ); go.transform.localPosition = sp;
        return true;
    }

    /// <summary>진행도 구동: 남은비율 t01(1=막 등장, 0=도착)로 마을 z 배치(그리드 위).</summary>
    public void PlaceByRemaining(float t01)
    {
        if (town == null) return;
        float z = Mathf.Lerp(stopZ, aheadZ, Mathf.Clamp01(t01));
        town.localPosition = new Vector3(0f, GroundY(z), z);
        approaching = false;   // 진행도가 위치를 정하므로 자기 접근 로직은 끔
    }

    /// <summary>마을을 치우고 다시 출발(스크롤 재개) 가능 상태로.</summary>
    public void Leave()
    {
        if (town != null) Destroy(town.gameObject);
        town = null;
        approaching = false;
        if (road != null) road.SetArrived(false);
    }

    private void Update()
    {
        if (!approaching || town == null) return;
        if (road == null) road = Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);

        float spd = road != null ? road.ForwardSpeed : 0f;   // 길 흐름 속도에 맞춰 다가옴
        if (spd <= 0.001f) return;                            // 정지 중이면 그대로 대기

        var p = town.localPosition;
        p.z -= spd * Time.deltaTime;
        if (p.z <= stopZ)                                     // 도착 지점 도달
        {
            p.z = stopZ;
            approaching = false;
            if (road != null) road.SetArrived(true);          // 길·동물 정지 = 도착
        }
        p.y = GroundY(p.z);                                   // 지면 커브 위에 서게(그리드 위)
        town.localPosition = p;
    }
}
