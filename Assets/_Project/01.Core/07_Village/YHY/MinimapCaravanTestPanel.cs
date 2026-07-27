// =============================================================================
// MinimapCaravanTestPanel — 미니맵 캐러밴 이동을 눈으로 확인하는 테스트 버튼 패널
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 개발 검증용 테스트 도구 (실제 게임플로우 아님)
//
// [역할] 화면에 테스트 버튼(OnGUI)을 띄워, 캐러밴을 거점→무역마을로 "출발"시키고
//        미니맵에서 마커가 3분에 걸쳐 이동하는 걸 보여준다. 즉시 도착 버튼도 제공.
//
// [원리] 정헌님/성욱님의 무역 시스템 코드는 전혀 건드리지 않는다.
//        미니맵(WorldMapPresenter)은 매 프레임 TradeProgressCoordinator.TryGetMapProgress로
//        진행 스냅샷을 읽어 마커를 route 위로 움직인다. 그 스냅샷의 원천이 곧
//        SaveData.tradeProgress(선택 캐러밴의 진행 엔트리)이므로,
//        여기서는 tradeProgress만 직접 세팅해 이동 애니메이션을 구동한다.
//        진행률 = (CurrentUtc - tradeStartUtcTick) / (expectedTradeEndUtcTick - tradeStartUtcTick)
//        (GameTimeService.CurrentUtc == 실시간) → end-start=180초면 정확히 3분.
//
// [route] BaseToRiver(거점→RiverTown), BaseToWindy(거점→WindyTown) — 미니맵 RouteVisual과 일치.
//
// [부착] InGame_Test 씬의 테스트용 GameObject에 붙인다. 저장(SaveService.Save)은 호출하지 않아
//        메모리상 상태만 바꾼다(다른 씬/세이브에 영향 없음).
// =============================================================================

using System;
using UnityEngine;
using ND.Framework;
using ND.UI.WorldMap;
// 전역(Sandbox) 동명 타입과 충돌하므로 Framework 타입을 alias로 고정한다.
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkTradeProgress = ND.Framework.TradeProgressSaveData;
using FrameworkTradeProgressState = ND.Framework.TradeProgressState;

