// =============================================================================
// VillageBuildingRegistry — 마을 건물 등록소 (종류별 유일 + 레벨 + 하이라이트)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 거점 마을 씬(Village_Home)의 건물을 관리한다. 건물은 종류별로 하나씩만
//        존재하며, 카탈로그에서 같은 종류를 다시 고르면 새로 짓지 않고 레벨을 올린다.
//        씬 분리(RenderTexture) 때문에 static Instance(싱글톤)로 UI가 접근.
//
// [레벨] 지어진 건물 = Lv.1 이상, 아직 없는 종류 = Lv.0.
//        AddOrUpgrade: 이미 있으면 레벨업, 없으면 새로 지음(Lv.1).
//
// [저장] FrameworkRoot.CurrentSaveData.player.villageBuildings에 displayName+level로 기록한다.
//        FrameworkRoot가 없으면 씬 로컬만 동작(테스트 씬 폴백).
//        키는 카탈로그 displayName이며, 건물 종류 한정·표시명 고정 전제이다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using ND.Framework;

/// <summary>마을 건물(종류별 유일+레벨) 관리 + 하이라이트 + 카탈로그. UI가 싱글톤 접근.</summary>
public class VillageBuildingRegistry : MonoBehaviour
{
    public static VillageBuildingRegistry Instance { get; private set; }

    /// <summary>현재 지어진 건물 한 채.</summary>
    [System.Serializable]
    public class Building
    {
        public string displayName;
        public Renderer renderer;
        public int level = 1;
        
        
        // [BuildData 외형 연동]
        // 증축 시 배치 좌표를 가진 루트는 유지하고 시각 자식만 교체하기 위한 런타임 전용 상태다.
        // 기존 Scene/Prefab 직렬화 계약을 바꾸지 않도록 NonSerialized로 둔다.
        [System.NonSerialized] public GameObject instanceRoot;
        
        // 다중 Renderer 외형의 하이라이트와 원색 복원을 위한 런타임 캐시다.
        [System.NonSerialized] public Renderer[] renderers;
        [System.NonSerialized] public Color[] originalColors;
[System.NonSerialized] public bool usesRuntimeRoot;
        [HideInInspector] public Color originalColor;
    }

    /// <summary>카탈로그(건물 종류) 한 항목.</summary>
    [System.Serializable]
    public class CatalogEntry
    {
        public string displayName;
        public GameObject prefab;   // 비면 큐브 폴백
        // 건설 Popup과 실제 비용 처리가 같은 레벨별 요구조건을 사용하도록 BuildData 원본을 직접 연결한다.
        // 기존 displayName 기반 SaveData 계약은 이번 작업에서 유지하므로 displayName과 함께 보관한다.
        public BuildData buildData;
        // 카테고리 구분: 이 항목이 '환경 아이템'인지. 건물추가 UI가 '건물'/'환경' 탭을 나눌 때만 쓴다.
        // (환경 아이템은 건물과 달리 다중 설치·레벨 없음·거래재화 비용 파이프라인을 탄다.)
        public bool isEnvironment;
        // 환경 아이템 1개를 지을 때 소모하는 거래재화(tradingCurrency) 비용이다.
        // 건물은 아이템(requireItems)으로 짓지만 환경은 재화로 짓는 첫 케이스라 여기에 종류별 값 하나만 둔다.
        // isEnvironment가 false인 건물 항목에서는 사용하지 않는다.
        public long envCost;
    }

