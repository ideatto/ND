# InGame UI, World Map, Trade, and SceneLoader Assembly Guide

## 1. 문서 목적

이 문서는 `Assets/_Project/07.Scenes/04_InGame/InGame.unity`와 같은 InGame 계열 씬에 다음 기능을 조립하는 기준을 정의한다.

- 메인 HUD와 최대 4개의 Caravan Overview 슬롯
- 캐러밴별 Setting 및 Cargo 편집 진입
- 메인 무역 버튼을 통한 다중 캐러밴 무역 준비 진입
- 선택한 캐러밴의 현재 도시를 기준으로 한 목적지 및 루트 출력
- World Map RenderTexture와 Overlay UI
- Additive 마을 SceneLoader
- 출발 실패 및 잠금 안내용 NoticeUI

이 문서의 무역 기준은 단일 전역 캐러밴이 아니라, 안정적인 `caravanId`를 가진 최대 4개의 독립 캐러밴이다.

> Revision reason (2026-07-23): 기존 문서의 깨진 문자와 단일 캐러밴 기준을 제거했다. `TradeBtn`의 구형 Presenter 직접 호출을 폐기하고, 캐러밴 선택 후 해당 캐러밴의 `currentTownId`를 기준으로 목적지와 루트를 조회하는 목표 구조로 갱신했다.
>
> Revision reason (2026-07-23): 현재 구현 완료 항목과 추가 연결이 필요한 항목을 분리했다. 문서에 적힌 목표 구조가 현재 프리팹에 모두 구현되어 있다고 오해하지 않도록 상태 표를 추가했다.
>
> Revision reason (2026-07-23): `MainUICanvas.prefab`의 `TradeBtn`에서 구형 Presenter 직접 호출을 제거하고 `TownTradePreparationButton`과 `TownTradePreparationEntryController`를 연결했다. 첫 Preparation은 저장 변경 없이 열고, 정산 후 Town에서는 Framework 진입 Command 성공 후 열도록 보정했다.

---

## 2. 핵심 책임과 권위 데이터

### 2.1 캐러밴 식별

- 각 캐러밴은 Framework가 발급하고 저장한 안정적인 `caravanId`로 식별한다.
- UI는 배열 순서, 슬롯 번호, 표시 이름으로 캐러밴을 다시 식별하지 않는다.
- Overview에서 보고 있는 캐러밴과 이번 출발 대상으로 선택한 캐러밴은 서로 다른 선택 상태다.
- 이번 무역의 출발 대상은 `TradePrepareDraft.departureCaravanId`에만 기록한다.

### 2.2 현재 도시

- 목적지와 루트 조회의 출발점은 선택한 캐러밴의 `currentTownId`다.
- `SaveData.player.currentTownId`는 다중 캐러밴의 위치를 대신할 수 없다.
- UI는 `currentTownId`를 추측하거나 이전 Draft에서 재사용하지 않는다.
- 캐러밴 선택이 바뀌면 Provider가 해당 캐러밴의 최신 `currentTownId`를 다시 제공해야 한다.

### 2.3 목적지와 루트

- 선택 가능한 루트는 선택한 캐러밴의 `currentTownId`에서 출발하는 루트다.
- 방향성 루트는 `route.fromTownId == selectedCaravan.currentTownId`인 경우만 후보로 삼는다.
- 양방향 이동이 필요한 경우 UI가 반대 방향을 임의로 허용하지 않는다. 콘텐츠 또는 Framework 조회 API가 양방향 후보를 명시해야 한다.
- 목적지 목록은 유효한 루트의 도착 도시 중, 도시 및 루트 해금 조건을 모두 만족하는 항목으로 만든다.
- 루트가 없는 도시는 선택 가능한 목적지로 표시하지 않는다. 정보 열람용으로 표시할 경우에도 `canSelect = false`와 차단 사유가 필요하다.
- 목적지 선택 가능 여부는 `Town`과 `Route` 중 하나의 해금 상태만으로 결정하지 않는다. 아래 조건을 모두 만족해야 한다.

```text
route.fromTownId == selectedCaravan.currentTownId
AND route.toTownId == destinationTownId
AND destinationTown.isUnlocked
AND route.isUnlocked
```

- 잠긴 목적지 아래에 해금된 Route가 잘못 연결되어 있어도 UI와 출발 Command는 선택과 출발을 거부해야 한다.
- Town 버튼은 정보 열람용 아코디언일 수 있지만, 잠긴 Town 또는 유효한 도착 Route가 없는 Town을 선택 성공으로 처리하거나 다음 단계로 이동해서는 안 된다.
- 현재 Town에서 출발 가능한 운영 Route 후보의 권위 데이터는 해당 `TownData.availableRoutes`다. Builder와 출발 Command는 전역 Route 배열을 합쳐 후보를 보완하지 않는다.
- `TownData.availableRoutes`에는 `route.fromTownId == town.townId`인 방향성 Route만 연결한다. 해당 Town으로 들어오는 Route나 Town과 관계없는 Route를 연결하지 않는다.
- 전역 Route Catalog는 전체 Route의 등록 여부, Route ID 조회, 중복 ID 및 참조 무결성 검증에 사용한다. Town별 목적지 후보를 생성하는 데이터로 `TownData.availableRoutes`와 병합하지 않는다.
- 전역 Catalog에 등록되지 않은 Route가 `TownData.availableRoutes`에 있거나 같은 Route ID가 중복 연결되어 있으면 Asset 검증 오류로 보고한다.
- 플레이어가 실제 도달 가능하도록 개방되는 Town의 Progression 정의에는 진입 및 후속 이동에 필요한 Town/Route ID를 명시한다. Command가 임의의 Route를 추론해 자동 해금하지 않는다.
- Town과 Route 해금 상태는 현재 SaveData에서 독립적으로 관리한다. Route 해금이 FromTown 또는 ToTown 해금을 자동으로 발생시키지는 않는다.
- 단, 실제 이동 가능 여부는 Route와 양 끝 Town의 현재 해금 상태를 함께 검사한다.

