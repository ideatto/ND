// =============================================================================
// MinimapMultiCaravanMarkers — 미니맵에 "여러 캐러밴"을 동시에 색으로 구분해 표시
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 프레임워크 미니맵(WorldMapPresenter+CaravanMapMarker)은 "선택 캐러밴 1대"만
//        그린다. 하지만 무역 진행 데이터(SaveData.tradeProgressEntries)는 캐러밴별
//        리스트라 여러 대가 동시에 이동할 수 있다. 이 컴포넌트는 모든 캐러밴을 읽어
//        - 이동 중이면 해당 route(RouteVisual) 위 진행 위치에,
//        - 정박 중이면 현재 마을(TownWorldView) 위에
//        슬롯별 색 마커를 찍어 "여러 캐러밴 동시 이동"을 눈으로 볼 수 있게 한다.
//
// [비침습] 정헌님/성욱님 코드는 읽기만 한다. 진행률은 TryGetMapProgress와 동일한
//        UTC tick 공식으로 계산(= (now-start)/(end-start)).
//
// [부착] V2 렌더 루트(WorldMapRenderRootV2)에 붙인다. 기존 단일 표시(프레임워크
//        CaravanMapMarker, MinimapCaravanIndicator)와 겹치지 않게 그것들은 숨긴다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using ND.Framework;
using ND.UI.WorldMap;
// 전역(Sandbox) 동명 타입과 충돌하므로 Framework 타입을 alias로 고정한다.
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkTradeProgress = ND.Framework.TradeProgressSaveData;
using FrameworkTradeProgressState = ND.Framework.TradeProgressState;