    [SerializeField] private List<Building> buildings = new List<Building>();
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] private GameObject fallbackPrefab;
    [SerializeField] private List<CatalogEntry> catalog = new List<CatalogEntry>();

    // 저장 복원 중인지. 복원 중 새로 만든 건물은 '유저 신축'이 아니므로 화면 중앙 포커스 대상에서 제외한다.
    private bool isRestoringFromSave;

    // ── 지어진 건물 ──
    public int Count => buildings.Count;
    public string GetName(int index) =>
        (index >= 0 && index < buildings.Count) ? buildings[index].displayName : string.Empty;
    public int GetLevel(int index) =>
        (index >= 0 && index < buildings.Count) ? buildings[index].level : 0;

    // ── 카탈로그(종류) ──
    public int CatalogCount => catalog.Count;
    public string GetCatalogName(int index) =>
        (index >= 0 && index < catalog.Count) ? catalog[index].displayName : string.Empty;
    /// <summary>이 카탈로그 항목이 '환경 아이템'인지(건물추가 UI의 건물/환경 탭 분리용).</summary>
    public bool GetCatalogIsEnvironment(int index) =>
        index >= 0 && index < catalog.Count && catalog[index] != null && catalog[index].isEnvironment;
    /// <summary>이 종류의 현재 레벨(안 지어졌으면 0).</summary>
    public int GetCatalogLevel(int index)
    {
        if (index < 0 || index >= catalog.Count) return 0;
        Building b = FindByName(catalog[index].displayName);
        return b != null ? b.level : 0;
    }

    /// <summary>
    /// 카탈로그 UI가 선택한 index에 대응하는 BuildData를 반환한다.
    /// BuildingAddPopup은 이 값과 현재 레벨을 Detail Popup에 전달하며 여기서는 상태를 변경하지 않는다.
    /// 잘못된 index, null 항목 또는 미연결 항목은 의도하지 않은 무료 건설로 우회하지 않고 null로 처리한다.
    /// </summary>
    public BuildData GetCatalogBuildData(int index)
    {
        if(index < 0 || index >= catalog.Count)
        {
            return null;
        }

        CatalogEntry entry = catalog[index];
        return entry != null ? entry.buildData : null;
    }

    /// <summary>
    /// Popup 최종 확인 이벤트의 buildId로 카탈로그 항목을 찾는다.
    /// BuildData는 다음 레벨 비용 조회에, displayName은 기존 SaveData 및 씬 반영 API에 사용한다.
    /// 즉, 신규 요청 식별자는 buildId를 사용하되 기존 displayName 저장 계약과 연결하는 호환 경계다.
    /// 동일 buildId가 중복 등록된 경우 모호한 요청이므로 실패한다.
    /// </summary>
    public bool TryGetCatalogEntry(string buildId, out BuildData buildData, out string displayName)
    {
        buildData = null;
        displayName = string.Empty;

        if (string.IsNullOrEmpty(buildId))
        {
            return false;
        }

        CatalogEntry found = null;

        foreach (CatalogEntry entry in catalog)
        {
            if(entry == null || entry.buildData == null)
            {
                continue;
            }

            if(!string.Equals(entry.buildData.BuildId, buildId, System.StringComparison.Ordinal))
            {
                continue;
            }

            if(found != null)
            {
                Debug.LogError($"Village building catalog has duplicate buildId '{buildId}'.", this);
                return false;
            }

            found = entry;
        }

        if(found == null || found.buildData == null || string.IsNullOrWhiteSpace(found.displayName))
        {
            return false;
        }

        buildData = found.buildData;
        displayName = found.displayName;
        return true;
    }

    /// <summary>
    /// buildId로 '환경 아이템' 카탈로그 항목을 찾아 BuildData·displayName·거래재화 비용을 반환한다.
    /// isEnvironment=false인 건물 항목은 무시한다(환경 전용 건설 경로에서만 사용).
    /// 동일 buildId 중복은 모호한 요청이므로 실패한다.
    /// </summary>
    public bool TryGetCatalogEnvironmentEntry(string buildId, out BuildData buildData, out string displayName, out long envCost)
    {
        buildData = null;
        displayName = string.Empty;
        envCost = 0;

        if (string.IsNullOrEmpty(buildId)) return false;

        CatalogEntry found = null;
        foreach (CatalogEntry entry in catalog)
        {
            if (entry == null || entry.buildData == null || !entry.isEnvironment) continue;   // 환경 항목만
            if (!string.Equals(entry.buildData.BuildId, buildId, System.StringComparison.Ordinal)) continue;

            if (found != null)
            {
                Debug.LogError($"Village environment catalog has duplicate buildId '{buildId}'.", this);
                return false;
            }
            found = entry;
        }

        if (found == null || string.IsNullOrWhiteSpace(found.displayName)) return false;

        buildData = found.buildData;
        displayName = found.displayName;
        envCost = found.envCost;
        return true;
    }

    private void Awake()
    {
        Instance = this;
        foreach (Building b in buildings)
        {
            if (b.level < 1) b.level = 1;   // 지어진 건물은 최소 Lv.1
            if (b.renderer != null && b.renderer.sharedMaterial != null)
                b.originalColor = b.renderer.sharedMaterial.color;
        }
    }

    // FrameworkRoot·SaveData가 준비된 뒤 거점 건물 진행을 복원한다.
    private void Start()
    {
        StripEnvironmentBuildingSaveEntries();   // 과거 잘못 저장된 '환경' 건물 항목 제거(마이그레이션, 팀원 세이브 치유).
        SeedMissingBuildingsToSave();   // 뉴게임: SaveData에 없는 거점 건물을 채운다(이미 있으면 보존). 이동 저장의 전제.
        RestoreFromSaveData();
        BuildingPlacementController placementController =
            FindAnyObjectByType<BuildingPlacementController>();
        if (placementController != null)
            placementController.RestoreAndRegisterExistingBuildings();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Building FindByName(string name)
    {
        foreach (Building b in buildings)
            if (b.displayName == name) return b;
        return null;
    }

    /// <summary>
    /// Registry가 소유한 runtime 건물과 저장 키를 연결한다.
    /// 반환되는 문자열은 내부 목록이 보유한 displayName 참조이며 호출자가 변경할 수 없다.
    /// </summary>
    public bool TryGetDisplayName(PlaceableBuilding placeable, out string displayName)
    {
        displayName = string.Empty;
        if (placeable == null) return false;

        foreach (Building building in buildings)
        {
            if (building == null || building.renderer == null) continue;
            if (building.renderer.transform.IsChildOf(placeable.transform)
                || placeable.transform.IsChildOf(building.renderer.transform))
            {
                displayName = building.displayName;
                return !string.IsNullOrWhiteSpace(displayName);
            }
        }

        return false;
    }

    private CatalogEntry FindCatalogByName(string displayName)
    {
        foreach (CatalogEntry entry in catalog)
            if (entry != null && entry.displayName == displayName) return entry;
        return null;
    }

    /// <summary>
    /// SaveData.player.villageBuildings를 읽어 씬 건물 레벨을 맞춘다.
    /// FrameworkRoot가 없으면 아무 것도 하지 않는다(테스트 씬 폴백).
    /// </summary>
    private void RestoreFromSaveData()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.CurrentSaveData == null || root.CurrentSaveData.player == null)
            return;

        List<VillageBuildingSaveData> savedBuildings = root.CurrentSaveData.player.villageBuildings;
        if (savedBuildings == null) return;

        // 저장된 항목마다 "씬 반영"만 수행 — 저장은 이미 확정된 상태이므로 다시 쓰지 않는다.
        // 복원 중 새로 만든 건물은 유저 신축이 아니므로 화면 중앙 포커스를 하지 않는다.
        isRestoringFromSave = true;
        try
        {
            foreach (VillageBuildingSaveData saved in savedBuildings)
            {
                if (saved == null) continue;
                if (IsEnvironmentCatalogByName(saved.displayName)) continue;   // 환경은 건물 경로로 복원하지 않음(다중·삭제는 환경 매니저 담당).
                ApplySavedBuildingLevel(saved.displayName, saved.level);
            }
        }
        finally
        {
            isRestoringFromSave = false;
        }
    }

    /// <summary>
    /// 이미 저장이 확정된 건물 레벨을 <b>씬에만</b> 반영한다.
    /// (건설 Command가 SaveResult.Succeeded를 반환한 뒤 UI가 호출)
    ///
    /// [계약] Progression 요청 문서(0720_Progression_Requested_All_Teams) 기준:
    ///  - prefab 생성 또는 기존 씬 건물의 level 적용만 수행한다.
    ///  - SaveData.player.villageBuildings를 다시 변경하지 않고 Save()도 호출하지 않는다.
    ///  - 씬 반영에 실패해도 이미 성공한 저장 데이터를 역변경하지 않는다.
    ///    (재진입 시 RestoreFromSaveData()가 복구한다)
    ///
    /// [주의] 레벨을 "증가(++)"시키지 않고 targetLevel로 "설정"한다.
    ///        AddOrUpgrade를 Command 성공 후 호출하면 레벨이 중복 증가하므로 금지.
    /// </summary>
    /// <param name="displayName">건물 종류 키(카탈로그 displayName과 일치).</param>
    /// <param name="targetLevel">저장에 확정된 목표 레벨(1 이상).</param>
