// =============================================================================
// WagonRepairRule — 마차 수리 가능 여부 판정 (Core 도메인 검증)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] "이 마차를 지금 수리할 수 있는가"만 판정한다.
//        수리 비용 계산과 재화 차감은 여기서 하지 않는다(경제 = Progression 소유).
//
// [근거] Multi_Caravan_Save_Architecture:
//        "수리는 최대 내구도를 넘을 수 없고, <b>파괴된 마차를 대상으로 할 수 없다.</b>
//         비용표와 희귀도 배율은 공유 데이터 또는 소유 기능의 데이터 정의에서 온다."
//
// [경계] 비용 공식(rawCost = repairedDurability × repairCostPerDurability × rarity,
//        floor 후 최소 1)은 Progression 소유다. Core가 복제하지 않는다.
//
// [순수 로직] static · UnityEngine 의존 없음.
// =============================================================================

/// <summary>수리 불가 사유.</summary>
public enum WagonRepairBlockReason
{
    None,             // 수리 가능
    NoWagon,          // 마차가 없음
    WagonDestroyed,   // 파괴된 마차 — 수리 대상 아님
    AlreadyFull,      // 이미 최대 내구도
    InJourney         // 이동/정산 중 — 준비 상태에서만 수리
}

/// <summary>마차 수리 가능 여부 판정. 비용은 다루지 않는다.</summary>
public static class WagonRepairRule
{
    /// <summary>
    /// 지금 이 상단의 마차를 수리할 수 있는지 판정한다.
    /// </summary>
    /// <param name="caravan">대상 상단.</param>
    /// <param name="reason">불가 사유(가능하면 None).</param>
    /// <returns>수리 가능하면 true.</returns>
    public static bool CanRepair(CaravanData caravan, out WagonRepairBlockReason reason)
    {
        if (caravan == null || caravan.wagon == null)
        {
            reason = WagonRepairBlockReason.NoWagon;
            return false;
        }

        // 파괴된 마차는 수리 대상이 아니다(계약).
        // 이번 여정에서 파괴됐거나, 내구도가 이미 0 이하로 남아 있으면 파괴로 본다.
        if (caravan.runWagonDestroyed || caravan.currentDurability <= 0)
        {
            reason = WagonRepairBlockReason.WagonDestroyed;
            return false;
        }

        // 이동·정산 중에는 구성을 바꿀 수 없다 → 수리도 준비 상태에서만.
        if (caravan.state != JourneyState.Prepare)
        {
            reason = WagonRepairBlockReason.InJourney;
            return false;
        }

        if (caravan.currentDurability >= caravan.wagon.maxDurability)
        {
            reason = WagonRepairBlockReason.AlreadyFull;
            return false;
        }

        reason = WagonRepairBlockReason.None;
        return true;
    }

    /// <summary>
    /// 수리로 채울 수 있는 최대 내구도 양(= 최대치 − 현재치). 수리 불가면 0.
    /// 비용 계산의 입력값(repairedDurability)으로 쓰라고 제공한다 — 비용 자체는 Progression이 계산.
    /// </summary>
    public static int GetRepairableAmount(CaravanData caravan)
    {
        WagonRepairBlockReason reason;
        if (!CanRepair(caravan, out reason)) return 0;

        int amount = caravan.wagon.maxDurability - caravan.currentDurability;
        return amount > 0 ? amount : 0;
    }
}
