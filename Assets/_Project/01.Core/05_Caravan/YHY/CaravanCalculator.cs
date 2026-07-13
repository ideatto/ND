// =============================================================================
// CaravanCalculator — 상단 계산 통합 (적재 · 이동 · 식량)  (Core 소유)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 상단(CaravanData) 하나에 대한 물리 계산을 한 곳에 모은다.
//        호출 시 계산기 하나만 기억하면 됨:  CaravanCalculator.xxx
//        · 적재: GetCurrentLoad / GetMaxLoad
//        · 이동: GetSpeedEfficiency / GetLoadEfficiency / GetTravelSeconds ...
//        · 식량: GetConsumptionPerSec / GetRequiredFood / GetRemainingFood  (시간 기반 — 초당 소모)
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

    /// <summary>무역품 무게 합 = Σ(아이템 무게 × 수량).</summary>
    public static float GetCargoWeight(CaravanData caravan)
    {
        if (caravan == null) return 0f;

        float total = 0f;
        foreach (CargoEntry entry in caravan.cargo)
        {
            if (entry != null && entry.item != null)
                total += entry.item.weight * entry.quantity;
        }
        return total;
    }

    /// <summary>식량 무게 = 식량 수량 × 1개당 무게.</summary>
    public static float GetFoodWeight(CaravanData caravan)
    {
        if (caravan == null) return 0f;
        return caravan.foodAmount * caravan.foodUnitWeight;
    }

    /// <summary>현재 적재 총 무게 = 무역품 무게 + 식량 무게.</summary>
    public static float GetCurrentLoad(CaravanData caravan)
    {
        return GetCargoWeight(caravan) + GetFoodWeight(caravan);
    }

    /// <summary>물리 상한 = 마차 maxLoad + 동물들 increaseMaxLoad 합. [M2 동물 추가 효율]</summary>
    public static float GetMaxLoad(CaravanData caravan)
    {
        if (caravan == null || caravan.wagon == null) return 0f;
        float total = caravan.wagon.maxLoad;
        foreach (imsiAnimalData a in caravan.animals)
            if (a != null) total += a.increaseMaxLoad;
        return total;
    }

    /// <summary>기본 적정 적재 = 마차 overLoad (동물 없을 때의 속도 100% 한계). [M2]</summary>
    public static float GetBaseEfficientLoad(CaravanData caravan)
    {
        return (caravan != null && caravan.wagon != null) ? caravan.wagon.overLoad : 0f;
    }

    /// <summary>동물 추가 효율 적재 = 동물들 increaseOverLoad 합. [M2]</summary>
    public static float GetAdditionalEfficientLoad(CaravanData caravan)
    {
        if (caravan == null) return 0f;
        float sum = 0f;
        foreach (imsiAnimalData a in caravan.animals)
            if (a != null) sum += a.increaseOverLoad;
        return sum;
    }

    /// <summary>최종 적정 적재 = 기본 + 동물 추가. 이 이하면 속도 100%, 넘으면 과적 감속. [M2]</summary>
    public static float GetFinalEfficientLoad(CaravanData caravan)
    {
        return GetBaseEfficientLoad(caravan) + GetAdditionalEfficientLoad(caravan);
    }

    /// <summary>과적 비율 = (현재적재 - 최종적정적재) / 최종적정적재. 적정 이하면 0. [M2]</summary>
    public static float GetOverloadRatio(CaravanData caravan)
    {
        float efficient = GetFinalEfficientLoad(caravan);
        if (efficient <= 0f) return 0f;
        float load = GetCurrentLoad(caravan);
        if (load <= efficient) return 0f;
        return (load - efficient) / efficient;
    }

    /// <summary>과적 상태인가 = 현재적재 &gt; 최종적정적재. [M2] UI 표시용.</summary>
    public static bool IsOverloaded(CaravanData caravan)
    {
        return GetCurrentLoad(caravan) > GetFinalEfficientLoad(caravan);
    }

    /// <summary>실은 무역품 총 개수 = 상품별 수량 합.</summary>
    public static int GetCargoCount(CaravanData caravan)
    {
        if (caravan == null) return 0;

        int total = 0;
        foreach (CargoEntry entry in caravan.cargo)
        {
            if (entry != null) total += entry.quantity;
        }
        return total;
    }

    /// <summary>마차 짐칸(슬롯) 수 = 마차 inventorySlotCount. 마차 없으면 0. [M2]</summary>
    public static int GetMaxSlots(CaravanData caravan)
    {
        return (caravan != null && caravan.wagon != null) ? caravan.wagon.inventorySlotCount : 0;
    }

    /// <summary>사용 중인 슬롯 수 = 아이템별 (수량 ÷ 스택크기, 올림) 합.
    /// 같은 아이템은 한 칸에 maxCount개까지 쌓임. [M2]</summary>
    public static int GetUsedSlots(CaravanData caravan)
    {
        if (caravan == null) return 0;

        int slots = 0;
        foreach (CargoEntry entry in caravan.cargo)
        {
            if (entry == null || entry.item == null) continue;
            int stack = (entry.item.maxCount > 0) ? entry.item.maxCount : 1;   // 스택크기(0 방어)
            slots += (entry.quantity + stack - 1) / stack;                     // 올림 나눗셈
        }
        return slots;
    }

    /// <summary>견인 동물이 전부 같은 종류인가 (0~1마리면 true). [M2] 단일종류 검증용.</summary>
    public static bool IsAnimalTypeUniform(CaravanData caravan)
    {
        if (caravan == null || caravan.animals == null) return true;

        bool hasFirst = false;
        DraftAnimalType first = default;
        foreach (imsiAnimalData a in caravan.animals)
        {
            if (a == null) continue;
            if (!hasFirst) { first = a.animalType; hasFirst = true; }   // 첫 동물 종류 기준
            else if (a.animalType != first) return false;               // 하나라도 다르면 섞임
        }
        return true;
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

    /// <summary>이 상단의 적재로 인한 속도 배수(과적 감속). UI "예상 속도 감소" 표시용. [M2]
    /// = GetLoadEfficiency(현재적재, 최종적정적재). 과적 아니면 1.0.</summary>
    public static float GetLoadSpeedModifier(CaravanData caravan)
    {
        return GetLoadEfficiency(GetCurrentLoad(caravan), GetFinalEfficientLoad(caravan));
    }

    /// <summary>[미정 자리 — 1.0] 동물 종류별 속도. imsiAnimalData.speed 생기면 반영.</summary>
    public static float GetAnimalTypeSpeed(List<imsiAnimalData> animals)
    {
        if (animals == null || animals.Count == 0) return 1f;

        float sum = 0f;
        int count = 0;
        foreach (imsiAnimalData a in animals)
        {
            if (a != null) { sum += a.speed; count++; }
        }
        return count > 0 ? sum / count : 1f;   // 동물들 속도의 '평균'
    }

    /// <summary>[미정 자리] 마차 속도 보정. speedModifier 0/미설정이면 1.0(중립).</summary>
    public static float GetWagonSpeedModifier(imsiWagonData wagon)
    {
        if (wagon != null && wagon.speedModifier > 0f) return wagon.speedModifier;
        return 1f;
    }

    /// <summary>1마리 기준 속도 (Km/초).</summary>
    public static float GetBaseSpeedKmPerSec()
    {
        // CaravanConfig.BaseSeconds 는 상수(>0)라 0 나눗셈 위험 없음.
        return CaravanConfig.BaseDistanceKm / CaravanConfig.BaseSeconds;
    }

    /// <summary>거리(Km) → 소요 시간(초). 뒤 두 항(동물종류·마차보정)은 지금 1.0.</summary>
    public static float GetTravelSeconds(CaravanData caravan, float distanceKm)
    {
        if (caravan == null) return 0f;

        float currentLoad = GetCurrentLoad(caravan);
        float overLoad = GetFinalEfficientLoad(caravan);   // [M2] 마차 기본 + 동물 추가 효율 적재

        float speed = GetBaseSpeedKmPerSec()
                    * GetSpeedEfficiency(caravan.animals.Count)
                    * GetLoadEfficiency(currentLoad, overLoad)
                    * GetAnimalTypeSpeed(caravan.animals)
                    * GetWagonSpeedModifier(caravan.wagon);

        if (speed <= 0f) return 0f;
        return distanceKm / speed;
    }

    // =========================================================================
    // 식량 (Food) — [2026-07-09 팀결정] '시간' 기준으로 소모한다. (거리 기준 폐기)
    //   1단계(지금): 초당 소모.  2단계(이후): 인게임 시간 배율 적용한 시간당 소모(GameTimeService.TimeScale).
    //   남은 식량 = 실은 식량 - (초당소모 × 흐른 시간) - 이벤트 차감
    // =========================================================================

    /// <summary>인게임 초당 식량 소모 = 동물들의 소모율 합. 동물 많을수록 커짐. [인게임시간]
    /// foodPerKm는 인게임 초당 소모율로 해석(단위 정규화는 Framework 정책).</summary>
    public static float GetConsumptionPerSec(CaravanData caravan)
    {
        if (caravan == null) return 0f;

        float perSec = 0f;
        foreach (imsiAnimalData a in caravan.animals)
        {
            // a.foodPerKm = 인게임 1초당 소모율(단위 정규화는 Framework). 필드명 rename은 별도 PR 예정.
            if (a != null) perSec += a.foodPerKm;
        }
        return perSec;
    }

    /// <summary>출발 전 예상 총 식량 = 인게임 초당 소모 × 예상 인게임 소요시간(초). [인게임시간]
    /// 예상 인게임 소요 = 현실 총시간 × 배율. 배율은 바깥(Framework/테스트)이 곱해 넘긴다.</summary>
    public static float GetRequiredFood(CaravanData caravan, float expectedInGameSeconds)
    {
        if (caravan == null) return 0f;
        return GetConsumptionPerSec(caravan) * expectedInGameSeconds;
    }

    /// <summary>출발 전 예상 식량(거리 기반) = 인게임 초당 소모 × (예상 현실 소요시간 × 배율). [인게임시간]
    /// 배율(inGameTimeMultiplier)은 바깥(Framework/테스트)이 넘긴다.</summary>
    public static float GetEstimatedFood(CaravanData caravan, float distanceKm, float inGameTimeMultiplier)
    {
        return GetConsumptionPerSec(caravan) * GetTravelSeconds(caravan, distanceKm) * inGameTimeMultiplier;
    }

    /// <summary>남은 식량 = 실은 식량 − (인게임 초당 소모 × 누적 인게임 경과초) − 이벤트 차감. [인게임시간]
    /// elapsedInGameSeconds는 바깥(Framework/테스트)이 채운다. 0 이하면 바닥(실패 판정은 JourneyRunner).</summary>
    public static float GetRemainingFood(CaravanData caravan)
    {
        if (caravan == null) return 0f;

        float consumed = GetConsumptionPerSec(caravan) * caravan.elapsedInGameSeconds;   // 인게임 경과 기준
        return caravan.foodAmount - consumed - caravan.runFoodLost;
    }

    // =========================================================================
    // 준비 화면 표시용 묶음 (UI 지원) — 위 계산값들을 한 번에 반환
    // =========================================================================

    /// <summary>준비 화면에 필요한 Core 계산값을 한 번에 묶어 반환한다. [1차 빌드 UI 지원]
    /// UI(이종현님)는 이 한 번의 호출로 PrepareDisplayData를 받아 바인딩만 하면 된다.</summary>
    /// <param name="distanceKm">선택한 무역로 거리(Km).</param>
    /// <param name="inGameTimeMultiplier">인게임 시간 배율 (예상 식량 계산용). Framework/테스트가 넘김.</param>
    public static PrepareDisplayData BuildPrepareDisplay(CaravanData caravan, float distanceKm, float inGameTimeMultiplier)
    {
        PrepareDisplayData d = new PrepareDisplayData();
        if (caravan == null) return d;

        // 적재(무게)
        d.currentLoad       = GetCurrentLoad(caravan);
        d.cargoWeight       = GetCargoWeight(caravan);
        d.foodWeight        = GetFoodWeight(caravan);
        d.overloadLimit     = GetFinalEfficientLoad(caravan);
        d.maxLoad           = GetMaxLoad(caravan);
        d.isOverloaded      = IsOverloaded(caravan);
        d.overloadRatio     = GetOverloadRatio(caravan);
        d.loadSpeedModifier = GetLoadSpeedModifier(caravan);

        // 슬롯(칸)
        d.usedSlots = GetUsedSlots(caravan);
        d.maxSlots  = GetMaxSlots(caravan);

        // 개수·식량·시간
        d.cargoCount             = GetCargoCount(caravan);
        d.estimatedTravelSeconds = GetTravelSeconds(caravan, distanceKm);
        d.requiredFood           = GetEstimatedFood(caravan, distanceKm, inGameTimeMultiplier);

        return d;
    }
}