public void ApplySavedBuildingLevel(string displayName, int targetLevel)
    {
        if (string.IsNullOrEmpty(displayName) || targetLevel < 1) return;

        CatalogEntry catalogEntry = FindCatalogByName(displayName);
        DataPerLevel levelData = ResolveLevelData(catalogEntry, targetLevel);
        if (catalogEntry != null && catalogEntry.buildData != null && levelData == null)
        {
            // 외형 없이 레벨만 적용되는 부분 성공을 막는 Registry 최종 방어다.
            Debug.LogError($"Building appearance is missing. name={displayName}, level={targetLevel}.", this);
            return;
        }

        Building existing = FindByName(displayName);
        if (existing != null)
        {
            bool appearanceChanged = ReplaceBuildingAppearance(existing, levelData);
            if (existing.usesRuntimeRoot && !appearanceChanged) return;

            existing.level = targetLevel;
            if (appearanceChanged) RefreshPlacementRegistration();
            return;
        }

        BuildNew(ResolveRuntimeRootPrefab(catalogEntry), levelData, displayName);
        Building built = FindByName(displayName);
        if (built != null) built.level = targetLevel;

        // 유저가 방금 지은 새 건물이면(복원 중이 아니면) 화면 중앙으로 옮기고 편집모드 선택으로 포커스한다.
        // 다른 마을/늦게 로드된 기존 건물은 이 경로를 타지 않으므로 폴링 오작동(엉뚱한 건물 이동)이 없다.
        if (!isRestoringFromSave && built != null && built.instanceRoot != null)
        {
            BuildingPlacementController controller = FindAnyObjectByType<BuildingPlacementController>();
            if (controller != null) controller.FocusNewlyBuilt(built.instanceRoot.transform);
        }
    }

    /// <summary>
    /// 건물 진행을 SaveData에 upsert한다.
    /// FrameworkRoot가 없으면 저장하지 않는다.
    /// </summary>
    private void WriteBuildingToSave(string displayName, int level)
    {
        if (string.IsNullOrEmpty(displayName) || level < 1) return;

        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.CurrentSaveData == null || root.CurrentSaveData.player == null)
            return;

        if (root.CurrentSaveData.player.villageBuildings == null)
            root.CurrentSaveData.player.villageBuildings = new List<VillageBuildingSaveData>();

        List<VillageBuildingSaveData> savedBuildings = root.CurrentSaveData.player.villageBuildings;
        for (int i = 0; i < savedBuildings.Count; i++)
        {
            VillageBuildingSaveData entry = savedBuildings[i];
            if (entry == null || entry.displayName != displayName) continue;
            entry.level = level;
            return;
        }

        savedBuildings.Add(new VillageBuildingSaveData
        {
            displayName = displayName,
            level = level
        });
    }

    /// <summary>
    /// 카탈로그의 거점 건물 중 SaveData에 아직 '없는' 것만 채워 넣는다(뉴게임 시딩).
    /// 이미 있는 건물은 절대 건드리지 않아(레벨·배치 보존) 매 로드마다 호출해도 안전하다.
    /// [이유] 미리 씬에 놓인 거점 건물은 지금껏 SaveData에 등록된 적이 없어, 이동 시 저장 계약이
    ///        displayName으로 못 찾아 BuildingNotFound가 났다. 여기서 한 번 채워주면 이동이 저장까지 된다.
    /// </summary>
    private void SeedMissingBuildingsToSave()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.CurrentSaveData == null || root.CurrentSaveData.player == null)
            return;

        if (root.CurrentSaveData.player.villageBuildings == null)
            root.CurrentSaveData.player.villageBuildings = new List<VillageBuildingSaveData>();
        List<VillageBuildingSaveData> saved = root.CurrentSaveData.player.villageBuildings;

        foreach (Building b in buildings)
        {
            if (b == null || string.IsNullOrEmpty(b.displayName) || b.level < 1) continue;
            if (IsEnvironmentCatalogByName(b.displayName)) continue;   // 환경은 건물 저장(villageBuildings) 대상이 아님.

            // 이미 저장돼 있으면(로드된 게임) 건너뛴다 — 레벨·좌표 보존
            bool exists = false;
            for (int i = 0; i < saved.Count; i++)
                if (saved[i] != null && saved[i].displayName == b.displayName) { exists = true; break; }
            if (exists) continue;

            // 뉴게임(또는 누락): displayName+level만 등록(hasPlacement=false → authored 위치 사용, 첫 이동 시 좌표 저장됨)
            saved.Add(new VillageBuildingSaveData { displayName = b.displayName, level = b.level });
        }
    }

    /// <summary>index 건물만 강조색, 나머지는 원래 색.</summary>
