// =============================================================================
// TreadmillTerrainDebug — 트레드밀 지형 전환 디버그 패널(IMGUI 버튼)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계 · 디버그 도구
//
// [역할] 플레이 중 지형 버튼(풀/숲/산 …)을 띄워, 누르면 그 지형으로 '슬라이드 전환'을 요청.
//        버튼 묶음을 화면의 '트레드밀 패널(RawImage)' 바로 옆에 붙여 컴팩트하게 보여준다.
//
// [필요] 대상 TreadmillRoad의 debugAllTerrains=On, debugManualTerrain=On.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

/// <summary>트레드밀 패널 옆에 붙는 컴팩트한 지형 전환 디버그 버튼 패널(IMGUI).</summary>
public class TreadmillTerrainDebug : MonoBehaviour
{
    [SerializeField] private TreadmillRoad road;      // 대상 길(비우면 자동 검색)
    [SerializeField] private bool forceScroll = true; // 디버그 중 길을 항상 흐르게
    [Tooltip("UI 배율(작게=값 낮춤).")]
    [SerializeField] private float uiScale = 1.5f;
    [Tooltip("트레드밀 패널과의 간격(px).")]
    [SerializeField] private float gapPx = 8f;
    [Tooltip("패널 오른쪽(true)/왼쪽(false)에 배치.")]
    [SerializeField] private bool placeRight = true;

    private RectTransform panelRect;   // 트레드밀 패널(RawImage)의 위치 기준
    private TreadmillRain rain;        // 비 연출(비우면 자동 검색)
    private bool rainRealWeather = true;   // true=실제 날씨, false=디버그 슬라이더
    private float rainDebugVal = 0.6f;     // 디버그 비 세기

    private TreadmillTownArrival arrival;   // 마을 도착 연출(비우면 자동 검색)
    [Tooltip("디버그 '마을 도착' 테스트에 쓸 건물 프리팹(예: Building_HarborVillage).")]
    [SerializeField] private GameObject debugTownPrefab;

    private TreadmillProgressSync sync;     // 진행도 동기화(비우면 자동 검색)
    private bool fakeTrip;                  // 가짜 여행 재생 중

