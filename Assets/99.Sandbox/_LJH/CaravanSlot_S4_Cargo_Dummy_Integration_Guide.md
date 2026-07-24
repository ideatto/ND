# Caravan Slot S4 Cargo 더미 연동

## 현재 범위

Caravan Overview 슬롯의 `Cargo` 버튼은 기존 무역 준비 UI의 S4 Cargo 화면을 독립 편집 모드로 연다. 현재 저장 대상은 `TestCaravanSettingService`의 메모리 데이터이며 운영 SaveData에는 기록하지 않는다.

## 호출 흐름

1. `CaravanSlotView.CargoRequested(caravanId)`
2. `CaravanOverviewPresenter.CargoRequested`
3. `CaravanOverviewEditBinding.HandleCargoRequested`
4. `TradePrepareUIManager.OpenCaravanCargo(caravanId)`
5. `OnCaravanCargoDataRequested`를 받은 Binding이 `ICaravanLoadSettingViewDataProvider.GetLoadSetting` 호출
6. `TradePrepareUIManager.ShowCaravanCargo(viewData)`가 S4를 독립 편집 모드로 표시
7. 확인 시 `CaravanLoadSettingDraft`를 만들어 `ICaravanLoadSettingCommand.Execute` 호출
8. 성공하면 S4를 닫고 Overview를 새로고침하며, 실패하면 화면을 유지하고 Notice를 표시

## 계약 기준

- Provider가 반환한 `CaravanLoadSettingViewData`가 표시의 기준이다.
- Draft는 `caravanId`와 `itemId/quantity`만 전달한다. UI Asset과 가격 정보는 저장 계약에 포함하지 않는다.
- Command가 식별자 유효성, 편집 가능 상태, 중복 품목, 수량 및 적재 한도를 최종 검증한다.
- Command 성공 전에는 저장 상태를 변경하지 않는다.
- 독립 S4에서 발생한 적재 변경은 일반 무역 준비 Draft나 S5 용병 단계로 전달하지 않는다.
- `canEdit == false`인 데이터는 조회만 가능하며 확인할 수 없다.
- S4 Provider와 Command의 적재 중량·슬롯 한도는 마지막으로 확정된 S3 Wagon 구성에서 계산한다.
- S3 변경으로 현재 계획 Cargo가 새 한도를 초과하면 S3 Command를 거부하고 기존 구성을 유지한다.
- 독립 S4는 일반 무역 준비용 `CargoProvider`가 없어도 열린다. 해당 Provider가 연결된 경우에만 실제 `TradeItemData` 카탈로그와 시장 표시 정보를 보강한다.

## 현재 더미 규칙

- 편집 가능 상태: `Prepare`
- 읽기 전용 상태: `Traveling`
- 중형 Wagon 선택 시 최대 품목 슬롯 5, 적재량 100
- Walking 선택 시 최대 품목 슬롯과 적재량 0
- 저장 수명: Play 세션 내 메모리

## 현재 SO 카탈로그

InGame 씬의 `TestCaravanSettingService`에는 기존 `TradeItemData` SO인 Apple, Wheat, Cloth, Stover가 연결되어 있다. S4의 이름, 아이콘, 설명, 가격, 최대 수량과 단위 무게는 이 SO에서 읽는다.

- 카탈로그 재고는 품목당 임시 값 20을 사용한다.
- 독립 S4의 소지 골드와 구매 가능 수량은 Trade runtime Provider가 제공한 현재 `tradingCurrency`를 사용한다.
- 현재 단계에서는 예상 구매 금액만 계산하며, S4 더미 Command는 실제 `tradingCurrency`를 차감하지 않는다.
- Cargo Draft와 메모리 계획에는 SO 참조가 아니라 `itemId/quantity`만 저장한다.
- S4 Command는 카탈로그에 없는 `itemId`를 거부하고 SO의 실제 `Weight`로 적재 한도를 검증한다.
- 먹이는 `DraftAnimalsFood`인 Stover만 인정한다. 일반 `Food`는 무역 화물로 유지한다.
- Route가 없으면 요구 먹이는 0이며, Route 선택 후에는 TradePrepare의 계산된 요구 먹이를 S4에 표시한다.
- 운영 연동에서는 동일한 `ICaravanCargoCatalogProvider`를 마을 및 Caravan 상태 기반 구현으로 교체한다.

## 운영 Framework 인계 시 교체 지점

운영 연동 담당자는 `ICaravanLoadSettingViewDataProvider`와 `ICaravanLoadSettingCommand` 구현을 제공하고, `CaravanOverviewEditBinding`의 S4 Provider/Command 참조만 교체한다. UI와 Presenter가 SaveData를 직접 읽거나 쓰는 방식으로 변경하지 않는다.

운영 구현에서 확정해야 할 항목은 다음과 같다.

- `caravanId`로 실제 Caravan 및 Cargo 저장 데이터를 조회하는 경로
- 상태별 편집 권한
- 품목 ID 유효성 및 보유량/재고 규칙
- 무게, Wagon 슬롯, 적재 한도 계산의 단일 소유자
- 저장 성공의 원자성 및 실패 코드
- 성공 후 Overview 재조회 시 최신 적재 요약 제공

## 확인 항목

- Overview의 Cargo 버튼이 선택한 Caravan ID로 S4를 여는가
- 더미 품목을 추가하고 확인한 뒤 다시 열었을 때 계획이 복원되는가
- 뒤로가기/취소 시 저장되지 않는가
- 독립 S4 조작이 일반 무역 준비 Cargo Draft에 영향을 주지 않는가
- `Traveling` 데이터가 읽기 전용으로 표시되는가
