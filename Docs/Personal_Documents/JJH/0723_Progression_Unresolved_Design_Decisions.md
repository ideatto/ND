# Progression 미결 기획 및 임시 구현 목록

- 작성일: 2026-07-23
- 담당: Progression & System / Economy
- 상태: 팀 합의 대기
- 기준 문서:
  - `00_Team_Rules_and_Milestone_Second_Build.md`
  - `05_Progression_System_Milestone_Second_Build.md`
  - `Earn_Money_While_Lying_Down_Game_Design.md`

## 1. 목적

검증 과정에서 발견한 미결 기획과 코드의 임의 가정을 분리해 기록한다.

이 문서에 적힌 공식, 수치, ID, 보너스와 실패 정책은 팀 합의 전까지 확정 기획이 아니다. 현재 코드를 유지해야 하는 근거로 사용하지 않으며, 실제 연결 작업 전에 기획 담당자와 관련 팀의 확인을 받아야 한다.

각 항목은 다음 형식으로 기록한다.

- **미결 내용**: 현재 상위 기획 문서만으로 결정할 수 없는 사항
- **현재 임의 구현 방식**: 검증 대상 코드가 임시로 선택한 처리 방식
- **임시 값**: 테스트 또는 기본값으로 코드에 입력된 수치와 ID
- **더 구체화해야 하는 기획**: 팀 합의로 확정해야 할 질문과 데이터
- **확정 전 처리 원칙**: 합의 전 코드 정리 및 연결 기준

---

## 2. Caravan 해금 수 계산

### 미결 내용

- 게임 시작 시 사용할 수 있는 Caravan 슬롯 수
- Caravan 성장과 슬롯 해금의 관계
- 투자 퀘스트가 Caravan 슬롯을 해금하는지 여부
- 성장과 퀘스트가 각각 몇 개의 슬롯을 해금하는지
- 최대 4개에 도달한 뒤 추가 보너스를 어떻게 처리할지

상위 기획에서 확정된 내용은 `최대 4개`와 해금·최대 보유 수 정책을 Progression/Shared Data가 제공한다는 점뿐이다.

### 현재 임의 구현 방식

`CaravanProgressionPolicy`는 다음 공식을 사용한다.

```text
growthBonus
= min(CaravanGrowthLevel × SlotsPerGrowthLevel,
      MaximumGrowthSlotBonus)

unlockedSlots
= BaseUnlockedSlots
 + growthBonus
 + QuestUnlockedSlotBonus

finalUnlockedSlots
= min(unlockedSlots, MaximumSupportedSlots)
```

### 임시 값

```text
MaximumSupportedSlots = 4
BaseUnlockedSlots = 1
SlotsPerGrowthLevel = 1
MaximumGrowthSlotBonus = 2
QuestUnlockedSlotBonus = 외부 입력
```

`MaximumSupportedSlots = 4`만 상위 기획에 근거한다. 나머지는 테스트 및 구현을 위해 넣은 임시 값이다.

### 더 구체화해야 하는 기획

- 기본 해금 슬롯은 몇 개인가?
- Caravan 성장 몇 레벨마다 슬롯을 몇 개 해금하는가?
- 성장으로 해금할 수 있는 슬롯의 상한이 있는가?
- 4번째 슬롯을 투자 퀘스트로 해금하는가?
- 슬롯 해금 보상은 특정 퀘스트 ID와 연결되는가?
- 이미 최대 4개라면 추가 해금 보상은 무효, 대체 보상, 또는 지급 완료 중 무엇으로 처리하는가?
- 해금 수를 재계산식으로 도출할지, SaveData에 확정 결과로 저장할지?

### 확정 전 처리 원칙

- 최대 4개 검증만 유효한 규칙으로 유지한다.
- 성장 및 퀘스트 보너스 공식은 확정 기획으로 취급하지 않는다.
- 임시 공식을 UI, SaveData 마이그레이션 또는 실제 생성 Command에 연결하지 않는다.

---

## 3. Caravan 생성 비용과 생성 결과

### 미결 내용

- 해금된 빈 슬롯에 Caravan을 생성할 때 비용이 필요한지
- 비용을 낸다면 어떤 재화를 사용하는지
- 슬롯 순서나 보유 수에 따라 비용이 증가하는지
- 생성 시 어떤 초기 프리셋과 자산을 제공하는지

### 현재 임의 구현 방식

`CaravanProgressionPolicy`와 생성 경제 계획은 고정 `CreationCost`를 검사하고 거래 재화에서 한 번 차감하는 방식을 가정한다.

