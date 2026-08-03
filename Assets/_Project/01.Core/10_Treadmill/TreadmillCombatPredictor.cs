// =============================================================================
// TreadmillCombatPredictor — 무역 중 '전투가 터질 progress 지점'을 결정론으로 미리 계산
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 전투 연출
//
// [왜] 산적을 도착 마을처럼 'N초 전에 미리' 도로에 띄우려면, 전투가 어느 진행도에서
//      발생할지 미리 알아야 한다. 다행히 게임의 전투 판정(TradeRouteEventProcessor)은
//      완전 결정론적(StableHash(tradeId, checkIndex))이라, 같은 판정을 재현하면
//      실제 발생 지점과 100% 일치한다.
//
// [무엇] 표시 중 캐러밴의 현재 무역에 대해, route 데이터(Events/MaxEventCount/BaseRiskLevel)와
//        StableHash를 그대로 재현해 '전투 발생 progress 목록'을 돌려준다. 어느 루트든 데이터로
//        동작(하드코딩 없음). ★결과(승/패)는 여기서 계산하지 않는다 — 그건 활동 로그에서 읽어
//        UI(CaravanCombatSequencePanel)와 똑같이 맞춘다.
//
// [주의] TradeRouteEventProcessor.StableHash / 판정 순서와 반드시 동일해야 한다(바뀌면 같이 갱신).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>표시 중 캐러밴 무역의 '전투 발생 progress 지점'을 결정론으로 예측한다(타이밍만, 결과는 로그).</summary>
public static class TreadmillCombatPredictor
{
    /// <summary>한 전투 발생 지점.</summary>
    public struct CombatPoint
    {
        public int checkIndex;      // 몇 번째 체크에서
        public float progress01;    // 발생 progress = (checkIndex+1)/MaxEventCount
        public string eventId;      // routeEventId — 활동 로그에서 결과 매칭용
    }

    /// <summary>caravanId의 현재 무역에서 전투가 터질 지점들을 계산(진행도 순). 없으면 빈 리스트.</summary>
    public static List<CombatPoint> Predict(string caravanId)
    {
        var list = new List<CombatPoint>();
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        var shared = fr != null ? fr.SharedGameData : null;
        if (save == null || shared == null || string.IsNullOrEmpty(caravanId)) return list;

        if (!ND.Framework.SaveDataLookup.TryGetTradeProgress(save, caravanId, out var entry) || entry == null) return list;
        if (entry.state != ND.Framework.TradeProgressState.Traveling) return list;
        string tradeId = entry.activeTradeId;
        if (string.IsNullOrEmpty(tradeId) || string.IsNullOrEmpty(entry.activeRouteId)) return list;
        if (!shared.TryGetRoute(entry.activeRouteId, out var route) || route == null) return list;

        var events = route.Events;
        int maxChecks = route.MaxEventCount;
        if (events == null || events.Length == 0 || maxChecks <= 0 || route.Distance <= 0f) return list;

        float baseRisk = Mathf.Clamp01(route.BaseRiskLevel);
        var now = (fr.GameTime != null) ? fr.GameTime.CurrentUtc : System.DateTime.UtcNow;
        // 전투 발생 게이트 배율(월드/퀘스트 상태 반영) — 게임과 동일 함수 사용(하드코딩 아님).
        float combatMul = Mathf.Clamp01(
            ND.Framework.QuestRuntimeService.ResolveBanditEncounterMultiplier(save.world, route, now));

        // TradeRouteEventProcessor.Process와 동일한 체크별 판정.
        for (int k = 0; k < maxChecks; k++)
        {
            if (ToUnit(StableHash(tradeId, k, "occur")) >= baseRisk) continue;        // 발생 안 함
            int ei = (int)(StableHash(tradeId, k, "select") % (uint)events.Length);   // 이벤트 선택
            var ev = events[ei];
            if (ev == null || ev.EventType != RouteEvent.Combat) continue;
            if (ToUnit(StableHash(tradeId, k, "combat")) >= combatMul) continue;      // 전투 게이트 통과 못함
            list.Add(new CombatPoint
            {
                checkIndex = k,
                progress01 = (float)(k + 1) / maxChecks,   // 체크 k는 traveled≥(k+1)×interval에서 처리됨
                eventId = ev.Id
            });
        }
        return list;
    }

    // ── TradeRouteEventProcessor.StableHash 재현(FNV-1a: tradeId|checkIndex|purpose) ──
    private static uint StableHash(string tradeId, int checkIndex, string purpose)
    {
        const uint offset = 2166136261u, prime = 16777619u;
        uint hash = offset;
        Append(ref hash, tradeId, prime);
        Append(ref hash, "|", prime);
        Append(ref hash, checkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), prime);
        Append(ref hash, "|", prime);
        Append(ref hash, purpose, prime);
        return hash;
    }

    private static void Append(ref uint hash, string value, uint prime)
    {
        if (value == null) return;
        for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= prime; }
    }

    private static float ToUnit(uint value) => (float)(value / 4294967296d);
}
