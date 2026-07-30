// =============================================================================
// TreadmillRouteSampler — 캐러밴이 실제 지나는 루트의 그리드 지형을 샘플링
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [역할] 트레드밀 길을 하드코딩 순서가 아니라, 그 캐러밴이 '실제로 지나는' 그리드 셀 지형으로
//        채우기 위해 — 캐러밴의 이동 루트(RouteVisual)를 따라가며 미니맵 그리드(MinimapGrid)의
//        지형을 셀 순서대로 읽어 트레드밀 길 문자열(P/G/F/M …)로 만든다.
//
// [흐름] 세이브 tradeProgressEntries에서 그 캐러밴의 이동중 엔트리 → activeRouteId
//        → RouteVisual.EvaluatePosition(p)로 루트를 촘촘히 따라가며 → MinimapGrid.WorldToCell
//        → 셀이 바뀔 때마다 그 셀 지형 문자를 기록. (강→숲→평지… 실제 경로 지형 순서)
// =============================================================================

using UnityEngine;
using ND.UI.WorldMap;   // RouteVisual

/// <summary>캐러밴 루트의 실제 그리드 지형을 트레드밀 길 문자열로 샘플링한다.</summary>
public static class TreadmillRouteSampler
{
    /// <summary>caravanId의 이동 루트를 따라 지나는 셀 지형을 순서대로 반환(없으면 "").</summary>
    public static string SampleRouteTerrain(string caravanId, int samples = 400)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.tradeProgressEntries == null) return string.Empty;

        // 1) 이동 중 엔트리 → activeRouteId
        string routeId = null;
        foreach (var e in save.tradeProgressEntries)
            if (e != null && e.caravanId == caravanId && e.state == ND.Framework.TradeProgressState.Traveling)
            { routeId = e.activeRouteId; break; }
        if (string.IsNullOrEmpty(routeId)) return string.Empty;

        // 2) 그 루트의 RouteVisual + 미니맵 그리드 찾기
        RouteVisual route = null;
        foreach (var rv in Object.FindObjectsByType<RouteVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rv != null && rv.RouteId == routeId) { route = rv; break; }
        if (route == null) return string.Empty;

        var grid = Object.FindAnyObjectByType<MinimapGrid>(FindObjectsInactive.Include);
        if (grid == null) return string.Empty;
        if (!grid.CellsBuilt) grid.BuildCells();

        // 3) 루트를 촘촘히 따라가며 셀이 바뀔 때마다 지형 문자 기록
        var sb = new System.Text.StringBuilder();
        int lastRow = int.MinValue, lastCol = int.MinValue;
        int n = Mathf.Max(1, samples);
        for (int i = 0; i <= n; i++)
        {
            float p = (float)i / n;
            Vector3 w = route.EvaluatePosition(p);
            int row, col;
            if (!grid.WorldToCell(w, out row, out col)) continue;
            if (row == lastRow && col == lastCol) continue;   // 같은 셀 연속은 스킵
            lastRow = row; lastCol = col;
            var cell = grid.GetCell(row, col);
            if (cell != null) sb.Append(MinimapCell.ToChar(cell.terrain));
        }
        return sb.ToString();
    }
}
