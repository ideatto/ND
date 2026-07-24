// =============================================================================
// FullLoopTest — 전체 무역 루프 통합 테스트 HUD (실데이터·실시간 이동)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [용도 — 임시] "가짜 부품 말고 진짜 조립된 자동차"로 전체 루프를 눈으로 본다:
//   실제 SO 데이터 상단 구성(천성욱님 하네스) → 실제 출발 커맨드 →
//   실제 시간이 흐르며 거리 비율대로 진행(TradeProgressCoordinator) →
//   식량 소모(우리 CaravanCalculator가 계산) → 도착 → 정산 → 수령까지.
//
// [누구 것을 쓰나]
//   - TradeStartDebugHarness (천성욱님): 샘플 상단 구성·출발·정산 수령 (ContextMenu를 버튼화)
//   - FrameworkDebugCommands (천성욱님): 인게임 배속·즉시 완료
//   - TradeProgressCoordinator (천성욱님): 실시간 진행 스냅샷 (지도용 API 재사용)
//   - CaravanCalculator·JourneyRunner (우리): 이동 시간·식량 계산은 이 밑에서 돌고 있음
//
// [사용법] 씬에 하네스·디버그커맨드와 함께 배치하고 Play.
//   ① 샘플 상단 채우기 → ② 출발 → 배속 올리고 진행률이 "시간 따라" 차는 걸 관찰
//   → 도착하면 정산 수령. 식량 부족 케이스로 실패 흐름도 확인.
// =============================================================================

using UnityEngine;

/// <summary>실데이터·실시간으로 전체 무역 루프를 확인하는 임시 테스트 HUD.</summary>
public class FullLoopTest : MonoBehaviour
{
    private ND.Framework.TradeStartDebugHarness harness;      // 천성욱님 수동 E2E 하네스
    private float currentMultiplier = 1f;                     // 현재 인게임 배속 (표시용)

