# Progression & Economy M0 Final Contract

- 작성일: 2026-07-20
- 담당: Progression & System / Economy
- 기준 브랜치: `dev2`
- 상태: M0 팀 검토 초안
- 적용 범위: 가격·정산·성장·건물·수리·손실 상한·파산·기부·투자·구조 대출
- 구조 대출 계산 계약: `0720_Rescue_Loan_Calculation_Contract.md`
- 구조 대출 Framework 요청: `0720_Progression_Requested_Framework_Integration.md`
- 건축 재료·건설 비용 계약: `0720_Construction_Material_Building_Cost_Contract.md`
- 도착·시장 판매 분리 팀 요청: `0720_Progression_Requested_All_Teams.md`

## 1. 목적

7월 22일 이후 구현에서 공용 데이터, SaveData, ViewData, Command API를 대규모로 다시 설계하지 않도록 Progression/Economy의 소유권과 입출력 계약을 고정한다.

이 문서는 밸런스 수치를 고정하는 문서가 아니다. 계산에 필요한 필드, 검증 규칙, 상태 변경 순서, 저장 시점과 실패 동작을 고정한다.

## 2. 공통 소유권

| 영역 | 책임 |
|---|---|
| Progression/Economy | 계산식, 검증, 비용·보상·손실 결과, 원인별 breakdown, 파산·대출 자격 판정 |
| Core | Caravan 및 무역 상태 입력, 확정 결과 적용, 출발 가능 여부 반영 |
| Framework | SaveData DTO, 저장·복구, SaveResult, Trade ID와 중복 처리 방지 |
| UI | ViewData 표시, Command 요청, 성공 전 선반영 금지, 실패 사유 표시 |
| Content/Tools | ID와 밸런스 값, 비용·효과 정의, 참조 무결성, 경계값 프리셋 |

Core와 UI는 Progression/Economy 계산식을 복제하지 않는다. Progression/Economy는 Unity 화면이나 씬을 직접 제어하지 않는다.

## 3. 공통 데이터 규칙

- 모든 재화와 금액은 `long`을 사용한다.
- 모든 수량, 레벨, 내구도와 누적 횟수는 0 미만이 될 수 없다.
- 비율 입력은 의미별 허용 범위를 검증하고 결과 계산 전에 정규화한다.
- 정의 데이터는 안정적인 `Id`로 참조한다. 표시 이름을 저장 키로 사용하지 않는다.
- 계산 결과는 최종 합계만 반환하지 않고 UI가 원인을 표시할 수 있는 breakdown을 반환한다.
- 동일 Command 재처리를 막아야 하는 기능은 안정적인 operation ID 또는 기능별 멱등 키를 사용한다.
- 계산은 가능한 한 순수 함수로 유지한다. 계산 단계에서 SaveData나 런타임 상태를 직접 변경하지 않는다.
- 경제 상태 변경은 `검증 -> snapshot/stage -> 저장 -> 성공 확정 -> event/UI refresh` 순서를 따른다.
- 저장 실패 시 성공 이벤트, 성공 UI, 해금 적용과 씬 전환을 실행하지 않는다.

## 4. 최종 경제 및 정산 계약

기존 진입점 `EconomyM1LoopCalculator.Execute(EconomyM1LoopInput)`와 결과 타입을 2차 빌드 확장의 기준으로 유지한다.

### 입력

- `TradeId`
- 거래 전 `CurrencyState`
- 판매 상품별 ID, 수량, 매입 총액, 판매 총액
- 식량 비용
- 용병 비용
- 수리 비용
- 손실 상품 가치
- 이벤트 수익과 손실
- 대출 상환액
- 발전용 재화 보상
- 가격 보정 원인: 도시, 경로, 계절, 재난, 이벤트, 성장, 공급 상태

모든 비용과 손실 입력은 음수가 아니어야 한다. 할인 또는 보너스는 음수 비용으로 표현하지 않고 별도 modifier와 breakdown으로 표현한다.

### 결과

- 총수익
- 총비용
- 매매 총이익
- 순이익
- 거래 후 재화
- 발전용 재화 보상
- 파산 또는 복구 필요 상태
- 최소 복구 필요 금액
- 원인별 `SettlementEntry`

