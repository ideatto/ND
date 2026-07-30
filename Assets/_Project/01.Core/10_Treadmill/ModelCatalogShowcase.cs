// =============================================================================
// ModelCatalogShowcase — 마차/동물 프리팹 검수용 진열대
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계 (검수 도구)
//
// [역할] 게임 중앙 카탈로그(SandboxSharedGameDataCatalog)에 등록된 모든 마차/동물 프리팹을
//        씬에 줄지어 세우고 이름표를 붙인다. 에디트 모드에서 바로(ExecuteAlways) 보이므로
//        "각 모델이 비주얼적으로 제대로 나오는지 + 크기/방향/배치가 정상인지"를 한눈에 검수한다.
//
// [원칙] 스테이지와 마찬가지로 프리팹/데이터를 소유하지 않는다 — 중앙 카탈로그를 읽어 보여주기만.
//        모델이 이상하면(색/방향/크기) 해당 '프리팹'을 열어 고친다(여기가 아니라).
//
// [부착] 검수 씬(Treadmill_ModelCheck)의 빈 오브젝트에 붙인다. 우클릭 → Rebuild로 갱신.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>중앙 카탈로그의 모든 마차/동물 프리팹을 이름표와 함께 진열하는 검수 도구.</summary>
[ExecuteAlways]
public class ModelCatalogShowcase : MonoBehaviour
{
    [SerializeField] private float spacing = 4f;      // 항목 간 X 간격
    [SerializeField] private float rowGap = 6f;       // 마차 줄 ↔ 동물 줄 Z 간격
    [SerializeField] private bool showLabels = true;  // 이름표 표시
    [SerializeField] private float labelHeight = 2.6f;

    private ND.Framework.SandboxSharedGameDataCatalog catalog;
    private ND.Framework.SandboxSharedGameDataCatalog Catalog =>
        catalog != null ? catalog
                        : (catalog = Resources.Load<ND.Framework.SandboxSharedGameDataCatalog>(
                              ND.Framework.SandboxSharedGameDataCatalog.ResourceName));

    // 이미 진열돼 있으면 자동으로 다시 만들지 않는다(재컴파일 때마다 손댄 게 사라지는 것 방지).
    // 갱신이 필요하면 우클릭 → Rebuild 를 직접 눌러라.
    private void OnEnable()
    {
        if (transform.childCount == 0) Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyObj(transform.GetChild(i).gameObject);

        var cat = Catalog;
        if (cat == null) { Debug.LogWarning("[Showcase] 중앙 카탈로그를 못 찾음"); return; }

        // 마차 줄(z=0), 동물 줄(z=rowGap)
        var wagons = new List<(GameObject prefab, string name)>();
        foreach (var w in cat.Wagons) if (w != null && w.Prefab != null) wagons.Add((w.Prefab, w.DisplayName));
        var animals = new List<(GameObject prefab, string name)>();
        foreach (var a in cat.DraftAnimals) if (a != null && a.Prefab != null) animals.Add((a.Prefab, a.DisplayName));

        LayoutRow(wagons, 0f, "마차");
        LayoutRow(animals, rowGap, "동물");

        Debug.Log("[Showcase] 마차 " + wagons.Count + "종 / 동물 " + animals.Count + "종 진열");
    }

    private void LayoutRow(List<(GameObject prefab, string name)> items, float z, string tag)
    {
        int n = items.Count;
        if (n == 0) return;
        float startX = -(n - 1) * 0.5f * spacing;
        for (int i = 0; i < n; i++)
        {
            float x = startX + i * spacing;
            // 에디트에선 '프리팹 연결 인스턴스'로 생성 → 인스턴스 수정 후 Inspector의
            // Overrides ▾ → Apply All 로 프리팹 원본에 바로 반영 가능(표준 Unity 방식).
            GameObject go;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(items[i].prefab, transform);
            else
#endif
                go = Instantiate(items[i].prefab, transform);
            go.name = tag + "_" + items[i].name;
            go.transform.localPosition = new Vector3(x, 0f, z);
            if (showLabels) AddLabel(new Vector3(x, labelHeight, z), items[i].name);
        }
    }

#if UNITY_EDITOR
    // 체크 씬에서 인스턴스 크기/회전을 눈으로 맞춘 뒤, 그 값을 '프리팹 원본'에 저장한다.
    // → 프리뷰 씬·Play 등 프리팹을 쓰는 모든 곳에 반영된다.
    [ContextMenu("현재 진열 크기를 프리팹에 저장")]
    private void CaptureSizesToPrefabs()
    {
        var cat = Catalog;
        if (cat == null) return;

        // 진열 이름("마차_XXX"/"동물_XXX") → 프리팹 매핑
        var map = new Dictionary<string, GameObject>();
        foreach (var w in cat.Wagons) if (w != null && w.Prefab != null) map["마차_" + w.DisplayName] = w.Prefab;
        foreach (var a in cat.DraftAnimals) if (a != null && a.Prefab != null) map["동물_" + a.DisplayName] = a.Prefab;

        int n = 0;
        foreach (Transform ch in transform)
        {
            if (!map.TryGetValue(ch.name, out var prefab)) continue;
            string p = UnityEditor.AssetDatabase.GetAssetPath(prefab);
            var root = UnityEditor.PrefabUtility.LoadPrefabContents(p);
            root.transform.localScale = ch.localScale;      // 크기 저장
            root.transform.localRotation = ch.localRotation; // 방향도 같이 저장
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, p);
            UnityEditor.PrefabUtility.UnloadPrefabContents(root);
            n++;
        }
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("[Showcase] " + n + "개 프리팹에 크기/회전 저장 완료 — 이제 프리뷰·Play에도 적용됨");
    }
#endif

    private void AddLabel(Vector3 localPos, string text)
    {
        var go = new GameObject("Label_" + text);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // -Z(카메라) 쪽을 향하게

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = 0.2f;
        tm.fontSize = 64;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;

        // 레거시 TextMesh는 폰트+그 머티리얼을 렌더러에 지정해야 보인다.
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null)
        {
            tm.font = f;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = f.material;
        }
    }

    private static void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
