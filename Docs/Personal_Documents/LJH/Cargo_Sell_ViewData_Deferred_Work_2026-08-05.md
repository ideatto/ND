# Cargo Sell ViewData 후속 작업 보류 기록

## 결정

- 현재 CargoSell 통합 작업에서는 `CargoSellViewData.cs`를 수정하지 않는다.
- MainUICanvas 연결은 완료되었지만 ViewData 필드 추가나 의미 변경은 별도 소유자 합의 이후 진행한다.
- 현재 Builder와 Popup은 기존 ViewData 계약 안에서만 값을 생성하고 표시한다.

## 보류 항목

1. Caravan 표시명 정책
   - 현재 진입점은 저장된 `CaravanSaveData.displayName`이 아닌 기존 계약 범위의 값을 전달한다.
   - `Caravan 1` 같은 표시명은 ID나 배열 순서를 UI Controller에서 추론하지 않는다.
   - production Caravan 조회 facade가 확정되면 표시명을 read-only projection으로 제공한다.

2. 물리 Cargo 슬롯 표현
   - 현재 ViewData에는 최대 슬롯 수, 사용 슬롯 수, 아이템별 최대 스택 수가 없다.
   - Prefab에 미리 배치된 슬롯은 재사용하되 정확한 용량 기반 잠금 표시는 보류한다.
   - 이후 별도 read-only 슬롯 layout 계약 또는 ViewData 확장을 소유자와 결정한다.

3. 외부 변경 후 완전한 재투영
   - transaction 계층은 확정 시 Caravan ID, Trade ID, 보유 수량과 구매가격 그룹을 재검증한다.
   - 실패 후 UI snapshot을 최신 상태로 완전히 재생성하는 facade/API는 후속 범위로 둔다.

## 책임 분리 기준

- UI/Controller가 `SaveData`를 직접 변경하거나 가격·슬롯 정책을 새로 소유하지 않는다.
- 조회와 command는 항상 명시적인 `caravanId`와 필요한 경우 `tradeId`를 사용한다.
- 저장, rollback, Journey 전환과 이벤트 발행은 기존 TradeProgress/Market 계층에 남긴다.
- `FrameworkRoot.Instance` 접근을 신규 하위 View나 계산 객체로 확산하지 않는다.

## 후속 결정 필요

- Caravan 표시명의 authoritative provider
- 슬롯 layout을 기존 ViewData에 추가할지 별도 projection으로 둘지
- 동일 Caravan Cargo가 외부에서 변경됐을 때 Popup draft 보존 또는 전량 폐기 정책
- 기존 `ArrivalSalePanelView`는 fallback으로 유지하며 CargoSellPopup 사용 중에는 비표시한다. 완전 제거 여부는 후속 결정한다.