public void Highlight(int index)
    {
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            CacheBuildingRenderers(building);
            if (building.renderers == null) continue;

            for (int rendererIndex = 0; rendererIndex < building.renderers.Length; rendererIndex++)
            {
                Renderer renderer = building.renderers[rendererIndex];
                if (renderer == null) continue;
                renderer.material.color = i == index
                    ? highlightColor
                    : GetOriginalColor(building, rendererIndex);
            }
        }
    }

    /// <summary>모든 건물 원래 색으로.</summary>
public void ClearHighlight()
    {
        foreach (Building building in buildings)
        {
            CacheBuildingRenderers(building);
            if (building.renderers == null) continue;

            for (int rendererIndex = 0; rendererIndex < building.renderers.Length; rendererIndex++)
            {
                Renderer renderer = building.renderers[rendererIndex];
                if (renderer != null)
                    renderer.material.color = GetOriginalColor(building, rendererIndex);
            }
        }
    }

    /// <summary>
    /// 카탈로그 종류를 짓거나(없으면 Lv.1) 레벨을 올린다(이미 있으면). 씬 + SaveData 둘 다 변경.
    ///
    /// ⚠ [레거시 경로] 비용 검증 없이 바로 올리는 그레이박스 시절 경로다.
    ///    건설 Command(CaravanBuildingConstructionCommand) 도입 후에는
    ///    <b>Command 성공 뒤에 이 메서드를 호출하면 안 된다</b> — 레벨이 중복 증가하고
    ///    SaveData를 두 번 쓰게 된다. 그 경우엔 ApplySavedBuildingLevel()을 쓸 것.
    /// </summary>
