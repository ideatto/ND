// =============================================================================
// CaravanDepartureOptions — 출발 상단 선택 옵션 판정 (Core 소유)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 무역 준비 UI의 "출발할 상단 고르기" 단계에서, 상단마다
//        "선택 가능한가 + 안 되면 왜"를 판정해 돌려준다.
//        이종현님 요청 문서(0721_TradePrepare_Multi_Caravan_Provider_Integration_Request)의
//        "출발 가능 판정과 차단 사유의 원본은 Framework/Caravan 기능이 제공" 부분의 Core 몫.
//
// [판정 규칙] (문서 3절 최소 차단 대상 기준)
//   - caravanId 없음/중복       → 차단 (Gateway 호출 전 차단 요구사항)
//   - 준비(Prepare) 상태가 아님  → 차단 (Traveling/Settling/Completed)
//   - 구성 오류(마차 없음 등)    → 차단 (CaravanValidator 재사용)
//   - 현재 도시에서 나가는 경로 없음 → 차단 (경로 유무 판정은 바깥이 함수로 주입)
//
// [경계 — 여기서 판정하지 않는 것]
//   - "저장 처리 중" / "정산 claim 대기" 같은 Framework 상태 → Framework/어댑터가 추가로 거른다.
//   - 표시 문구 최종본은 UI 몫. 여기선 안정적인 enum 코드 + 기본 한국어 문구만 제공.
//
// [의존] UnityEngine 무의존(순수 로직). 경로 데이터도 직접 안 읽고 Func로 주입받는다.
// =============================================================================

using System;
using System.Collections.Generic;

/// <summary>출발 상단 선택 차단 사유 (안정 코드 — UI가 문구로 변환).</summary>
public enum DepartureOptionBlockReason
{
    None,                   // 차단 없음 (선택 가능)
    MissingCaravanId,       // caravanId가 비어 있음 → 저장 오류 가능성
    DuplicateCaravanId,     // 같은 ID가 두 번 나옴 → 저장 오류 가능성
    NotInPrepare,           // 준비 단계가 아님 (이동 중/정산 중/완료)
    InvalidComposition,     // 구성 오류 (마차 없음·동물 부족 등 — CaravanValidator 사유)
    NoRouteFromCurrentTown, // 현재 도시에서 출발하는 경로가 없음
}

/// <summary>출발 상단 선택 옵션 한 줄 — UI가 TradePrepareCaravanOptionViewData로 변환해 쓴다.</summary>
public class CaravanDepartureOption
{
    public string caravanId;                     // 안정적인 상단 ID (선택 결과로 이 값을 전달)
    public string displayName;                   // 표시용 이름 (마차 이름 기반, 없으면 ID)
    public JourneyState state;                   // 현재 여정 상태 (배지 표시용)
    public bool canSelect;                       // 선택 가능 여부
    public DepartureOptionBlockReason blockReason = DepartureOptionBlockReason.None; // 안 되는 이유 (안정 코드)

    /// <summary>구성 오류일 때 세부 사유 (CaravanValidator 결과 그대로 — 툴팁 등에 사용).</summary>
    public List<DepartureBlockReason> compositionReasons = new List<DepartureBlockReason>();
}

