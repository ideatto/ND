# 향후 무역·Caravan·건물 UI 작업 결정 사항

- 작성일: 2026-08-04
- 목적: 앞으로 진행할 판매 UI, Caravan 선택 ID, 실패 정산, 건물 편집 UI 작업에서 이미 합의된 방향과 미확정 사항을 구분한다.
- 관련 문서:
  - `Cargo_Sell_UI_Status_2026-08-03.md`
  - `Cargo_Sell_Currency_Display_Policy_2026-08-03.md`
  - `0721_Caravan_Overview_UI_Contract.md`
  - `0730_Building_Popup_External_Integration_Request.md`
  - `../JJH/0803_Caravan_Setting_Service_Responsibility_Extraction_Work_Log.md`

## 공통 원칙

- UI는 SaveData를 직접 수정하지 않는다. Provider가 ViewData를 제공하고 Command가 저장 변경을 담당한다.
- 복수 Caravan 기능은 표시 이름이나 현재 화면의 암묵적 선택이 아니라 `caravanId`를 조회·명령 경계로 사용한다.
- `FrameworkRoot.Instance` 직접 접근은 facade 또는 Framework integration 계층으로 제한한다.
- 하위 서비스는 SaveData, catalog, 저장 함수 등 필요한 입력을 명시적으로 전달받는다.
- 저장 변경은 검증, stage, save, 성공 후 UI 갱신 순서를 지킨다. 저장 실패 시 관련 상태를 전부 rollback한다.
- 기존 Provider/Command 공개 계약, SaveData 스키마, Scene/Prefab 참조를 바꿔야 한다면 별도 범위로 검토한다.
- 자동 테스트는 로직과 연결 회귀를 확인하는 수단이다. 실제 UI 배치와 조작감은 수동 검증 항목으로 별도 관리한다.

## 1. 판매 UI를 구매 UI 베이스로 구성

### 확정된 방향

- 구매 UI의 공통 시각 요소와 조작 방식을 재사용한다.
  - 아이템 슬롯
  - Tooltip
  - 가격·수량 선택 Modal
  - 스크롤 영역과 선택 강조
- 판매 대상 Caravan은 Popup 안에서 다시 선택하지 않는다. 진입 전에 선택된 `caravanId`를 입력으로 받는다.
- Cargo 표시는 `caravanId`에 해당하는 실제 저장 Cargo를 기준으로 만든다.
- 같은 상품도 구매 단가가 다르면 `itemId + purchaseUnitPrice` 묶음을 유지한다.
- 판매 대기 목록의 동작은 다음과 같다.
  - Cargo 슬롯 클릭: 단가 묶음과 수량 선택 후 판매 대기에 추가
  - 판매 대기 항목 좌클릭: 수량 변경
  - 판매 대기 항목 우클릭: 해당 묶음 전체 제거
  - 목록 비우기: 판매 draft만 초기화하고 저장 Cargo는 변경하지 않음
- 실제 Cargo 차감, 시장 재고 증가, 통화 증가는 판매 확정 Command에서만 수행한다.
- 성공 시 Cargo·통화·시장 UI를 Provider로 다시 조회한다.
- 실패 시 Cargo, 시장 재고, 통화를 모두 원복하고 현재 화면을 유지하며 실패 사유를 NoticeUI에 표시한다.
- 금액 계산값은 `long`을 유지하고 View 표시 시에만 `CurrencyTextFormatter.Format(long)`을 적용한다.

### 구매/판매 정책

- 구매 준비 단계에서는 저장 Cargo와 상점 구매 예약을 별도 데이터로 유지한다.
- 같은 `itemId`의 저장분과 예약분은 UI에서 한 슬롯으로 합쳐 표시할 수 있지만, 취소는 예약분만 감소시킨다.
- 구매 예약은 출발/거래 확정 전까지 시장 재고와 통화를 실제로 변경하지 않는다.
- 판매 draft도 판매 확정 전까지 Cargo를 실제로 차감하지 않는다.
- 구매와 판매를 한 Transaction에서 동시에 허용할지는 아직 확정하지 않는다. 1차 구현은 화면별 단일 방향 Transaction을 권장한다.
- 성공 도착은 목적지 시장의 판매 단계를 거친 뒤 정산 표시를 요청한다.
- 실패 도착은 판매 단계가 없으므로 즉시 실패 정산 대상으로 처리한다.

