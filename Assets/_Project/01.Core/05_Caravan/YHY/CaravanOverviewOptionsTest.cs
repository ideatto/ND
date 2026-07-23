// =============================================================================
// CaravanOverviewOptionsTest — Overview 조립기·출발 판정기 확인용 임시 테스트 HUD
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [용도 — 임시] 오늘(7/23) 만든 두 조각을 눈으로 확인한다:
//   - CaravanOverviewBuilder : 상단들 → 고정 4슬롯 화면 스냅샷 (Occupied/Empty/Locked)
//   - CaravanDepartureOptions: 상단마다 "출발 선택 가능? 왜 안 돼?" 판정
//   실제 화면(이종현님 어댑터)이 연결되면 이 파일은 지운다.
//
// [사용법] 빈 GameObject에 붙이고 Play.
//   왼쪽 = Overview 슬롯 4개 (조립기 결과)
//   오른쪽 = 출발 선택 판정 (판정기 결과)
//   아래 버튼으로 상단 상태를 바꾸면 → 두 결과가 같이 바뀌는 걸 확인한다.
//
// [확인 포인트]
//   1. 슬롯 배치: A→0번, B→2번 (주입한 배정대로), 1번 Empty, 3번 Locked+안내문
//   2. 판정 사유: 이동중/구성오류/경로없음이 각각 다른 사유로 차단되는가
//   3. 상태 변경 → 새로고침 → 판정·슬롯이 함께 갱신되는가 (Provider 재조회 흐름 흉내)
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Overview 조립기와 출발 판정기를 화면으로 확인하는 임시 테스트 HUD.</summary>
public class CaravanOverviewOptionsTest : MonoBehaviour
{
    private readonly List<CaravanData> caravans = new List<CaravanData>();

    // 화면에 그릴 최신 결과 (새로고침 때만 다시 조립 — 스냅샷 방식 흉내)
    private CaravanOverviewViewData overview;
    private List<CaravanDepartureOption> options;

    private void Start()
    {
        // A: 정상 구성 + 경로 있는 도시 → 유일하게 "선택 가능"이어야 함
        caravans.Add(Build("car_A", "town_A", withWagon: true));
        // B: 정상 구성인데 이동 중 → NotInPrepare 차단
        CaravanData b = Build("car_B", "town_A", withWagon: true);
        b.state = JourneyState.Traveling;
        caravans.Add(b);
        // C: 마차 없음 → InvalidComposition 차단
        caravans.Add(Build("car_C", "town_A", withWagon: false));
        // D: 경로 없는 외딴 도시 → NoRouteFromCurrentTown 차단
        caravans.Add(Build("car_D", "town_ISLAND", withWagon: true));

        Refresh();
    }

    /// <summary>테스트 상단 조립. withWagon=false면 구성오류 상태가 된다.</summary>
    private CaravanData Build(string id, string townId, bool withWagon)
    {
        CaravanData c = new CaravanData();
        c.caravanId = id;
        c.currentTownId = townId;
        c.currentDurability = 100;

        if (withWagon)
        {
            imsiWagonData w = new imsiWagonData();
            w.wagonName = id + "호 마차"; w.instanceId = id + "_wagon";
            w.minAnimals = 1; w.maxAnimals = 4;
            w.overLoad = 500f; w.maxLoad = 700f;
            w.maxDurability = 100; w.inventorySlotCount = 10;
            c.wagon = w;

            imsiAnimalData a = new imsiAnimalData();
            a.animalName = "말"; a.instanceId = id + "_animal"; a.speed = 1f;
            c.animals.Add(a);

            imsiTradeItemData it = new imsiTradeItemData();
            it.id = "apple"; it.itemName = "사과"; it.weight = 1f; it.maxCount = 99;
            CargoEntry ce = new CargoEntry(); ce.item = it; ce.quantity = 5;
            c.cargo.Add(ce);
        }
        return c;
    }

