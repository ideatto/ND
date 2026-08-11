// =============================================================================
// CaravanOverviewProviderTests — CaravanOverviewProvider 자동 검증 (에디터 전용)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [용도] CaravanOverviewProvider의 두 메서드를 가짜 저장데이터로 돌려
//        결과가 계약대로 나오는지 자동 검증한다. (수동 HUD 확인을 자동화)
//
// [실행법] Unity 상단 메뉴: Tools → Tests → Caravan Overview Provider
//          → Console에 각 검사 결과 + 최종 요약(PASS/FAIL)이 찍힌다.
//
// [경계] 저장 DTO는 Framework 소유. 여기선 읽기용 가짜 데이터만 조립해 검증한다.
//        전역 SaveData와 겹치므로 ND.Framework.SaveData는 별칭으로 명시한다.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;

/// <summary>CaravanOverviewProvider를 메뉴에서 자동 검증하는 에디터 테스트.</summary>
public static class CaravanOverviewProviderTests
{
    // 검사 카운터 (RunAll 한 번의 통과/실패 집계)
    private static int passed;
    private static int failed;

    [MenuItem("Tools/Tests/Caravan Overview Provider")]
    public static void RunAll()
    {
        passed = 0;
        failed = 0;

        Overview_PlacesCaravansAtSavedSlotIndex();
        Overview_NullSaveData_ReturnsFourEmptySlots();
        DepartureOptions_ReturnsOnePerCaravan_EmptyIsNotSelectable();

        // 최종 요약
        if (failed == 0)
            Debug.Log($"<color=#7CFC00>[CaravanOverviewProviderTests] 전부 통과 ✅ ({passed}건)</color>");
        else
            Debug.LogError($"[CaravanOverviewProviderTests] 실패 {failed}건 / 통과 {passed}건");
    }

    // -------------------------------------------------------------------------
    // 검사 1) 저장된 slotIndex대로 슬롯에 배치되는가
    // -------------------------------------------------------------------------
    private static void Overview_PlacesCaravansAtSavedSlotIndex()
    {
        // car_X는 슬롯 2번, car_Y는 슬롯 0번에 저장돼 있다고 가정
        FrameworkSaveData sd = MakeSaveData(
            (id: "car_X", slot: 2),
            (id: "car_Y", slot: 0));

        CaravanOverviewViewData overview = CaravanOverviewProvider.BuildOverview(sd);

        Check(overview != null && overview.caravans != null, "overview·slots는 null이 아니어야 한다");
        if (overview == null || overview.caravans == null) return;

        CheckEqual(4, overview.caravans.Length, "슬롯은 항상 4개");
        CheckEqual("car_Y", SlotCaravanId(overview, 0), "슬롯0 = car_Y (저장 slotIndex 0)");
        CheckEqual("car_X", SlotCaravanId(overview, 2), "슬롯2 = car_X (저장 slotIndex 2)");
        CheckEqual(CaravanSlotState.Locked, overview.caravans[1].slotState, "BaseCamp Lv.0 슬롯1은 잠김");
        CheckEqual(CaravanSlotState.Locked, overview.caravans[3].slotState, "BaseCamp Lv.0 슬롯3은 잠김");
    }

    // -------------------------------------------------------------------------
    // 검사 2) saveData가 null이어도 안전하게 4빈칸을 돌려주는가
    // -------------------------------------------------------------------------
    private static void Overview_NullSaveData_ReturnsFourEmptySlots()
    {
        CaravanOverviewViewData overview = CaravanOverviewProvider.BuildOverview(null);

        Check(overview != null && overview.caravans != null, "null 입력에도 null을 돌려주지 않아야 한다");
        if (overview == null || overview.caravans == null) return;

        CheckEqual(4, overview.caravans.Length, "null이어도 슬롯 4개");
        bool anyOccupied = false;
        foreach (CaravanBlockViewData b in overview.caravans)
            if (b != null && b.slotState == CaravanSlotState.Occupied) anyOccupied = true;
        Check(!anyOccupied, "null 입력이면 점유된 슬롯이 없어야 한다");
    }

    // -------------------------------------------------------------------------
    // 검사 3) 출발 판정: 캐러밴마다 항목이 나오고, 구성 빈 캐러밴은 선택 불가인가
    // -------------------------------------------------------------------------
    private static void DepartureOptions_ReturnsOnePerCaravan_EmptyIsNotSelectable()
    {
        FrameworkSaveData sd = MakeSaveData(
            (id: "car_X", slot: 2),
            (id: "car_Y", slot: 0));

        // 경로는 전부 있다고 가정 (구성이 비어서 경로 검사 전에 막히는 게 정상)
        Func<string, bool> hasRoute = (town) => true;

        List<CaravanDepartureOption> options = CaravanOverviewProvider.BuildDepartureOptions(sd, hasRoute);

        Check(options != null, "options는 null이 아니어야 한다");
        if (options == null) return;

        CheckEqual(2, options.Count, "캐러밴 수만큼(2개) 판정이 나와야 한다");

        // 구성이 빈 최소 캐러밴 → 선택 불가(구성 미완성)
        foreach (CaravanDepartureOption o in options)
        {
            Check(!o.canSelect, $"{o.caravanId}: 구성 빈 캐러밴은 선택 불가여야 한다");
            CheckEqual(DepartureOptionBlockReason.InvalidComposition, o.blockReason,
                $"{o.caravanId}: 차단 사유는 구성 미완성이어야 한다");
        }
    }

    // =========================================================================
    // 도우미
    // =========================================================================

    /// <summary>가짜 저장데이터 조립 — (caravanId, slotIndex) 목록으로 캐러밴을 넣는다.</summary>
    private static FrameworkSaveData MakeSaveData(params (string id, int slot)[] entries)
    {
        FrameworkSaveData sd = new FrameworkSaveData();
        sd.caravans = new List<FrameworkCaravanSaveData>();
        foreach ((string id, int slot) in entries)
        {
            FrameworkCaravanSaveData c = new FrameworkCaravanSaveData();
            c.caravanId = id;
            c.slotIndex = slot;
            sd.caravans.Add(c);
        }
        return sd;
    }

    /// <summary>해당 슬롯의 caravanId(없으면 빈 문자열).</summary>
    private static string SlotCaravanId(CaravanOverviewViewData overview, int slot)
    {
        if (overview == null || overview.caravans == null) return string.Empty;
        if (slot < 0 || slot >= overview.caravans.Length) return string.Empty;
        CaravanBlockViewData b = overview.caravans[slot];
        return b == null ? string.Empty : (b.caravanId ?? string.Empty);
    }

    /// <summary>조건이 참이면 통과, 아니면 실패 로그.</summary>
    private static void Check(bool condition, string label)
    {
        if (condition) { passed++; }
        else { failed++; Debug.LogError($"[FAIL] {label}"); }
    }

    /// <summary>기대값 == 실제값이면 통과, 아니면 실패 로그(값 표시).</summary>
    private static void CheckEqual(object expected, object actual, string label)
    {
        bool ok = Equals(expected, actual);
        if (ok) { passed++; }
        else { failed++; Debug.LogError($"[FAIL] {label} — 기대:{expected} 실제:{actual}"); }
    }
}
