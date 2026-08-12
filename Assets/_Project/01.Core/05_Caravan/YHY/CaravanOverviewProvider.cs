// =============================================================================
// CaravanOverviewProvider — 상단 목록 화면(Overview) 실데이터 공급자 (Core 소유)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 저장된 실제 캐러밴을 읽어, 이종현님 Overview UI가 쓰는
//        CaravanOverviewViewData(고정 4슬롯 스냅샷)를 만들어 돌려준다.
//        "조립"은 CaravanOverviewBuilder가 하고, 여기선 그 조립기에
//        "진짜 데이터·진짜 판정"을 꽂아주는 역할만 한다.
//
// [왜 필요한가] CaravanOverviewBuilder는 슬롯 배정(getSlotIndex)·해금 규칙을
//        "주입받아" 쓰도록 설계돼 있다. 임시 테스트(CaravanOverviewOptionsTest)는
//        이걸 하드코딩으로 꽂아 확인만 했고, 실제 게임에선 이 Provider가
//        저장 데이터(SaveData)에서 진짜 값을 꽂아준다.
//
// [경계] 저장 DTO·조회 API(SaveDataLookup)는 Framework(천성욱님) 소유.
//        여기선 "읽어서 조합"만 하고, 상태 변경·저장은 절대 하지 않는다.
//
// [주의] 전역 네임스페이스에도 SaveData(이종현님)가 있어 이름이 겹친다.
//        저장용은 반드시 풀네임 ND.Framework.SaveData 로 명시한다.
// =============================================================================

using System;
using System.Collections.Generic;

/// <summary>저장된 실제 캐러밴 → Overview 화면 스냅샷을 만들어 공급한다.</summary>
public static class CaravanOverviewProvider
{
    /// <summary>
    /// 현재 저장 상태로 Overview 4슬롯 스냅샷을 조립해 돌려준다.
    /// saveData가 null이거나 캐러밴이 없어도 항상 유효한 스냅샷을 반환한다(빈 슬롯으로 채워짐).
    /// </summary>
    /// <param name="saveData">현재 게임 저장 데이터(Framework 소유). 여기서 읽기만 한다.</param>
    public static CaravanOverviewViewData BuildOverview(ND.Framework.SaveData saveData)
    {
        // 1) 저장된 캐러밴 리스트 → Core 런타임 리스트로 변환 (절대 null 아님)
        List<CaravanData> caravans = CaravanRuntimeList.Build(saveData);

        // 2) 진짜 슬롯 주입: caravanId → 저장된 slotIndex
        //    (테스트가 하던 'id=="car_A"?0' 하드코딩을, 실제 저장된 슬롯 번호로 대체)
        //    저장에서 못 찾으면 -1 → 조립기가 알아서 빈 슬롯으로 방어 배치한다.
        Func<string, int> slotOf = (caravanId) =>
        {
            if (saveData != null
                && ND.Framework.SaveDataLookup.TryGetCaravan(saveData, caravanId, out ND.Framework.CaravanSaveData saved)
                && saved != null)
            {
                return saved.slotIndex;
            }
            return -1;
        };

        // 3) BaseCamp SaveData level is the single slot-unlock authority.
        //    The Builder receives the rule instead of reading presentation objects or legacy flags.
        Func<int, bool> isSlotUnlocked = (slotIndex) =>
            ND.Framework.BaseCampProgressionPolicy.IsCaravanSlotUnlocked(
                saveData?.player?.villageBuildings,
                slotIndex);
        Func<int, string> getUnlockHint = (slotIndex) =>
            $"베이스 캠프 레벨이 부족하여 캐러밴 슬롯을 해금할 수 없습니다. 필요 레벨: Lv.{slotIndex + 1}";

        // 4) 조립기에 진짜 데이터·판정을 넣어 최종 스냅샷을 만든다.
        return CaravanOverviewBuilder.Build(caravans, slotOf, isSlotUnlocked, getUnlockHint);
    }

    /// <summary>
    /// 저장된 실제 캐러밴들의 "출발 선택 가능 여부(canSelect)"를 판정해 돌려준다.
    /// 각 항목에 canSelect·차단 사유(blockReason, 안정 코드)가 담긴다.
    /// </summary>
    /// <param name="saveData">현재 게임 저장 데이터(Framework 소유). 읽기만 한다.</param>
    /// <param name="hasRouteFromTown">
    /// "이 도시에서 출발할 경로가 있나?" 판정 함수. (townId → true/false)
    /// [경계] 경로 데이터(SharedGameDataView.routes)는 Core 소유가 아니라 Framework/Content 공유 데이터다.
    ///        그래서 Core가 소유하지 않고, 권위 있는 판정을 "주입받아" 쓴다.
    ///        (slotIndex를 주입했던 것과 같은 패턴)
    ///        null이면 경로 판정을 생략한다(경로 이외의 사유만 검사).
    /// TODO: 런타임 연결 단계에서 SharedGameDataView(도시별 출발 경로 유무)로 이 함수를 채운다.
    /// </param>
    public static List<CaravanDepartureOption> BuildDepartureOptions(
        ND.Framework.SaveData saveData,
        Func<string, bool> hasRouteFromTown)
    {
        // 1) 저장된 캐러밴 리스트 → 런타임 리스트 (절대 null 아님)
        List<CaravanData> caravans = CaravanRuntimeList.Build(saveData);

        // 2) 판정기에 진짜 캐러밴 + 주입된 경로 판정을 넘겨 출발 가능 여부를 계산한다.
        //    (이동중/구성오류/ID문제 등 나머지 판정은 판정기가 캐러밴 데이터로 알아서 한다)
        return CaravanDepartureOptions.Build(caravans, hasRouteFromTown);
    }
}