정산 preview는 SaveData를 변경하지 않는다. Claim 성공 시에만 최종 재화와 성장·런타임 상태를 반영한다.

## 5. 손실 정책 계약

- 손실 상한 기준은 출발 시 확정한 원본 상태다.
- 화물 손실 기준값은 `runOriginalCargoCount`다.
- 2차 빌드 기본 정책은 화물과 내구도가 0까지 내려갈 수 있도록 전체 손실을 허용한다.
- 기존 `lossLimitRate` 계약으로 표현할 때 기본값은 `1`이다.
- 약탈 내구도에도 별도의 보호 하한을 두지 않으며 최종 내구도 0을 허용한다.
- 식량 소비, 정상 이동 마모, 명시적 수리 비용은 약탈 손실 상한에 포함하지 않는다.
- 여러 이벤트가 연속 발생해도 화물 수량과 내구도는 음수가 될 수 없다.
- UI 결과에는 원래 손실량, 최종 손실량과 원인 ID를 제공한다.

추후 성장·건물 효과로 손실 보호를 추가할 경우에만 `lossLimitRate`를 1보다 낮게 설정한다.

## 6. 파산 및 최소 복구 계약

파산은 재화가 음수가 된 상태가 아니라, 현재 사용할 수 있는 거래 재화가 승인된 최소 무역 비용보다 부족한 상태다.

```text
NeedsRecovery = usableTradeMoney < minimumRecoveryTradeMoney
```

- `usableTradeMoney`는 플레이어 전체가 즉시 무역 준비에 사용할 수 있는 거래 재화다.
- `minimumRecoveryTradeMoney`는 설정 가능한 고정 최소 거래 비용이며 Progression/System 또는 공유 정의 데이터가 공급한다.
- 최소 거래 비용에는 말 구매 비용, 마차 구매 비용, 선택한 마차의 모든 슬롯을 채우는 데 필요한 기준 상품 가격을 포함한다.
- Framework와 UI는 최저가 상품 조합을 자체 계산하지 않는다.
- 정산 결과 재화는 0 미만으로 내려가지 않는다.
- 실패 정산 후에도 복구 상태와 필요한 부족액을 결과에 포함한다.
- 복구 필요 상태에서는 구조 대출을 안내한다.

Framework는 위 구성의 최저가 조합이나 preset을 자동 선택하지 않는다. Progression/Content가 승인한 기준값 하나를 사용한다.

## 7. 구조 대출 계약

2차 빌드는 무이자 고정 원금 구조 대출 상품 1종만 지원한다. 상세 제한 모드 정책은 성욱 담당의 `Donation_Investment_Loan_Save_Contract.md`를 기준으로 한다.

### 저장 필드

- `loanId`
- `originalPrincipal`
- `remainingPrincipal`
- `isActive`
- `issuedUtcTicks`

### 규칙

- 활성 대출은 한 번에 하나만 허용한다.
- 원금, 잔액, 상환액은 음수가 될 수 없다.
- 사용 가능한 거래 재화가 최소 거래 비용보다 낮을 때만 발급할 수 있다.
- 발급 원금은 부족분 계산값이 아니라 설정된 고정 최소 거래 비용 전액이다.
- 발급 시 거래 재화 증가와 대출 활성화를 하나의 즉시 저장 작업으로 처리한다.
- 부분 상환을 허용한다.
- 전액 상환 시 잔액을 0으로 만들고 비활성화한다.
- 중복 발급, 초과 상환, 음수 상환을 거부한다.
- 정산 이익에서 자동으로 차감하지 않는다.
- 상환은 플레이어가 명시적으로 요청하며 부분 상환과 전액 상환을 지원한다.
- 대출 후에는 유효한 무역 준비와 출발만 허용하는 제한 모드에 들어간다.
- 제한 모드에서는 기부, 투자와 무관한 성장 구매를 차단한다.
- 승인된 출발이 저장까지 성공한 뒤 제한 모드를 해제한다.
- 활성 대출이 남아 있는 상태에서 다시 복구 필요 상태가 발생하면 게임오버 처리한다.
- 게임오버가 확정되기 전에 현재 대출 잔액과 재파산 사유를 설명하는 경고 문구를 표시한다.

