// =============================================================================
// JourneyRunner — 무역 진행 단계 전환 (Core 소유)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [이 파일이 하는 일 — 딱 하나]
//   무역의 "단계"를 바꾸는 통로. (준비 → 이동중 → 정산대기 → 완료)
//   상태를 여기저기서 직접 바꾸면 추적이 안 되니, 바꾸는 곳을 한 군데로 모은 것.
//   계산(속도·수익)은 안 한다. 그건 TravelCalculator 등 딴 곳 몫.
//
// [시간을 모른다 — 중요]
//   이 파일은 시간을 델타타임/타임스탬프/게임시간 중 뭘로 재는지 모른다.
//   바깥이 "진행도(0~1)"를 만들어 SetProgress로 넘겨주면, 그걸로 도착만 판정한다.
//   → 시간 출처(테스트든 Framework든), 진행 곡선, 배속 전부 바깥에서 결정 가능.
//
// [바깥이 부르는 것]
//   TryDepart      : 출발 검증 통과 시  준비 → 이동중
//   SetProgress    : 진행도(0~1) 갱신   (바깥이 매 순간 밀어넣음)
//   Settle         : 도착(또는 실패) 시  이동중 → 정산대기 + 결과 생성
//   ClaimSettlement: 정산 수령           정산대기 → 완료
//   ResetToPrepare : 다음 무역           완료 → 준비
// =============================================================================

/// <summary>무역 진행 단계 전환. 시간은 모르고 진행도만 받는다.</summary>
public static class JourneyRunner
{
    /// <summary>도착으로 판정하는 진행도 기준(상수로 열어둠).</summary>
    public const float ArrivalProgress = 1f;

    /// <summary>
    /// 출발 시도. 검증 통과 시 이동 시작(소요 시간 계산·저장, 진행도 0으로).
    /// 시간 값은 안 받는다. 소요 시간만 계산해 둔다 — 실제 흐름은 바깥이 진행도로 민다.
    /// </summary>
    public static DepartureValidationResult TryDepart(CaravanData caravan, float distanceKm)
    {
        DepartureValidationResult v = CaravanValidator.Validate(caravan);
        if (!v.canDepart) return v;

        caravan.currentDistanceKm = distanceKm;
        caravan.totalSeconds = CaravanCalculator.GetTravelSeconds(caravan, distanceKm);
        caravan.progress01 = 0f;
        caravan.settlementClaimed = false;
        caravan.runCargoLost = 0;
        caravan.runFoodLost = 0f;
        caravan.runDurabilityLost = 0;
        caravan.runBattlesFought = 0;
        caravan.runFoodDepleted = false;
        caravan.runFoodDepletedProgress = 0f;
        caravan.runFatalReason = JourneyFailureReason.None;
        caravan.state = JourneyState.Traveling;
        return v;
    }

    /// <summary>진행도(0~1) 갱신. 바깥이 계산한 값을 넣는다. 이동 중일 때만 반영.</summary>
    public static void SetProgress(CaravanData caravan, float progress01)
    {
        if (caravan == null || caravan.state != JourneyState.Traveling) return;

        if (progress01 < 0f) progress01 = 0f;
        if (progress01 > 1f) progress01 = 1f;
        caravan.progress01 = progress01;

        CheckFoodDepletion(caravan);   // 진행할수록 식량 소모 → 바닥나면 실패
    }

    /// <summary>도착했는가 (진행도가 기준 도달).</summary>
    public static bool IsArrived(CaravanData caravan)
    {
        return caravan != null
            && caravan.state == JourneyState.Traveling
            && caravan.progress01 >= ArrivalProgress;
    }

    /// <summary>이동 중 이벤트 — 무역품 손실(무역 계속). → 부분 성공 요인.
    /// 실제 cargo 수량을 줄여서 정산 판매량에도 반영한다(첫 상품부터 차감).</summary>
    public static void ApplyCargoLoss(CaravanData caravan, int amount)
    {
        if (caravan == null || caravan.state != JourneyState.Traveling) return;
        if (amount <= 0) return;

        // 실제 cargo에서 amount만큼 줄인다(첫 상품부터). 실제로 뺀 개수를 runCargoLost에 기록.
        int remaining = amount;
        foreach (CargoEntry entry in caravan.cargo)
        {
            if (remaining <= 0) break;
            if (entry == null) continue;

            int take = (entry.quantity < remaining) ? entry.quantity : remaining;  // 이 상품에서 뺄 수 있는 만큼
            entry.quantity -= take;      // 실제 수량 감소 → 판매·적재에 반영됨
            remaining -= take;
            caravan.runCargoLost += take;  // 실제로 잃은 개수만 기록(부분성공 판정·표시용)
        }
    }

    /// <summary>이동 중 이벤트 — 식량 차감(도난 등). 나중에 이벤트 시스템이 이 함수를 부른다.</summary>
    public static void ApplyFoodLoss(CaravanData caravan, float amount)
    {
        if (caravan == null || caravan.state != JourneyState.Traveling) return;
        if (amount > 0f) caravan.runFoodLost += amount;
        CheckFoodDepletion(caravan);   // 이 차감으로 바로 바닥날 수도 있음
    }