    private void Reset() => road = FindRoad();
    private void Awake() { if (road == null) road = FindRoad(); }
    private static TreadmillRoad FindRoad() => Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);

    // 디버그 강제 스크롤 → 캐러밴 이동과 무관하게 길이 흐르고, 동물도 걷는다(스테이지가 IsScrolling으로 판단).
    private void LateUpdate() { if (road != null) road.SetDebugScroll(forceScroll); }

    // 트레드밀 패널(RawImage) 위치를 찾는다(패널 옆에 붙이기 위해).
    private void FindPanelRect()
    {
        var panel = Object.FindFirstObjectByType<TreadmillPanel>(FindObjectsInactive.Include);
        if (panel == null) return;
        var ri = panel.GetComponentInChildren<RawImage>(true);
        panelRect = ri != null ? ri.rectTransform : panel.transform as RectTransform;
    }

    private void OnGUI()
    {
        if (road == null) return;
        if (panelRect == null) FindPanelRect();

        var terrains = (TerrainType[])System.Enum.GetValues(typeof(TerrainType));
        float boxW = 168f;
        int bh = 24;
        float boxH = 84 + 62 + bh + 6 + (bh + 6) + 24 + terrains.Length * (bh + 3);   // +비 +마을도착 +여행재생

        // 패널 옆 위치 계산(IMGUI는 y가 위→아래). 못 찾으면 좌상단 폴백.
        Vector2 anchor = new Vector2(12f, 12f);
        if (panelRect != null)
        {
            var canvas = panelRect.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            var c = new Vector3[4]; panelRect.GetWorldCorners(c);
            Vector2 tl = RectTransformUtility.WorldToScreenPoint(cam, c[1]); // 좌상(y 위로)
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, c[2]); // 우상
            float x = placeRight ? tr.x + gapPx : tl.x - gapPx - boxW * uiScale;
            float y = Screen.height - tr.y;   // 위쪽 모서리 → IMGUI top-down
            // 패널이 화면 밖으로 넘칠 수 있으니 디버그 박스를 화면 안으로 클램프
            x = Mathf.Clamp(x, 4f, Screen.width - boxW * uiScale - 4f);
            y = Mathf.Clamp(y, 8f, Screen.height - boxH * uiScale - 8f);
            anchor = new Vector2(x, y);
        }

        var oldM = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(uiScale, uiScale), anchor);

        var btn = Style(ref _btn, GUI.skin.button, 12, true);
        var lbl = Style(ref _lbl, GUI.skin.label, 11, false);
        var title = Style(ref _title, GUI.skin.label, 13, true);

        GUILayout.BeginArea(new Rect(anchor.x, anchor.y, boxW, boxH), GUI.skin.box);
        GUILayout.Label("지형 디버그", title);
        GUILayout.Label("현재: " + Label(road.CurrentTerrain) + (road.IsTransitioning ? " (전환중)" : ""), lbl);
        forceScroll = GUILayout.Toggle(forceScroll, " 스크롤", lbl);

        // ── 비 연출 디버그(항상 표시) ──
        if (rain == null) rain = Object.FindFirstObjectByType<TreadmillRain>(FindObjectsInactive.Include);
        if (rain != null)
        {
            GUILayout.Label("― 비 ―", lbl);
            rainRealWeather = GUILayout.Toggle(rainRealWeather, " 실제 날씨 연동", lbl);
            GUI.enabled = !rainRealWeather;                                  // 연동 끄면 슬라이더로 수동
            GUILayout.BeginHorizontal(GUILayout.Width(boxW - 12f));
            GUILayout.Label("세기 " + rainDebugVal.ToString("F1"), lbl, GUILayout.Width(52));
            rainDebugVal = GUILayout.HorizontalSlider(rainDebugVal, 0f, 1f, GUILayout.Width(95));
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            // 연동 ON=실제 날씨(WeatherState), OFF=슬라이더 강제 세기
            rain.DebugIntensity = rainRealWeather ? -1f : rainDebugVal;
            if (GUILayout.Button("⚡ 번개 테스트", btn, GUILayout.Height(bh)))
                rain.TriggerLightning();
        }

        // ── 마을 도착 디버그 ──
        if (arrival == null) arrival = Object.FindFirstObjectByType<TreadmillTownArrival>(FindObjectsInactive.Include);
        if (arrival != null && debugTownPrefab != null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🏘 마을 도착", btn, GUILayout.Height(bh)))
                arrival.Arrive(debugTownPrefab);
            if (GUILayout.Button("출발", btn, GUILayout.Height(bh)))
                arrival.Leave();
            GUILayout.EndHorizontal();
        }

        // ── 진행도 동기화 디버그(가짜 여행 재생) ──
        if (sync == null) sync = Object.FindFirstObjectByType<TreadmillProgressSync>(FindObjectsInactive.Include);
        if (sync != null)
        {
            bool f = GUILayout.Toggle(fakeTrip, " 🚚 여행 재생(가짜)", lbl);
            if (f != fakeTrip) { fakeTrip = f; sync.SetFakeProgress(f); }
        }
        GUILayout.Space(3);
        GUI.enabled = !road.IsTransitioning;
        foreach (var t in terrains)
        {
            bool cur = t == road.CurrentTerrain;
            if (GUILayout.Button((cur ? "▶ " : "") + Label(t), btn, GUILayout.Height(bh)))
                road.RequestTerrain(t);
        }
        GUI.enabled = true;
        GUILayout.EndArea();
        GUI.matrix = oldM;
    }

    private static GUIStyle _btn, _lbl, _title;
    private static GUIStyle Style(ref GUIStyle s, GUIStyle basis, int size, bool bold)
    {
        if (s == null) s = new GUIStyle(basis) { fontSize = size, richText = true, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
        return s;
    }

    // 지형 → 한글 라벨(없으면 enum 이름).
    private static string Label(TerrainType t)
    {
        switch (t)
        {
            case TerrainType.Plain:     return "평지";
            case TerrainType.River:     return "강";
            case TerrainType.Riverbank: return "강변";
            case TerrainType.Bridge:    return "다리";
            case TerrainType.Mountain:  return "산";
            case TerrainType.Water:     return "호수";
            case TerrainType.Cloud:     return "구름";
            case TerrainType.Forest:    return "숲";
            case TerrainType.Farmland:  return "논밭";
            case TerrainType.Grass:     return "풀";
            default:                    return t.ToString();
        }
    }
}