```text
canTravel(routeId)
-> effectiveRouteUnlocked(routeId)
AND effectiveTownUnlocked(route.fromTownId)
AND effectiveTownUnlocked(route.toTownId)
```

- Town과 Route의 해금 주체는 별도의 Town/Route Runtime 객체가 아니라 Quest/Progression 보상 처리다. 보상 처리는 `TownData`나 `RouteData` SO를 변경하지 않고, Progression 정의에 명시된 ID를 `SaveData.world.unlockedTownIds`와 `SaveData.world.unlockedRouteIds`에 중복 없이 추가한다.
- 하나의 Quest/Progression 보상으로 여러 Town/Route를 해금할 때는 모든 ID 변경과 저장을 하나의 트랜잭션으로 처리한다. 저장에 실패하거나 일부 조건을 만족하지 못하면 해금 목록 전체를 적용 전 상태로 롤백하며, 저장 성공 후 Provider와 ViewData를 갱신한다.
- Quest/Progression 보상 저장 처리의 실제 구현은 현재 Town/Route Asset 방향 정리 및 TradePrepare Route 조회 마이그레이션 범위에 포함하지 않는다. 본 문서에서는 향후 Quest/Progression 시스템 연결 시 지켜야 할 저장 계약만 정의하며, 담당 보상 Command와 저장 트랜잭션이 확정된 뒤 별도 작업으로 구현한다.
- 반대 방향은 자동으로 해금하지 않는다. 양방향 이동을 열어야 한다면 반대 방향 Route ID도 같은 해금 계획에 명시한다.

### 2.4 상태 변경

- ViewData는 표시용 스냅샷이며 SaveData를 직접 변경하지 않는다.
- UI 입력은 Draft 또는 Command로 전달한다.
- 출발 확정과 저장 성공 전에는 캐러밴의 실제 Journey 상태를 `Traveling`으로 바꾸지 않는다.
- Command 성공 후 Provider를 다시 조회해 Overview와 TradePrepareUI를 갱신한다.

---

## 3. 사용하는 프리팹

| 역할 | 프리팹 경로 |
|---|---|
| 메인 HUD, Caravan Overview, World Map 패널, 무역 기능 | `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab` |
| 월드맵 카메라와 월드 오브젝트 | `Assets/_Project/08.Prefabs/UI/Maps/WorldMapRenderRoot.prefab` |
| Additive 마을 로더 | `Assets/_Project/08.Prefabs/UI/Maps/SceneLoader.prefab` |
| 무역 UI와 Runtime 묶음 | `Assets/_Project/08.Prefabs/UI/Trade/TradeFeature.prefab` |

`TradeFeature.prefab`은 `MainUICanvas.prefab` 안에 중첩되어야 한다. 일반 씬 조립에서는 `TradePrepareUI`, `FrameworkTradeScreenPresenter`, `TradePrepareRuntimeContextProvider`를 따로 중복 배치하지 않는다.

---

## 4. 목표 Hierarchy

```text
InGame Scene
├─ Main Camera
├─ Directional Light
├─ EventSystem
├─ InGameSceneController
├─ MainUICanvas                              <- MainUICanvas.prefab
│  ├─ InfoPanel
│  │  ├─ CurrencyBar
│  │  │  └─ TradeBtn
│  │  └─ CaravanPanel
│  │     └─ CaravanScrollView
│  │        └─ Viewport
│  │           └─ Content
│  │              ├─ CaravanSlot 0
│  │              ├─ CaravanSlot 1
│  │              ├─ CaravanSlot 2
│  │              └─ CaravanSlot 3
│  ├─ WorldMapPanel
│  │  └─ RawImage
│  │     └─ WorldUiCanvas
│  │        ├─ ProgressPercentLabel
│  │        └─ RiskLabel
│  ├─ TradeFeature                           <- nested TradeFeature.prefab
│  │  ├─ TradePrepareUI                      <- 화면 루트, 최초 비활성
│  │  │  └─ UIManager
│  │  │     └─ TradePrepareUiRuntimeBinding
│  │  └─ Runtime                             <- 항상 활성
│  │     ├─ FrameworkTradeScreenPresenter
│  │     └─ TradePrepareRuntimeContextProvider
│  └─ NoticeUI                               <- 최초 비활성
├─ WorldMapRenderRoot                        <- WorldMapRenderRoot.prefab
│  ├─ WorldMapCamera
│  └─ WorldMapRoot
│     └─ WorldMapPresenter
└─ SceneLoader                               <- SceneLoader.prefab
```

`MainUICanvas`, `WorldMapRenderRoot`, `SceneLoader`는 씬 최상위 오브젝트로 둔다. `WorldMapRenderRoot`를 Canvas 아래로 옮기지 않는다.

---

## 5. 다중 캐러밴 UI 흐름

### 5.1 Caravan Overview

Overview는 최대 4개의 고정 슬롯을 표시한다.