### 임시 값

```text
CreationCost = 100
비용 재화 = tradingCurrency
비용 증가율 = 없음
```

`100`은 테스트 값이며 기획 근거가 없다.

### 더 구체화해야 하는 기획

- 슬롯 해금과 Caravan 생성은 같은 보상인가, 별도 절차인가?
- 생성 비용이 있는가?
- 비용 재화는 거래 재화, 개발 재화 또는 별도 자산 중 무엇인가?
- 비용 공식은 고정인가, 보유 Caravan 수에 따라 증가하는가?
- 생성 시 마차·동물·용병·식량·화물의 초기 상태는 무엇인가?
- 초기 프리셋 ID는 Content Data에서 어떻게 지정하는가?
- 비용 차감, Caravan 추가, 마지막 선택 Caravan 변경을 하나의 저장 transaction으로 처리하는가?

### 확정 전 처리 원칙

- `CreationCost = 100`을 실제 밸런스 값으로 사용하지 않는다.
- 생성 Command는 기획 확정 전 Framework 및 UI에 연결하지 않는다.
- Content의 초기 프리셋과 SaveData 생성 계약이 합의된 뒤 구현한다.

---

## 4. Caravan 슬롯 식별과 저장

### 미결 내용

- UI의 고정 슬롯과 SaveData의 Caravan 목록을 어떤 값으로 연결할지
- 목록 순서를 슬롯으로 간주할지, 안정적인 슬롯 식별자를 저장할지
- 삭제·마이그레이션·정렬 후 슬롯 위치를 유지할지

### 현재 임의 구현 방식

`CaravanProgressionPolicy`는 `OccupiedSlotIndices`를 외부 입력으로 받고, 0부터 최대 슬롯 수 미만의 고유 정수인지 검증한다. 현재 SaveData에 해당 슬롯을 안정적으로 저장한다는 계약은 없다.

### 임시 값

```text
slotIndex 범위 = 0..3
빈 슬롯 = OccupiedSlotIndices에 없는 가장 작은 index
```

### 더 구체화해야 하는 기획

- Caravan SaveData에 `slotIndex`를 저장할 것인가?
- 또는 `caravanId` 목록 순서만 사용할 것인가?
- Caravan 삭제를 허용하는가?
- 삭제 후 빈 슬롯을 유지하는가, 앞쪽으로 당기는가?
- 마지막 선택 Caravan은 `caravanId`와 `slotIndex` 중 무엇으로 저장하는가?
- 기존 저장에는 어떤 마이그레이션 규칙으로 슬롯을 배정하는가?

### 확정 전 처리 원칙

- 목록 순서를 영구 식별자로 가정하지 않는다.
- UI 고정 슬롯과 저장 복구가 필요하므로 stable `slotIndex` 저장을 우선 검토하되, 팀 합의 전 필드를 추가하지 않는다.

---

## 5. 플레이어 성장과 Caravan 성장의 저장 단위

### 미결 내용

- 각 성장 축에 성장 항목이 정확히 하나인지
- 한 축에 여러 `growthId`가 존재하는 성장 트리인지
- 레벨을 축별 하나로 저장할지, `growthId`별로 저장할지

상위 기획은 플레이어 성장 1종 이상, Caravan 성장 1종 이상과 두 축의 역할이 중복되지 않아야 한다는 점을 요구하지만 저장 단위까지 확정하지 않는다.

### 현재 임의 구현 방식

`DualGrowthPolicyCalculator`는 여러 `GrowthId` 정의를 받을 수 있지만 결과 레벨은 다음 두 값만 사용한다.

```text
playerGrowthLevel
caravanGrowthLevel
```

같은 축의 서로 다른 `GrowthId`가 하나의 축 레벨을 공유하는 형태다.

### 임시 값

테스트에서 사용하는 예:

```text
GrowthId = player_capacity
GrowthId = caravan_load
Cost = 20
EffectValue = 10
```

ID, 비용, 효과 값 모두 테스트용이다.

### 더 구체화해야 하는 기획

- 2차 빌드에서 각 축당 성장 항목을 하나로 제한하는가?
- 여러 성장 항목을 허용한다면 SaveData에 `growthId + level` 목록을 저장하는가?
- 성장별 최대 레벨은 얼마인가?
- 선행 성장이나 해금 조건이 있는가?
- 플레이어 성장과 Caravan 성장이 각각 변경하는 실제 능력치는 무엇인가?
- 성장 효과를 구매 직후 어느 파생 상태에 반영하고, 다음 거래 입력에 어떻게 전달하는가?