### 제한 모드 팀 책임

| 영역 | 담당 | 책임 |
|---|---|---|
| 제한 조건 | Progression/Economy | 허용·차단할 경제 행동, 최소 거래 비용, 재파산과 게임오버 판정 제공 |
| 출발 검증 | Core | 말·마차·화물 구성의 유효성 및 출발 가능 여부 제공 |
| 저장·복구 | Framework | 대출·제한 상태 즉시 저장, Title·종료 처리, 재실행 복구 |
| 세부 화면 동작 | UI & Data | 화면 이동, 버튼 활성·비활성, 안내·경고 문구, 뒤로가기, 설정·Title·종료 UX 구현 |

제한 모드의 세부 화면 동작은 UI & Data 팀에 전달한다. Progression/Economy는 UI 화면을 직접 제어하지 않고 제한 상태와 실패 코드를 제공한다. UI 팀은 이를 사용해 무역 준비 화면 고정, 제한된 메뉴 표시, 구성 초기화, 저장 실패 표시, 재접속 복귀와 게임오버 사전 경고 흐름을 설계·구현한다.

이자·신용도·연체·복수 상품은 두지 않는다. 기존 기획 문서의 “이자 또는 수익 차감”과 자동 상환은 이 계약으로 대체한다.

## 8. 기부 계약

기부는 시간에 따라 감소하는 누적 시스템을 철폐하고 퀘스트 형식으로 구현한다.

### 저장 필드

- `donationQuestId`
- `townId`
- `status`
- `contributedAmount`
- `isRewardClaimed`
- 완료·보상 소비 멱등 데이터

### 규칙

- 기부는 도시별 기부 퀘스트 단위로 관리한다.
- 퀘스트 정의가 목표 금액 또는 요구 재화, 완료 조건과 보상 효과를 제공한다.
- 기부 비용 차감과 퀘스트 진행 증가를 하나의 즉시 저장 작업으로 처리한다.
- 기부 진행값은 0 미만이 될 수 없고 목표값을 초과한 처리 규칙은 퀘스트 정의를 따른다.
- 퀘스트 완료와 보상 수령은 멱등이어야 하며 보상을 두 번 지급하지 않는다.
- 시간 경과 감소, 오프라인 감소, 보호 최소값은 사용하지 않는다.
- 완료 효과와 이벤트는 저장 성공 뒤 한 번만 발행한다.
- 기부 퀘스트의 반복 가능 여부, 반복 주기와 완료 후 재등장 정책은 후속 협의로 남긴다.
- 후속 정책이 확정되기 전까지 일회성 또는 반복형을 기본값으로 가정해 구현하지 않는다.

## 9. 투자 계약

### 저장 필드

- `investmentId`
- `sourceTownId`
- `progressAmount`
- 안정적인 요구조건 또는 목표 ID
- `isCompleted`
- 해금된 콘텐츠 또는 상태 ID 목록

### 규칙

- 투자 대상은 2차 빌드에서 최소 1개를 제공한다.
- 비용 지불, 진행 증가, 완료와 해금을 하나의 즉시 저장 작업으로 처리한다.
- 도시 기부금을 투자 재화로 전환할 경우 사용 가능 기부금을 초과할 수 없다.
- 완료와 해금은 멱등이어야 하며 보상을 두 번 지급하지 않는다.
- 2차 빌드 기본 계약에는 시간 기반 완료와 투자 실패를 포함하지 않는다.
- 최소 보상은 신규 도시 또는 무역로 1개 해금이다.

## 10. 성장 계약

- 플레이어 성장과 Caravan 성장을 각각 최소 1종 구현한다.
- 성장 ID, 현재 레벨, 최대 레벨, 다음 비용과 효과 ID를 입력으로 사용한다.
- 비용은 발전용 재화를 기본으로 한다.
- 구매 결과는 이전 레벨, 새 레벨, 지불 비용, 남은 재화, 실패 사유를 반환한다.
- 플레이어 성장과 Caravan 성장은 동일한 역할을 중복하지 않는다.
- 성장 효과는 `CoreRuntimeStatModifier`를 통해 다음 무역에 반영한다.
- 성장 구매와 재화 차감은 하나의 즉시 저장 작업이다.
- 현재 `GrowthPurchaseCalculator`와 `GrowthCalculator` 계약을 확장하되 Core/UI에 계산식을 복제하지 않는다.

