// =============================================================================
// MinimapWindDebug — 바람 시각화(화살표) + 조작(계절/이벤트) 디버그
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 개발 디버그 도구
//
// [역할] 바람 필드를 셀별 화살표로 그리고(매 프레임 갱신), 버튼으로
//        계절 전환(여름/겨울 → 주풍 반전)과 이벤트(큰불=저기압/메테오=고기압)를
//        일으켜 바람이 반응하는 걸 눈으로 본다.
//
// [부착] 미니맵 렌더 루트(WorldMapRenderRootV2). MinimapWind·MinimapGrid 필요.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>바람 화살표 시각화 + 계절/이벤트 조작 디버그.</summary>
public class MinimapWindDebug : MonoBehaviour
{
    [SerializeField] private MinimapWind wind;
    [SerializeField] private MinimapGrid grid;
    [SerializeField] private Transform renderRoot;
    [SerializeField] private bool showPanel = true;
    [SerializeField] private float arrowWidth = 0.09f;

    private bool overlayOn;
    private Transform arrowRoot;
    private readonly List<LineRenderer> arrows = new List<LineRenderer>();
    private GUIStyle btnStyle;
    private static Material lineMat;

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponent<MinimapGrid>() ?? GetComponentInChildren<MinimapGrid>(true);
        if (wind == null) wind = GetComponent<MinimapWind>() ?? GetComponentInChildren<MinimapWind>(true);
    }

    // ------------------------------------------------------------------ 화살표 갱신

    private void LateUpdate()
    {
        if (!overlayOn || wind == null || grid == null || arrows.Count == 0) return;
        Vector2 cell = grid.CellSize();
        float maxLen = Mathf.Min(cell.x, cell.y) * 0.6f;   // 셀 안에 들어오게(이웃과 안 겹침)
        int idx = 0;
        for (int r = 0; r < grid.Rows; r++)
            for (int c = 0; c < grid.Cols; c++)
            {
                if (idx >= arrows.Count) return;
                var lr = arrows[idx++];
                Vector3 center = grid.CellToWorld(r, c);
                Vector2 w = wind.WindAtCell(r, c);
                float mag = w.magnitude;
                Vector2 dir = mag > 1e-4f ? w / mag : Vector2.right;
                float len = maxLen;   // 고정 길이 = 방향 표시(세기는 색으로)
                Vector3 tail = center - (Vector3)(dir * (len * 0.5f));
                Vector3 tip = center + (Vector3)(dir * (len * 0.5f));
                tail.z = center.z; tip.z = center.z;
                lr.SetPosition(0, tail);
                lr.SetPosition(1, tip);
                // 세기 색: 약함(청록) → 강함(빨강)
                Color col = Color.Lerp(new Color(0.3f, 0.9f, 1f), new Color(1f, 0.3f, 0.2f), Mathf.Clamp01(mag * 0.7f));
                lr.startColor = new Color(col.r, col.g, col.b, 0.35f);
                lr.endColor = col;
            }
    }

    public void SetOverlay(bool on)
    {
        overlayOn = on;
        if (on) BuildArrows();
        else if (arrowRoot != null) { Destroy(arrowRoot.gameObject); arrowRoot = null; arrows.Clear(); }
    }

    private void BuildArrows()
    {
        if (arrowRoot != null) Destroy(arrowRoot.gameObject);
        arrows.Clear();
        arrowRoot = new GameObject("WindArrows").transform;
        arrowRoot.SetParent(renderRoot, false);
        for (int r = 0; r < grid.Rows; r++)
            for (int c = 0; c < grid.Cols; c++)
            {
                var go = new GameObject("A");
                go.transform.SetParent(arrowRoot, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.numCapVertices = 1;
                lr.startWidth = arrowWidth * 0.25f;  // 꼬리 얇게
                lr.endWidth = arrowWidth;            // 머리 두껍게(방향 표시)
                lr.material = LineMat();
                lr.sortingOrder = 22;                // 지형(4)·마을(10) 위
                arrows.Add(lr);
            }
    }

    // ------------------------------------------------------------------ 조작 UI

    private void OnGUI()
    {
        if (!showPanel) return;
        if (btnStyle == null) btnStyle = new GUIStyle(GUI.skin.button);
        float s = Mathf.Max(1f, Screen.height / 1080f);
        btnStyle.fontSize = Mathf.RoundToInt(22f * s);
        float w = 240f * s, h = 62f * s, pad = 8f * s;
        float x = Screen.width - w - 24f * s, y = Screen.height * 0.28f;

        if (GUI.Button(new Rect(x, y, w, h), overlayOn ? "바람 끄기" : "바람 보기", btnStyle))
            SetOverlay(!overlayOn);
        y += h + pad;
        if (!overlayOn) return;

        if (GUI.Button(new Rect(x, y, w * 0.5f - 2f, h), "여름", btnStyle)) SetSeason("summer");
        if (GUI.Button(new Rect(x + w * 0.5f + 2f, y, w * 0.5f - 2f, h), "겨울", btnStyle)) SetSeason("winter");
        y += h + pad;
        if (GUI.Button(new Rect(x, y, w, h), "🔥 큰불/전쟁 (저기압)", btnStyle))
            wind.DropEventAtCenter(false, 2.5f, 0.18f, 8f);
        y += h + pad;
        if (GUI.Button(new Rect(x, y, w, h), "☄ 메테오 (고기압)", btnStyle))
            wind.DropEventAtCenter(true, 3.5f, 0.14f, 5f);
        y += h + pad;
        GUI.Label(new Rect(x, y, w, 30f * s), "계절: " + CurrentSeason(), btnStyle);
    }

    private static string CurrentSeason()
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        return fr != null && fr.CurrentSaveData != null && fr.CurrentSaveData.world != null
            ? fr.CurrentSaveData.world.currentSeasonId : "?";
    }

    private static void SetSeason(string season)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        if (fr != null && fr.CurrentSaveData != null && fr.CurrentSaveData.world != null)
            fr.CurrentSaveData.world.currentSeasonId = season;   // MinimapWind가 감지해 재구성
    }

    private static Material LineMat()
    {
        if (lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default"));
        return lineMat;
    }
}