| 슬롯 상태 | 표시 및 입력 규칙 |
|---|---|
| `Locked` | Empty 기본 외형 위에 잠금 Overlay를 표시하고 Setting, Cargo, Journey 입력을 숨긴다. 잠금 Overlay 선택 시 `unlockHintText`를 NoticeUI로 표시한다. |
| `Empty` | 캐러밴 생성 버튼만 표시한다. Setting, Cargo, Journey 입력은 숨긴다. |
| `Occupied` | Provider가 준 `caravanId`, Journey 상태, Setting 및 Cargo 요약을 표시한다. |
| `Unknown` | 정상 슬롯처럼 보이게 대체하지 말고 Provider 또는 조립 오류로 취급한다. |

Occupied 슬롯의 Setting 및 Cargo 버튼은 반드시 해당 슬롯의 `caravanId`를 전달한다.

```text
Setting Button
-> CaravanSlotView.SettingRequested(caravanId)
-> CaravanOverviewPresenter.SettingRequested(caravanId)
-> CaravanOverviewEditBinding
-> 선택 캐러밴의 Setting 조회 및 편집 화면

Cargo Button
-> CaravanSlotView.CargoRequested(caravanId)
-> CaravanOverviewPresenter.CargoRequested(caravanId)
-> CaravanOverviewEditBinding
-> 선택 캐러밴의 currentTownId 시장과 Cargo 조회
```

Overview에서 클릭하거나 편집한 캐러밴을 무역 출발 대상으로 자동 선택하지 않는다.

#### Journey 및 행동 아이콘 연결

`CaravanSlotView`는 아이콘 Asset이 준비되기 전에도 기존 텍스트가 유지되도록 선택적 Sprite 슬롯을 제공한다.

| Inspector 구역 | 연결 필드 |
|---|---|
| Journey State Icons | `Journey State Icon Image`, `Prepare State Icon`, `Traveling State Icon`, `Settling State Icon`, `Completed State Icon`, `Journey State Icon Animator` |
| Action Icons | `Setting Button Icon Image`, `Setting Button Icon`, `Cargo Button Icon Image`, `Cargo Load Button Icon`, `Cargo Sell Button Icon` |
| Text fallback | `Setting Button Text`, `Cargo Button Text`, 기존 `Journey State Text` |

- 대상 `Image`와 상태 Sprite가 모두 연결된 경우에만 아이콘을 표시하고 대응 텍스트를 숨긴다.
- 참조가 하나라도 비어 있으면 기존 텍스트 표시를 유지한다.
- 상태 아이콘 Animator Controller는 bool 파라미터 `IsTraveling`과 Idle/TravelingSpin 상태를 제공한다.
- `CaravanSlotView`는 상태가 바뀔 때 `IsTraveling`만 변경하고 `Update()`에서 직접 회전시키지 않는다.
- `TravelingSpin` AnimationClip은 회전 Loop를 담당하고 Idle 복귀 시 회전값을 0으로 되돌린다.
- `Prepare`에서만 Setting과 Cargo 적재 버튼을 활성화한다.
- `Traveling`, `Settling`, `Completed`에서는 현재 단계의 Setting/Cargo 편집을 비활성화한다.
- `Cargo Sell Button Icon`은 도착 판매 전용 상태가 Framework에 추가될 때 연결할 예약 슬롯이다. 현재 `Settling`에 판매 동작을 추론해서 연결하지 않는다.
- 아이콘 파일은 별도 Asset으로 유지하고 `CaravanSlotView`가 외부 상태를 새로 저장하거나 전환하지 않게 한다.

### 5.2 메인 무역 버튼

`TradeBtn`은 캐러밴 ID 없이 TradePrepareUI 진입만 요청한다. 출발 캐러밴은 TradePrepareUI의 첫 단계에서 별도로 선택한다.

현재 단일 사이클 호환 경로는 다음과 같다.

```text
TradeBtn
-> TownTradePreparationButton
-> TownTradePreparationEntryController.TryBeginTradePreparation()
-> FrameworkRoot.TryBeginTradePreparationFromTown()
-> TradePreparationEntryCommand.TryExecute()
-> Framework 상태 Preparation 저장 성공
-> FrameworkTradeScreenPresenter.OpenTradeScreen()
-> 캐러밴 프리셋 선택 화면
```

`TradeBtn.onClick`에서 `FrameworkTradeScreenPresenter.OpenTradeScreen()`을 직접 호출하면 안 된다. 정산 후 상태는 Town이므로 Presenter가 UI를 열자마자 다시 닫는다.

단, `TownTradePreparationButton`과 `TradePreparationEntryCommand`는 전역 화면 상태가 Town인 기존 단일 사이클을 다시 여는 호환 경로다. 하나의 캐러밴이 Traveling이어도 다른 Prepare 캐러밴을 추가 출발시킬 수 있는 최종 다중 캐러밴 구조에서는 다음 흐름이 필요하다.

```text
TradeBtn
-> 캐러밴 선택 UI 열기                       <- 아직 특정 캐러밴 SaveData를 변경하지 않음
-> SelectDepartureCaravan(caravanId)
-> Framework가 해당 캐러밴의 출발 가능 여부와 currentTownId 검증
-> 선택된 caravanId 전용 Preparation Draft 시작
-> 목적지 및 루트 ViewData 조회
```

전역 `tradeProgress`를 먼저 Preparation으로 초기화하거나 이동 중인 다른 캐러밴의 진행 상태를 변경해서는 안 된다. 최종 Framework 진입 Command의 이름과 시그니처가 확정되기 전까지 현재 Town 진입 경로를 다중 캐러밴 완료 구조로 간주하지 않는다.

### 5.3 TradePrepareUI의 목표 순서