## 11. 건물 비용·효과 계약

- 2차 빌드에서 거점 건물 또는 시설을 최소 1종 구현한다.
- 건축 재료는 별도 화폐가 아니라 일반 무역품과 동일한 상품 데이터 및 시장 규칙을 사용한다.
- 건축 재료는 도시 시장에서 구매·판매할 수 있고 Caravan 화물로 운송할 수 있다.
- 건축 재료는 기존 Caravan 화물 계약을 따른다.
- 플레이어 본기지의 건물 건설과 강화는 `SaveData.caravan.cargo`에 있는 지정 건축 재료를 직접 소비한다.
- 시장 재고나 이동 중인 Caravan의 재료는 건설 비용으로 사용할 수 없다.
- 건설 비용은 하나 이상의 `itemId + quantity` 요구 목록으로 정의한다.
- `developmentCurrency`는 건축 재료를 뜻하지 않으며 기존 성장 보상·성장 구매 계약과 분리한다.
- 정의 데이터는 `buildingId`, 최대 레벨, 레벨별 재료 요구 목록, 효과 ID와 효과 값을 제공한다.
- SaveData는 현재 `VillageBuildingSaveData` 정책을 유지한다.
- 건설 결과는 이전 레벨, 새 레벨, 소비한 재료 목록, 거점 잔여 재고와 실패 사유를 제공한다.
- 건물 효과는 준비 preview와 정산에서 같은 정의 데이터를 사용한다.
- 건물 건설·레벨 증가와 Caravan cargo 차감은 하나의 즉시 저장 작업이다.

안정적인 `buildingId` 추가는 이번 M0 범위에서 수행하지 않는다. 기존 표시 이름 기반 저장 정책의 변경이 필요해질 경우 Framework와 별도 마이그레이션으로 다룬다.

## 12. 수리 비용 계약

```text
repairAmount = clamp(targetDurability - currentDurability, 0, maxDurability - currentDurability)
repairCost = floor(repairAmount * baseCostPerDurability * wagonCostMultiplier)
```

- 부분 수리와 전체 수리를 허용한다.
- 비용과 내구도는 음수가 될 수 없다.
- 현재 내구도가 최대 내구도 이상이면 수리를 거부한다.
- 재화가 부족하면 상태를 변경하지 않고 부족액을 반환한다.
- 결과는 수리 전후 내구도, 수리량, 비용, 남은 재화와 실패 사유를 반환한다.
- 재화 차감과 내구도 변경은 하나의 즉시 저장 작업이다.
- 수리 preview와 실제 구매는 동일한 계산기를 사용한다.

수리 비용의 소수점 이하는 내림 처리한다.

## 13. Command 및 저장 계약

최소 Command 후보:

- `PurchaseGrowth`
- `PurchaseOrUpgradeBuilding`
- `RepairWagon`
- `DonateToTown`
- `ProcessDonationDecay`
- `ConsumeDonationEvent`
- `Invest`
- `IssueRescueLoan`
- `RepayRescueLoan`

Progression Command는 1단계에서 Framework의 `SaveResult`를 직접 반환한다. 기능별 결과 확장이 필요해질 때 통합 결과 타입 도입을 검토한다.

모든 값 소비·지급 Command는 최소한 다음 저장 결과를 상위 호출자까지 전달한다.

- 성공 여부
- 안정적인 실패 코드
- `SaveResult`

기능별 변경 전후 값과 breakdown은 기존 계산 결과 또는 별도 query/ViewData로 제공한다. 직접 반환 방식만으로 실패 원인 표현이 부족해지는 시점에 `ProgressionCommandResult<T>` 같은 통합 결과로 확장한다.

`ISaveService.Save()` 반환 계약이 최종 확정될 때까지 Progression/Economy는 자체 저장 구현을 만들지 않고 Framework command/service 경유를 우선한다.

