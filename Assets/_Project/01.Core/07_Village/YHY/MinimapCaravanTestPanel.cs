// =============================================================================
// MinimapCaravanTestPanel — 미니맵 캐러밴 이동을 눈으로 확인하는 테스트 버튼 패널
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 개발 검증용 테스트 도구 (실제 게임플로우 아님)
//
// [역할] 화면 버튼(OnGUI)으로 캐러밴을 거점→무역마을로 "출발"시키고, 미니맵에서
//        마커가 3분에 걸쳐 이동하는 걸 보여준다. 여러 캐러밴 동시 이동/즉시 도착도.
//
// [원리] 정헌님/성욱님의 무역 시스템 코드는 전혀 건드리지 않는다.
//        무역 진행은 SaveData.tradeProgressEntries(캐러밴별 리스트)에 기록되므로,
//        캐러밴마다 엔트리를 직접 넣어 여러 대를 동시에 Traveling 상태로 만든다.
//        진행률 = (CurrentUtc - start) / (end - start), GameTime.CurrentUtc==실시간
//        → end-start=180초면 정확히 3분. 미니맵 표시는 MinimapMultiCaravanMarkers가 담당.
//
// [route] BaseToRiver(거점→RiverTown), BaseToWindy(거점→WindyTown) — 미니맵 RouteVisual과 일치.
//
// [부착] InGame_Test 씬의 테스트용 GameObject. 저장(SaveService.Save)은 호출하지 않아
//        메모리상 상태만 바꾼다(다른 씬/세이브에 영향 없음).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ND.Framework;
using ND.UI.WorldMap;
// 전역(Sandbox) 동명 타입과 충돌하므로 Framework 타입을 alias로 고정한다.
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkTradeProgress = ND.Framework.TradeProgressSaveData;
using FrameworkTradeProgressState = ND.Framework.TradeProgressState;

