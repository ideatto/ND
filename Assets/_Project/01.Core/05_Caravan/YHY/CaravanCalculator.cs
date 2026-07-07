// =============================================================================
// CaravanCalculator — 상단 계산 통합 (적재 · 이동 · 식량)  (Core 소유)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 상단(CaravanData) 하나에 대한 물리 계산을 한 곳에 모은다.
//        호출 시 계산기 하나만 기억하면 됨:  CaravanCalculator.xxx
//        · 적재: GetCurrentLoad / GetMaxLoad
//        · 이동: GetSpeedEfficiency / GetLoadEfficiency / GetTravelSeconds ...
//        · 식량: GetConsumptionPerKm / GetRequiredFood / GetRemainingFood  (거리 기반 연료)
//
//        튜닝 값(기준속도·효율·감속 등)은 CaravanConfig 에 분리돼 있다.
//        UnityEngine 의존 없음 → 순수 로직 테스트 가능.
//
// [값만 계산, 판단 안 함] "부족하니 막아라/경고해라"는 정하지 않는다. UI/검증이 결정.
// =============================================================================

using System.Collections.Generic;

/// <summary>상단 적재·이동·식량 계산 통합. static 유틸.</summary>
public static class CaravanCalculator
{
    // =========================================================================
    // 적재 (Load)
    // =========================================================================

    /// <summary>현재 적재 총 무게 = 무역품 무게 합 + 식량 무게 합.</summary>
    public static float GetCurrentLoad(CaravanData caravan)
    {
        if (caravan == null) return 0f;

        float total = 0f;
        foreach (CargoEntry entry in caravan.cargo)
        {
            if (entry != null && entry.item != null)
                total += entry.item.weight * entry.quantity;
        }
        total += caravan.foodAmount * caravan.foodUnitWeight;
        return total;
    }

    /// <summary>물리 상한 = 마차 maxLoad. 마차 없으면 0.</summary>
    public static float GetMaxLoad(CaravanData caravan)
    {
        return (caravan != null && caravan.wagon != null) ? caravan.wagon.maxLoad : 0f;
    }

    // =========================================================================
    // 이동 (Travel)   속도 = 기준속도 × 동물수효율 × 적재효율 × 동물종류속도 × 마차보정
    // =========================================================================

    /// <summary>동물 수 → 속도 효율 배수. (1→1.0 / 2→1.5 / 3→2.0)</summary>
    public static float GetSpeedEfficiency(int animalCount)
    {
        if (animalCount <= 1) return 1f;

        float eff = 1f + CaravanConfig.PerExtraAnimal * (animalCount - 1);
        if (CaravanConfig.MaxEfficiency > 0f && eff > CaravanConfig.MaxEfficiency)
            eff = CaravanConfig.MaxEfficiency;
        return eff;
    }

    /// <summary>적재 무게 → 속도 효율 배수. 기준선(overLoad) 이하 1.0, 넘으면 감속.</summary>
    public static float GetLoadEfficiency(float currentLoad, float overLoad)
    {
        if (overLoad <= 0f) return 1f;
        if (currentLoad <= overLoad) return 1f;

        float overRatio = (currentLoad - overLoad) / overLoad;
        float factor = 1f - CaravanConfig.LoadPenalty * overRatio;
        if (factor < CaravanConfig.LoadFactorMin) factor = CaravanConfig.LoadFactorMin;
        return factor;
    }

    /// <summary>[미정 자리 — 1.0] 동물 종류별 속도. AnimalData.speed 생기면 반영.</summary>
    public static float GetAnimalTypeSpeed(List<AnimalData> animals)
    {
        // TODO: 당나귀 느림/타조 빠름. 평균/합산 규칙 정해서 반영.
        return 1f;
    }

    /// <summary>[미정 자리] 마차 속도 보정. speedModifier 0/미설정이면 1.0(중립).</summary>
    public static float GetWagonSpeedModifier(WagonData wagon)
    {
        if (wagon != null && wagon.speedModifier > 0f) return wagon.speedModifier;
        return 1f;
    }

    /// <summary>1마리 기준 속도 (Km/초).</summary>
    public static float GetBaseSpeedKmPerSec()
    {
        if (CaravanConfig.BaseSeconds <= 0f) return 0f;
        return CaravanConfig.BaseDistanceKm / CaravanConfig.BaseSeconds;
    }

    /// <summary>거리(Km) → 소요 시간(초). 뒤 두 항(동물종류·마차보정)은 지금 1.0.</summary>
    public static float GetTravelSeconds(CaravanData caravan, float distanceKm)
    {
        if (caravan == null) return 0f;

        float currentLoad = GetCurrentLoad(caravan);
        float overLoad = (caravan.wagon != null) ? caravan.wagon.overLoad : 0f;

        float speed = GetBaseSpeedKmPerSec()
                    * GetSpeedEfficiency(caravan.animals.Count)
                    * GetLoadEfficiency(currentLoad, overLoad)
                    * GetAnimalTypeSpeed(caravan.animals)
                    * GetWagonSpeedModifier(caravan.wagon);

        if (speed <= 0f) return 0f;
        return distanceKm / speed;
    }

    // =========================================================================
    // 식량 (Food) — "연료" 개념. 시간이 아니라 거리로 소모한다.
    //   남은 식량 = 실은 식량 - (거리당소모 × 간 거리) - 이벤트 차감
    // =========================================================================

    /// <summary>Km당 식량 소모 = 동물들의 foodPerKm 합. 동물 많을수록 커짐(연료 더 듦).</summary>
    public static float GetConsumptionPerKm(CaravanData caravan)
    {
        if (caravan == null) return 0f;

        float perKm = 0f;
        foreach (AnimalData a in caravan.animals)
        {
            if (a != null) perKm += a.foodPerKm;
        }
        return perKm;
    }

    /// <summary>이 무역에 필요한 총 식량(출발 전 UI 표시용) = Km당 소모 × 이동 거리.</summary>
    public static float GetRequiredFood(CaravanData caravan)
    {
        if (caravan == null) return 0f;
        return GetConsumptionPerKm(caravan) * caravan.currentDistanceKm;
    }

    /// <summary>지금 진행도에서 남은 식량. 0 이하면 바닥(실패 판정은 JourneyRunner).</summary>
    public static float GetRemainingFood(CaravanData caravan)
    {
        if (caravan == null) return 0f;

        float traveledKm = caravan.progress01 * caravan.currentDistanceKm;   // 간 거리
        float consumed = GetConsumptionPerKm(caravan) * traveledKm;
        return caravan.foodAmount - consumed - caravan.runFoodLost;
    }
}