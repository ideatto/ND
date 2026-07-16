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
        [HideInInspector] public Color originalColor;
    }

    /// <summary>카탈로그(건물 종류) 한 항목.</summary>
    [System.Serializable]
    public class CatalogEntry
    {
        public string displayName;
        public GameObject prefab;   // 비면 큐브 폴백
    }

    [SerializeField] private List<Building> buildings = new List<Building>();
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] private GameObject fallbackPrefab;
    [SerializeField] private List<CatalogEntry> catalog = new List<CatalogEntry>();

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
    /// <summary>이 종류의 현재 레벨(안 지어졌으면 0).</summary>
    public int GetCatalogLevel(int index)
    {
        if (index < 0 || index >= catalog.Count) return 0;
        Building b = FindByName(catalog[index].displayName);
        return b != null ? b.level : 0;
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
        RestoreFromSaveData();
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

        foreach (VillageBuildingSaveData saved in savedBuildings)
        {
            if (saved == null || string.IsNullOrEmpty(saved.displayName) || saved.level < 1)
                continue;

            Building existing = FindByName(saved.displayName);
            if (existing != null)
            {
                existing.level = saved.level;
                continue;
            }

            CatalogEntry catalogEntry = FindCatalogByName(saved.displayName);
            GameObject prefab = catalogEntry != null && catalogEntry.prefab != null
                ? catalogEntry.prefab
                : fallbackPrefab;
            BuildNew(prefab, saved.displayName);

            Building built = FindByName(saved.displayName);
            if (built != null)
                built.level = saved.level;
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

    /// <summary>index 건물만 강조색, 나머지는 원래 색.</summary>
    public void Highlight(int index)
    {
        for (int i = 0; i < buildings.Count; i++)
        {
            Building b = buildings[i];
            if (b.renderer == null) continue;
            b.renderer.material.color = (i == index) ? highlightColor : b.originalColor;
        }
    }

    /// <summary>모든 건물 원래 색으로.</summary>
    public void ClearHighlight()
    {
        foreach (Building b in buildings)
            if (b.renderer != null) b.renderer.material.color = b.originalColor;
    }

    /// <summary>카탈로그 종류를 짓거나(없으면 Lv.1) 레벨을 올린다(이미 있으면).</summary>
    public void AddOrUpgrade(int catalogIndex)
    {
        if (catalogIndex < 0 || catalogIndex >= catalog.Count) return;
        CatalogEntry entry = catalog[catalogIndex];

        Building existing = FindByName(entry.displayName);
        if (existing != null)
        {
            existing.level++;   // 이미 있으면 레벨업만
            WriteBuildingToSave(existing.displayName, existing.level);
            return;
        }
        // 없으면 새로 짓기(Lv.1)
        BuildNew(entry.prefab != null ? entry.prefab : fallbackPrefab, entry.displayName);
        Building built = FindByName(entry.displayName);
        if (built != null)
            WriteBuildingToSave(built.displayName, built.level);
    }

    private void BuildNew(GameObject prefab, string displayName)
    {
        int n = buildings.Count;
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(1.8f, 2f, 1.8f);
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = new Color(0.72f, 0.68f, 0.62f);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
        go.name = "Building_" + displayName;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        float x = -4f + (n % 4) * 2.6f;
        float z = -4f + (n / 4) * 2.6f;
        go.transform.position = new Vector3(x, 1f, z);

        Renderer r = go.GetComponentInChildren<Renderer>();
        buildings.Add(new Building
        {
            displayName = displayName,
            renderer = r,
            level = 1,
            originalColor = (r != null && r.sharedMaterial != null) ? r.sharedMaterial.color : Color.white
        });
    }
}