/// <summary>상단 목록 → 출발 선택 옵션 목록 판정.</summary>
public static class CaravanDepartureOptions
{
    /// <summary>
    /// 상단 전체를 훑어 선택 옵션 목록을 만든다. 순서는 입력 순서 유지, null 항목은 건너뜀.
    /// </summary>
    /// <param name="caravans">런타임 상단 목록 (CaravanRuntimeList.Build 결과 등)</param>
    /// <param name="hasRouteFromTown">
    /// "이 도시에서 나가는 경로가 있나?" 판정 함수. 경로 데이터는 Framework/SO 소유라 주입받는다.
    /// null이면 경로 검사는 생략한다(경로 데이터가 아직 연결 전인 호출자 배려).
    /// </param>
    public static List<CaravanDepartureOption> Build(
        IReadOnlyList<CaravanData> caravans,
        Func<string, bool> hasRouteFromTown)
    {
        List<CaravanDepartureOption> result = new List<CaravanDepartureOption>();
        if (caravans == null) return result;

        // 중복 ID 검출용 — 같은 ID가 두 번 나오면 둘 다 차단한다(어느 쪽이 진짜인지 모르므로).
        HashSet<string> seenIds = new HashSet<string>();
        HashSet<string> duplicatedIds = new HashSet<string>();
        foreach (CaravanData c in caravans)
        {
            if (c == null || string.IsNullOrEmpty(c.caravanId)) continue;
            if (!seenIds.Add(c.caravanId)) duplicatedIds.Add(c.caravanId);
        }

        foreach (CaravanData c in caravans)
        {
            if (c == null) continue;

            CaravanDepartureOption option = new CaravanDepartureOption();
            option.caravanId = c.caravanId ?? string.Empty;
            option.state = c.state;
            option.displayName = (c.wagon != null && !string.IsNullOrEmpty(c.wagon.wagonName))
                ? c.wagon.wagonName
                : option.caravanId;

            option.blockReason = Judge(c, duplicatedIds, hasRouteFromTown, option.compositionReasons);
            option.canSelect = (option.blockReason == DepartureOptionBlockReason.None);
            result.Add(option);
        }
        return result;
    }

    /// <summary>상단 하나 판정 — 위에서부터 순서대로 걸리는 첫 사유를 반환.</summary>
    private static DepartureOptionBlockReason Judge(
        CaravanData c,
        HashSet<string> duplicatedIds,
        Func<string, bool> hasRouteFromTown,
        List<DepartureBlockReason> compositionReasonsOut)
    {
        // 1. ID 문제 — 잘못된 ID는 Gateway 호출 전에 차단해야 한다(문서 완료 조건).
        if (string.IsNullOrEmpty(c.caravanId)) return DepartureOptionBlockReason.MissingCaravanId;
        if (duplicatedIds.Contains(c.caravanId)) return DepartureOptionBlockReason.DuplicateCaravanId;

        // 2. 상태 — 준비 단계만 출발 후보. (이동/정산/완료 중복 출발 차단)
        if (c.state != JourneyState.Prepare) return DepartureOptionBlockReason.NotInPrepare;

        // 3. 구성 — 기존 검증기 재사용. 세부 사유는 밖에 넘겨 툴팁 등에 쓰게 한다.
        DepartureValidationResult v = CaravanValidator.Validate(c);
        if (!v.canDepart)
        {
            compositionReasonsOut.AddRange(v.reasons);
            return DepartureOptionBlockReason.InvalidComposition;
        }

        // 4. 위치 — 현재 도시에서 나가는 경로가 없으면 출발 불가.
        //    경로 판정 함수가 주입 안 됐으면(연결 전) 이 검사는 건너뛴다.
        if (hasRouteFromTown != null && !hasRouteFromTown(c.currentTownId))
            return DepartureOptionBlockReason.NoRouteFromCurrentTown;

        return DepartureOptionBlockReason.None;
    }

    /// <summary>차단 사유 기본 문구 (한국어). 최종 문구·현지화는 UI 몫 — 없을 때의 기본값용.</summary>
    public static string GetDefaultReasonText(DepartureOptionBlockReason reason)
    {
        switch (reason)
        {
            case DepartureOptionBlockReason.None: return string.Empty;   // 선택 가능이면 빈 문자열 (문서 규칙 8)
            case DepartureOptionBlockReason.MissingCaravanId: return "상단 정보에 문제가 있어요 (ID 없음)";
            case DepartureOptionBlockReason.DuplicateCaravanId: return "상단 정보에 문제가 있어요 (ID 중복)";
            case DepartureOptionBlockReason.NotInPrepare: return "이동 또는 정산 중인 상단이에요";
            case DepartureOptionBlockReason.InvalidComposition: return "상단 구성이 완성되지 않았어요";
            case DepartureOptionBlockReason.NoRouteFromCurrentTown: return "지금 있는 도시에서 출발할 수 있는 경로가 없어요";
            default: return "출발할 수 없는 상단이에요";
        }
    }
}
