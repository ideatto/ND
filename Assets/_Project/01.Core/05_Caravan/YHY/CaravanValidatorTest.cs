// =============================================================================
// CaravanValidatorTest — 상단 출발 테스트 (인스펙터에서 구성 → 1대 보내기)
// =============================================================================
// [작성] 윤호영
// [영역] Core Gameplay (테스트) — UI 없이 인스펙터·콘솔로 검증
//
// [핵심 포인트]
//  · 인스펙터에서 caravan 값(마차·동물·용병·무역품·식량)을 직접 세팅한다.
//  · 컴포넌트 우클릭 → "상단 출발 시도" 로 1대를 보내 본다(검증 실행).
//  · 결과(출발 가능 여부 + 사유 + 적재량)를 Console에 출력한다.
//  · 처음엔 우클릭 → "샘플 상단 채우기" 로 기본값을 넣고 시작하면 편하다.
//  · 임시 테스트용 스크립트. 검증이 끝나면 삭제해도 된다.
// =============================================================================

using UnityEngine;

/// <summary>인스펙터에서 상단을 구성해 출발시켜 보는 테스트. 자세한 설명은 상단 주석 참고.</summary>
public class CaravanValidatorTest : MonoBehaviour
{
    [Header("상단 구성 (인스펙터에서 세팅)")]
    public CaravanData caravan = new CaravanData();

    /// <summary>구성한 상단 1대를 출발시켜 본다. (컴포넌트 우클릭 → 이 메뉴)</summary>
    [ContextMenu("상단 출발 시도")]
    public void TryDepart()
    {
        DepartureValidationResult result = CaravanValidator.Validate(caravan);

        float load = caravan.GetCurrentLoad();
        float max = (caravan.wagon != null) ? caravan.wagon.maxLoad : 0f;

        if (result.canDepart)
        {
            caravan.state = TradeState.Ready;   // 검증 통과 → 출발 준비 완료 상태로
            Debug.Log($"[상단] 출발 O   (적재 {load} / 최대 {max})");
        }
        else
        {
            string reasons = string.Join(", ", result.reasons);
            Debug.Log($"[상단] 출발 X   → 사유: {reasons}   (적재 {load} / 최대 {max})");
        }
    }

    /// <summary>처음 테스트용 기본 상단 값을 채운다. (컴포넌트 우클릭 → 이 메뉴)</summary>
    [ContextMenu("샘플 상단 채우기")]
    public void FillSample()
    {
        caravan = new CaravanData();

        // 마차: 최대 적재 100, 최소 견인 동물 2마리
        caravan.wagon = new WagonData { wagonName = "기본 수레", maxLoad = 100f, minAnimals = 2 };

        // 견인 동물 2마리
        caravan.animals.Add(new AnimalData { animalName = "말" });
        caravan.animals.Add(new AnimalData { animalName = "말" });

        // 무역품: 무게 5짜리 밀 5개
        TradeItemData wheat = new TradeItemData { id = "wheat", itemName = "밀", weight = 5f, basePrice = 10 };
        caravan.cargo.Add(new CargoEntry { item = wheat, quantity = 5 });

        // 식량 10개
        caravan.foodAmount = 10;

        Debug.Log("[상단] 샘플 상단으로 채웠습니다. 인스펙터에서 값을 바꿔보세요.");
    }
}