public void AddOrUpgrade(int catalogIndex)
    {
        if (catalogIndex < 0 || catalogIndex >= catalog.Count) return;
        CatalogEntry entry = catalog[catalogIndex];
        Building existing = FindByName(entry.displayName);
        int targetLevel = existing != null ? existing.level + 1 : 1;
        DataPerLevel levelData = ResolveLevelData(entry, targetLevel);

        if (entry.buildData != null && levelData == null)
        {
            // 누락 prefab이면 외형·레벨·SaveData를 모두 그대로 유지한다.
            Debug.LogError($"Building appearance is missing. name={entry.displayName}, level={targetLevel}.", this);
            return;
        }

        if (existing != null)
        {
            if (!ReplaceBuildingAppearance(existing, levelData)) return;

            existing.level = targetLevel;
            RefreshPlacementRegistration();
            WriteBuildingToSave(existing.displayName, existing.level);
            return;
        }

        BuildNew(ResolveRuntimeRootPrefab(entry), levelData, entry.displayName);
        Building built = FindByName(entry.displayName);
        if (built != null) WriteBuildingToSave(built.displayName, built.level);
    }

private void BuildNew(GameObject rootPrefab, DataPerLevel levelData, string displayName)
    {
        int n = buildings.Count;
        GameObject go = rootPrefab != null
            ? Instantiate(rootPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        go.name = "Building_" + displayName;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);

        float x = -4f + (n % 4) * 2.6f;
        float z = -4f + (n / 4) * 2.6f;
        go.transform.position = new Vector3(x, 0f, z);

        bool usesRuntimeRoot = go.transform.Find("VisualRoot") != null;
        Renderer renderer = usesRuntimeRoot
            ? ApplyAppearanceToRuntimeRoot(go, levelData)
            : go.GetComponentInChildren<Renderer>();

        // 외형을 먼저 넣은 뒤 Bounds를 계산해야 공통 wrapper의 Collider와 footprint가 유효하다.
        if (usesRuntimeRoot)
        {
            RefreshRuntimeRootCollider(go, levelData);
        }

        var building = new Building
        {
            displayName = displayName,
            renderer = renderer,
            level = 1,
            originalColor = renderer != null && renderer.sharedMaterial != null
                ? renderer.sharedMaterial.color
                : Color.white,
            instanceRoot = go,
            usesRuntimeRoot = usesRuntimeRoot
        };

        buildings.Add(building);
        CacheBuildingRenderers(building);
    }

    /// <summary>
    /// 환경 아이템 인스턴스 하나를 생성한다.
    /// 건물과 달리 <b>buildings 목록·villageBuildings 저장을 건드리지 않는다</b>(다중 설치·레벨 없음).
    /// 외형·footprint는 건물과 동일한 BuildData 파이프라인을 재사용하고, PlacedEnvironment 표식을 붙여 반환한다.
    /// 격자 점유·저장은 호출자(BuildingPlacementController/VillageEnvironmentManager)가 담당한다.
    /// </summary>
    /// <param name="envId">환경 종류 키(BuildData.buildId).</param>
    /// <param name="instanceId">이 설치 인스턴스의 고유 ID.</param>
    /// <param name="position">생성 월드 위치(격자 정렬 전 임시 위치).</param>
    public GameObject BuildEnvironmentInstance(string envId, string instanceId, Vector3 position)
    {
        if (string.IsNullOrEmpty(envId) || string.IsNullOrEmpty(instanceId)) return null;

        CatalogEntry entry = FindEnvironmentCatalog(envId);
        if (entry == null)
        {
            Debug.LogError($"Environment build failed: catalog not found. envId={envId}", this);
            return null;
        }

        DataPerLevel levelData = ResolveLevelData(entry, 1);   // 환경은 레벨이 없어 항상 레벨1 데이터를 쓴다.
        if (entry.buildData != null && levelData == null)
        {
            Debug.LogError($"Environment appearance is missing (level 1). envId={envId}", this);
            return null;
        }

        GameObject rootPrefab = ResolveRuntimeRootPrefab(entry);
        GameObject go = rootPrefab != null
            ? Instantiate(rootPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        string shortId = instanceId.Length > 8 ? instanceId.Substring(0, 8) : instanceId;
        go.name = "Env_" + entry.displayName + "_" + shortId;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        go.transform.position = position;

        // 공통 wrapper(Building_RuntimeRoot) 사용 시에만 외형 주입 + Collider/footprint 보정.
        if (go.transform.Find("VisualRoot") != null)
        {
            ApplyAppearanceToRuntimeRoot(go, levelData);
            RefreshRuntimeRootCollider(go, levelData);
        }

        // 인스턴스 식별 표식 부착(이동/삭제/저장 분기용).
        PlacedEnvironment marker = go.GetComponent<PlacedEnvironment>();
        if (marker == null) marker = go.AddComponent<PlacedEnvironment>();
        marker.Initialize(instanceId, envId);
        return go;
    }

    /// <summary>buildId로 '환경 아이템' 카탈로그 항목을 찾는다(없으면 null).</summary>
    private CatalogEntry FindEnvironmentCatalog(string envId)
    {
        foreach (CatalogEntry entry in catalog)
            if (entry != null && entry.isEnvironment && entry.buildData != null
                && string.Equals(entry.buildData.BuildId, envId, System.StringComparison.Ordinal))
                return entry;
        return null;
    }

    /// <summary>displayName이 '환경 아이템' 카탈로그 종류인지. 건물 저장/복원 경로에서 환경을 배제하는 데 쓴다.</summary>
    private bool IsEnvironmentCatalogByName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return false;
        foreach (CatalogEntry entry in catalog)
            if (entry != null && entry.isEnvironment && entry.displayName == displayName) return true;
        return false;
    }

    /// <summary>
    /// 과거(환경 분리 이전) 재현님 건물 경로로 잘못 저장된 '환경' 항목을 villageBuildings에서 제거한다.
    /// 환경은 villageEnvironments에만 저장되어야 하며, villageBuildings에 남아 있으면 팝업이 '최대 레벨'로 막고
    /// 삭제 불가한 건물 인스턴스가 생긴다. 매 로드마다 호출해도 안전(idempotent)하다.
    /// </summary>
    private void StripEnvironmentBuildingSaveEntries()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.CurrentSaveData == null || root.CurrentSaveData.player == null) return;

        List<VillageBuildingSaveData> list = root.CurrentSaveData.player.villageBuildings;
        if (list == null) return;
        list.RemoveAll(e => e != null && IsEnvironmentCatalogByName(e.displayName));
    }


