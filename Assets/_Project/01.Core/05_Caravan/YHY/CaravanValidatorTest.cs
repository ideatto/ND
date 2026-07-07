// =============================================================================
// CaravanValidatorTest — 출발 검증 더미 테스트 (M0 산출물)
// =============================================================================
// [작성] 윤호영
// [영역] Core Gameplay (테스트) — UI 없이 코드로만 검증
//
// [핵심 포인트]
//  · M0 산출물 "UI 없이 실행 가능한 더미 테스트".
//  · 가짜 상단 3개(정상 / 적재량 초과 / 구성 누락)를 만들어 CaravanValidator.Validate()를 돌린다.
//  · 결과(출발 가능 여부 + 사유)를 Console에 출력한다.
//  · 사용법: 아무 씬의 빈 GameObject에 이 스크립트를 붙이고 Play → Console 확인.
//  · 검증이 끝나면 삭제해도 되는 임시 테스트용 스크립트다.
// =============================================================================

using UnityEngine;

/// <summary>출발 검증 더미 테스트. 자세한 설명은 상단 주석 참고.</summary>
public class CaravanValidatorTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("===== 상단 출발 검증 테스트 시작 =====");

        // ── 테스트 1: 정상 상단 (출발 가능해야 함) ──
        CaravanData normal = MakeSampleCaravan();
        PrintResult("정상 상단", normal);

        // ── 테스트 2: 적재량 초과 상단 ──
        CaravanData overloaded = MakeSampleCaravan();
        overloaded.cargo[0].quantity = 999;   // 무역품 수량을 확 늘려 무게 초과
        PrintResult("적재량 초과 상단", overloaded);

        // ── 테스트 3: 구성 누락 상단 (마차·무역품 없음) ──
        CaravanData missing = MakeSampleCaravan();
        missing.wagon = null;                  // 마차 제거
        missing.cargo.Clear();                 // 무역품 제거
        PrintResult("구성 누락 상단", missing);

        Debug.Log("===== 테스트 끝 =====");
    }

    /// <summary>테스트용 "정상" 상단을 하나 만든다.</summary>
    private CaravanData MakeSampleCaravan()
    {
        CaravanData c = new CaravanData();

        // 마차: 최대 적재 100, 최소 견인 동물 2마리
        c.wagon = new WagonData { wagonName = "기본 수레", maxLoad = 100f, minAnimals = 2 };

        // 견인 동물 2마리 (최소 조건 충족)
        c.animals.Add(new AnimalData { animalName = "말" });
        c.animals.Add(new AnimalData { animalName = "말" });

        // 무역품: 무게 5짜리 밀 5개 = 25
        TradeItemData wheat = new TradeItemData { id = "wheat", itemName = "밀", weight = 5f, basePrice = 10 };
        c.cargo.Add(new CargoEntry { item = wheat, quantity = 5 });

        // 식량 10개 (1개당 무게 1 = 10)  → 총 적재 35 (<= 100 이라 정상)
        c.foodAmount = 10;

        return c;
    }

    /// <summary>검증 결과를 콘솔에 보기 좋게 출력한다.</summary>
    private void PrintResult(string label, CaravanData caravan)
    {
        DepartureValidationResult result = CaravanValidator.Validate(caravan);

        if (result.canDepart)
        {
            Debug.Log($"[{label}] 출발 가능 O   (현재 적재 {caravan.GetCurrentLoad()})");
        }
        else
        {
            string reasons = string.Join(", ", result.reasons);
            Debug.Log($"[{label}] 출발 불가 X   → 사유: {reasons}   (현재 적재 {caravan.GetCurrentLoad()})");
        }
    }
}