```text
1. 출발 캐러밴 선택
2. 선택 캐러밴의 currentTownId 확정
3. 이동 가능한 목적지 및 루트 조회
4. 목적지 및 루트 선택
5. 용병 고용
6. 예상 시간, 위험도, 비용 및 출발 조건 확인
7. 출발 Command 실행
8. 저장 성공 후 해당 캐러밴만 Traveling으로 전환
```

기존 S3 Setting과 S4 Cargo는 Overview의 캐러밴별 편집 진입점으로 이관하는 방향이다. TradePrepareUI는 선택한 캐러밴의 확정된 Setting과 Cargo를 출발 요약 및 검증 입력으로 받는다.

### 5.4 캐러밴 선택 변경 시 초기화 규칙

출발 캐러밴이 바뀌면 이전 캐러밴에 종속된 다음 Draft 값을 제거한다.

- 목적지 ID
- 루트 ID
- 용병 선택
- 이전 캐러밴에서 임시로 만든 출발 전용 계산값

그리고 새 캐러밴에서 다음 값을 다시 조회한다.

- `currentTownId`
- 확정된 Wagon 및 Draft Animal 구성
- 실제 Cargo
- 최대 중량과 인벤토리 슬롯
- 현재 Journey 상태와 출발 가능 여부
- 출발지 기준 목적지 및 루트

다른 캐러밴의 Cargo, Setting, 위치 또는 Journey 상태를 복사하지 않는다.

---

## 6. MainUICanvas 조립

### 6.1 기본 배치

1. `MainUICanvas.prefab`을 씬 최상위에 배치한다.
2. RectTransform이 전체 화면 Stretch인지 확인한다.
3. 씬에 EventSystem이 정확히 하나만 있는지 확인한다.
4. `MainUICanvas` 아래에 `TradeFeature`가 정확히 하나 있는지 확인한다.
5. `CaravanPanel/CaravanScrollView`에 `CaravanOverviewPresenter`와 슬롯 4개의 참조가 연결되어 있는지 확인한다.

### 6.2 Overview 연결

| Component | Field 또는 이벤트 | 연결 대상 |
|---|---|---|
| `CaravanOverviewPresenter` | Provider Behaviour | 운영용 `ICaravanOverviewViewDataProvider` 구현체 |
| `CaravanOverviewPresenter` | Slot Views | 고정 순서의 `CaravanSlotView` 4개 |
| `CaravanOverviewEditBinding` | Overview Presenter | 같은 Canvas의 `CaravanOverviewPresenter` |
| `CaravanOverviewEditBinding` | Trade Prepare UI | `TradeFeature/TradePrepareUI/UIManager` |
| `CaravanOverviewEditBinding` | Notice UI | `MainUICanvas/NoticeUI` |

`TestCaravanOverviewViewDataProvider`, `TestCaravanSettingService` 등 `Test` 또는 `Temporary` 구현체는 조립 확인용이다. 운영 씬의 최종 데이터 공급자로 사용하지 않는다.

### 6.3 TradeBtn 연결

1. `TradeBtn > Button > On Click()`에 남아 있는 Presenter 직접 호출을 제거한다.
2. `TradeBtn`에 `TownTradePreparationButton`을 추가한다.
3. `TownTradePreparationButton.entryController`에 같은 오브젝트의 `TownTradePreparationEntryController`를 연결한다.
4. `TownTradePreparationButton.tradeScreenPresenter`에 `TradeFeature/Runtime/FrameworkTradeScreenPresenter`를 연결한다.
5. 한 번의 클릭이 한 번의 진입 요청만 발생시키는지 확인한다.

### 6.4 TradeFeature 내부 연결

| Component 위치 | Field | 연결 대상 |
|---|---|---|
| `FrameworkTradeScreenPresenter` | `viewBehaviour` | `TradePrepareUI/UIManager`의 화면 View 구현체 |
| `TradePrepareUiRuntimeBinding` | `runtimeContext` | `TradeFeature/Runtime/TradePrepareRuntimeContextProvider` |
| `TradePrepareUiRuntimeBinding` | `departureWarning` | `MainUICanvas/NoticeUI` |
| `TradePrepareRuntimeContextProvider` | 콘텐츠 배열 | 운영 Town, Item, Wagon, Animal, Mercenary 데이터와 현재 호환용 Route 배열 |
| `TradePrepareRuntimeContextProvider` | Caravan Option Provider | 운영용 `ITradePrepareCaravanOptionProvider` 구현체 |

`Runtime`은 `TradePrepareUI`와 형제여야 한다. UI 화면을 비활성화해도 RuntimeContext와 Presenter는 이벤트를 계속 수신해야 한다.

`TradePrepareRuntimeContextProvider`의 Route 배열은 현재 Builder 및 출발 Route 해석기와의 호환을 위한 임시 연결이다. Town별 Route 후보의 권위 데이터는 `TownData.availableRoutes`이며, Builder와 출발 Route 해석기가 이를 직접 사용하도록 전환한 뒤 RuntimeContext의 Route 배열은 제거한다.

---

## 7. 선택 캐러밴 기준 목적지 및 루트 조립 규칙

### 7.1 Provider가 제공해야 할 값

TradePrepare의 캐러밴 선택 항목에는 최소 다음 정보가 필요하다.

- `caravanId`
- 표시 이름
- Journey 상태
- 출발 선택 가능 여부와 차단 사유
- 해당 캐러밴의 권위 있는 `currentTownId`