### 남은 결정

- 구매 Popup과 판매 Popup을 하나의 모드형 Prefab으로 합칠지, 공통 하위 View만 재사용할지 결정이 필요하다.
- 판매 가능 수량의 상한을 저장 Cargo 묶음 단위로 둘지, 동일 item ID 전체 합계로 제공할지 UX 확정이 필요하다.
- 부분 판매 후 남은 구매 단가 묶음의 표시 순서 정책이 필요하다.

## 2. Caravan 선택 UI에서 selectedCaravanId 갱신

### 확정된 방향

- Caravan 관련 UI 버튼은 자신이 표시하는 `caravanId`를 보유해야 한다.
- 버튼 클릭 이벤트는 인덱스나 표시 이름이 아니라 `caravanId`를 함께 전달한다.
- Binding은 전달받은 ID를 Framework의 `SaveData.selectedCaravanId` 갱신 Command로 전달한다.
- 갱신 성공 후 해당 ID로 Overview, Setting, Cargo, 판매 UI ViewData를 다시 조회한다.
- 저장 실패 시 이전 `selectedCaravanId`를 유지하고 대상 UI를 열지 않는다.
- 버튼이 직접 SaveData를 수정하거나 다른 UI의 내부 필드를 변경하지 않는다.

### ID 역할 구분

- `selectedCaravanId`: Overview와 Framework 상호작용의 현재 선택 대상
- `departureCaravanId`: 이번 무역 준비 draft에서 출발시킬 대상
- 두 값은 개념적으로 분리한다.
- TradePrepare Caravan 선택에서 의도적으로 출발 대상을 고른 경우에만 기존 계약에 따라 두 ID를 동기화할 수 있다.
- Setting, Cargo, 판매 버튼은 클릭한 블록의 명시적 `caravanId`를 이벤트에 담아야 하며 전역 선택값을 추측하지 않는다.

### 권장 이벤트 경계

```text
Caravan Button Click(caravanId, targetUi)
    → Selection Binding
    → SelectCaravan Command(caravanId)
    → Save 성공
    → Provider 재조회(caravanId)
    → targetUi 표시
```

정확한 이벤트 타입과 메서드명은 기존 `CaravanOverview` 공개 계약을 확인한 뒤 확정한다. 핵심 계약은 이벤트 payload에 `caravanId`가 포함되는 것이다.

## 3. 무역 실패 시 정산창 표시

### 확인된 현재 원인

- 다중 Caravan 진행 처리 이후 실패 정산 화면 요청이 다음 조건으로 제한되어 있다.

```text
실패한 notification.CaravanId == SaveData.selectedCaravanId
```

- 이 조건은 2026-07-24 `b40a276c`의 다중 Caravan 복구 작업에서 들어왔다.
- 2026-07-31 `46e07743`에서 닫힌 UI의 실패 정산 재오픈을 보완했지만 위 ID 제한은 남아 있다.
- `FrameworkTradeScreenPresenter.HandleTradeSettlementReady`도 실패 결과에서는 즉시 반환하므로, 선택되지 않은 Caravan의 실패에는 화면 전환을 보장하는 대체 경로가 없다.

### 확정된 방향

- 실패 결과와 Pending Settlement는 실패한 `caravanId + fullTradeId` 기준으로 SaveData에 항상 유지한다.
- UI는 `SettlementUiBridge` 또는 동등한 Framework 조회 경계를 통해 Pending Settlement를 읽는다.
- 현재 선택/표시 중인 Caravan이 실패했다면 실패 정산창을 즉시 연다.
- 다른 Caravan이 백그라운드에서 실패했다면 현재 화면을 강제로 가로채지 않는다.
  - 해당 Caravan에 `정산 대기` 상태를 표시한다.
  - 사용자가 해당 Caravan을 선택하거나 정산 버튼을 누르면 그 ID의 정산창을 연다.
- 실패 정산은 판매 단계를 요구하지 않는다.
- Claim은 화면의 암묵적 선택값이 아니라 정산 ViewData가 가진 `caravanId + fullTradeId`로 실행한다.
- Claim 성공 후에만 Pending Settlement를 제거하고 Caravan 상태를 Prepare/Town 흐름으로 복귀시킨다.

### 구현 시 주의

