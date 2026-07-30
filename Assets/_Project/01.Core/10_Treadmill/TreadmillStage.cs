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

    [Header("바닥 정렬")]
    [Tooltip("각 모델을 자기 바운즈 기준으로 바닥(앵커 높이)에 자동으로 앉힌다. 모델마다 독립 계산이라 서로 영향 없음.")]
    [SerializeField] private bool snapToGround = true;

    [Header("프리뷰")]
    [SerializeField] private bool autoShowOnStart = false;   // 프리뷰 씬: 시작 시 표시
    [SerializeField] private string autoShowKey = "1";

    private const string ModelPrefix = "TMModel_";
    private readonly List<GameObject> spawned = new List<GameObject>();

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
        bool moving = IsCaravanTraveling(currentKey);

        if (road == null) road = GetComponentInChildren<TreadmillRoad>(true);
        if (road != null) road.SetScrollEnabled(moving);

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
            if (an != null) an.speed = moving ? 1f : 0f;          // 걷기 재생/정지
            var jo = go.GetComponentInChildren<TreadmillJostle>(true);
            if (jo != null && jo.enabled != moving) jo.enabled = moving;   // 덜컹거림 on/off
        }
    }

    // 해당 캐러밴이 이동 중인지(state == Traveling). enum 참조 없이 이름으로 비교.
    private bool IsCaravanTraveling(string key)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null) return false;
        int slot; int.TryParse(key, out slot);
        foreach (var c in save.caravans)
        {
            if (c == null) continue;
            if (c.caravanId == key || (c.slotIndex + 1) == slot || c.slotIndex == slot)
                return c.state.ToString() == "Traveling";
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
            int slot; int.TryParse(caravanKey, out slot);
            foreach (var c in save.caravans)
            {
                if (c == null) continue;
                bool match = c.caravanId == caravanKey || (c.slotIndex + 1) == slot || c.slotIndex == slot;
                if (!match) continue;
                if (c.wagon != null) wagonName = c.wagon.wagonName;
                if (c.animals != null) foreach (var a in c.animals) if (a != null) animalTypes.Add(a.animalType);
                break;
            }
        }

        // 마차: 중앙 카탈로그에서 ID로 찾아 그 .Prefab을 세운다.
        GameObject wagonPrefab = ResolveWagonPrefab(wagonName);
        Vector3 wagonPos = wagonMount != null ? wagonMount.localPosition : Vector3.zero;
        if (wagonPrefab != null) SpawnLocal(wagonPrefab, wagonPos);

        // 동물: 마릿수만큼 마차 앞에 편성(세이브 없으면 프리뷰 마릿수).
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
            float x = (col - (perRow - 1) * 0.5f) * animalSideGap;
            float z = basePos.z + row * animalRowGap;
            SpawnLocal(prefab, new Vector3(basePos.x + x, basePos.y, z));
        }

        currentKey = caravanKey;   // 이동 여부 판정 대상
        routeFed = false;          // 새로 표시 → 실제 루트 지형 다시 반영
        Debug.Log("[Treadmill] 표시 — key=" + caravanKey + " wagon=" + (wagonName ?? "(기본)") + " 동물=" + count + "마리");
    }

    /// <summary>세워둔 모델만 제거(마운트 등 다른 자식 보존). 에디트/런타임 안전.</summary>
    public void Clear()
    {
        currentKey = null;   // 정지 판정도 멈춤
        spawned.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            if (ch != null && ch.name.StartsWith(ModelPrefix)) DestroyObj(ch.gameObject);
        }
    }

    // ── 내부 ──

    private GameObject SpawnLocal(GameObject prefab, Vector3 localPos)
    {
        GameObject go;
#if UNITY_EDITOR
        // 에디트 미리보기는 '프리팹 연결 인스턴스'로 생성 → 프리팹을 고치면 즉시 반영(실시간).
        if (!Application.isPlaying)
            go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
        else
#endif
            go = Instantiate(prefab, transform);   // 런타임(게임)은 일반 인스턴스
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

    private static void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
