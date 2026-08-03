# 마켓 최소 재고 및 구매·판매 가격 Modifier 적용 작업 기록

## Purpose

- 마켓 재고의 최소·최대 생성 수량을 같은 `MarketData` SO에서 설정할 수 있게 한다.
- `TradeItemData`에 이미 작성 가능한 구매·판매 가격 Modifier를 마켓 UI와 실제 거래에 연결한다.
- 기존 SO 개별 편집 방식과 기존 마켓 SaveData 구조를 최대한 유지한다.

## Scope

### 변경

- `Assets/99.Sandbox/_LJH/01.Script/Data/MarketData.cs`
- `Assets/_Project/03.Economy/01_Market/PriceCalculator.cs`
- `Assets/Scripts/UI/MarketInventoryIntegration.cs`
- `Assets/Scripts/UI/Market/MarketTradePanelController.cs`
- `Assets/_Project/11.CoreServices/Scripts/Debug/MarketTravelValidationHarness.cs`
- `Assets/_Project/03.Economy/01_Market/Editor/MarketPriceModifierTests.cs`
- `Docs/Guide/Market_And_Route_Event_Data_Setup_Guide.md`

### 확인만 수행

- `Assets/99.Sandbox/_LJH/01.Script/Data/TradeItemData.cs`
- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Data/Calculation/ModifierInput.cs`
- `Assets/_Project/03.Economy/06_Integration/LjhEconomyM1InputAdapter.cs`
- `Assets/_Project/03.Economy/01_Market/MarketTransactionCalculator.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs`

### 제외

- Scene 및 Prefab
- Package 설정
- 운영용 `MarketData`, `TradeItemData` SO의 실제 밸런스 값 입력
- 랜덤 가격 변동과 갱신별 판매가 저장 구조

## Ownership

- `MarketData`, `TradeItemData`와 Sandbox 가격 입력 구조의 원천 작성자는 Git 이력상 `ljh-ccc`다.
- 마켓 통합 경로에는 `ljh-ccc`, `junghen001-oss`, `csu1222`의 변경 이력이 함께 존재한다.
- 마켓 및 Economy 담당자 리뷰가 필요하다.

## Changes

### MarketData 최소 재고

- `Market_Info`에 `itemMinimumQuantity` 직렬화 필드를 추가했다.
- Inspector에는 `Item Minimum Quantity`로 표시된다.
- 최소 허용값은 1이다.
- `Item Max Quantity`가 최소값보다 작지 않도록 `OnValidate()`에서 보정한다.
- 기존 마켓 SO에서 신규 필드가 0으로 역직렬화되어도 공개 속성은 1을 반환한다.

마켓 재고 생성 범위:

```text
MarketData.ItemMinimumQuantity
~
MarketData.ItemMaxQuantity
```

### 가격 단가 계산 API

- `PriceCalculator.CalculateUnitPrices()`를 추가했다.
- 출발 도시, 도착 도시, Route, 수량 없이 기준 구매가·판매가와 Modifier만으로 단가를 계산한다.
- 기존 `PriceCalculator.Calculate()`도 같은 단가 계산 API를 사용하게 해 적용 순서와 반올림 규칙을 공유한다.
- 최종 가격은 기존 정책대로 최소 1로 제한한다.

### TradeItemData Modifier 연결

- `TradeItemData.AffectModify`가 켜진 경우에만 기존 Modifier 배열을 가격 입력으로 변환한다.
- `AffectModify`가 꺼져 있거나 Modifier 배열이 비어 있으면 기본 구매가와 기본 판매가를 유지한다.
- 현재 Adapter가 가격에 연결하는 연산은 `Add`, `Percent`다.
- `BuyPrice` 대상 Modifier는 구매가에만, `SellPrice` 대상 Modifier는 판매가에만 적용한다.

### 구매 가격 흐름

```text
TradeItemData.BaseBuyPrice
→ BuyPrice Modifier 적용
→ 마켓 재고 생성 시 stock.unitPrice에 저장
→ UI 구매 표시
→ 실제 구매 결제 및 매입 단가 기록
```

- 현재 갱신 구간에 이미 생성된 구매가는 다음 재고 갱신 전까지 저장값을 유지한다.

### 판매 가격 흐름

```text
TradeItemData.BaseSellPrice
→ SellPrice Modifier 적용
→ UI 판매 표시
→ MarketTransactionCalculator 입력
→ 실제 판매 정산
```

- UI와 실제 거래가 동일한 `ResolveUnitPrices()` 결과를 사용한다.

### 기존 호출 호환

- `MarketInventoryMutationSession.TryOpen()`에 최소 재고 인자를 받는 오버로드를 추가했다.
- 기존 호출부는 최소 재고 1로 새 오버로드에 위임한다.
- 실제 마켓 UI와 `MarketTravelValidationHarness`는 현재 `MarketData.ItemMinimumQuantity`를 전달한다.

### 테스트와 팀 가이드

- 구매·판매 Modifier가 각 Target에 독립적으로 적용되는 테스트를 추가했다.
- `MarketData.ItemMinimumQuantity`가 최소 1로 Clamp되는 테스트를 추가했다.
- 팀 공용 가이드에 마켓 설정 위치와 RouteData/RouteEventData 산적 등장 조건·판정 방식을 정리했다.

## Check

- `dotnet build Assembly-CSharp.csproj --no-restore`
  - 오류 0
  - 기존 경고 43
- 변경 대상 `git diff --check`
  - 통과
- `minimumMarketStock`, `MinimumMarketStock` 잔여 참조
  - 없음
- Unity EditMode 테스트
  - 수동 실행 필요
- 원본 운영용 SO 밸런스 값
  - 변경하지 않음

## Risk

- Scene 변경: No
- Prefab 변경: No
- Meta 변경: Yes
  - 신규 Editor 테스트 스크립트 `.meta`가 추가된다.
- Package 변경: No
- SaveData 구조 변경: No
- 직렬화 필드 변경: Yes
  - `MarketData.itemMinimumQuantity`가 추가된다.
  - 기존 에셋에서는 기본적으로 최소 재고 1로 동작한다.
- Enum 직렬화 변경: No
- Public API 변경: Yes
  - `MarketData.ItemMinimumQuantity`와 가격 단가 계산 API, 마켓 세션 오버로드가 추가된다.
- 이벤트 연결 변경: No
- 다중 객체 또는 ID 연결 변경: No
- 기존 데이터 마이그레이션 필요: No
- 원천 소유자 리뷰 필요: Yes

## Remaining

- 운영용 마켓 SO별 `Item Minimum Quantity` 밸런스 값 입력
- Unity EditMode 테스트 및 실제 마켓 구매·판매 수동 검증
- 가격 Modifier를 사용하는 운영 상품 SO의 Target, Operation, Value 검토