    /// <summary>이동 중 이벤트 — 마차 내구도 손실. 실제 내구도를 줄이고 이번 무역 손실에 기록. [M2]</summary>
    public static void ApplyDurabilityLoss(CaravanData caravan, int amount)
    {
        if (caravan == null || caravan.state != JourneyState.Traveling) return;
        if (amount <= 0) return;
        caravan.currentDurability -= amount;
        caravan.runDurabilityLost += amount;
    }

    /// <summary>약탈(전투) 판정 — [임시 규칙] 용병 1마리당 전투 1번 방어.
    /// 이번 무역 N번째 전투가 용병 수 이하면 방어 성공(손실 없음), 넘으면 약탈당함(내구도·무역품 손실).
    /// 반환: true=방어 성공 / false=약탈당함.
    /// [주의] 진짜 판정은 전투력 기반 = Progression 영역. 이건 임시 placeholder. [M2]</summary>
    public static bool ResolveRaid(CaravanData caravan, int durabilityDamage, int cargoDamage)
    {
        if (caravan == null || caravan.state != JourneyState.Traveling) return false;

        caravan.runBattlesFought++;
        int mercCount = (caravan.mercenaries != null) ? caravan.mercenaries.Count : 0;

        if (caravan.runBattlesFought <= mercCount) return true;   // 용병 수 이하 전투면 방어 성공

        // 방어 실패 → 약탈당함
        ApplyDurabilityLoss(caravan, durabilityDamage);
        ApplyCargoLoss(caravan, cargoDamage);
        return false;
    }

    /// <summary>식량이 바닥(0 이하)나면 실패 확정. 소모+이벤트 반영된 잔량으로 판정.</summary>
    private static void CheckFoodDepletion(CaravanData caravan)
    {
        if (caravan.runFatalReason != JourneyFailureReason.None) return;   // 이미 실패면 스킵
        if (CaravanCalculator.GetRemainingFood(caravan) > 0f) return;      // 아직 식량 있음

        // 식량 바닥 — 처음 바닥나면 시점 기록 (즉사 아님, 여기서 제한시간 시작)
        if (!caravan.runFoodDepleted)
        {
            caravan.runFoodDepleted = true;
            caravan.runFoodDepletedProgress = caravan.progress01;
        }

        // 바닥난 뒤 흐른 시간이 제한시간을 넘었는데 아직 도착 못했으면 → 무역 실패
        float secondsSinceDepleted = (caravan.progress01 - caravan.runFoodDepletedProgress) * caravan.totalSeconds;
        if (secondsSinceDepleted >= caravan.starveGraceSeconds && !IsArrived(caravan))
            MarkFatal(caravan, JourneyFailureReason.FoodDepleted);
    }

    /// <summary>이동 중 치명 상태 — 더 못 감(식량 고갈 등). → 실패 확정.</summary>
    public static void MarkFatal(CaravanData caravan, JourneyFailureReason reason)
    {
        if (caravan == null || caravan.state != JourneyState.Traveling) return;
        if (caravan.runFatalReason == JourneyFailureReason.None)
            caravan.runFatalReason = reason;
    }

    /// <summary>
    /// 정산: 도착했거나 치명 상태면 등급을 판정해 결과 생성 + 정산대기로.
    /// 아직 도착 전이고 치명 상태도 아니면 null.
    /// </summary>
    public static JourneyResultData Settle(CaravanData caravan)
    {
        if (caravan == null || caravan.state != JourneyState.Traveling) return null;

        bool fatal = caravan.runFatalReason != JourneyFailureReason.None;
        if (!fatal && !IsArrived(caravan)) return null;   // 아직 정산할 때 아님

        JourneyResultData result = new JourneyResultData();
        if (fatal)
        {
            result.grade = JourneyResultGrade.Failed;
            result.failureReason = caravan.runFatalReason;
            result.cargoLost = caravan.runCargoLost;
        }
        else if (caravan.runCargoLost > 0)
        {
            result.grade = JourneyResultGrade.PartialSuccess;
            result.cargoLost = caravan.runCargoLost;
        }
        else
        {
            result.grade = JourneyResultGrade.Success;
        }

        result.durabilityLost = caravan.runDurabilityLost;   // 이번 무역 내구도 손실 기록 [M2]

        caravan.state = JourneyState.Settling;
        return result;
    }

    /// <summary>정산 수령: 정산대기 → 완료. 이미 받았으면 false(중복 방지).</summary>
    public static bool ClaimSettlement(CaravanData caravan)
    {
        if (caravan == null || caravan.state != JourneyState.Settling) return false;
        if (caravan.settlementClaimed) return false;

        caravan.settlementClaimed = true;
        caravan.state = JourneyState.Completed;
        return true;
    }

    /// <summary>다음 무역 준비로: 완료 → 준비.</summary>
    public static bool ResetToPrepare(CaravanData caravan)
    {
        if (caravan == null || caravan.state != JourneyState.Completed) return false;
        caravan.state = JourneyState.Prepare;
        return true;
    }
}