/// <summary>미니맵 캐러밴 이동을 시연하는 개발용 테스트 버튼 패널.</summary>
public class MinimapCaravanTestPanel : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float travelSeconds = 180f;          // 이동 시간(초) — 기본 3분
    [SerializeField] private string homeTownId = "BaseCamp";      // 거점(출발지) townId

    [Header("목적지 route (미니맵 RouteVisual의 RouteId와 일치해야 함)")]
    [SerializeField] private string routeToRiver = "BaseToRiver"; // 거점 → RiverTown
    [SerializeField] private string riverTownId = "RiverTown";
    [SerializeField] private string routeToWindy = "BaseToWindy"; // 거점 → WindyTown
    [SerializeField] private string windyTownId = "WindyTown";

    [Header("UI")]
    [SerializeField] private bool showPanel = true;               // 패널 표시 여부
    [SerializeField] private Vector2 panelPos = new Vector2(20f, 200f); // 화면 좌상단 기준 위치(px)

    private string lastDestTownId;   // 즉시 도착 시 캐러밴을 놓을 목적지(마지막 출발의 도착지)

    // (자동 초기화 없음 — 캐러밴은 기본적으로 거점에 있고, 필요하면 "④ 거점으로 리셋" 버튼 사용.
    //  Update에서 자동 리셋하면 수동 출발과 레이스가 나 출발이 지워질 수 있어 두지 않는다.)

    // ------------------------------------------------------------------ 동작

    /// <summary>거점에서 지정 route로 출발 — 미니맵 마커가 travelSeconds 동안 이동한다.</summary>
    public void Depart(string routeId, string destTownId)
    {
        var save = GetSaveData();
        var caravan = EnsureSelectedCaravan(save);
        if (caravan == null) { Debug.LogWarning("[테스트패널] 세이브/캐러밴이 아직 준비되지 않음"); return; }

        caravan.currentTownId = homeTownId;   // 출발은 거점에서
        lastDestTownId = destTownId;

        // 실시간 기준 시작/도착 tick — GameTime.CurrentUtc(==실시간)과 같은 시계를 쓴다.
        long startTick = NowUtc().Ticks;
        long endTick = startTick + (long)(travelSeconds * TimeSpan.TicksPerSecond);

        // 선택 캐러밴의 진행 엔트리로 세팅(프로퍼티 setter가 caravanId·리스트 처리).
        save.tradeProgress = new FrameworkTradeProgress
        {
            activeTradeId = "test_trade",
            activeRouteId = routeId,
            state = FrameworkTradeProgressState.Traveling,
            tradeStartUtcTick = startTick,
            expectedTradeEndUtcTick = endTick,
            inGameTimeMultiplierAtStart = 1f,
        };

        RefreshMinimaps();
        Debug.Log($"[테스트패널] 출발: {homeTownId} → {destTownId} (route={routeId}, {travelSeconds}s)");
    }

    /// <summary>이동 중인 캐러밴을 즉시 도착 처리 — 목적지에 정박시키고 진행을 비운다.</summary>
    public void InstantArrive()
    {
        var save = GetSaveData();
        var caravan = EnsureSelectedCaravan(save);
        if (caravan == null) return;

        if (!string.IsNullOrEmpty(lastDestTownId))
            caravan.currentTownId = lastDestTownId;   // 목적지에 도착
        save.tradeProgress = null;                    // 진행 엔트리 제거 → 정박 상태

        RefreshMinimaps();
        Debug.Log($"[테스트패널] 즉시 도착 → {caravan.currentTownId}");
    }

    /// <summary>캐러밴을 거점에 정박시키고 진행을 비운다(데모 초기 상태).</summary>
    public void ResetToBase()
    {
        var save = GetSaveData();
        var caravan = EnsureSelectedCaravan(save);
        if (caravan == null) return;

        caravan.currentTownId = homeTownId;
        save.tradeProgress = null;

        RefreshMinimaps();
        Debug.Log($"[테스트패널] 거점 정박으로 리셋 → {homeTownId}");
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

    /// <summary>
    /// 선택 캐러밴을 보장한다. 선택이 없으면 첫 캐러밴을 선택하고, 캐러밴이 하나도 없으면 만든다.
    /// </summary>
    private static FrameworkCaravanSaveData EnsureSelectedCaravan(FrameworkSaveData save)
    {
        if (save == null) return null;
        if (save.caravans == null || save.caravans.Count == 0) return null; // 신규 생성은 게임 플로우에 맡김

        // 선택 ID가 유효하면 그대로, 아니면 첫 캐러밴을 선택.
        if (SaveDataLookup.TryGetCaravan(save, save.selectedCaravanId, out var selected))
            return selected;

        var first = save.caravans[0];
        if (first != null && !string.IsNullOrEmpty(first.caravanId))
        {
            save.selectedCaravanId = first.caravanId;
            return first;
        }
        return null;
    }

    /// <summary>씬의 모든 WorldMapPresenter를 즉시 갱신(활성 route 표시 + 마커/정박 아이콘 반영).</summary>
    private static void RefreshMinimaps()
    {
        foreach (var p in FindObjectsOfType<WorldMapPresenter>(true))
            p.RefreshAll();
    }

    // ------------------------------------------------------------------ UI

    private void OnGUI()
    {
        if (!showPanel) return;

        const float w = 220f, h = 40f, pad = 8f;
        float x = panelPos.x, y = panelPos.y;

        GUI.Box(new Rect(x - pad, y - pad - 24f, w + pad * 2f, h * 4f + pad * 5f + 24f), "미니맵 캐러밴 테스트");
        y += 4f;

        if (GUI.Button(new Rect(x, y, w, h), "① 리버타운으로 출발 (3분)"))
            Depart(routeToRiver, riverTownId);
        y += h + pad;

        if (GUI.Button(new Rect(x, y, w, h), "② 윈디타운으로 출발 (3분)"))
            Depart(routeToWindy, windyTownId);
        y += h + pad;

        if (GUI.Button(new Rect(x, y, w, h), "③ 즉시 도착"))
            InstantArrive();
        y += h + pad;

        if (GUI.Button(new Rect(x, y, w, h), "④ 거점으로 리셋"))
            ResetToBase();
        y += h + pad;

        // 현재 진행 상태 표시(디버그).
        GUI.Label(new Rect(x, y, w, 22f), StatusText());
    }

    /// <summary>현재 진행 스냅샷을 사람이 읽을 문자열로.</summary>
    private static string StatusText()
    {
        var fr = FrameworkRoot.Instance;
        if (fr == null || fr.TradeProgressCoordinator == null) return "상태: (프레임워크 준비 전)";
        if (fr.TradeProgressCoordinator.TryGetMapProgress(out var snap) && snap.HasActiveTrade)
            return $"이동중 {snap.ActiveRouteId} {(snap.Progress01 * 100f):0}%";

        var save = fr.CurrentSaveData;
        string town = null;
        if (save != null && save.caravans != null && save.caravans.Count > 0 && save.caravans[0] != null)
            town = save.caravans[0].currentTownId;
        return $"정박: {(string.IsNullOrEmpty(town) ? "-" : town)}";
    }
}
