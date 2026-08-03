// =============================================================================
// TreadmillStage — 트레드밀 스테이지(캐러밴 데이터를 읽어 마차/동물을 '보여주기만')
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [원칙] 스테이지는 데이터를 소유하지 않는다. 무엇을 보여줄지는 이미 세이브데이터(어느 캐러밴,
//        그 캐러밴의 wagon/animal)와 게임의 공용 중앙 카탈로그(SandboxSharedGameDataCatalog)가
//        갖고 있다. 스테이지는 그걸 '읽어와 보여주기만' 한다. (자기 프리팹 목록을 들지 않는다.)
//
// [모델 찾기] 공용 데이터 provider(SharedGameData)는 스탯만 있고 모델(prefab)은 없다. 모델은
//        WagonData/DraftAnimalData SO에 있으므로, 게임이 쓰는 중앙 카탈로그(Resources의
//        SandboxSharedGameDataCatalog)에서 ID로 SO를 찾아 그 .Prefab을 세운다. 프리팹 안에
//        크기·회전·재질·Animator가 다 들어 있어, 스테이지는 위치만 잡는다.
//
// [편성] 동물은 마차 앞(+Z)에 마릿수만큼, 한 줄 animalsPerRow마리씩 세운다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>캐러밴 데이터를 읽어 마차/동물 프리팹을 배치만 하는 트레드밀 스테이지.</summary>
public class TreadmillStage : MonoBehaviour
{
    [Header("배치 앵커(스테이지 로컬)")]
    [SerializeField] private Transform wagonMount;    // 마차 위치
    [SerializeField] private Transform animalMount;    // 맨 앞줄 동물 위치(마차 앞)

    [Header("동물 편성(마차 앞에 매달기)")]
    [SerializeField] private int animalsPerRow = 2;       // 한 줄 마릿수(2=쌍두)
    [SerializeField] private float animalSideGap = 1.2f;  // 좌우 간격(m)
    [SerializeField] private float animalRowGap = 1.6f;   // 앞뒤 줄 간격(m)
    [SerializeField] private int previewAnimalCount = 2;  // 세이브 없을 때(프리뷰) 마릿수
    [Tooltip("걷기 애니메이션 재생 속도(1=원본, 클수록 빠름).")]
    [SerializeField] private float animWalkSpeed = 1.4f;

    [Header("바닥 정렬")]
    [Tooltip("각 모델을 자기 바운즈 기준으로 바닥(앵커 높이)에 자동으로 앉힌다. 모델마다 독립 계산이라 서로 영향 없음.")]
    [SerializeField] private bool snapToGround = true;

    [Header("프리뷰")]
    [SerializeField] private bool autoShowOnStart = false;   // 프리뷰 씬: 시작 시 표시
    [SerializeField] private string autoShowKey = "1";

    private const string ModelPrefix = "TMModel_";
    private readonly List<GameObject> spawned = new List<GameObject>();
    // 관절식 조향: 말(앞)과 마차(뒤)를 별도 피벗으로 담아 각각 다른 각도로 튼다(연결점=원점 근처).
    private Transform caravanRoot, animalRoot, wagonRoot;

    private Transform EnsureChild(ref Transform cache, Transform parent, string name)
    {
        if (cache == null)
        {
            var t = parent.Find(name);
            if (t == null) { var go = new GameObject(name); go.transform.SetParent(parent, false); t = go.transform; }
            cache = t;
        }
        return cache;
    }
    private Transform CaravanRoot() => EnsureChild(ref caravanRoot, transform, "CaravanRoot");
    private Transform AnimalRoot()  => EnsureChild(ref animalRoot, CaravanRoot(), "AnimalRoot");
    private Transform WagonRoot()   => EnsureChild(ref wagonRoot, CaravanRoot(), "WagonRoot");

    /// <summary>관절식 회피: 말(앞)은 animal*, 마차(뒤)는 wagon*로 각각 조향. 연결점은 원점.</summary>
    public void SetSteerArticulated(float animalLat, float animalYaw, float wagonLat, float wagonYaw)
    {
        var a = AnimalRoot(); a.localPosition = new Vector3(animalLat, 0f, 0f); a.localRotation = Quaternion.Euler(0f, animalYaw, 0f);
        var w = WagonRoot();  w.localPosition = new Vector3(wagonLat, 0f, 0f);  w.localRotation = Quaternion.Euler(0f, wagonYaw, 0f);
    }
    /// <summary>하위호환(리지드): 말·마차 같은 값.</summary>
    public void SetSteer(float lateralX, float yawDeg) => SetSteerArticulated(lateralX, yawDeg, lateralX, yawDeg);

    private string currentKey;      // 현재 표시 중인 캐러밴 key(이동 여부 판정용)
    private TreadmillRoad road;      // 자식 길(스크롤 제어)
    private bool routeFed;           // 이 캐러밴 실제 루트 지형을 길에 반영했는지

    // 게임 공용 중앙 카탈로그(Resources). 모델 목록의 유일한 출처 — 스테이지는 참조만 한다.
    private ND.Framework.SandboxSharedGameDataCatalog catalog;
    private ND.Framework.SandboxSharedGameDataCatalog Catalog =>
        catalog != null ? catalog
                         : (catalog = Resources.Load<ND.Framework.SandboxSharedGameDataCatalog>(
                               ND.Framework.SandboxSharedGameDataCatalog.ResourceName));

    private void Start()
    {
        if (autoShowOnStart) ShowCaravan(autoShowKey);
    }

    // 캐러밴이 '이동 중(Traveling)'일 때만 길 스크롤·동물 걷기·덜컹거림을 켠다.
    // 출발 전(Prepare)·정산(Settling) 등에는 정지 상태로 서 있는다.
    private void Update()
    {
        if (string.IsNullOrEmpty(currentKey)) return;   // 표시 중 아님
        bool traveling = IsCaravanTraveling(currentKey);

        // 길 찾기: 자식에 없으면(프리뷰는 Stage·Road가 형제 루트) 씬 전체에서 찾는다.
        // 레인 안이면 그 레인의 길만(다른 레인 길과 안 섞이게).
        if (road == null) { var lane = TreadmillLane.Of(this); if (lane != null) { lane.Resolve(); road = lane.road; } }
        if (road == null) road = GetComponentInChildren<TreadmillRoad>(true);
        if (road == null) road = Object.FindAnyObjectByType<TreadmillRoad>(FindObjectsInactive.Include);
        if (road != null) road.SetScrollEnabled(traveling);
        // 동물 걷기·덜컹은 길이 '실제로' 흐르는지로 판단(디버그 강제스크롤도 함께 반영).
        bool moving = road != null ? road.IsScrolling : traveling;

        // 이동을 시작하면(루트 확정) 실제 지나는 그리드 지형을 길에 반영(한 번).
        if (moving && !routeFed && road != null)
        {
            string realRoute = TreadmillRouteSampler.SampleRouteTerrain(currentKey);
            if (!string.IsNullOrEmpty(realRoute)) { road.SetRoute(realRoute); routeFed = true; }
        }

        for (int i = 0; i < spawned.Count; i++)
        {
            var go = spawned[i];
            if (go == null) continue;
            var an = go.GetComponentInChildren<Animator>(true);
            if (an != null) an.speed = moving ? animWalkSpeed : 0f;   // 걷기 재생(속도 조절)/정지
            var jo = go.GetComponentInChildren<TreadmillJostle>(true);
            if (jo != null && jo.enabled != moving) jo.enabled = moving;   // 덜컹거림 on/off
        }
    }

    /// <summary>지금 표시 중인 캐러밴 키(슬롯 "1"~"4" 또는 caravanId). 없으면 빈 문자열.</summary>
    public string CurrentCaravanKey => currentKey ?? "";

    /// <summary>표시 중 캐러밴의 실제 caravanId를 해석(진행도·날씨 동기화용). 없으면 "".</summary>
    public string CurrentCaravanId()
    {
        if (string.IsNullOrEmpty(currentKey)) return "";
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null) return "";
        bool isSlot = int.TryParse(currentKey, out int slot);
        foreach (var c in save.caravans)
        {
            if (c == null) continue;
            bool match = isSlot ? ((c.slotIndex + 1) == slot || c.slotIndex == slot) : (c.caravanId == currentKey);
            if (match) return c.caravanId;
        }
        return "";
    }

    // 해당 캐러밴이 이동 중인지(state == Traveling). enum 참조 없이 이름으로 비교.
    private bool IsCaravanTraveling(string key)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null) return false;
        bool isSlot = int.TryParse(key, out int slot);
        foreach (var c in save.caravans)
        {
            if (c == null) continue;
            bool match = isSlot ? ((c.slotIndex + 1) == slot || c.slotIndex == slot) : (c.caravanId == key);
            if (match) return c.state.ToString() == "Traveling";
        }
        return false;
    }

    [ContextMenu("씬에 미리보기 생성 (마차+동물)")]
    private void BuildPreviewInScene() => ShowCaravan(autoShowKey);

    [ContextMenu("씬 미리보기 지우기")]
    private void ClearPreviewInScene() => Clear();

    /// <summary>caravanKey(슬롯번호 "1"~"4" 또는 caravanId)의 마차/동물을 중앙 카탈로그에서 찾아 세운다.</summary>
    public void ShowCaravan(string caravanKey)
    {
        Clear();

        string wagonName = null;
        var animalTypes = new List<DraftAnimalType>();

        // 세이브데이터에서 이 캐러밴이 무엇을 쓰는지 읽는다(스테이지는 결정하지 않음).
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save != null && save.caravans != null)
        {
            // caravanKey가 숫자면 슬롯 라벨("1"~"4"), 아니면 실제 caravanId로 본다.
            // (실제 id일 땐 슬롯매칭을 쓰면 안 됨 — 파싱 실패로 slot=0이 되어 slotIndex 0 캐러밴이 오매칭됨)
            bool isSlot = int.TryParse(caravanKey, out int slot);
            foreach (var c in save.caravans)
            {
                if (c == null) continue;
                bool match = isSlot
                    ? ((c.slotIndex + 1) == slot || c.slotIndex == slot)   // 슬롯 라벨로 지정
                    : (c.caravanId == caravanKey);                          // 실제 caravanId로 지정
                if (!match) continue;
                if (c.wagon != null) wagonName = c.wagon.wagonName;
                if (c.animals != null) foreach (var a in c.animals) if (a != null) animalTypes.Add(a.animalType);
                break;
            }
        }

        // 마차: 중앙 카탈로그에서 ID로 찾아 그 .Prefab을 세운다(비면 기본 마차).
        GameObject wagonPrefab = ResolveWagonPrefab(wagonName);
        Vector3 wagonPos = wagonMount != null ? wagonMount.localPosition : Vector3.zero;
        if (wagonPrefab != null) SpawnLocal(wagonPrefab, wagonPos, WagonRoot());

        // 동물: 마릿수만큼 편성. 세이브에 동물이 없으면 프리뷰 마릿수로 폴백(빈 화면 방지).
        int count = animalTypes.Count > 0 ? animalTypes.Count : Mathf.Max(0, previewAnimalCount);
        Vector3 basePos = animalMount != null ? animalMount.localPosition : new Vector3(0f, 0f, 2f);
        int perRow = Mathf.Max(1, animalsPerRow);
        for (int i = 0; i < count; i++)
        {
            bool hasType = i < animalTypes.Count;
            DraftAnimalType type = hasType ? animalTypes[i] : default(DraftAnimalType);
            GameObject prefab = ResolveAnimalPrefab(hasType, type);
            if (prefab == null) continue;

            int row = i / perRow;
            int col = i % perRow;
            int inRow = Mathf.Min(perRow, count - row * perRow);      // 이 줄의 실제 마릿수(홀수면 마지막 줄만 적음)
            float x = (col - (inRow - 1) * 0.5f) * animalSideGap;      // 그 줄 기준 중앙 정렬 → 남는 1마리는 가운데
            float z = basePos.z + row * animalRowGap;
            SpawnLocal(prefab, new Vector3(basePos.x + x, basePos.y, z), AnimalRoot());
        }

        currentKey = caravanKey;   // 이동 여부 판정 대상
        routeFed = false;          // 새로 표시 → 실제 루트 지형 다시 반영
        Debug.Log("[Treadmill] 표시 — key=" + caravanKey + " wagon=" + (wagonName ?? "(기본)") + " 동물=" + count + "마리");
    }

    /// <summary>디버그: 세이브 무시하고 지정한 마차 + 동물(종류·마릿수)을 직접 세운다.</summary>
    public void ShowCustom(string wagonId, DraftAnimalType animalType, int count)
    {
        Clear();
        GameObject wagonPrefab = ResolveWagonPrefab(wagonId);
        Vector3 wagonPos = wagonMount != null ? wagonMount.localPosition : Vector3.zero;
        if (wagonPrefab != null) SpawnLocal(wagonPrefab, wagonPos, WagonRoot());

        Vector3 basePos = animalMount != null ? animalMount.localPosition : new Vector3(0f, 0f, 2f);
        int perRow = Mathf.Max(1, animalsPerRow);
        for (int i = 0; i < Mathf.Max(0, count); i++)
        {
            GameObject prefab = ResolveAnimalPrefab(true, animalType);
            if (prefab == null) continue;
            int row = i / perRow; int col = i % perRow;
            int inRow = Mathf.Min(perRow, count - row * perRow);      // 이 줄의 실제 마릿수
            float x = (col - (inRow - 1) * 0.5f) * animalSideGap;      // 그 줄 기준 중앙 정렬 → 남는 1마리는 가운데
            float z = basePos.z + row * animalRowGap;
            SpawnLocal(prefab, new Vector3(basePos.x + x, basePos.y, z), AnimalRoot());
        }
        currentKey = "debug";   // Update가 돌게(동물 걷기 판정). 디버그 스크롤로 걷는다.
        routeFed = true;        // 디버그는 실제 루트 샘플 생략
    }

    /// <summary>세워둔 모델만 제거(마운트 등 다른 자식 보존). 에디트/런타임 안전.</summary>
    public void Clear()
    {
        currentKey = null;   // 정지 판정도 멈춤
        spawned.Clear();
        // 말·마차 피벗 아래 모델 제거 + 조향 초기화
        ClearRoot(AnimalRoot());
        ClearRoot(WagonRoot());
        // 레거시(직접 자식) 모델도 정리
        var cr = CaravanRoot();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            if (ch != null && ch != cr && ch.name.StartsWith(ModelPrefix)) DestroyObj(ch.gameObject);
        }
    }

    // ── 내부 ──

    private GameObject SpawnLocal(GameObject prefab, Vector3 localPos, Transform parent)
    {
        GameObject go;
#if UNITY_EDITOR
        // 에디트 미리보기는 '프리팹 연결 인스턴스'로 생성 → 프리팹을 고치면 즉시 반영(실시간).
        if (!Application.isPlaying)
            go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
        else
#endif
            go = Instantiate(prefab, parent);   // 런타임(게임)은 일반 인스턴스
        go.name = ModelPrefix + prefab.name;
        go.transform.localPosition = localPos;     // 회전·크기·재질·Animator는 프리팹 소관
        if (snapToGround) SnapBaseToGround(go, localPos);
        spawned.Add(go);
        return go;
    }

    // 모델의 렌더러 바운즈 아래끝을 앵커 높이에 맞춰 바닥에 앉힌다(모델마다 독립 자동 보정).
    private void SnapBaseToGround(GameObject go, Vector3 localPos)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float targetBaseY = transform.TransformPoint(localPos).y;   // 앵커의 월드 높이(=길 표면)
        go.transform.position += Vector3.up * (targetBaseY - b.min.y);
    }

    /// <summary>중앙 카탈로그의 Wagons에서 wagonName(WagonId/DisplayName)으로 찾아 .Prefab 반환.</summary>
    private GameObject ResolveWagonPrefab(string wagonName)
    {
        var cat = Catalog;
        if (cat == null) return null;
        var wagons = cat.Wagons;
        if (!string.IsNullOrEmpty(wagonName))
            foreach (var w in wagons)
                if (w != null && (w.WagonId == wagonName || w.DisplayName == wagonName) && w.Prefab != null)
                    return w.Prefab;
        foreach (var w in wagons) if (w != null && w.Prefab != null) return w.Prefab;   // 폴백
        return null;
    }

    /// <summary>중앙 카탈로그의 DraftAnimals에서 animalType으로 찾아 .Prefab 반환.</summary>
    private GameObject ResolveAnimalPrefab(bool hasAnimal, DraftAnimalType animalType)
    {
        var cat = Catalog;
        if (cat == null) return null;
        var animals = cat.DraftAnimals;
        if (hasAnimal)
            foreach (var a in animals)
                if (a != null && a.AnimalType == animalType && a.Prefab != null)
                    return a.Prefab;
        foreach (var a in animals) if (a != null && a.Prefab != null) return a.Prefab;   // 폴백
        return null;
    }

    // 피벗의 자식 모델 제거 + 조향 초기화.
    private void ClearRoot(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--) DestroyObj(root.GetChild(i).gameObject);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
    }

    private static void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
