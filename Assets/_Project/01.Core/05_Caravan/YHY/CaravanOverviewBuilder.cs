// =============================================================================
// CaravanOverviewBuilder — 상단 목록 화면 전체 스냅샷 조립기 (Core 소유)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 상단 목록 + 슬롯 배정 + 해금 정보를 받아, 이종현님 Overview UI가 쓰는
//        CaravanOverviewViewData(고정 슬롯 순서의 완성 스냅샷)를 조립한다.
//        블록 하나하나의 변환은 CaravanBlockViewMapper가 하고, 여기선 "배치"만 한다.
//
// [계약 준수] 0721_Caravan_Overview_UI_Contract 3.1절:
//   - 반환 ViewData와 모든 배열은 null 아님
//   - 호출마다 새 스냅샷 (UI가 변경해도 다음 조회가 오염되지 않음)
//   - 같은 caravanId를 두 번 반환하지 않음 (중복이면 뒤의 것을 버림)
//   - 슬롯 상태·해금 안내는 권위 있는 판정을 "주입받아" 변환만 한다 (여기서 재계산 안 함)
//
// [주입 패턴 — 왜 Func로 받나]
//   슬롯 배정(slotIndex)·해금 규칙은 Framework 영속화/기획 확정 대기 중이라
//   Core가 소유하지 않는다. 확정되면 어댑터가 진짜 판정을 꽂는다.
//   - getSlotIndex가 null이면: 목록 순서대로 앞에서부터 배치 [임시 — slotIndex 영속화 전까지만]
//   - isSlotUnlocked가 null이면: 모든 슬롯 해금으로 간주 [임시 — 해금 규칙 확정 전까지만]
// =============================================================================

using System;
using System.Collections.Generic;

/// <summary>상단 목록 화면(Overview) 전체 스냅샷을 조립한다.</summary>
public static class CaravanOverviewBuilder
{
    /// <summary>고정 슬롯 수 — 계약 문서 "최대 4개의 고정 슬롯" 기준. 기획 변경 시 여기만 수정.</summary>
    public const int MaxSlots = 4;

    /// <summary>
    /// 전체 슬롯 스냅샷 조립. 항상 MaxSlots 길이의 배열을 slotIndex 순서로 반환한다.
    /// </summary>
    /// <param name="caravans">런타임 상단 목록 (CaravanRuntimeList.Build 결과 등)</param>
    /// <param name="getSlotIndex">caravanId → 슬롯 번호(0~3). 권위 있는 배정을 주입. null이면 목록 순서 배치[임시]</param>
    /// <param name="isSlotUnlocked">슬롯 번호 → 해금 여부. null이면 전부 해금[임시]</param>
    /// <param name="getUnlockHint">잠긴 슬롯의 해금 안내문. null이면 기본 문구</param>
    public static CaravanOverviewViewData Build(
        IReadOnlyList<CaravanData> caravans,
        Func<string, int> getSlotIndex,
        Func<int, bool> isSlotUnlocked,
        Func<int, string> getUnlockHint)
    {
        // 슬롯 자리표 — 채워질 때까지 null, 마지막에 Empty/Locked로 메꾼다.
        CaravanBlockViewData[] slots = new CaravanBlockViewData[MaxSlots];

        // 같은 caravanId 중복 방지 — 먼저 온 것만 인정 (계약: 같은 ID 두 번 반환 금지)
        HashSet<string> placedIds = new HashSet<string>();

        if (caravans != null)
        {
            int fallbackCursor = 0;   // getSlotIndex 없을 때 앞에서부터 채우는 커서 [임시]

            foreach (CaravanData c in caravans)
            {
                if (c == null || string.IsNullOrEmpty(c.caravanId)) continue;   // ID 없는 상단은 배치 불가
                if (!placedIds.Add(c.caravanId)) continue;                       // 중복 ID → 뒤의 것 버림

                // 슬롯 결정: 주입된 권위 판정 우선, 없으면 순서대로 [임시]
                int slot = (getSlotIndex != null) ? getSlotIndex(c.caravanId) : fallbackCursor;

                // 범위 밖·이미 찬 슬롯·잠긴 슬롯이면 → 해금된 빈 자리로 방어 배치 (데이터 꼬임 방어)
                // ※ 잠긴 슬롯엔 상단을 앉히지 않는다 — 해금 규칙이 무의미해지므로.
                if (slot < 0 || slot >= MaxSlots || slots[slot] != null || !IsUnlocked(isSlotUnlocked, slot))
                    slot = FindFirstFreeUnlockedSlot(slots, isSlotUnlocked);
                if (slot < 0) continue;   // 해금된 빈 자리가 없으면 이 상단은 표시하지 않음 (초과·꼬임 방어)

                slots[slot] = CaravanBlockViewMapper.BuildOccupied(c, slot);
                if (getSlotIndex == null) fallbackCursor = slot + 1;
            }
        }

        // 남은 자리 메꾸기: 해금됐으면 Empty(생성 버튼), 안 됐으면 Locked(+안내문)
        for (int i = 0; i < MaxSlots; i++)
        {
            if (slots[i] != null) continue;

            bool unlocked = (isSlotUnlocked == null) || isSlotUnlocked(i);
            if (unlocked)
            {
                slots[i] = CaravanBlockViewMapper.BuildEmpty(i);
            }
            else
            {
                string hint = (getUnlockHint != null) ? getUnlockHint(i) : "아직 열리지 않은 슬롯이에요";
                slots[i] = CaravanBlockViewMapper.BuildLocked(i, hint ?? string.Empty);
            }
        }

        // 매 호출 새 객체 조립이므로 스냅샷 오염 없음 (매퍼도 새 객체를 만든다)
        CaravanOverviewViewData overview = new CaravanOverviewViewData();
        overview.caravans = slots;
        return overview;
    }

    /// <summary>슬롯 해금 여부 — 판정 함수가 없으면(연결 전) 전부 해금으로 간주 [임시].</summary>
    private static bool IsUnlocked(Func<int, bool> isSlotUnlocked, int slot)
    {
        if (slot < 0 || slot >= MaxSlots) return false;   // 범위 밖은 항상 잠금 취급
        return (isSlotUnlocked == null) || isSlotUnlocked(slot);
    }

    /// <summary>앞에서부터 첫 "해금된" 빈 슬롯 번호. 없으면 -1.</summary>
    private static int FindFirstFreeUnlockedSlot(CaravanBlockViewData[] slots, Func<int, bool> isSlotUnlocked)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null && IsUnlocked(isSlotUnlocked, i)) return i;
        return -1;
    }
}