### 확정 전 처리 원칙

- `player_capacity`, `caravan_load`, 비용 20, 효과 10을 콘텐츠 확정값으로 사용하지 않는다.
- 기존 `GrowthPurchaseCalculator`와 `GrowthCalculator`에 통합할 때 저장 단위가 확정될 때까지 복수 Growth ID 구조를 추가하지 않는다.

---

## 6. 성장 비용과 효과 계산

### 미결 내용

- 성장 레벨별 비용 공식
- 효과가 고정 증가, 비율 증가 또는 테이블 값인지
- 효과 합산 순서와 상한
- 개발 재화 부족 및 최대 레벨 실패의 UI 표시값

### 현재 임의 구현 방식

신규 검증 코드는 정의 데이터에서 고정 비용과 고정 효과 값을 읽어 구매 전후 레벨 및 잔액을 계산하는 방식을 사용한다.

### 임시 값

```text
Cost = 20
EffectValue = 10
비용 증가 공식 = 없음
효과 상한 = 없음
```

### 더 구체화해야 하는 기획

- 각 성장 ID의 레벨별 비용표
- 각 레벨이 제공하는 효과와 적용 대상
- 비용과 효과를 Shared Data 테이블로 직접 정의할지, 공식으로 계산할지
- 비율 효과와 고정 효과가 동시에 존재할 때 적용 순서
- 최대 레벨과 초과 구매 처리
- 성장 효과가 거래 준비 Preview와 실제 정산에 동일하게 반영되는 검증 기준

### 확정 전 처리 원칙

- 코드 상수로 비용과 효과를 확정하지 않는다.
- 공식이 결정되지 않으면 레벨별 명시적 테이블을 우선 사용한다.

---

## 7. 계절·재난 Modifier 계산식

### 미결 내용

- 여러 계절·재난 효과가 동시에 적용될 때 합산 또는 곱연산 여부
- 가격, 속도, 식량, 위험, 손실 modifier의 적용 순서
- 각 수치의 최소·최대 한계
- 가격 반올림 및 최종 최소 가격 규칙

상위 기획은 여름·겨울·가뭄·홍수와 가격 또는 수량 변화, 이동 속도·식량·위험 modifier 및 표시 사유를 요구하지만 정확한 조합 공식은 정하지 않는다.

### 현재 임의 구현 방식

`JourneyProgressionModifierCalculator`는 여러 modifier를 곱하고 자체 상한과 하한을 적용하는 방식을 가정한다.

### 임시 값

```text
가격 배율 상한 = 10
속도 배율 상한 = 3
식량 배율 상한 = 5
위험 배율 상한 = 5
손실 배율 상한 = 5
```

이 값들은 상위 기획에 없다. 또한 기존 `PriceCalculator`의 연산·반올림·최소 가격 규칙과 중복될 수 있다.

### 더 구체화해야 하는 기획

- modifier 종류별 연산 방식: Add, Multiply, Override
- 적용 우선순위와 동일 우선순위 정렬 기준
- 가격은 기존 `PriceModifierInput`과 `PriceCalculator`만 사용하도록 할지
- 속도가 0이 될 수 있는지와 최소 이동 속도
- 식량 소비량의 반올림 시점
- 위험 확률 및 손실률의 범위
- 손실량이 0까지 감소할 수 있는 조건
- UI에 표시할 원인과 실제 계산 source ID의 대응 방식

### 확정 전 처리 원칙

- 임시 상한을 실제 규칙으로 사용하지 않는다.
- 가격 계산은 기존 `PriceCalculator`로 통합하고 Journey 계산기에서 중복 처리하지 않는다.
- 속도·식량·위험·손실 공식은 합의 후 각 소유 계층에 분리한다.

---

## 8. 투자 보상과 기존 해금의 중복

### 미결 내용

- 투자 퀘스트가 제공하려는 해금이 이미 다른 경로로 해금되어 있을 때의 처리
- 일부 보상만 이미 보유한 경우 투자 완료 가능 여부
- 중복 보상을 대체 보상으로 전환할지 여부

### 현재 임의 구현 방식

신규 Stage Validator는 대상 해금 중 하나라도 이미 존재하면 투자 전체를 실패시키는 방식을 사용한다.

### 임시 값

```text
기존 해금 발견 시 = 전체 투자 실패
대체 보상 = 없음
```

### 더 구체화해야 하는 기획

