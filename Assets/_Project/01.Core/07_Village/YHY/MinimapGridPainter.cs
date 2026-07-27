// =============================================================================
// MinimapGridPainter — 미니맵 클릭으로 셀 지형을 칠하는 디버그 페인트 툴
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 개발 디버그 도구
//
// [역할] 페인트 모드에서 브러시(지형)를 고르고 미니맵 셀을 클릭하면 그 셀 지형이 바뀐다.
//        디버그 오버레이가 그 칸 색을 즉시 갱신한다. "파일 저장"으로 지형 텍스트맵에 기록.
//
// [연동] 미니맵 RawImage(RT 표시)에 붙인다. 카메라·그리드·디버그는 RT로 자동 탐색.
//        MinimapGrid.terrainMap(TextAsset)에 파일이 할당돼 있어야 저장 가능.
// [주의] 클릭은 Play 모드에서 동작(EventSystem). 저장은 에디터에서만 파일에 씀.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>미니맵 클릭으로 셀 지형을 칠하는 디버그 페인트 툴.</summary>
public class MinimapGridPainter : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RawImage view;         // 미니맵 RawImage(비면 자기 자신)
    [SerializeField] private bool showPanel = true;

    private static readonly TerrainType[] Brushes =
    {
        TerrainType.Plain, TerrainType.Grass, TerrainType.Forest, TerrainType.Farmland,
        TerrainType.River, TerrainType.Riverbank, TerrainType.Bridge,
        TerrainType.Mountain, TerrainType.Water, TerrainType.Cloud
    };
    private static readonly string[] BrushNames =
    { "평지", "풀", "숲", "논밭", "강", "강변", "다리", "산", "호수", "구름" };

    private TerrainType brush = TerrainType.River;
    private bool paintMode;
    private Camera cachedCam;
    private MinimapGrid grid;
    private MinimapGridDebug debug;
    private GUIStyle btnStyle;

    private void Awake()
    {
        if (view == null) view = GetComponent<RawImage>();
    }

    // ------------------------------------------------------------------ 클릭 → 칠하기

    public void OnPointerClick(PointerEventData e)
    {
        if (!paintMode) return;
        PaintAt(e);
    }

    private void PaintAt(PointerEventData e)
    {
        Camera cam = ResolveCamera();
        if (cam == null || view == null) return;
        RectTransform rt = view.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
            return;
        float u = Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, local.x);
        float v = Mathf.InverseLerp(rt.rect.yMin, rt.rect.yMax, local.y);
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(u, v, Mathf.Abs(cam.transform.position.z)));

        var g = ResolveGrid();
        if (g == null) return;
        if (!g.WorldToCell(world, out int row, out int col)) return;   // 맵 밖이면 무시
        g.SetTerrain(row, col, brush);
        ResolveDebug()?.RefreshCell(row, col);
        Debug.Log($"[페인트] ({row},{col}) → {brush}");
    }

    // ------------------------------------------------------------------ UI

    private void OnGUI()
    {
        if (!showPanel) return;
        if (btnStyle == null) btnStyle = new GUIStyle(GUI.skin.button);
        float s = Mathf.Max(1f, Screen.height / 1080f);
        btnStyle.fontSize = Mathf.RoundToInt(22f * s);
        float w = 240f * s, h = 60f * s, pad = 8f * s;
        float x = 24f * s, y = Screen.height * 0.35f;

        if (GUI.Button(new Rect(x, y, w, h), paintMode ? "페인트 모드 끄기" : "페인트 모드 켜기", btnStyle))
        {
            paintMode = !paintMode;
            // 페인트 중엔 마을 클릭 진입(라우터)을 꺼 충돌 방지.
            var router = GetComponent<MinimapTownClickRouter>();
            if (router != null) router.enabled = !paintMode;
            if (paintMode) ResolveDebug()?.SetOverlay(true);   // 켜면 오버레이도 켬
        }
        y += h + pad;
        if (!paintMode) return;

        // 브러시 팔레트
        for (int i = 0; i < Brushes.Length; i++)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = MinimapGridDebug.TerrainColor(Brushes[i]);
            string label = (brush == Brushes[i] ? "● " : "") + BrushNames[i];
            if (GUI.Button(new Rect(x, y, w, h * 0.8f), label, btnStyle)) brush = Brushes[i];
            GUI.backgroundColor = prevBg;
            y += h * 0.8f + pad * 0.5f;
        }
        y += pad;
        if (GUI.Button(new Rect(x, y, w, h), "파일 저장", btnStyle)) SaveToFile();
    }

    private void SaveToFile()
    {
#if UNITY_EDITOR
        var g = ResolveGrid();
        if (g == null || g.TerrainMap == null)
        { Debug.LogWarning("[페인트] MinimapGrid.terrainMap(텍스트 파일) 미할당 — 저장 불가"); return; }
        string path = UnityEditor.AssetDatabase.GetAssetPath(g.TerrainMap);
        System.IO.File.WriteAllText(path, g.BuildTerrainMapText());
        UnityEditor.AssetDatabase.ImportAsset(path);
        Debug.Log("[페인트] 지형맵 저장: " + path);
#else
        Debug.LogWarning("[페인트] 저장은 에디터에서만 가능");
#endif
    }

    // ------------------------------------------------------------------ 탐색

    private Camera ResolveCamera()
    {
        if (cachedCam != null) return cachedCam;
        RenderTexture rtTex = (view != null) ? view.texture as RenderTexture : null;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam.targetTexture == null) continue;
            if (rtTex == null || cam.targetTexture == rtTex) { cachedCam = cam; return cam; }
        }
        return null;
    }

    private MinimapGrid ResolveGrid()
    {
        if (grid != null) return grid;
        var cam = ResolveCamera();
        if (cam != null) grid = cam.transform.root.GetComponentInChildren<MinimapGrid>(true);
        return grid;
    }

    private MinimapGridDebug ResolveDebug()
    {
        if (debug != null) return debug;
        var cam = ResolveCamera();
        if (cam != null) debug = cam.transform.root.GetComponentInChildren<MinimapGridDebug>(true);
        return debug;
    }
}