## 14. 공통 실패 코드

- `InvalidInput`
- `DefinitionNotFound`
- `InsufficientCurrency`
- `AlreadyCompleted`
- `AlreadyActive`
- `NotEligible`
- `LimitExceeded`
- `InvalidState`
- `DuplicateOperation`
- `SaveFailed`

UI 표시 문구는 실패 코드를 기반으로 UI/Data가 결정한다. 계산기는 최종 사용자 문장을 반환하지 않는다.

## 15. M0 테스트 시나리오

- 금액·수량·레벨 음수 입력 거부
- 비용과 보상이 `long` 범위를 넘을 때 안전하게 실패
- 정산 preview에서 SaveData 불변
- Claim 한 번만 적용 및 중복 Claim 거부
- 연속 이벤트로 화물과 내구도가 0까지 내려갈 수 있으나 음수가 되지 않음
- 실패 정산 후 최소 복구 금액 표시
- 활성 대출 중 추가 발급 거부
- 부분 상환과 전액 상환 저장·복구
- 기부 퀘스트·투자 완료 보상 중복 방지
- 저장 실패 시 재화·레벨·내구도·해금 rollback
- 성장·건물·수리 preview와 실제 비용 일치
- 건축 재료를 일반 상품처럼 구매·판매·운송할 수 있음
- Caravan cargo가 부족하면 건설을 거부하고 재료와 레벨을 변경하지 않음
- 건설 저장 실패 시 소비한 거점 재료와 건물 레벨 rollback
- 재실행 후 기부·투자·대출·성장·건물·내구도 유지

## 16. 7/20 확정 결정

1. 구조 대출은 무이자 고정 원금 상품 1종으로 한다.
2. 정산 자동 차감은 사용하지 않고 명시적 부분·전액 상환을 허용한다.
3. 최소 거래 비용은 말, 마차, 마차의 모든 슬롯을 채우는 기준 상품 비용을 포함한다.
4. 대출 제한 모드의 세부 정책은 성욱 담당 구조 대출 저장 계약을 따른다.
5. 기부 시간 감소를 철폐하고 퀘스트 형식으로 전환한다.
6. 화물과 내구도는 0까지 손실될 수 있다.
7. 수리 비용 소수점은 내림 처리한다.
8. 건축 재료는 일반 무역품처럼 매매·운송하며, 본기지에 도착한 `SaveData.caravan.cargo`에서 건설·강화 비용으로 직접 소비한다. `developmentCurrency`와는 분리한다.
9. `VillageBuildingSaveData`는 현재 정책을 유지한다.
10. Progression Command는 `SaveResult`를 직접 반환하고 필요할 때 통합 결과로 확장한다.

## 17. 남은 확인 항목

1. 제한 모드의 취소, Title, 설정, 종료 세부 화면 동작은 UI & Data 팀에 전달하고, 성욱 담당 저장 계약과 정렬한다.
2. 고정 최소 거래 비용의 실제 수치와 기준 말·마차·상품 정의 ID를 Content/Tools가 제공한다.
3. 활성 대출 상태의 재파산 경고 문구, 확인 흐름과 게임오버 화면은 UI & Data 팀이 담당한다.
4. 건축 재료의 상품 ID, 도시별 공급량, 가격과 건물별 요구 수량을 Content/Tools가 제공한다.
5. `SaveData.caravan.cargo`를 `BuildingCostInput`으로 변환하고 요구 재료만 원자적으로 차감하는 Command와 UI 흐름을 Core/UI/Framework와 정렬한다.
6. 기부 퀘스트의 반복 가능 여부, 반복 주기와 완료 후 재등장 정책은 추후 협의한다.

## 18. M0 완료 조건

- Core, Framework, UI, Content/Tools가 위 입출력과 소유권에 동의한다.
- 남은 확인 항목에 담당자와 결정 기한이 지정된다.
- SaveData 추가 필드가 Framework 문서와 정렬된다.
- 기능별 최소 테스트 시나리오가 owner 브랜치에 배정된다.
- 7월 22일 이후 계산식 구현 중 공용 계약을 대규모로 바꾸지 않는다.