`currentTownId`는 캐러밴 선택 전에는 비어 있을 수 있다. 선택이 확정된 뒤에는 목적지 화면을 열기 전에 반드시 유효해야 한다.

### 7.2 ViewData 생성 규칙

```text
선택 caravanId
-> Provider가 캐러밴 조회
-> selectedCaravan.currentTownId
-> currentTown의 TownData.availableRoutes 조회
-> route.fromTownId == currentTownId인 항목만 유효 후보로 검증
-> Route 도착 도시 집합 생성
-> Town/Route 잠금 상태 반영
-> TradePrepareViewData.currentTownId, towns, routes 생성
```

`TradePrepareViewData.currentTownId`와 각 `RouteViewData.fromTownId`가 일치하지 않는 루트는 표시하거나 선택할 수 없다.

ViewData Builder와 출발 Route 해석기는 `context.routes`와 `TownData.availableRoutes`를 합치지 않는다. 선택 Caravan의 현재 Town에 연결된 `TownData.availableRoutes`만 목적지 후보로 사용한다.

전역 Catalog는 후보 생성용이 아니라 Route 등록 및 ID 조회용이다. Editor 검증기 또는 콘텐츠 로드 검증 단계에서는 다음 관계를 검사한다.

```text
candidate in town.availableRoutes
-> candidate is registered in GlobalRouteCatalog
AND candidate.fromTownId == town.townId
AND candidate.routeId is unique within town.availableRoutes
```

다음은 모두 검증 오류다.

- `availableRoutes`에 해당 Town으로 들어오는 Route가 포함됨
- `availableRoutes`에 해당 Town과 관계없는 Route가 포함됨
- `availableRoutes`에는 있지만 전역 Catalog에 등록되지 않은 Route가 있음
- 같은 Route ID가 중복 연결됨

전역 Catalog에 어떤 Town에서 출발하는 Route가 등록되어 있더라도 `TownData.availableRoutes`에 선언되지 않았다면 해당 Town의 목적지 후보로 자동 추가하지 않는다. 의도된 이동 경로라면 Town Asset에 명시적으로 연결한다.

목적지를 선택하면 해당 목적지로 향하는 루트만 표시한다. 목적지를 바꾸면 이전 루트 선택을 제거한다.

`TownViewData.canSelect`와 `RouteViewData.canSelect`은 동일한 Town/Route 해금 판정 규칙을 사용한다. 두 값이 항상 같은 결과여야 한다는 의미는 아니다. 한 Town으로 향하는 Route가 여러 개라면 각 Route의 선택 가능 여부는 서로 다를 수 있으며, Town은 해당 Town으로 향하는 선택 가능한 Route가 하나 이상 있을 때 선택 가능하다.

```text
TownViewData.canSelect
    = destinationTownUnlocked
      && hasAtLeastOneSelectableRouteFromCurrentTown

RouteViewData.canSelect
    = routeUnlocked
      && destinationTownUnlocked
      && route.fromTownId == currentTownId
```

표시 계층에서 `[잠김]` 문구만 추가하고 버튼 입력을 그대로 허용해서는 안 된다. 단, 버튼 비활성화는 사용자 경험을 위한 1차 방어이며 출발 가능 여부의 권위 있는 판정은 아니다.

### 7.3 선택 및 출발 재검증

UI 클릭 Binding과 출발 Command는 ViewData가 오래되었거나 잘못된 Asset 참조가 있어도 다음 항목을 다시 검증한다.

- 전달된 `destinationTownId`가 선택 Route의 `toTownId`와 같은가
- 선택 Route의 `fromTownId`가 출발 Caravan의 최신 `currentTownId`와 같은가
- 선택 Route가 최신 출발 Town의 `TownData.availableRoutes`에 실제 등록되어 있는가
- 목적지 Town과 Route가 현재 SaveData 기준으로 모두 해금됐는가
- Route와 Town이 Shared catalog에 실제 존재하는가
- 출발 Caravan이 현재 출발 가능한 Journey 상태인가

UI Binding 검증 실패 시 Draft를 변경하거나 용병 화면으로 이동하지 않는다. 출발 Command 검증 실패 시 Commit을 stage하거나 Caravan 상태를 `Traveling`으로 변경하지 않는다.

### 7.4 실패 처리

다음 경우에는 목적지 단계로 넘어가지 않고 캐러밴 선택 화면을 유지한다.

- `caravanId`가 비어 있거나 존재하지 않음
- 캐러밴의 `currentTownId`가 비어 있음
- 현재 Journey 상태가 출발 가능한 상태가 아님
- 캐러밴 위치에 대응하는 Town 콘텐츠가 없음
- 현재 도시에서 출발 가능한 루트가 없음
- Provider 조회 또는 저장이 실패함

UI는 실패 코드를 임의로 성공 상태로 바꾸지 않고 NoticeUI 또는 캐러밴 선택 항목의 `disabledReason`으로 표시한다.

---

## 8. 현재 구현 상태와 남은 연결

2026-07-27 기준으로 다음 항목은 준비되어 있다.

