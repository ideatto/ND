// =============================================================================
// MultiCaravanJourneyTest — 복수 상단 이동·정산 흐름 확인용 임시 테스트 HUD
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [용도 — 임시] 7/27~28 마일스톤(이동중·식량·도착·실패·정산·중복 Claim 차단)을
//   UI 없이 눈으로 확인하는 테스트 페이지. 예전 JourneyRunTest처럼
//   실제 화면(이종현님 UI)이 붙으면 이 파일은 지운다.
//
// [사용법] 빈 GameObject에 붙이고 Play → 화면 왼쪽 HUD에서 버튼 클릭.
//   상단 2개가 만들어진다:
//   - 상단 A "튼튼이": 식량 넉넉 + 용병 1 → 정상 도착 흐름 확인용
//   - 상단 B "아슬이": 식량 빠듯 + 용병 0 → 식량 고갈 실패 흐름 확인용
//
// [확인 포인트]
//   1. 두 상단이 서로 독립적으로 진행되는가 (A 조작이 B에 안 섞이는가)
//   2. 식량 고갈 → 유예시간 지나면 Failed 되는가
//   3. 약탈 → 용병 있으면 방어, 없으면 내구도·화물 손실 (내구도 0이면 파괴 전손)
//   4. 정산 수령을 두 번 누르면 두 번째는 차단되는가 (중복 Claim 차단)
//   5. 완료 → 준비 리셋 후 재출발이 되는가
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>복수 상단 여정 흐름을 버튼으로 확인하는 임시 테스트 HUD.</summary>
public class MultiCaravanJourneyTest : MonoBehaviour
{
    private readonly List<CaravanData> caravans = new List<CaravanData>();   // 테스트 상단들
    private readonly List<JourneyResultData> results = new List<JourneyResultData>(); // 상단별 마지막 정산 결과
    private readonly List<string> logs = new List<string>();                 // 화면 하단 로그

    private void Start()
    {
        // 상단 A "튼튼이" — 식량 넉넉, 용병 1 (정상 도착용)
        caravans.Add(BuildCaravan("car_A", "튼튼이", foodAmount: 100, mercCount: 1));
        // 상단 B "아슬이" — 식량 빠듯, 용병 0 (식량 고갈 실패용)
        caravans.Add(BuildCaravan("car_B", "아슬이", foodAmount: 3, mercCount: 0));
        results.Add(null);
        results.Add(null);
        Log("테스트 상단 2개 생성 완료 (A: 튼튼이 / B: 아슬이)");
    }

    /// <summary>테스트용 상단 하나 조립 (마차+동물+화물+식량+용병).</summary>
    private CaravanData BuildCaravan(string id, string name, int foodAmount, int mercCount)
    {
        // 마차 — 견인 1~4마리, 최대적재 여유, 내구도 20 (약탈 몇 번이면 파괴되는 값)
        // ※ imsi 데이터는 ScriptableObject가 아니라 일반 클래스 → new로 생성
        imsiWagonData wagon = new imsiWagonData();
        wagon.wagonName = name + "의 마차";
        wagon.instanceId = id + "_wagon";
        wagon.minAnimals = 1; wagon.maxAnimals = 4;
        wagon.overLoad = 500f; wagon.maxLoad = 700f;   // overLoad=적정(감속 기준) < maxLoad=물리 상한
        wagon.maxDurability = 20;
        wagon.inventorySlotCount = 10;

        // 견인 동물 1마리
        imsiAnimalData animal = new imsiAnimalData();
        animal.animalName = "말";
        animal.instanceId = id + "_animal";
        animal.speed = 1f;         // 이동 속도 배수 (말=1 기준)
        animal.foodPerKm = 0.1f;   // 인게임 1초당 식량 소모율 (필드명은 rename 예정)

        // 무역품 (사과 10개)
        imsiTradeItemData item = new imsiTradeItemData();
        item.id = "apple"; item.itemName = "사과"; item.weight = 1f; item.maxCount = 99;
        CargoEntry entry = new CargoEntry { item = item, quantity = 10 };

        CaravanData c = new CaravanData();
        c.caravanId = id;
        c.wagon = wagon;
        c.animals.Add(animal);
        c.cargo.Add(entry);
        c.foodAmount = foodAmount;
        c.currentDurability = wagon.maxDurability;
        c.starveGraceSeconds = 10f;    // 식량 바닥 후 10초(진행도 환산) 버티면 실패
        c.lossLimitRate = 0.5f;        // 약탈 손실 상한 50%

        // 용병
        for (int i = 0; i < mercCount; i++)
        {
            imsiMercenaryData merc = new imsiMercenaryData();
            merc.mercName = "용병" + (i + 1);
            merc.instanceId = id + "_merc" + i;
            c.mercenaries.Add(merc);
        }
        return c;
    }