/// <summary>여러 캐러밴을 슬롯 색으로 구분해 미니맵에 동시 표시한다.</summary>
public class MinimapMultiCaravanMarkers : MonoBehaviour
{
    [SerializeField] private Transform renderRoot;              // 마을/경로 탐색 범위(비면 자기 자신)
    [SerializeField] private float iconScale = 0.4f;            // 마커 크기(월드)
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.35f, 0f); // 마을/경로 위로 살짝
    [SerializeField] private bool hideFrameworkSingleMarker = true; // 프레임워크 단일 마커/구 인디케이터 숨김

    // 슬롯(캐러밴)별 색 — 1번=주황, 2번=파랑, 3번=초록, 4번=노랑
    private static readonly Color[] SlotColors =
    {
        new Color(0.95f, 0.40f, 0.15f), // slot 0
        new Color(0.20f, 0.60f, 0.95f), // slot 1
        new Color(0.30f, 0.85f, 0.35f), // slot 2
        new Color(0.95f, 0.85f, 0.20f), // slot 3
    };

    private readonly Dictionary<string, SpriteRenderer> markers = new Dictionary<string, SpriteRenderer>();
    private RouteVisual[] routes;
    private TownWorldView[] towns;

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
    }

    private void LateUpdate()
    {
        var fr = FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null) { HideAll(); return; }

        // 프레임워크 단일 표시가 켜져 있으면 매 프레임 숨긴다(WorldMapPresenter가 다시 켜므로 LateUpdate에서 눌러 이김).
        if (hideFrameworkSingleMarker) HideSingleDisplays();

        // 경로/마을 참조 캐시(런타임 중 구조 변화 없다고 가정, 최초 1회).
        if (routes == null) routes = renderRoot.GetComponentsInChildren<RouteVisual>(true);
        if (towns == null) towns = renderRoot.GetComponentsInChildren<TownWorldView>(true);

        var used = new HashSet<string>();
        for (int i = 0; i < save.caravans.Count; i++)
        {
            var c = save.caravans[i];
            if (c == null || string.IsNullOrEmpty(c.caravanId)) continue;

            Vector3 pos;
            if (!TryResolvePosition(save, c, out pos)) continue;

            var m = GetOrCreateMarker(c.caravanId, SlotColors[Mathf.Abs(c.slotIndex) % SlotColors.Length]);
            m.transform.position = pos + offset;
            m.enabled = true;
            used.Add(c.caravanId);
        }

        // 이번 프레임에 안 쓰인 마커는 숨긴다.
        foreach (var kv in markers)
            if (!used.Contains(kv.Key) && kv.Value != null) kv.Value.enabled = false;
    }

    /// <summary>이동 중이면 경로 위 진행 위치, 정박 중이면 현재 마을 위치를 구한다.</summary>
    private bool TryResolvePosition(FrameworkSaveData save, FrameworkCaravanSaveData c, out Vector3 pos)
    {
        pos = Vector3.zero;

        // 이동 중 엔트리 찾기
        FrameworkTradeProgress entry = FindTravelingEntry(save, c.caravanId);
        if (entry != null)
        {
            var route = FindRoute(entry.activeRouteId);
            if (route != null)
            {
                pos = route.EvaluatePosition(CalcProgress(entry));
                return true;
            }
            // route를 못 찾으면 정박 위치로 폴백
        }

        // 정박: 현재 마을 위
        var town = FindTown(c.currentTownId);
        if (town != null) { pos = town.transform.position; return true; }
        return false;
    }

    private static FrameworkTradeProgress FindTravelingEntry(FrameworkSaveData save, string caravanId)
    {
        if (save.tradeProgressEntries == null) return null;
        for (int i = 0; i < save.tradeProgressEntries.Count; i++)
        {
            var e = save.tradeProgressEntries[i];
            if (e != null && e.caravanId == caravanId && e.state == FrameworkTradeProgressState.Traveling)
                return e;
        }
        return null;
    }

    /// <summary>진행률 = (now - start) / (end - start), 0~1. TryGetMapProgress와 동일 공식.</summary>
    private static float CalcProgress(FrameworkTradeProgress e)
    {
        if (e.tradeStartUtcTick <= 0 || e.expectedTradeEndUtcTick <= e.tradeStartUtcTick) return 1f;
        var fr = FrameworkRoot.Instance;
        long now = (fr != null && fr.GameTime != null) ? fr.GameTime.CurrentUtc.Ticks : System.DateTime.UtcNow.Ticks;
        float p = (float)(now - e.tradeStartUtcTick) / (e.expectedTradeEndUtcTick - e.tradeStartUtcTick);
        return Mathf.Clamp01(p);
    }

    private RouteVisual FindRoute(string routeId)
    {
        if (string.IsNullOrEmpty(routeId) || routes == null) return null;
        for (int i = 0; i < routes.Length; i++)
            if (routes[i] != null && routes[i].RouteId == routeId) return routes[i];
        return null;
    }

    private TownWorldView FindTown(string townId)
    {
        if (string.IsNullOrEmpty(townId) || towns == null) return null;
        for (int i = 0; i < towns.Length; i++)
            if (towns[i] != null && towns[i].TownId == townId) return towns[i];
        return null;
    }

    private SpriteRenderer GetOrCreateMarker(string caravanId, Color color)
    {
        if (markers.TryGetValue(caravanId, out var sr) && sr != null)
        {
            sr.color = color;   // 슬롯 색 최신화
            return sr;
        }
        var go = new GameObject("CaravanMarker_" + caravanId);
        go.transform.SetParent(renderRoot, false);
        go.transform.localScale = Vector3.one * iconScale;
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSquareSprite();
        sr.color = color;
        sr.sortingOrder = 32;   // 마을(10)·구 인디케이터(30)보다 위
        markers[caravanId] = sr;
        return sr;
    }

    private void HideAll()
    {
        foreach (var kv in markers) if (kv.Value != null) kv.Value.enabled = false;
    }

    /// <summary>프레임워크 단일 마커와 구 정박 인디케이터를 숨겨 중복 표시를 막는다.</summary>
    private void HideSingleDisplays()
    {
        foreach (var mk in renderRoot.GetComponentsInChildren<CaravanMapMarker>(true))
            if (mk.gameObject.activeSelf) mk.gameObject.SetActive(false);
        var old = renderRoot.Find("CaravanLocationIndicator");
        if (old != null)
        {
            var sr = old.GetComponent<SpriteRenderer>();
            if (sr != null && sr.enabled) sr.enabled = false;
        }
    }

    private static Sprite cachedSquare;
    private static Sprite MakeSquareSprite()
    {
        if (cachedSquare != null) return cachedSquare;
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var px = new Color[64];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white; // 흰색 + SpriteRenderer.color로 착색
        tex.SetPixels(px); tex.Apply();
        tex.filterMode = FilterMode.Point;
        cachedSquare = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        return cachedSquare;
    }
}