- `CaravanBlockViewData`와 고정 4슬롯 Overview 표시 계약
- `SettingRequested(caravanId)`와 `CargoRequested(caravanId)` 전달 경로
- `TradePrepareDraft.departureCaravanId`
- `ITradePrepareCaravanOptionProvider` 계약과 테스트 Provider
- `TradePrepareCaravanOptionViewData.currentTownId` 위치 스냅샷
- S_CaravanSlot 선택 시 `departureCaravanId`와 `currentTownId` 동시 Draft 반영
- 명시적 `departureCaravanId`를 유지하되, 현재 Framework 화면·정산 호환성을 위해 성공한 선택을 `SaveData.selectedCaravanId`에도 동기화
- RuntimeContext와 Builder의 플레이어 위치 fallback 제거
- S3 `CaravanSettingViewData`와 Route 위치 전달의 분리
- Builder의 `route.fromTownId == currentTownId` 필터
- `TownTradePreparationButton`과 `TownTradePreparationEntryController`
- `MainUICanvas.prefab`의 TradeBtn 구형 OnClick 제거 및 EntryController 연결
- 첫 `Preparation` 진입과 정산 후 `Town` 재진입 분기

다음 항목은 아직 운영 연결이 완료되지 않았다.

| 미완료 항목 | 현재 상태 | 필요한 방향 |
|---|---|---|
| `TradeBtn` 단일 사이클 재진입 | 구형 Presenter 직접 호출 제거 및 UI EntryController 연결 완료 | Play Mode에서 첫 진입과 정산 후 재진입 최종 확인 |
| 병렬 캐러밴 Preparation 진입 | 현재 Entry Command가 전역 Town 상태를 요구함 | 캐러밴 선택 UI를 먼저 열고, 선택된 `caravanId`만 대상으로 하는 Framework 진입 계약 필요 |
| 운영 Caravan Option Provider | SaveData 기반 임시 Adapter가 연결됐지만 클래스가 `TestCaravanSettingService`에 남아 있음 | Framework 판정을 제공하는 운영 전용 Provider로 교체 |
| 목적지 목록 | 모든 Town을 만든 뒤 Town 잠금과 Route 잠금을 독립적으로 표시함 | 현재 도시에서 출발하며 목적지 Town과 Route가 모두 해금된 항목으로 제한 |
| 목적지 클릭 방어 | Town 표시는 잠김이어도 아코디언 입력이 가능하고 Route 선택이 별도 판정을 사용함 | Binding에서 Town/Route/방향/ID를 함께 검증하고 실패 시 다음 단계 이동 금지 |
| 출발 최종 방어 | Prepare ViewData의 `isRouteUnlocked` 중심으로 판정함 | 최신 Caravan 위치와 Town/Route 해금을 출발 Command에서 다시 검증 |
| Route 콘텐츠 방향 | Town Asset의 방향성 Route 연결 수정은 완료됐지만 Builder가 전역 Route 배열과 `TownData.availableRoutes`를 합치는 기존 로직은 남아 있음 | `TownData.availableRoutes`만 후보로 사용하고 각 항목의 `fromTownId`가 Town ID와 같은지 검증 |
| Quest/Progression 해금 저장 | SaveData에 Town/Route 해금 목록은 존재하지만 담당 보상 Command와 저장 트랜잭션 연결은 현재 작업 범위 밖임 | Quest/Progression 시스템 계약 확정 후 중복 방지, 일괄 저장, 실패 롤백 및 성공 후 UI 갱신을 별도 구현 |
| 도착 후 복귀 가능성 | 잠긴 Town으로 열린 Route가 향하거나 도착 Town의 후속 Route가 잠길 수 있음 | Progression 정의에 필요한 Town/Route ID를 명시하고 도달 가능한 상태의 막다른 도시 검사 |
| 운영 프리팹 연결 | `Test`/`Temporary` 연결이 남을 수 있음 | 운영 Provider 및 Command로 교체 후 검사 |

이 표의 미완료 항목을 해결하기 전에는 “다중 캐러밴 위치 기반 루트 출력 완료”로 판정하지 않는다.

---

## 9. NoticeUI 조립

```text
MainUICanvas
└─ NoticeUI
   ├─ CanvasGroup
   ├─ NoticeUI (Script)
   ├─ Panel
   └─ Text (TMP)
```

| Component | Field | 값 또는 연결 |
|---|---|---|
| `CanvasGroup` | Alpha | `1` |
| `CanvasGroup` | Interactable | `false` |
| `CanvasGroup` | Blocks Raycasts | `false` |
| `NoticeUI` | Canvas Group | 같은 오브젝트의 CanvasGroup |
| `NoticeUI` | Message Text | 자식 Text (TMP) |
| `NoticeUI` | Fade Duration | `3` |

NoticeUI는 `TradePrepareUI` 아래가 아니라 `MainUICanvas` 직속 자식으로 유지한다. `Show()` 시 최상단 형제로 이동하되 다른 UI 입력을 막지 않아야 한다.

---

## 10. World Map 조립

### 10.1 WorldMapRenderRoot 배치

1. `WorldMapRenderRoot.prefab`을 씬 최상위에 배치한다.
2. `WorldMapCamera`, `WorldMapRoot`, `WorldMapPresenter`가 각각 하나인지 확인한다.
3. `WorldMapCamera`를 `MainUICanvas` 자식으로 옮기지 않는다.

### 10.2 씬 전용 참조

| Component 위치 | Field | 연결 대상 |
|---|---|---|
| `MainUICanvas/WorldMapPanel`의 `SlidePanel` | `Rend Cam` | `WorldMapRenderRoot/WorldMapCamera` |
| `WorldUiCanvas`의 `WorldMapOverlayLabelBinding` | `Presenter` | `WorldMapRenderRoot/WorldMapRoot/WorldMapPresenter` |

### 10.3 RenderTexture

다음 두 필드는 같은 `WorldMapRenderTexture`를 사용해야 한다.

| Component | Field |
|---|---|
| `WorldMapCamera` | Target Texture |
| `MainUICanvas/WorldMapPanel/RawImage` | Texture |