    // =========================================================================
    // HUD
    // =========================================================================
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 640, Screen.height - 20));
        GUILayout.Label("<b>[복수 상단 여정 테스트]</b> — 7/27~28 검증용 (임시)", RichStyle(16));

        for (int i = 0; i < caravans.Count; i++)
            DrawCaravanRow(i);

        // 로그 (최근 8줄)
        GUILayout.Space(8);
        GUILayout.Label("── 로그 ──");
        int start = (logs.Count > 8) ? logs.Count - 8 : 0;
        for (int i = start; i < logs.Count; i++)
            GUILayout.Label(logs[i]);

        GUILayout.EndArea();
    }

    /// <summary>상단 한 줄 — 상태 표시 + 조작 버튼.</summary>
    private void DrawCaravanRow(int idx)
    {
        CaravanData c = caravans[idx];
        float food = CaravanCalculator.GetRemainingFood(c);

        GUILayout.Space(6);
        // 상태 요약 한 줄
        string summary = string.Format(
            "<b>{0}</b> [{1}] 진행 {2:P0} | 식량 {3:F1} | 내구도 {4}/{5} | 화물 {6}개{7}",
            c.caravanId, c.state, c.progress01, food,
            c.currentDurability, (c.wagon != null ? c.wagon.maxDurability : 0),
            CaravanCalculator.GetCargoCount(c),
            (c.runFatalReason != JourneyFailureReason.None ? " | ⚠ " + c.runFatalReason : ""));
        GUILayout.Label(summary, RichStyle(13));

        // 마지막 정산 결과 표시
        if (results[idx] != null)
        {
            JourneyResultData r = results[idx];
            GUILayout.Label(string.Format(
                "   └ 정산: {0} | 화물손실 {1} | 내구도손실 {2} | 식량소모 {3:F1} | 파괴 {4}",
                r.grade, r.cargoLost, r.durabilityLost, r.foodConsumed, r.wagonDestroyed));
        }

        // 버튼 줄
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("출발(100km)", GUILayout.Width(100)))
        {
            DepartureValidationResult v = JourneyRunner.TryDepart(c, 100f);
            Log(c.caravanId + " 출발 → " + (v.canDepart ? "성공" : "차단: " + string.Join(",", v.reasons)));
        }
        if (GUILayout.Button("진행 +10%", GUILayout.Width(90)))
        {
            JourneyRunner.SetProgress(c, c.progress01 + 0.1f);
            Log(c.caravanId + " 진행 → " + c.progress01.ToString("P0"));
        }
        if (GUILayout.Button("약탈!", GUILayout.Width(60)))
        {
            bool defended = JourneyRunner.ResolveRaid(c, durabilityDamage: 8, cargoDamage: 3);
            Log(c.caravanId + " 약탈 → " + (defended ? "용병이 방어함" : "약탈당함(내구-8, 화물-3)"));
        }
        if (GUILayout.Button("정산", GUILayout.Width(60)))
        {
            JourneyResultData r = JourneyRunner.Settle(c);
            if (r != null) { results[idx] = r; Log(c.caravanId + " 정산 → " + r.grade); }
            else Log(c.caravanId + " 정산 불가 (도착 전 + 치명상태 아님)");
        }
        if (GUILayout.Button("수령", GUILayout.Width(60)))
        {
            bool ok = JourneyRunner.ClaimSettlement(c);
            Log(c.caravanId + " 수령 → " + (ok ? "성공" : "차단(중복 또는 정산대기 아님)"));
        }
        if (GUILayout.Button("리셋", GUILayout.Width(60)))
        {
            bool ok = JourneyRunner.ResetToPrepare(c);
            Log(c.caravanId + " 리셋 → " + (ok ? "준비 단계로" : "차단(완료 상태 아님)"));
        }
        GUILayout.EndHorizontal();
    }

    private void Log(string msg)
    {
        logs.Add(string.Format("[{0:HH:mm:ss}] {1}", System.DateTime.Now, msg));
        Debug.Log("[MultiCaravanJourneyTest] " + msg);
    }

    /// <summary>리치텍스트 지원 라벨 스타일.</summary>
    private static GUIStyle RichStyle(int size)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.richText = true;
        s.fontSize = size;
        return s;
    }
}
