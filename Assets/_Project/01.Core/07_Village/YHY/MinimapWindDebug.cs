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
        else PurgeArrows();
    }

    /// <summary>renderRoot 밑 WindArrows를 전부 제거(중복/잔존 방지).</summary>
    private void PurgeArrows()
    {
        arrows.Clear();
        arrowRoot = null;
        if (renderRoot == null) return;
        var stray = new List<Transform>();
        foreach (Transform ch in renderRoot) if (ch.name == "WindArrows") stray.Add(ch);
        foreach (var ch in stray)
        {
            if (Application.isPlaying) Destroy(ch.gameObject);
            else DestroyImmediate(ch.gameObject);
        }
    }

    private void BuildArrows()
    {
        PurgeArrows();   // 기존/잔존 화살표 먼저 싹 제거
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
        float w = 190f * s, h = 62f * s, pad = 8f * s;
        // 좌열, '페인트 모드 켜기' 바로 아래. 아래로 격자·이벤트 버튼이 이어짐(지도 버튼과 안 겹치게)
        float x = 545f * s, y = 90f * s;

        if (GUI.Button(new Rect(x, y, w, h), overlayOn ? "바람 끄기" : "바람 보기", btnStyle))
            SetOverlay(!overlayOn);
        y += h + pad;
        if (!overlayOn) return;

        if (GUI.Button(new Rect(x, y, w * 0.5f - 2f, h), "여름", btnStyle)) SetSeason("summer");
        if (GUI.Button(new Rect(x + w * 0.5f + 2f, y, w * 0.5f - 2f, h), "겨울", btnStyle)) SetSeason("winter");
        y += h + pad;
        GUI.Label(new Rect(x, y, w, 30f * s), "계절: " + CurrentSeason(), btnStyle);
        y += 30f * s + pad;
        // 큰불/메테오는 아래 "놓기" 버튼을 누른 뒤 미니맵을 클릭해 그 지점에 배치(MinimapEventPlacer)
        GUI.Label(new Rect(x, y, w, 30f * s), "🔥/☄: 아래 버튼→클릭", btnStyle);
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