맵 패널을 닫으면 `WorldMapCamera`도 비활성화해 불필요한 RenderTexture 갱신을 멈춘다.

---

## 11. SceneLoader 조립

1. `SceneLoader.prefab`을 씬 최상위에 하나만 배치한다.
2. `AdditiveSceneLoader.sceneName`이 `Village_Home`인지 확인한다.
3. 다음 씬이 Build Settings에 활성 등록되어 있는지 확인한다.

```text
Assets/_Project/07.Scenes/04_InGame/Village_Home.unity
```

SceneLoader는 MainUICanvas, WorldMap 또는 FrameworkRoot를 새로 생성하지 않는다. 마을 콘텐츠 씬만 Additive로 로드한다.

---

## 12. Play Mode 검증 순서

### 12.1 기본 조립

1. Missing Prefab, Missing Script, NullReference 오류가 없는지 확인한다.
2. `InGame`과 `Village_Home`이 함께 로드되는지 확인한다.
3. EventSystem, MainUICanvas, TradeFeature, RuntimeContext, Presenter가 각각 하나인지 확인한다.
4. TradePrepareUI는 최초 비활성이며 `TradeFeature/Runtime`은 활성인지 확인한다.

### 12.2 Overview

1. 슬롯이 정확히 4개 표시되는지 확인한다.
2. Locked, Empty, Occupied 외형과 버튼 상태가 구분되는지 확인한다.
3. 서로 다른 Occupied 슬롯의 Setting 또는 Cargo 버튼이 서로 다른 `caravanId`를 전달하는지 확인한다.
4. 한 캐러밴의 편집이 다른 캐러밴의 Setting 또는 Cargo를 바꾸지 않는지 확인한다.

### 12.3 위치 기반 무역 준비

다음과 같은 테스트 데이터를 준비한다.

```text
Caravan A: currentTownId = BaseCamp
Caravan B: currentTownId = RiverTown
Caravan C: Traveling
Caravan D: Empty 또는 Locked
```

1. `TradeBtn`을 눌러 캐러밴 선택 화면이 열리는지 확인한다.
2. Caravan A를 선택하면 BaseCamp 출발 목적지와 루트만 표시되는지 확인한다.
3. Caravan B로 바꾸면 기존 목적지와 루트가 지워지고 RiverTown 출발 목록으로 교체되는지 확인한다.
4. Caravan C는 선택 불가이며 `disabledReason`이 표시되는지 확인한다.
5. Empty 또는 Locked 슬롯은 출발 후보에 들어오지 않는지 확인한다.
6. 선택한 루트의 `fromTownId`가 선택 캐러밴의 `currentTownId`와 같은지 확인한다.
7. 출발 성공 후 선택한 캐러밴만 Traveling으로 바뀌는지 확인한다.
8. 다른 캐러밴의 위치, Setting, Cargo, Journey 상태가 유지되는지 확인한다.
9. 한 캐러밴이 Traveling이어도 다른 Prepare 캐러밴을 선택하고 별도 출발 준비를 시작할 수 있는지 확인한다.

### 12.4 한 사이클 후 재진입

1. 무역 출발부터 정산 Claim까지 한 사이클을 완료한다.
2. Overview가 최신 캐러밴 상태를 다시 조회하는지 확인한다.
3. `TradeBtn`을 한 번 눌러 새 Preparation이 열리는지 확인한다.
4. Presenter 직접 호출이 아니라 Town 진입 Command 로그가 한 번만 발생하는지 확인한다.
5. 다시 선택한 캐러밴의 현재 위치 기준으로 목적지와 루트가 표시되는지 확인한다.

### 12.5 Town/Route 잠금 및 방향성

1. 잠긴 Town으로 향하는 Route Asset만 `unlockedByDefault = true`인 불일치 데이터를 준비한다.
2. 잠긴 Town은 선택 불가이고 해당 Route도 클릭할 수 없는지 확인한다.
3. 클릭 이벤트를 직접 호출해도 Draft의 목적지와 Route가 변경되지 않고 다음 단계로 이동하지 않는지 확인한다.
4. 잠긴 Town ID 또는 Route ID를 Draft에 직접 넣어 출발을 요청해도 Commit stage와 `Traveling` 전환이 거부되는지 확인한다.
5. 현재 Town으로 들어오는 역방향 Route가 목록에 표시되지 않는지 확인한다.
6. 양방향 이동이 필요한 두 Town에는 서로 반대 방향의 Route Asset이 각각 존재하고 각 출발 Town의 `availableRoutes`에 연결됐는지 확인한다.
7. 플레이어가 실제 도달 가능하도록 개방되는 Town의 Progression 정의에 후속 이동 Route가 명시됐는지 확인한다.
8. 한 사이클 도착 후 해당 Town에서 선택 가능한 Route가 0개가 되어 무역 진행이 막히지 않는지 확인한다.

---

## 13. 문제 해결표

