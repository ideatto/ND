# 1차 빌드 무역 UI 패널 데이터 연결 인계

## 기준과 작업 범위

- 제출일: 2026-07-16 23:59
- 구현 기준은 `Docs/Personal_Documents/CSU/0714_Idle_Trade_First_Build_Docs/0714_03_UI_Data_Milestone.md`이다.
- 화면 구성과 표시 항목은 `Section 9 (1).pdf`를 참고하되, 충돌하면 위 MD를 우선한다.
- 담당 범위는 패널 내부 데이터 연결과 누락 패널/프리팹 생성까지다.
- Scene 배치, 화면 라우팅, 기존 프리팹끼리의 연결은 SceneOwner/통합 담당 범위다.
- UI는 가격, 비용, 적재량, 전투력 등을 재계산하지 않고 Builder/ViewData 결과를 표시한다.

## Section 9 패널 대응 현황

| Section 9 | 구현 파일 | 데이터 연결 |
|---|---|---|
| 1~2 도시·경로 | `TownRoutePanel.cs`, `TownInfoPopup.cs` | `TownViewData`, `RouteViewData` |
| 3 이동수단 | `TransportSelectPanel.cs` | `WagonViewData` |
| 3-1 마차·견인 동물 | `WagonSelectPopup.cs`, `AnimalInventoryPanel.cs` | `WagonViewData`, `DraftAnimalViewData` |
| 4 상품 적재 | `ItemLoadPanel.cs`, `QuantityRow.cs` | `TradeItemViewData`, 현재 화폐, 인벤토리 슬롯 |
| 5 용병 고용 | `MercenarySelectionPanel.cs` | `MercenaryViewData`, 용병 비용·전투력 |
| 6 경로/준비 요약 | `TradePreparePanel.cs` | `TradePrepareViewData`, `startCondition` |
| 7 무역 진행 | `TradeTravelingPanelController.cs` | Framework SaveData의 진행 시각·Caravan 상태 |
| 8 정산 | `TradeSettlementPanelController.cs` | Framework `SettlementViewData` |
| 9 Claim/지급 | `PaymentPanelController.cs` | 정산 금액, 경로, Claim 가능 상태 |

## Preparation 패널 호출 계약

기존 임시 DTO용 API는 호환성을 위해 유지했다. 통합 측에서는 공식 데이터가 갱신될 때 다음 API를 호출한다.

- `TownRoutePanel.Populate(TradePrepareViewData)`
- `TransportSelectPanel.Populate(TradePrepareViewData)`
- `AnimalInventoryPanel.Populate(TradePrepareViewData)`
- `ItemLoadPanel.Populate(TradePrepareViewData)`
- `MercenarySelectionPanel.Bind(TradePrepareViewData)`
- `TradePreparePanel.Bind(TradePrepareViewData)`

입력은 `TradePrepareRuntimeContextProvider`로 전달한다.

- 목적지: `SelectDestination(townId)`
- 경로: `SelectRoute(routeId)`
- 마차: `SelectWagon(wagonId)`
- 견인 동물 수량: `SetAnimalQuantity(animalId, quantity)`
- 구매 상품 수량: `SetBuyItemQuantity(itemId, quantity)`
- 용병 선택/해제: `SelectMercenary(mercenaryId)`, `DeselectMercenary(mercenaryId)`
- 출발: `TryStartTrade(tradeId)`

`TradePrepareRuntimeContextProvider.ViewDataChanged`를 구독해 위 패널을 다시 바인딩해야 한다. 이 이벤트 연결과 패널 전환은 통합 담당이 처리한다.

## 표시 및 제한 처리

