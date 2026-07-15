# 무역 정산 UI 연동 요청

작성일: 2026-07-15  
요청 및 협업 대상:

- **Core**: 이동 결과와 정산에 필요한 원시 수량 제공
- **Economy / Progression**: 손실 가치, 이벤트 손익, 수리 비용 계산 정책과 정산 항목 제공
- **QA / 통합**: CoreServices 공개 View 계약, Claim·저장·pending 처리 경로와 Economy 입력 연결 제공 및 통합 검증
- **통합 UI 담당 Core**: 확정된 공개 계약을 사용해 정산·결제 화면 최종 연결

## 배경

통합 UI 담당 Core 영역에서 PDF 8번 무역 결과 정산 창과 9번 결제 창을 구현한다. 담당 영역 경계를 지키기 위해 QA / 통합이 소유한 `11.CoreServices` 내부 코드를 임의로 수정하지 않으며, 공개 계약 없이 해당 타입을 직접 사용하는 임시 연동 코드도 비활성화했다.

## 요청 사항

### 1. UI용 정산 결과 계약 제공

주 담당: **QA / 통합**  
협업 담당: **Economy / Progression**

현재 `SettlementViewData`에는 총수익, 총비용, 순이익, 먹이 소모, 화물 손실, 내구도 손실 등이 있지만 다음 개별 정산 행은 제공되지 않습니다.

- 먹이 비용
- 용병 고용 비용
- 마차 수리 비용
- 상품 손실 금액
- 이벤트 수익
- 이벤트 손실
- 구매 상품 및 판매 상품별 수량·단가·금액

UI가 Economy/Core 내부 구현을 직접 참조하지 않도록 위 항목을 포함한 읽기 전용 UI DTO 또는 공개 view contract를 제공해 주세요. 가능하면 `SettlementBreakdown.Entries`와 동등한 항목 목록을 `SettlementViewData`를 통해 전달해 주세요.

### 2. 결과 표시 및 결제 완료 진입점 확정

주 담당: **QA / 통합**  
연결 담당: **통합 UI 담당 Core**

다음 흐름을 공식 UI 연동 경로로 사용할 수 있는지 확인해 주세요.

`TradeSettlementReady -> SettlementUiBridge -> SettlementUiDataAdapter -> ISettlementView`

결제 애니메이션 완료 후 호출할 공식 API도 확정해 주세요. 현재 후보는 `SettlementUiDataAdapter.OnClickClaimSettlement()`이며, 이 호출이 아래 처리를 모두 담당하는 구조로 이해하고 있습니다.

- 중복 claim 방지
- Economy 정산 결과 적용
- 저장
- pending settlement 정리
- Preparation 상태 복귀

UI 프리팹에서 수동 참조를 요구하지 않는 바인딩 방법이나 공식 bootstrap 연결 지점도 함께 제공해 주세요.

### 3. 비용 매핑 규칙 확정

정책·금액 계산 담당: **Economy / Progression**  
원시 결과 제공 담당: **Core**  
입력 연결·검증 담당: **QA / 통합**

`FrameworkEconomyM1InputBuilder`에서 확인한 현재 매핑은 다음과 같습니다.

- `RouteDefinition.BaseFoodCost -> FoodCost`
- `RouteDefinition.BaseMercenaryCost -> MercenaryCost`
- `JourneyResultData.durabilityLost * 임시 단가 1 -> CartRepairCost`

다음 항목의 공식 계산 및 매핑을 추가하거나 규칙을 알려 주세요.

- 상품 손실 가치 `LostItemValue`
- 이벤트 수익 `EventProfit`
- 이벤트 손실 `EventLoss`
- 실제 수리 단가를 적용한 `CartRepairCost`

### 4. 01.Core 이동 결과 데이터 제공 범위

주 담당: **Core**  
공개 계약 연결 담당: **QA / 통합**

정산 UI에는 다음 정보가 필요합니다. `JourneyResultData` 또는 별도 UI DTO에서 안정적으로 제공해 주세요.

- 결과 등급과 실패 사유
- 실제 이동 시간
- 먹이 소모량과 이벤트로 인한 먹이 손실
- 용병 수와 전투 횟수
- 상품 손실 수량 및 손실 가치
- 현재/최대 마차 내구도와 내구도 손실

UI가 `CaravanData` 내부 필드를 직접 읽지 않도록 최종 결과 스냅샷 형태를 권장합니다.

## 통합 UI 담당 Core 측 현재 조치

- `Assets/_Project/01.Core`, `Assets/_Project/11.CoreServices` 내부 변경은 원복했습니다.
- `TradeSettlementPanelController`의 `ND.Framework.ISettlementView` 직접 구현을 제거했습니다.
- `PaymentPanelController`의 `SettlementViewData` 직접 오버로드를 제거했습니다.
- `Assets/Scripts/UI/CoreJourneySettlementViewAdapter.cs`는 `#if false`로 전체 비활성화했습니다.
- UI는 현재 `ND.Economy`의 정산 결과를 직접 전달받는 독립 실행 경로만 유지합니다.

Economy / Progression이 정산 정책과 항목을 확정하고 QA / 통합이 공개 계약을 제공하면, 통합 UI 담당 Core가 해당 계약만 사용해 정산 창과 결제 창을 최종 연결한다.