/// <summary>
    /// 실제 생성 외형은 건축 UI와 동일하게 BuildData의 레벨별 prefab만 조회한다.
    /// 월드 배치 루트 선택은 ResolveRuntimeRootPrefab에서 별도로 처리해 외형과 배치 책임을 분리한다.
    /// </summary>
    private DataPerLevel ResolveLevelData(CatalogEntry entry, int targetLevel)
    {
        if (entry == null || entry.buildData == null)
        {
            return null;
        }

        DataPerLevel[] levelDataList = entry.buildData.DataPerLevels;
        for (int i = 0; i < levelDataList.Length; i++)
        {
            DataPerLevel levelData = levelDataList[i];
            if (levelData != null &&
                levelData.level == targetLevel &&
                levelData.buildPrefab != null)
            {
                // Prefab과 같은 레벨의 외형/점유 보정값을 함께 전달한다.
                return levelData;
            }
        }

        return null;
    }

    /// <summary>
    /// BuildData 건물은 fallbackPrefab 슬롯의 공통 Building_RuntimeRoot를 사용한다.
    /// BuildData가 없는 레거시 항목만 catalog.prefab을 생성해 이전 동작을 보존한다.
    /// </summary>
    private GameObject ResolveRuntimeRootPrefab(CatalogEntry entry)
    {
        if (entry != null && entry.buildData == null && entry.prefab != null)
        {
            return entry.prefab;
        }

        return fallbackPrefab;
    }