| 증상 | 가능한 원인 | 확인 및 해결 |
|---|---|---|
| 첫 사이클 후 TradeBtn이 반응하지 않음 | `OpenTradeScreen()` 직접 호출이 남아 Town 상태에서 즉시 닫힘 | Persistent OnClick 제거 후 `TownTradePreparationButton` 경로 사용 |
| 한 캐러밴이 이동 중이면 다른 캐러밴 준비 화면도 못 엶 | 전역 Town 상태만 허용하는 호환 Entry Command 사용 | 캐러밴 선택 선행 및 `caravanId` 단위 Framework 진입 계약으로 교체 |
| 모든 캐러밴이 같은 도시에서 출발함 | `player.currentTownId`를 사용함 | 선택한 `caravanId`의 `currentTownId` 조회 여부 확인 |
| 캐러밴을 바꿔도 이전 루트가 남음 | 캐러밴 변경 시 종속 Draft가 초기화되지 않음 | 목적지, 루트, 용병을 지우고 새 위치로 ViewData 재생성 |
| 현재 도시와 관계없는 도시가 선택됨 | 모든 Town을 그대로 선택 가능하게 표시함 | 유효한 출발 Route의 도착 도시 집합으로 제한 |
| 반대 방향 루트가 임의로 표시됨 | UI가 Route를 양방향으로 추론함 | Framework 또는 콘텐츠가 제공한 방향만 사용 |
| 잠긴 도시로 무역이 출발함 | Route 해금만 검사하고 목적지 Town 해금을 검사하지 않음 | Town과 Route 해금을 같은 선택 조건으로 묶고 출발 Command에서 재검증 |
| 잠긴 도시 아래 Route 버튼이 활성화됨 | Town 표시 상태와 Route `canSelect`가 독립적으로 계산됨 | `RouteViewData.canSelect`에 목적지 Town 해금을 포함하고 Binding에서도 목적지 ID 검증 |
| 도착 후 갈 수 있는 Route가 없음 | Progression 정의에 도착 도시의 후속 Route가 누락되었거나 `availableRoutes` 연결 방향이 잘못됨 | 필요한 Town/Route ID를 Progression 정의에 명시하고 막다른 도시 회귀 테스트 추가 |
| 해금된 도시를 눌러도 아무 Route가 없음 | 현재 Town에서 해당 도시로 향하는 방향성 Route가 없음 | 직접 이동이 의도라면 Route Asset 추가, 경유가 의도라면 `canSelect = false`와 안내 표시 |
| Traveling 캐러밴이 출발 후보에 표시됨 | UI가 JourneyState만 보고 `canSelect`를 재계산함 | Provider의 `canSelect`와 `disabledReason`을 그대로 사용 |
| 다른 캐러밴의 Cargo가 보임 | Overview 선택 또는 전역 선택 ID를 출발 Draft로 재사용함 | `departureCaravanId`로 다시 조회하고 캐러밴별 Cargo 사용 |
| UI를 닫았다 열면 선택이 섞임 | RuntimeContext가 UI 화면의 자식이거나 Draft가 캐러밴별로 정리되지 않음 | Runtime을 형제로 유지하고 캐러밴 변경 초기화 규칙 확인 |
| 지도는 보이지만 Overlay가 갱신되지 않음 | Overlay Binding의 Presenter가 None | 씬의 WorldMapPresenter 연결 |
| 지도를 닫아도 카메라가 계속 렌더링함 | SlidePanel의 Rend Cam이 None | WorldMapCamera 연결 및 닫힘 시 비활성 확인 |

---

## 14. 최종 체크리스트

- [ ] MainUICanvas, WorldMapRenderRoot, SceneLoader를 씬 최상위에 각각 하나만 배치
- [ ] EventSystem 하나 유지
- [ ] Caravan Overview 슬롯 4개와 `CaravanOverviewPresenter` 연결
- [ ] Overview 운영 Provider 연결, Test Provider 제거
- [ ] Setting/Cargo 이벤트가 각 슬롯의 `caravanId` 전달
- [ ] TradeBtn의 Presenter 직접 OnClick 제거
- [ ] TradeBtn에 `TownTradePreparationButton`과 EntryController 연결
- [ ] 병렬 출발 목표에서는 전역 Town 진입 경로를 캐러밴별 Preparation 진입 계약으로 교체
- [ ] 운영 `ITradePrepareCaravanOptionProvider` 연결
- [ ] 선택 캐러밴의 `currentTownId`가 TradePrepare Draft와 ViewData에 반영
- [ ] 목적지 목록을 현재 도시 출발 Route의 도착 도시로 제한
- [ ] 루트의 `fromTownId`와 선택 캐러밴의 `currentTownId` 일치
- [ ] 목적지 Town과 Route가 모두 해금된 경우에만 `canSelect = true`
- [ ] Route의 `toTownId`와 UI가 전달한 목적지 ID 일치
- [ ] 선택 Route가 최신 출발 Town의 `TownData.availableRoutes`에 등록되어 있음
- [ ] 잠긴 Town 클릭 또는 직접 Draft 주입이 다음 단계와 출발 Command에서 모두 차단
- [ ] 런타임 Route 후보는 현재 Town의 `TownData.availableRoutes`만 사용하고 전역 Route 배열과 합치지 않음
- [ ] `TownData.availableRoutes`의 모든 Route가 전역 Catalog에 등록되고 `fromTownId == townId`를 만족
- [ ] 플레이어가 실제 도달 가능하도록 개방되는 모든 Town의 Progression 정의에 후속 이동 Route 명시
- [ ] 캐러밴 변경 시 목적지, 루트, 용병 Draft 초기화
- [ ] 출발 성공 후 선택 캐러밴만 Traveling으로 변경
- [ ] NoticeUI의 CanvasGroup, Message Text, Fade Duration 연결
- [ ] SlidePanel과 Overlay Binding의 씬 전용 WorldMap 참조 연결
- [ ] Camera와 RawImage에 같은 RenderTexture 지정
- [ ] SceneLoader의 `Village_Home` 및 Build Settings 확인
- [ ] 한 사이클 완료 후 TradeBtn 재진입 및 새 위치 기반 루트 갱신 검증