    /// <summary>조립기·판정기 다시 실행 — 실제 UI의 "Provider 재조회"에 해당.</summary>
    private void Refresh()
    {
        // 슬롯 배정 주입: A→0, B→2, 나머지는 순서 방어 배치에 맡김
        Func<string, int> slotOf = (id) => id == "car_A" ? 0 : id == "car_B" ? 2 : -1;
        Func<int, bool> unlocked = (i) => i != 3;                     // 슬롯 3만 잠금 [임시 규칙]
        Func<int, string> hint = (i) => "해금 조건 미정 — 팀 확인 대기";
        overview = CaravanOverviewBuilder.Build(caravans, slotOf, unlocked, hint);

        Func<string, bool> hasRoute = (townId) => townId == "town_A"; // town_A만 경로 있음
        options = CaravanDepartureOptions.Build(caravans, hasRoute);
    }

    // =========================================================================
    // HUD
    // =========================================================================
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 900, Screen.height - 20));
        GUILayout.Label("<b>[Overview·출발판정 테스트]</b> — 오늘(7/23) 작업 검증용 (임시)", Rich(16));
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();

        // ── 왼쪽: Overview 슬롯 4개 (조립기 결과) ──
        GUILayout.BeginVertical(GUILayout.Width(430));
        GUILayout.Label("<b>◼ Overview 슬롯 (조립기)</b>", Rich(14));
        foreach (CaravanBlockViewData block in overview.caravans)
        {
            string line;
            if (block.slotState == CaravanSlotState.Occupied)
                line = string.Format("[{0}] ● {1} — {2} ({3}) 동물{4} 화물{5}",
                    block.slotIndex, block.displayName, block.caravanId, block.state,
                    block.animalIcons.Length, block.cargoIcons.Length);
            else if (block.slotState == CaravanSlotState.Locked)
                line = string.Format("[{0}] 🔒 잠김 — \"{1}\"", block.slotIndex, block.unlockHintText);
            else
                line = string.Format("[{0}] ▢ 빈 슬롯 (생성 버튼 자리)", block.slotIndex);
            GUILayout.Label(line, Rich(13));
        }
        GUILayout.EndVertical();

        // ── 오른쪽: 출발 선택 판정 (판정기 결과) ──
        GUILayout.BeginVertical();
        GUILayout.Label("<b>◼ 출발 선택 판정 (판정기)</b>", Rich(14));
        foreach (CaravanDepartureOption o in options)
        {
            string mark = o.canSelect ? "<color=#7CFC00>선택 가능</color>" : "<color=#FF6B6B>차단</color>";
            string reason = o.canSelect ? "" : " — " + CaravanDepartureOptions.GetDefaultReasonText(o.blockReason);
            GUILayout.Label(string.Format("{0} [{1}] {2}{3}", o.caravanId, o.state, mark, reason), Rich(13));
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        // ── 상태 조작 버튼: 누르면 상황이 바뀌고, 새로고침하면 두 패널이 따라 바뀐다 ──
        GUILayout.Label("<b>◼ 상황 바꾸기</b> (누른 뒤 아래 [새로고침]으로 재조회)", Rich(14));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("B 도착시키기 (이동중→준비)", GUILayout.Width(210)))
            caravans[1].state = JourneyState.Prepare;
        if (GUILayout.Button("C에 마차 달아주기", GUILayout.Width(150)))
        {
            if (caravans[2].wagon == null)
            {
                CaravanData fixedC = Build("car_C", "town_A", withWagon: true);
                caravans[2].wagon = fixedC.wagon;
                caravans[2].animals.AddRange(fixedC.animals);
                caravans[2].cargo.AddRange(fixedC.cargo);
            }
        }
        if (GUILayout.Button("D를 town_A로 이사", GUILayout.Width(150)))
            caravans[3].currentTownId = "town_A";
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        if (GUILayout.Button("↻ 새로고침 (Provider 재조회)", GUILayout.Width(250), GUILayout.Height(30)))
            Refresh();

        GUILayout.EndArea();
    }

    private static GUIStyle Rich(int size)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.richText = true;
        s.fontSize = size;
        return s;
    }
}