- 이미 해금된 보상이 있어도 퀘스트를 완료할 수 있는가?
- 해금 목록 추가를 idempotent하게 처리할 것인가?
- 전부 해금된 상태라면 비용 제출을 허용하는가?
- 중복 보상을 거래 재화·개발 재화 등으로 대체하는가?
- 퀘스트 완료 여부와 보상 지급 여부를 별도로 저장해야 하는가?

### 확정 전 처리 원칙

- 이미 해금된 항목이 있다는 이유만으로 투자 전체를 실패시키는 규칙을 확정하지 않는다.
- 기본 기술 정책으로는 목록 추가를 idempotent하게 처리하고, 동일 퀘스트의 재완료는 `investmentQuestId` 완료 상태로 차단하는 방식을 우선 검토한다.

---

## 9. 저장 성공 후 이벤트 실패 처리

### 미결 내용

- Save 성공 후 event listener가 예외를 발생시키면 Command 전체를 실패로 표시할지
- 이미 디스크에 반영된 상태를 rollback할지
- UI 재동기화와 오류 기록 정책

### 현재 임의 구현 방식

일부 신규 transaction 구현은 이벤트 발행 실패를 전체 작업 실패로 반환하는 방식을 가정한다.

### 임시 값

```text
event 실패 = Command 실패
저장 성공 후 rollback 가능 여부 = 정의되지 않음
```

### 더 구체화해야 하는 기획

- transaction의 commit 지점을 Save 성공으로 볼 것인가?
- 이벤트는 transaction 내부 작업인가, post-commit 통지인가?
- listener 실패 시 재시도 또는 다음 화면에서 SaveData 재조회가 필요한가?
- 로그, 사용자 오류 메시지, telemetry 책임 팀은 어디인가?

### 확정 전 처리 원칙

- 저장 성공 후에는 durable state가 확정된 것으로 보는 방식을 우선한다.
- 이벤트 실패는 post-commit notification failure로 기록하고, 저장 결과를 실패로 뒤집지 않는 방향을 관련 팀과 합의한다.

---

## 10. Preview와 실행 사이의 데이터 변경

### 미결 내용

- UI Preview 이후 실제 Command 실행 전에 재화, 재고, 레벨 또는 Shared Data가 바뀐 경우의 처리
- Preview 결과인 Plan을 실행 입력으로 신뢰할지
- 콘텐츠 버전 또는 저장 revision 검증이 필요한지

### 현재 임의 구현 방식

일부 신규 코드는 Preview에서 만든 Plan을 transaction 입력으로 전달하는 구조를 사용한다. Plan 생성 이후 상태 변경을 어느 계층에서 재검증할지는 통일되지 않았다.

### 임시 값

```text
Plan 유효 기간 = 정의되지 않음
Save revision 검사 = 없음
Content version 검사 = 없음
```

### 더 구체화해야 하는 기획

- 실행 Command가 현재 SaveData와 Shared Data로 항상 재계산하는가?
- Preview와 실행 결과가 달라지면 UI에 어떤 실패 코드를 반환하는가?
- 동일 버튼 중복 입력 방지는 UI 잠금, request ID 또는 Command idempotency 중 무엇으로 처리하는가?
- 장시간 열린 Popup에서 콘텐츠 정의가 바뀌는 상황을 지원하는가?

### 확정 전 처리 원칙

- Preview Plan을 권위 있는 저장 입력으로 사용하지 않는다.
- 실행 Command는 현재 SaveData와 현재 Shared Data를 다시 조회해 검증·계산한다.
- 저장 revision 또는 별도 request ID가 필요하다면 Framework 계약으로 합의한 후 추가한다.

---

## 11. 확정 및 반영 절차

각 항목은 다음 절차로 확정한다.

1. Progression이 미결 내용과 현재 임의 구현을 제시한다.
2. 기획 담당자와 데이터·Framework·Core·UI 관련 팀이 영향 범위를 확인한다.
3. 공식, 수치, ID, 저장 필드, 실패 코드를 문서에 명시한다.
4. 상위 기획 문서 또는 팀 공용 계약에 확정 내용을 반영한다.
5. 임시 구현과 테스트 값을 확정 계약에 맞게 수정한다.
6. 기존 시스템과 중복되는 신규 계층을 제거하거나 기존 코드로 통합한다.
7. 실제 SaveData와 Framework Command를 사용하는 통합 테스트로 검증한다.

팀 합의가 끝나지 않은 항목은 `미결` 상태로 유지하고 실제 게임 흐름에 연결하지 않는다.