/// <summary>
    /// 건물의 배치 루트와 위치·회전은 유지하고, 목표 레벨의 시각 모델만 교체한다.
    /// Registry가 보관하는 Renderer도 새 모델로 갱신해 하이라이트와 배치 저장 연결이 끊기지 않게 한다.
    /// </summary>
// [최소 연동 범위]
    // Registry만 실제 생성 인스턴스와 내부 Renderer 참조를 소유하므로 외부 어댑터에서 안전하게 교체할 수 없다.
    // 따라서 생성/복원/증축 진입점은 유지하고, 이 메서드에서 외형과 Renderer 참조만 갱신한다.
    
private bool ReplaceBuildingAppearance(Building building, DataPerLevel levelData)
    {
        if (building == null || levelData == null || levelData.buildPrefab == null)
        {
            return false;
        }

        GameObject root = building.instanceRoot;
        if (root == null && building.renderer != null)
        {
            PlaceableBuilding placeable = building.renderer.GetComponentInParent<PlaceableBuilding>();
            root = placeable != null ? placeable.gameObject : null;
            building.instanceRoot = root;
        }

        if (root == null || root.transform.Find("VisualRoot") == null)
        {
            // 기존 개별 prefab에는 VisualRoot 계약이 없으므로 외형을 임의로 파괴하지 않는다.
            return false;
        }

        Renderer newRenderer = ApplyAppearanceToRuntimeRoot(root, levelData);
        if (newRenderer == null)
        {
            return false;
        }

        
        building.renderers = null;
        building.originalColors = null;
building.renderer = newRenderer;
        
        CacheBuildingRenderers(building);
building.originalColor = newRenderer.sharedMaterial != null
            ? newRenderer.sharedMaterial.color
            : Color.white;
        RefreshRuntimeRootCollider(root, levelData);
        return true;
    }

    /// <summary>
    /// 공통 wrapper의 배치 컴포넌트와 Transform은 유지하고 VisualRoot 자식만 교체한다.
    /// Registry의 Renderer 참조는 반환된 새 외형으로 갱신한다.
    /// </summary>
    private Renderer ApplyAppearanceToRuntimeRoot(GameObject root, DataPerLevel levelData)
    {
        if (root == null || levelData == null || levelData.buildPrefab == null)
        {
            return null;
        }

        Transform visualRoot = root.transform.Find("VisualRoot");
        if (visualRoot == null)
        {
            return null;
        }

        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            GameObject oldVisual = visualRoot.GetChild(i).gameObject;
            Renderer[] oldRenderers = oldVisual.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < oldRenderers.Length; rendererIndex++)
            {
                oldRenderers[rendererIndex].enabled = false;
            }
            Destroy(oldVisual);
        }

        GameObject newVisual = Instantiate(levelData.buildPrefab, visualRoot, false);
        DisableNestedPlacementComponents(newVisual);
        // Prefab이 가진 축 보정(예: BaseCamp X=270)을 보존하고 DataPerLevel 값은 추가 보정으로 적용한다.
        // Wrapper의 배치 Transform은 유지하므로 이동 좌표와 저장 계약에는 영향을 주지 않는다.
        newVisual.transform.localPosition += levelData.visualOffset;
        newVisual.transform.localRotation = Quaternion.Euler(levelData.visualEulerAngles) * newVisual.transform.localRotation;
        newVisual.transform.localScale = Vector3.Scale(newVisual.transform.localScale, levelData.visualScale);
        return newVisual.GetComponentInChildren<Renderer>(true);
    }

    /// <summary>
    /// 외형 프리팹에 포함된 배치용 컴포넌트가 wrapper와 별도 건물로 다시 등록되는 것을 막는다.
    /// Renderer와 애니메이션은 유지하고, 실제 배치/클릭/점유는 공통 wrapper 하나만 담당한다.
    /// </summary>
    private static void DisableNestedPlacementComponents(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        Collider[] nestedColliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < nestedColliders.Length; i++)
        {
            Collider nestedCollider = nestedColliders[i];
            if (nestedCollider == null)
            {
                continue;
            }

            // 외형 Collider는 wrapper의 클릭/점유 범위를 침범하지 않도록 비활성화만 한다.
            nestedCollider.enabled = false;
        }

        PlaceableBuilding[] nestedPlaceables = visual.GetComponentsInChildren<PlaceableBuilding>(true);
        for (int i = 0; i < nestedPlaceables.Length; i++)
        {
            if (nestedPlaceables[i] != null)
            {
                // 외형을 독립 건물로 등록하지 않되 프리팹 구성 자체는 보존한다.
                nestedPlaceables[i].enabled = false;
            }
        }
    }


