// =============================================================================
// TradeDataDraft — 상단 부품 데이터 초안 (무역품·마차·동물·용병)
// =============================================================================
// [작성] 윤호영 (요구사항 초안)
// [영역] 공용 데이터 — 정의 소유는 UI & Data 모듈.
//
// [핵심 포인트]
//  · 출발 검증·이동 계산에 필요한 "최소 필드"만 담은 초안이다.
//  · 지금 쓰는 필드: 무역품 weight, 마차 overLoad·maxLoad·minAnimals.
//  · 나머지 필드(basePrice·speedModifier·combatPower 등)는 "나중에 쓸 예정" 표시만.
//  · [Serializable]은 Inspector·테스트에서 다루기 쉽게 붙여둔 것.
// =============================================================================

using System;

/// <summary>무역품 하나의 데이터 (초안)</summary>
[Serializable]
public class imsiTradeItemData
{
    public string id;        // 아이템 구분용 ID
    public string itemName;  // 이름
    public float weight;     // 무게 → 적재량 계산에 사용
    public long basePrice;   // 기본 가격(돈=long, 팀 결정) → 정산 때 사용
    public int maxCount = 1;  // [M2] 한 칸(슬롯)에 쌓이는 최대 개수(스택). SO TradeItemData.maxCount
}

/// <summary>마차 데이터 (초안)</summary>
[Serializable]
public class imsiWagonData
{
    // [2차/멀티 상단] 보유 개체 식별자. "어떤 종류"가 아니라 "내가 가진 이 한 대"를 가리킨다.
    //   같은 종류 마차를 2대 보유할 수 있으므로 종류 ID로는 자산 잠금을 못 한다.
    //   값은 소유·저장 시스템(Framework)이 부여하며 Core는 받아서 비교만 한다.
    public string instanceId = string.Empty;

    public string wagonName;     // 마차 이름

    // ── 적재 기준 2개 ─────────────────────────────────────────
    // 무게에는 서로 다른 두 기준선이 있다. (overLoad ≤ maxLoad 로 세팅)
    //
    //   0 ─────── overLoad ─────── maxLoad ─────── ✕
    //     속도 100%     속도 점점 감소      출발 불가
    //
    //  · overLoad : "1:1로 갈 수 있는 최대치". 이 무게까지는 속도 100%.
    //               넘으면 넘을수록 이동 속도가 감소한다(출발은 됨). 감속 곡선은 CaravanConfig.
    //  · maxLoad  : 물리적 상한. 이 무게를 넘으면 마차가 감당 못 해 출발 자체가 불가(하드 컷).
    public float overLoad;       // 기준 적재량(속도 100% 한계) — 초과 시 감속
    public float maxLoad;        // 물리 상한 — 초과 시 출발 불가

    public int minAnimals;       // 이 마차를 끌 최소 견인 동물 수 (기본 1)
    public int maxAnimals;       // 매달 수 있는 최대 견인 동물 수 (수레 1 / 마차 5 등)
    public float speedModifier;  // 이동 속도 보정 → 이동 계산에 사용 예정
    public int maxDurability = 100;  // [임시] 최대 내구도. 진짜 값은 Content. [M2]
    public int inventorySlotCount = 1;  // [M2] 마차 짐칸(슬롯) 수. SO WagonData.inventorySlotCount
}

/// <summary>견인 동물 데이터 (초안)</summary>
[Serializable]
public class imsiAnimalData
{
    // [2차/멀티 상단] 보유 개체 식별자 (imsiWagonData.instanceId와 같은 규칙).
    public string instanceId = string.Empty;

    public string animalName;  // 동물 이름
    public float speed = 1f;   // 이동 속도 배수 (말=1 기준 / 당나귀=0.5 / 타조=1.5)
    // [인게임시간] 이 값은 '인게임 1초당' 식량 소모율(raw). 단위(분/시간/하루) 정규화는 Framework(ToConsumptionPerInGameSecond).
    // 필드명 foodPerKm 은 천성욱 harness가 참조 중이라, rename(foodPerInGameSecond 등)은 별도 PR 예정.
    public float foodPerKm;    // (현재 의미: 인게임 1초당 식량 소모율)
    public float increaseOverLoad;  // [M2] 이 동물이 늘려주는 적정적재(효율 한계). SO DraftAnimalData.increaseOverLoad
    public float increaseMaxLoad;   // [M2] 이 동물이 늘려주는 최대적재(물리 한계). SO DraftAnimalData.increaseMaxLoad
    public DraftAnimalType animalType;  // [M2] 동물 종류(말·당나귀 등). SO DraftAnimalData.AnimalType → 단일종류 검증에 사용
    // TODO(M1 이후): 종류별 특성(속도·적재보너스)은 여기 필드가 생기면 반영.
    //               당나귀 느림/적재↑, 말 중간, 타조 빠름/적재↓ 를 한 묶음으로 설계.
}

/// <summary>용병 데이터 (초안)</summary>
[Serializable]
public class imsiMercenaryData
{
    // [2차/멀티 상단] 보유 개체 식별자 (imsiWagonData.instanceId와 같은 규칙).
    public string instanceId = string.Empty;

    public string mercName;     // 용병 이름
    public int combatPower;     // 전투력 → 약탈 판정에 사용 예정
    public int contractCount;   // 남은 계약 횟수
}