- 도시/경로: 잠금·선택 가능 여부, 비활성 사유, 거리, 예상 시간, 필요 식량, 필요 전투력, 위험도를 표시한다.
- 도시 기여도와 특산품은 현재 공식 `TownViewData`에 값이 없으므로 임의 생성하지 않는다. 기여도 `0/0`도 표시하지 않는다.
- 마차: 보유량, 슬롯, 적재량, 내구도, 필요 견인 동물 수, 선택 제한을 표시한다.
- 견인 동물: 보유량, 속도, 식량 소모, 적재량 증가치, 선택 마차 적합성과 제한을 반영한다.
- 상품: 구매·판매 가격, 단위 무게, 선택량, 화폐 기준 구매 가능 수량과 비활성 사유를 표시한다.
- 용병 비용과 전투력은 `TradePrepareViewDataBuilder` 결과를 그대로 사용한다.
- 준비 요약: 적재량, 슬롯, 식량, 전투력, 상품/식량/용병 비용, 총 준비 비용, 예상 매출·순이익, 이동 시간을 표시한다.
- 출발 버튼은 `startCondition.canStart`를 사용하고 `disabledReason`과 경고 메시지를 표시한다.

## 생성된 프리팹

- `Assets/_Project/08.Prefabs/UI/Trade/MercenarySelectionPanel.prefab`
- `Assets/_Project/08.Prefabs/UI/Trade/TradeTravelingPanel.prefab`
- `Assets/_Project/08.Prefabs/UI/Trade/TradePrepareRuntimeContext.prefab`

Traveling 프리팹은 2026-07-15에 Unity 메뉴 `ND > UI > Generate Trade Traveling Panel`로 실제 생성됐으며, 경로·남은 시간·식량/상태·진행 Slider 참조가 저장되어 있다.

## Traveling 연결

- `InGameScreenState.Traveling`에서 `TradeTravelingPanel.prefab`을 활성화한다.
- 컨트롤러는 Framework의 현재 UTC, 시작/종료 tick, 활성 경로, Caravan 식량 상태를 읽기만 한다.
- 무역 완료 및 Settlement 전환은 `TradeProgressCoordinator`/화면 라우터 담당이다.
- 무역 취소 및 Section 9의 7-1 취소 경고창은 1차 빌드 제외 범위라 구현하지 않았다.

## Settlement/Claim 연결

- `TradeSettlementPanelController`는 `ND.Framework.ISettlementView`를 구현한다.
- Framework `SettlementViewData`의 성공·실패, 수익, 비용, 순이익, 실패 원인, Claim 가능 상태를 표시한다.
- Claim 실제 처리, 중복 입력 방지, SaveData 갱신, Preparation 복귀는 Framework Adapter/Coordinator가 담당한다.
- UI는 전달받은 정산 금액을 재계산하지 않는다.

## 의도적으로 진행하지 않은 항목

- 기존 프리팹끼리의 참조 연결
- Scene 배치 및 화면 라우팅
- 이전 Caravan 구성 자동 불러오기 고급 UX
- 용병 전투/스탯 상세 기능
- 계절·재난·로드 이벤트 상세 UI
- 무역 취소 경고창
- 공식 ViewData에 없는 기여도·특산품 값의 임의 생성

## 통합 전 확인 사항

- `TradePrepareRuntimeContext.prefab`에 Sandbox가 아닌 확정 Town/Route/TradeItem/Wagon/DraftAnimal/Mercenary 에셋을 할당한다.
- 제출용 `ITradePrepareCommitSink` 구현을 `commitSinkBehaviour`에 연결한다. Temporary/InMemory 구현은 제출 빌드에서 사용하지 않는다.
- 각 패널의 ViewData 갱신과 사용자 입력 이벤트를 Provider에 연결한다.
- Traveling, SettlementPending, Settlement 상태에서 올바른 패널이 복구되는지 확인한다.
- Claim 후 Preparation으로 복귀하고 중복 Claim이 차단되는지 확인한다.
- 목표 해상도에서 긴 비활성 사유, 가격, 경로명, 정산 내역이 잘리지 않는지 확인한다.
- Console Error와 Missing Reference가 없는지 최종 확인한다.

## 알려진 빌드 검사 사항

- Unity에서 Runtime 코드의 `SaveData`, `TradeProgressState` 이름 충돌은 `ND.Framework` 타입을 명시해 해결했다.
- 외부 `dotnet build`는 기존 `Assets/_Project/11.CoreServices/Scripts/Debug/TimeScaleProgressTicker.cs`의 런타임 `NUnit` 참조 때문에 중단된다. 해당 파일은 이번 UI 작업 범위 밖이므로 수정하지 않았다.