- 단순히 `selectedCaravanId` 조건을 제거해 모든 실패가 전역 화면을 강제로 열게 만들지 않는다.
- `TradeSettlementReady`, `InGameScreenChanged`, Pending Settlement 저장 순서를 확인해 이벤트 타이밍에 의존하지 않도록 한다.
- 로드 복구와 온라인 실패가 같은 Pending Settlement 조회 경로를 사용해야 한다.
- 복수 Pending Settlement가 있을 때 선택 ID 없이 첫 항목을 임의로 열지 않는다.

## 4. 건물 생성 UI에 편집 버튼과 분류 목록 추가

### 요구사항으로 확정된 방향

- 건물 생성 진입부에 `편집` 버튼을 추가한다.
- 편집 화면에서 항목을 최소 두 분류로 나눈다.
  - 건물
  - 환경
- 분류는 드롭다운 또는 접기/펼치기 가능한 목록 방식으로 제공한다.
- 목록 선택은 즉시 건설하지 않고 기존 Detail Popup을 연다.
- 실제 건설/업그레이드는 Confirm 이후 production Command가 수행한다.
- 성공 후에만 Scene 반영과 `BuildingListPanel.Rebuild()`를 수행한다.
- 실패 또는 취소 시 Scene과 저장 목록을 변경하지 않는다.

### 기존 건물 계약에서 유지할 사항

- 카탈로그 식별자는 표시 이름이 아니라 `BuildData.buildId`를 사용한다.
- `BuildingAddPopup`에서 `VillageBuildingRegistry.AddOrUpgrade(int)`를 직접 호출하지 않는다.
- Detail → Confirm → Command → Save → Scene 적용 순서를 유지한다.
- 건설 비용은 HomeInventory를 기준으로 처리하며 Caravan Cargo를 변경하지 않는다.
- 재료, Gold, 건물 레벨은 하나의 저장 단위로 처리하고 실패 시 모두 rollback한다.

### 아직 미확정된 사항

- `환경` 항목의 데이터 원천이 기존 `BuildData`인지 별도 Environment catalog인지 결정되지 않았다.
- 편집이 신규 배치, 기존 건물 이동, 회전, 철거 중 어디까지 포함하는지 확정되지 않았다.
- 건물/환경 분류 필드를 ScriptableObject에 추가할지 외부 catalog metadata로 둘지 결정이 필요하다.
- 드롭다운 UI의 정확한 Prefab 구조와 Scene 저장 범위가 결정되지 않았다.
- 환경 배치의 비용·저장·rollback 계약은 별도로 정의해야 한다.

## 권장 작업 순서

1. Caravan 선택 버튼의 `caravanId` 이벤트와 선택 Command 경계를 먼저 확정한다.
2. 해당 ID 경계를 이용해 Cargo Sell UI의 Provider/Command를 연결한다.
3. 구매/판매 Transaction 정책과 NoticeUI 실패 코드를 정리한다.
4. 실패 Pending Settlement를 Caravan ID 기준으로 노출하고 정산 진입을 수정한다.
5. 건물 편집 범위와 환경 데이터 원천을 확정한 뒤 UI 구조를 구현한다.

## 완료 확인표

- [ ] 구매 UI 공통 요소를 재사용한 판매 UI가 실제 `caravanId` Cargo를 표시한다.
- [ ] 구매/판매 draft가 확정 전 SaveData를 변경하지 않는다.
- [ ] 구매 취소는 저장 Cargo를 보존하고 예약분만 제거한다.
- [ ] 판매 취소는 Cargo를 보존하고 판매 draft만 제거한다.
- [ ] Caravan 버튼 이벤트가 정확한 `caravanId`를 전달한다.
- [ ] 선택 저장 실패 시 관련 UI가 잘못된 Caravan을 열지 않는다.
- [ ] 선택된 Caravan 실패는 정산창을 즉시 연다.
- [ ] 백그라운드 Caravan 실패는 정산 대기 상태로 접근할 수 있다.
- [ ] 실패 정산 Claim이 `caravanId + fullTradeId`를 사용한다.
- [ ] 건물/환경 목록이 분리되어 표시된다.
- [ ] 목록 선택은 Detail Popup을 열고 즉시 건설하지 않는다.
- [ ] 건설 저장 실패 시 재료·Gold·레벨·Scene이 원복된다.