/// <summary>
    /// 공통 Building_RuntimeRoot의 BoxCollider를 현재 레벨 외형의 로컬 Bounds에 맞춘다.
    /// 개별 레거시 prefab은 VisualRoot 계약이 없어 이 메서드까지 진입하지 않는다.
    /// </summary>
private void RefreshRuntimeRootCollider(GameObject root, DataPerLevel levelData)
    {
        if (root == null)
        {
            return;
        }

        BoxCollider boxCollider = root.GetComponent<BoxCollider>();
        PlaceableBuilding placeable = root.GetComponent<PlaceableBuilding>();
        if (boxCollider == null || placeable == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds localBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                Vector3 localCorner = root.transform.InverseTransformPoint(worldCorner);

                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        if (!hasBounds)
        {
            return;
        }

        boxCollider.center = localBounds.center;
        boxCollider.size = localBounds.size;

        // Collider의 실제 외형 크기를 그리드 CellSize 단위로 올림해 모델이 점유 범위를 벗어나지 않게 한다.
        int automaticCellsX = Mathf.Max(1, Mathf.CeilToInt(localBounds.size.x / VillageGrid.CellSize));
        int automaticCellsZ = Mathf.Max(1, Mathf.CeilToInt(localBounds.size.z / VillageGrid.CellSize));
        // 0은 Bounds 자동 계산, 1 이상은 authored prefab의 명시적 점유 크기를 계승한다.
        int cellsX = levelData != null && levelData.footprintCellsX > 0 ? levelData.footprintCellsX : automaticCellsX;
        int cellsZ = levelData != null && levelData.footprintCellsZ > 0 ? levelData.footprintCellsZ : automaticCellsZ;
        placeable.ConfigureFootprint(cellsX, cellsZ);
    }

/// <summary>
    /// 증축으로 점유 크기가 바뀐 뒤 기존 grid 점유 정보를 새 footprint로 다시 구축한다.
    /// 저장 데이터는 변경하지 않고 현재 저장 좌표를 우선 복원한다.
    /// </summary>
    private void RefreshPlacementRegistration()
    {
        BuildingPlacementController placementController =
            FindAnyObjectByType<BuildingPlacementController>();
        if (placementController != null)
        {
            placementController.RestoreAndRegisterExistingBuildings();
        }
    }



private void CacheBuildingRenderers(Building building)
    {
        if (building == null || building.renderers != null) return;

        GameObject root = building.instanceRoot;
        if (root == null && building.renderer != null)
        {
            PlaceableBuilding placeable = building.renderer.GetComponentInParent<PlaceableBuilding>();
            root = placeable != null ? placeable.gameObject : building.renderer.gameObject;
            building.instanceRoot = root;
        }

        building.renderers = root != null
            ? root.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];
        building.originalColors = new Color[building.renderers.Length];
        for (int i = 0; i < building.renderers.Length; i++)
        {
            Renderer renderer = building.renderers[i];
            building.originalColors[i] = renderer != null && renderer.sharedMaterial != null
                ? renderer.sharedMaterial.color
                : Color.white;
        }
    }

    private static Color GetOriginalColor(Building building, int rendererIndex)
    {
        return building.originalColors != null && rendererIndex < building.originalColors.Length
            ? building.originalColors[rendererIndex]
            : building.originalColor;
    }
}
