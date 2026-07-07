using System;

// ⚠️ 초안(Draft): 출발 검증 로직을 만들기 위한 "최소 필드"만 담았다.
// 최종 필드와 형식(ScriptableObject로 갈지 등)은 UI & Data(이종현)와 확정한다.

/// <summary>무역품 하나의 데이터 (초안)</summary>
[Serializable]
public class TradeItemData
{
    public string id;        // 아이템 구분용 ID
    public string itemName;  // 이름
    public float weight;     // 무게 → 적재량 계산에 사용
    public int basePrice;    // 기본 가격 → 정산 때 사용 예정
}

/// <summary>마차 데이터 (초안)</summary>
[Serializable]
public class WagonData
{
    public string wagonName;     // 마차 이름
    public float maxLoad;        // 최대 적재량 → 이 이상 실으면 출발 불가
    public int minAnimals;       // 이 마차를 끌 최소 견인 동물 수
    public float speedModifier;  // 이동 속도 보정 → 이동 계산에 사용 예정
}

/// <summary>견인 동물 데이터 (초안)</summary>
[Serializable]
public class AnimalData
{
    public string animalName;  // 동물 이름
    public float foodPerHour;  // 시간당 식량 소모 → 식량 계산에 사용 예정
}

/// <summary>용병 데이터 (초안)</summary>
[Serializable]
public class MercenaryData
{
    public string mercName;     // 용병 이름
    public int combatPower;     // 전투력 → 약탈 판정에 사용 예정
    public int contractCount;   // 남은 계약 횟수
}