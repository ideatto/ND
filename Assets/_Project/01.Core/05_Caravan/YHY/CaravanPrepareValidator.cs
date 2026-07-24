// =============================================================================
// CaravanPrepareValidator — 복수 상단 준비/출발 검증 통합 진입점
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 상단 하나가 "지금 출발/준비 가능한가"를 검증하는 여러 규칙을 하나로 묶는다.
//        호출하는 쪽(Framework·UI)은 이 함수 하나만 부르면 된다 — 개별 검증기를
//        각각 부르고 결과를 합칠 필요가 없다.
//
//        묶는 검증 3가지:
//          ① 구성 검증   : 마차·견인수·과적·슬롯·무역품 등        (CaravanValidator)
//          ② 경로 검증   : 지금 있는 도시에서 출발하는 경로인가     (CaravanValidator 3인자)
//          ③ 자산 잠금   : 같은 마차·동물·용병을 다른 상단이 쓰나  (CaravanAssetLock)
//
// [반환] 통합 결과 하나(CaravanPrepareResult)로 "가능 여부 + 막힌 사유 전부"를 돌려준다.
//        구성·경로 사유(DepartureBlockReason)와 자산 사유(AssetLockConflict)를 둘 다 담는다.
//
// [경계] 상태 변경·저장은 하지 않는다(순수 검증). caravanId로 대상을 고르거나 출발을
//        실제로 커밋하는 것은 호출자(Framework) 몫이다.
//
// [순수 로직] static · UnityEngine 의존 없음 → 테스트 가능.
// =============================================================================

using System.Collections.Generic;

/// <summary>준비/출발 검증 입력. 필요한 것만 채워서 넘긴다(없으면 해당 검증을 건너뜀).</summary>
public class CaravanPrepareInput
{
    /// <summary>검증할 대상 상단(필수).</summary>
    public CaravanData caravan;

    /// <summary>플레이어가 지금 있는 도시. routeFromTownId와 함께 있어야 경로 검증을 한다.</summary>
    public string currentTownId;

    /// <summary>고른 경로의 출발 도시. currentTownId와 함께 있어야 경로 검증을 한다.</summary>
    public string routeFromTownId;

    /// <summary>플레이어의 전체 상단 목록(자산 잠금용). null이면 자산 검증을 건너뜀.</summary>
    public IEnumerable<CaravanData> allCaravans;
}

/// <summary>통합 검증 결과 = 가능 여부 + 막힌 사유 전부(구성·경로 + 자산).</summary>
public class CaravanPrepareResult
{
    /// <summary>모든 검증을 통과했는가(출발/준비 가능).</summary>
    public bool canProceed;

    /// <summary>구성·경로에서 막힌 사유들(CaravanValidator).</summary>
    public List<DepartureBlockReason> departureReasons = new List<DepartureBlockReason>();

    /// <summary>자산 잠금 충돌들(CaravanAssetLock). 어떤 자산이 어느 상단에 묶였는지 포함.</summary>
    public List<AssetLockConflict> assetConflicts = new List<AssetLockConflict>();
}

/// <summary>복수 상단 준비/출발 검증을 하나로 묶는 진입점. 자세한 설명은 파일 상단 주석 참고.</summary>
public static class CaravanPrepareValidator
{
    /// <summary>
    /// 상단 하나가 지금 출발/준비 가능한지 통합 검증한다.
    /// </summary>
    public static CaravanPrepareResult Validate(CaravanPrepareInput input)
    {
        CaravanPrepareResult result = new CaravanPrepareResult();

        // 입력 자체가 없으면 불가(사유 없이). 프로그래밍 오류 상황.
        if (input == null || input.caravan == null)
        {
            result.canProceed = false;
            return result;
        }

        // ① + ② 구성·경로 검증
        //   경로 정보(현재 도시 + 경로 출발지)가 둘 다 있으면 3인자(경로 검증 포함)로,
        //   아니면 기본 구성 검증만 한다.
        DepartureValidationResult depart;
        if (!string.IsNullOrEmpty(input.currentTownId) || !string.IsNullOrEmpty(input.routeFromTownId))
            depart = CaravanValidator.Validate(input.caravan, input.currentTownId, input.routeFromTownId);
        else
            depart = CaravanValidator.Validate(input.caravan);

        if (depart != null && depart.reasons != null)
            result.departureReasons.AddRange(depart.reasons);

        // ③ 자산 잠금 (전체 상단 목록이 있을 때만)
        if (input.allCaravans != null)
        {
            AssetLockResult assets = CaravanAssetLock.Validate(input.caravan, input.allCaravans);
            if (assets != null && assets.conflicts != null)
                result.assetConflicts.AddRange(assets.conflicts);
        }

        // 모든 검증에서 막힌 사유가 하나도 없어야 통과.
        result.canProceed = (result.departureReasons.Count == 0) && (result.assetConflicts.Count == 0);
        return result;
    }
}