/// <summary>미니맵 캐러밴 이동을 시연하는 개발용 테스트 버튼 패널(여러 대 동시 지원).</summary>
public class MinimapCaravanTestPanel : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float travelSeconds = 180f;          // 이동 시간(초) — 기본 3분
    [SerializeField] private string homeTownId = "BaseCamp";      // 거점(출발지) townId

    [Header("목적지 route (미니맵 RouteVisual의 RouteId와 일치)")]
    [SerializeField] private string routeToRiver = "BaseToRiver"; // 거점 → RiverTown
    [SerializeField] private string riverTownId = "RiverTown";
    [SerializeField] private string routeToWindy = "BaseToWindy"; // 거점 → WindyTown
    [SerializeField] private string windyTownId = "WindyTown";

    [Header("UI")]
    [SerializeField] private bool showPanel = true;               // 패널 표시 여부
    [SerializeField] private Vector2 panelPos = new Vector2(24f, 90f); // 화면 좌상단 기준 위치(px)

    // 캐러밴별 목적지(즉시 도착 시 정박시킬 곳). 출발할 때 기록한다.
    private readonly Dictionary<string, string> destByCaravan = new Dictionary<string, string>();

    private GUIStyle boxStyle, btnStyle, labelStyle;

    // ------------------------------------------------------------------ 동작

    /// <summary>index번 캐러밴을 거점에서 지정 route로 출발시킨다(다른 캐러밴과 동시 가능).</summary>
    public void DepartCaravan(int index, string routeId, string destTownId)
    {
        var save = GetSaveData();
        if (save == null || save.caravans == null || index < 0 || index >= save.caravans.Count)
        { Debug.LogWarning($"[테스트패널] {index + 1}번 캐러밴 없음/세이브 미준비"); return; }

        var c = save.caravans[index];
        if (c == null || string.IsNullOrEmpty(c.caravanId)) { Debug.LogWarning("[테스트패널] 캐러밴 ID 없음"); return; }

        c.currentTownId = homeTownId;   // 출발은 거점에서

        long start = NowUtc().Ticks;
        long end = start + (long)(travelSeconds * TimeSpan.TicksPerSecond);
        SetTravelingEntry(save, c.caravanId, routeId, start, end);
        destByCaravan[c.caravanId] = destTownId;

        RefreshMinimaps();
        Debug.Log($"[테스트패널] {index + 1}번 출발: {homeTownId} → {destTownId} (route={routeId}, {travelSeconds}s)");
    }

    /// <summary>1·2번 캐러밴을 동시에 각각 리버타운/윈디타운으로 출발.</summary>
    public void DepartBoth()
    {
        DepartCaravan(0, routeToRiver, riverTownId);
        DepartCaravan(1, routeToWindy, windyTownId);
    }

    /// <summary>이동 중인 모든 캐러밴을 즉시 도착 처리 — 목적지에 정박시키고 진행을 비운다.</summary>
    public void InstantArriveAll()
    {
        var save = GetSaveData();
        if (save == null || save.tradeProgressEntries == null) return;

        // 이동 중인 각 캐러밴을 목적지로 이동.
        for (int i = 0; i < save.tradeProgressEntries.Count; i++)
        {
            var e = save.tradeProgressEntries[i];
            if (e == null || e.state != FrameworkTradeProgressState.Traveling) continue;
            string dest = ResolveDest(e.caravanId, e.activeRouteId);
            if (!string.IsNullOrEmpty(dest)
                && SaveDataLookup.TryGetCaravan(save, e.caravanId, out var cv) && cv != null)
                cv.currentTownId = dest;
        }
        // 이동 엔트리 제거 → 정박 상태로.
        save.tradeProgressEntries.RemoveAll(e => e != null && e.state == FrameworkTradeProgressState.Traveling);

        RefreshMinimaps();
        Debug.Log("[테스트패널] 즉시 도착(전체)");
    }

    /// <summary>모든 캐러밴을 거점에 정박시키고 진행을 비운다(데모 초기 상태).</summary>
    public void ResetAll()
    {
        var save = GetSaveData();
        if (save == null) return;
        if (save.caravans != null)
            foreach (var c in save.caravans) if (c != null) c.currentTownId = homeTownId;
        if (save.tradeProgressEntries != null) save.tradeProgressEntries.Clear();

        RefreshMinimaps();
        Debug.Log("[테스트패널] 거점 정박으로 리셋(전체)");
    }

    // ------------------------------------------------------------------ 보조

    private static FrameworkSaveData GetSaveData()
    {
        var fr = FrameworkRoot.Instance;
        return fr != null ? fr.CurrentSaveData : null;
    }

    /// <summary>실시간 UTC(진행률 계산과 같은 시계). GameTime 없으면 시스템 UTC.</summary>
    private static DateTime NowUtc()
    {
        var fr = FrameworkRoot.Instance;
        return fr != null && fr.GameTime != null ? fr.GameTime.CurrentUtc : DateTime.UtcNow;
    }

    /// <summary>지정 캐러밴의 이동 엔트리를 tradeProgressEntries에 직접 넣는다(기존 것은 교체).</summary>
    private static void SetTravelingEntry(FrameworkSaveData save, string caravanId, string routeId, long start, long end)
    {
        if (save.tradeProgressEntries == null) save.tradeProgressEntries = new List<FrameworkTradeProgress>();
        save.tradeProgressEntries.RemoveAll(e => e == null || e.caravanId == caravanId);
        save.tradeProgressEntries.Add(new FrameworkTradeProgress
        {
            caravanId = caravanId,
            activeTradeId = "test_" + caravanId,
            activeRouteId = routeId,
            state = FrameworkTradeProgressState.Traveling,
            tradeStartUtcTick = start,
            expectedTradeEndUtcTick = end,
            inGameTimeMultiplierAtStart = 1f,
        });
    }

    /// <summary>즉시 도착 목적지: 기록된 값 우선, 없으면 route id로 유추.</summary>
    private string ResolveDest(string caravanId, string routeId)
    {
        if (!string.IsNullOrEmpty(caravanId) && destByCaravan.TryGetValue(caravanId, out var d)) return d;
        if (routeId == routeToRiver) return riverTownId;
        if (routeId == routeToWindy) return windyTownId;
        return null;
    }

    /// <summary>씬의 모든 WorldMapPresenter를 즉시 갱신(활성 route 표시 반영).</summary>
    private static void RefreshMinimaps()
    {
        foreach (var p in FindObjectsOfType<WorldMapPresenter>(true))
            p.RefreshAll();
    }

    // ------------------------------------------------------------------ UI

    private void EnsureStyles()
    {
        if (btnStyle != null) return;
        boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
        btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
    }

    private void OnGUI()
    {
        if (!showPanel) return;
        EnsureStyles();

        const float w = 360f, h = 64f, pad = 10f, header = 34f;
        float x = panelPos.x, y = panelPos.y;

        GUI.Box(new Rect(x - pad, y - pad, w + pad * 2f, header + (h + pad) * 5f + 30f + pad), "미니맵 캐러밴 테스트", boxStyle);
        y += header;

        if (GUI.Button(new Rect(x, y, w, h), "1번 캐러밴 → 리버타운 (3분)", btnStyle))
            DepartCaravan(0, routeToRiver, riverTownId);
        y += h + pad;

        if (GUI.Button(new Rect(x, y, w, h), "2번 캐러밴 → 윈디타운 (3분)", btnStyle))
            DepartCaravan(1, routeToWindy, windyTownId);
        y += h + pad;

        if (GUI.Button(new Rect(x, y, w, h), "1·2번 동시 출발", btnStyle))
            DepartBoth();
        y += h + pad;

        if (GUI.Button(new Rect(x, y, w, h), "즉시 도착 (전체)", btnStyle))
            InstantArriveAll();
        y += h + pad;

        if (GUI.Button(new Rect(x, y, w, h), "거점으로 리셋 (전체)", btnStyle))
            ResetAll();
        y += h + pad;

        GUI.Label(new Rect(x, y, w, 30f), StatusText(), labelStyle);
    }

    /// <summary>각 캐러밴의 현재 상태(이동중 %/정박 마을) 요약.</summary>
    private string StatusText()
    {
        var save = GetSaveData();
        if (save == null || save.caravans == null || save.caravans.Count == 0) return "상태: (세이브 준비 전)";

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < save.caravans.Count && i < 4; i++)
        {
            var c = save.caravans[i];
            if (c == null) continue;
            var e = FindTraveling(save, c.caravanId);
            if (e != null)
            {
                float p = (e.expectedTradeEndUtcTick > e.tradeStartUtcTick)
                    ? Mathf.Clamp01((float)(NowUtc().Ticks - e.tradeStartUtcTick) / (e.expectedTradeEndUtcTick - e.tradeStartUtcTick))
                    : 1f;
                sb.Append($"{i + 1}번: 이동 {e.activeRouteId} {(p * 100f):0}%   ");
            }
            else sb.Append($"{i + 1}번: 정박 {c.currentTownId}   ");
        }
        return sb.ToString();
    }

    private static FrameworkTradeProgress FindTraveling(FrameworkSaveData save, string caravanId)
    {
        if (save.tradeProgressEntries == null) return null;
        for (int i = 0; i < save.tradeProgressEntries.Count; i++)
        {
            var e = save.tradeProgressEntries[i];
            if (e != null && e.caravanId == caravanId && e.state == FrameworkTradeProgressState.Traveling) return e;
        }
        return null;
    }
}