    private void Start()
    {
        harness = FindAnyObjectByType<ND.Framework.TradeStartDebugHarness>();
        // 배속·즉시완료는 FrameworkRoot.Instance.DebugCommands로 직접 접근 (별도 배치 불필요)

        // 공용 기준 데이터(SharedGameData) 로드 — 경로 검증(#188)이 이 데이터를 본다.
        // 정식 경로(CompleteLoadingAndEnterGame)는 마지막에 씬을 InGame으로 넘겨버리므로,
        // 테스트 씬에서는 내부 로드 단계만 리플렉션으로 호출한다. [임시 테스트 전용]
        var root = ND.Framework.FrameworkRoot.Instance;
        if (root != null)
        {
            var m = typeof(ND.Framework.FrameworkRoot).GetMethod("EnsureSharedGameDataLoaded",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (m != null)
            {
                bool loaded = (bool)m.Invoke(root, null);
                Debug.Log("[FullLoopTest] 공용 데이터 로드 = " + loaded);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 760, Screen.height - 20));
        GUILayout.Label("<b>[전체 루프 통합 테스트]</b> — 실데이터·실시간 이동 (임시)", Rich(16));

        var root = ND.Framework.FrameworkRoot.Instance;
        if (root == null || root.CurrentSaveData == null)
        {
            GUILayout.Label("FrameworkRoot 기동 대기 중...");
            GUILayout.EndArea();
            return;
        }
        if (harness == null)
        {
            GUILayout.Label("⚠ TradeStartDebugHarness가 씬에 없음 — 같은 씬에 배치 필요");
            GUILayout.EndArea();
            return;
        }
        ND.Framework.FrameworkDebugCommands commands = root.DebugCommands;

        ND.Framework.SaveData save = root.CurrentSaveData;
        ND.Framework.CaravanSaveData car = save.caravan;   // 선택 상단 호환 접근자

        // ── 실시간 상태 표시 ──
        GUILayout.Space(4);
        if (car != null)
        {
            GUILayout.Label(string.Format("<b>상단</b> id={0} | 상태={1} | 위치={2} | 식량 {3} | 마차 {4}",
                Short(save.selectedCaravanId), car.state, car.currentTownId, car.foodAmount,
                (car.wagon != null ? car.wagon.wagonName : "-")), Rich(13));

            // 지도용 실시간 진행 스냅샷 — 시간이 흐르면 여기 %가 저절로 찬다 (거리 비율 이동)
            ND.Framework.TradeMapProgressSnapshot snap;
            if (root.TradeProgressCoordinator != null
                && root.TradeProgressCoordinator.TryGetMapProgress(out snap)
                && snap.HasActiveTrade)
            {
                long remainTicks = snap.ExpectedTradeEndUtcTick - System.DateTime.UtcNow.Ticks;
                double remainSec = remainTicks > 0 ? System.TimeSpan.FromTicks(remainTicks).TotalSeconds : 0.0;
                GUILayout.Label(string.Format(
                    "<b>진행</b> {0:F1}%  |  남은 시간(현실) 약 {1:F0}초  |  배속 x{2:F0}  |  tradeId={3}",
                    snap.ProgressPercent, remainSec, currentMultiplier, Short(snap.ActiveTradeId)), Rich(14));
                // 진행 바
                Rect bar = GUILayoutUtility.GetRect(700, 14);
                GUI.Box(bar, "");
                Rect fill = new Rect(bar.x, bar.y, bar.width * snap.Progress01, bar.height);
                GUI.Box(fill, "", GUI.skin.button);
            }
            else
            {
                GUILayout.Label("<b>진행</b> 활성 무역 없음 (출발 전이거나 정산 단계)", Rich(13));
            }
        }
        else
        {
            GUILayout.Label("상단 없음 — 먼저 [① 샘플 상단 채우기]", Rich(13));
        }

        GUILayout.Label(string.Format("정산 대기 {0}건",
            (save.pendingSettlements != null ? save.pendingSettlements.Count : 0)), Rich(12));

        // ── 멀티 상단: 전체 상단별 상태·실시간 진행률 ──
        GUILayout.Space(6);
        GUILayout.Label("<b>◼ 전체 상단 (멀티)</b> — 상단별 독립 진행 확인", Rich(14));
        if (save.caravans != null)
        {
            foreach (ND.Framework.CaravanSaveData c in save.caravans)
            {
                if (c == null) continue;
                float p = GetLiveProgress(save, c.caravanId);   // 진행 엔트리에서 시간 기반 계산
                string selMark = (c.caravanId == save.selectedCaravanId) ? " ★선택" : "";
                GUILayout.Label(string.Format("  {0}{1} [{2}] 위치={3} 식량={4} 진행={5:F1}%",
                    Short(c.caravanId), selMark, c.state, c.currentTownId, c.foodAmount,
                    p * 100f), Rich(12));
                if (p > 0f && p < 1f)
                {
                    Rect bar2 = GUILayoutUtility.GetRect(700, 8);
                    GUI.Box(bar2, "");
                    GUI.Box(new Rect(bar2.x, bar2.y, bar2.width * p, bar2.height), "", GUI.skin.button);
                }
            }
        }

        // ── 조작 버튼 ──
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("① 샘플 상단 채우기", GUILayout.Width(150))) harness.FillSampleCaravan();
        if (GUILayout.Button("② 출발!", GUILayout.Width(90))) harness.StartTradeAndRecordTime();
        if (GUILayout.Button("식량부족 케이스", GUILayout.Width(120))) harness.SetLowFoodFailureCase();
        if (GUILayout.Button("진행 로그 확인", GUILayout.Width(110))) harness.CheckTradeProgressAndCompletion();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("배속:", GUILayout.Width(40));
        if (GUILayout.Button("x1", GUILayout.Width(50))) { commands.ResetInGameTimeMultiplier(); currentMultiplier = 1f; }
        if (GUILayout.Button("x10", GUILayout.Width(50))) { if (commands.SetInGameTimeMultiplier(10f)) currentMultiplier = 10f; }
        if (GUILayout.Button("x60", GUILayout.Width(50))) { if (commands.SetInGameTimeMultiplier(60f)) currentMultiplier = 60f; }
        if (GUILayout.Button("⏩ 즉시 완료", GUILayout.Width(100))) commands.CompleteTradeImmediately();
        if (GUILayout.Button("정산 수령 + 리셋", GUILayout.Width(130))) harness.ClaimSettlementAndReset();
        GUILayout.EndHorizontal();

        // ── 멀티 상단 조작: B 추가 + caravanId 기반 출발 (#188 커맨드 직접 호출) ──
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+ B 상단 추가", GUILayout.Width(110))) AddSecondCaravan(save);
        if (GUILayout.Button("B 출발! (ID 기반)", GUILayout.Width(130)))
        {
            // 경로는 실존 ID여야 함 — #188 커맨드는 SharedGameData로 검증한다 ("BaseRoute"는 가짜라 거부됨)
            var req = new ND.Framework.TradeDepartureRequest { CaravanId = "debug_caravan_B", RouteId = "BaseToRiver" };
            var res = root.TradeStart.Depart(req);
            Debug.Log("[FullLoopTest] B 출발 → 성공=" + res.DepartureSucceeded + " 사유=" + res.FailureReason);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("순서: ①채우기 → ②출발 → [+B 추가] → [B 출발] → 두 진행바가 따로 차는 것 확인", Rich(11));
        GUILayout.EndArea();
    }

    /// <summary>
    /// 두 번째 상단(debug_caravan_B)을 저장 목록에 추가한다.
    /// 선택 상단 DTO를 JSON 복제 후 ID·개체ID만 바꿈 — 테스트 전용 저장 조작(제품 코드는 CreateCaravan 커맨드 예정).
    /// </summary>
    private void AddSecondCaravan(ND.Framework.SaveData save)
    {
        if (save.caravans == null || save.caravan == null) { Debug.Log("[FullLoopTest] 먼저 ①채우기"); return; }
        foreach (ND.Framework.CaravanSaveData c in save.caravans)
            if (c != null && c.caravanId == "debug_caravan_B") { Debug.Log("[FullLoopTest] B는 이미 있음"); return; }

        // JSON 왕복으로 깊은 복제 (SaveData DTO는 전부 직렬화 가능)
        string json = JsonUtility.ToJson(save.caravan);
        ND.Framework.CaravanSaveData clone = JsonUtility.FromJson<ND.Framework.CaravanSaveData>(json);
        clone.caravanId = "debug_caravan_B";
        // 자산 개체 ID를 B 전용으로 교체 — A와 같은 마차·동물을 공유하면 자산 잠금 위반 상황이 됨
        if (clone.wagon != null) clone.wagon.instanceId = "debug_wagon_B";
        if (clone.animals != null)
            for (int i = 0; i < clone.animals.Count; i++)
                if (clone.animals[i] != null) clone.animals[i].instanceId = "debug_animal_B_" + i;
        if (clone.mercenaries != null)
            for (int i = 0; i < clone.mercenaries.Count; i++)
                if (clone.mercenaries[i] != null) clone.mercenaries[i].instanceId = "debug_merc_B_" + i;

        // A가 이미 출발한 뒤 복제했을 수 있으므로 여정 상태를 준비로 리셋 (안 하면 가짜 Traveling이 됨)
        clone.state = JourneyState.Prepare;
        clone.progress01 = 0f;
        clone.elapsedInGameSeconds = 0f;
        clone.settlementClaimed = false;

        save.caravans.Add(clone);
        Debug.Log("[FullLoopTest] B 상단 추가 완료 (id=debug_caravan_B, 상태=Prepare 리셋)");
    }

    /// <summary>상단별 실시간 진행률 — 진행 엔트리의 시작·예상종료 시각으로 계산 (0~1).</summary>
    private static float GetLiveProgress(ND.Framework.SaveData save, string caravanId)
    {
        if (save.tradeProgressEntries == null) return 0f;
        foreach (ND.Framework.TradeProgressSaveData e in save.tradeProgressEntries)
        {
            if (e == null || e.caravanId != caravanId) continue;
            long total = e.expectedTradeEndUtcTick - e.tradeStartUtcTick;
            if (total <= 0) return 0f;
            float p = (float)((double)(System.DateTime.UtcNow.Ticks - e.tradeStartUtcTick) / total);
            return Mathf.Clamp01(p);
        }
        return 0f;
    }

    /// <summary>긴 ID를 앞 8자만 표시.</summary>
    private static string Short(string id)
    {
        if (string.IsNullOrEmpty(id)) return "-";
        return id.Length <= 8 ? id : id.Substring(0, 8);
    }

    private static GUIStyle Rich(int size)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.richText = true;
        s.fontSize = size;
        return s;
    }
}
