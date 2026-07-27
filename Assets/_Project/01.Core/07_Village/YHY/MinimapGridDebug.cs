// =============================================================================
// MinimapGridDebug — 격자 디버그 오버레이(토글): 셀별 지역색 + 좌표 라벨(A1)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 개발 디버그 도구
//
// [역할] 화면 버튼으로 켜면, 미니맵 각 셀을
//        - 그 자리 맵 아트의 평균 색(=지역 색)으로 반투명하게 칠하고
//        - "A1"(열=알파벳, 행=숫자) 좌표 라벨을 표시한다.
//        끄면 오버레이를 제거한다. 지형 저작·확인용.
//
// [부착] 미니맵 렌더 루트(WorldMapRenderRootV2). MinimapGrid와 같은 오브젝트/부모.
// [주의] 배경 텍스처는 Read/Write Enabled(isReadable) 여야 색 샘플 가능.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>격자 디버그 오버레이(셀 지역색 + 좌표 라벨) 토글.</summary>
public class MinimapGridDebug : MonoBehaviour
{
    [SerializeField] private MinimapGrid grid;          // 비면 자동 탐색
    [SerializeField] private Transform renderRoot;      // 셀/배경 범위(비면 자기 자신)
    [SerializeField] private bool showPanel = true;     // 화면 토글 버튼 표시
    [SerializeField] private float tintAlpha = 0.5f;    // 지역색 반투명도
    [SerializeField] private Color labelColor = Color.black;
    [SerializeField] private float labelCharSize = 0.045f;

    private bool overlayOn;
    private Transform overlayRoot;
    private GUIStyle btnStyle;
    private static Sprite whiteSquare;
    private static Font labelFont;
    private readonly Dictionary<int, SpriteRenderer> tintByCell = new Dictionary<int, SpriteRenderer>();

    public bool IsOverlayOn => overlayOn;
    public MinimapGrid Grid => grid;

    /// <summary>페인트 후 그 셀 한 칸의 색만 즉시 갱신한다.</summary>
    public void RefreshCell(int row, int col)
    {
        if (!overlayOn || grid == null) return;
        var mc = grid.GetCell(row, col);
        if (mc == null) return;
        if (tintByCell.TryGetValue(row * grid.Cols + col, out var sr) && sr != null)
        {
            Color col2 = TerrainColor(mc.terrain); col2.a = tintAlpha; sr.color = col2;
        }
    }

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponent<MinimapGrid>() ?? GetComponentInChildren<MinimapGrid>(true);
    }

    // ------------------------------------------------------------------ UI

    private void OnGUI()
    {
        if (!showPanel) return;
        if (btnStyle == null) btnStyle = new GUIStyle(GUI.skin.button);
        float s = Mathf.Max(1f, Screen.height / 1080f);
        btnStyle.fontSize = Mathf.RoundToInt(24f * s);
        float w = 300f * s, h = 76f * s;
        // 오른쪽 상단 근처(테스트 패널과 안 겹치게)
        if (GUI.Button(new Rect(Screen.width - w - 24f * s, 24f * s, w, h),
                overlayOn ? "격자 디버그 끄기" : "격자 디버그 켜기", btnStyle))
            ToggleOverlay();
    }

    /// <summary>오버레이 토글(외부 버튼에서도 호출 가능).</summary>
    public void ToggleOverlay() => SetOverlay(!overlayOn);

    public void SetOverlay(bool on)
    {
        overlayOn = on;
        if (on) BuildOverlay();
        else if (overlayRoot != null) { Destroy(overlayRoot.gameObject); overlayRoot = null; }
    }

    // ------------------------------------------------------------------ 오버레이 생성

    private void BuildOverlay()
    {
        if (grid == null) { Debug.LogWarning("[격자디버그] MinimapGrid 없음"); return; }
        grid.BuildCells();

        if (overlayRoot != null) Destroy(overlayRoot.gameObject);
        tintByCell.Clear();
        overlayRoot = new GameObject("GridDebugOverlay").transform;
        overlayRoot.SetParent(renderRoot, false);

        Vector2 cell = grid.CellSize();
        int rows = grid.Rows, cols = grid.Cols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var mc = grid.GetCell(r, c);
                Vector3 pos = mc != null ? mc.worldCenter : grid.CellToWorld(r, c);

                // 1) 지형 종류별 색(범례)으로 칠하기
                Color col = TerrainColor(mc != null ? mc.terrain : TerrainType.Plain);
                col.a = tintAlpha;
                var sr = MakeTint(pos, new Vector2(cell.x * 0.98f, cell.y * 0.98f), col);
                tintByCell[r * cols + c] = sr;

                // 2) 좌표 라벨 (열=알파벳, 행=숫자) 예: A1
                MakeLabel(pos, CoordLabel(r, c));
            }
    }

    /// <summary>지형 종류별 범례 색.</summary>
    public static Color TerrainColor(TerrainType t)
    {
        switch (t)
        {
            case TerrainType.Plain:    return new Color(0.85f, 0.82f, 0.40f);
            case TerrainType.Grass:    return new Color(0.55f, 0.80f, 0.35f);
            case TerrainType.Forest:   return new Color(0.15f, 0.45f, 0.15f);
            case TerrainType.Farmland: return new Color(0.90f, 0.60f, 0.20f);
            case TerrainType.River:    return new Color(0.25f, 0.55f, 0.95f);
            case TerrainType.Riverbank:return new Color(0.55f, 0.80f, 0.75f);  // 강변(옅은 청록)
            case TerrainType.Bridge:   return new Color(0.55f, 0.35f, 0.15f);
            case TerrainType.Mountain: return new Color(0.55f, 0.55f, 0.58f);
            case TerrainType.Water:    return new Color(0.20f, 0.70f, 0.85f);
            case TerrainType.Cloud:    return new Color(0.96f, 0.96f, 0.96f);
            default:                   return Color.gray;
        }
    }

    /// <summary>열→알파벳(A..), 행→숫자(1..). 예: (row0,col0)=A1.</summary>
    private static string CoordLabel(int row, int col)
    {
        string letters = "";
        int c = col;
        do { letters = (char)('A' + c % 26) + letters; c = c / 26 - 1; } while (c >= 0);
        return letters + (row + 1);
    }

    private SpriteRenderer MakeTint(Vector3 pos, Vector2 size, Color color)
    {
        var go = new GameObject("Tint");
        go.transform.SetParent(overlayRoot, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSquare();
        sr.color = color;
        sr.sortingOrder = 4;   // 배경(0) 위, 격자선(5)·마을(10) 아래
        return sr;
    }

    private void MakeLabel(Vector3 pos, string text)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(overlayRoot, false);
        go.transform.position = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = labelCharSize;
        tm.fontSize = 48;
        tm.color = labelColor;
        var font = LabelFont();
        if (font != null) { tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
        go.GetComponent<MeshRenderer>().sortingOrder = 33;   // 최상단
    }

    // ------------------------------------------------------------------ 공용 리소스

    private static Sprite WhiteSquare()
    {
        if (whiteSquare != null) return whiteSquare;
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
        t.SetPixels(px); t.Apply(); t.filterMode = FilterMode.Point;
        whiteSquare = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // 4px/1unit → 1 world
        return whiteSquare;
    }

    private static Font LabelFont()
    {
        if (labelFont != null) return labelFont;
        labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (labelFont == null) labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return labelFont;
    }
}
