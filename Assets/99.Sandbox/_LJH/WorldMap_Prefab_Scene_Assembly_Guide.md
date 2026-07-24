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
| `TradePrepareRuntimeContextProvider` | 콘텐츠 배열 | 운영 Town, Route, Item, Wagon, Animal, Mercenary 데이터 |
| `TradePrepareRuntimeContextProvider` | Caravan Option Provider | 운영용 `ITradePrepareCaravanOptionProvider` 구현체 |

`Runtime`은 `TradePrepareUI`와 형제여야 한다. UI 화면을 비활성화해도 RuntimeContext와 Presenter는 이벤트를 계속 수신해야 한다.

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
-> 해당 도시에서 출발 가능한 Route 조회
-> Route 도착 도시 집합 생성
-> Town/Route 잠금 상태 반영
-> TradePrepareViewData.currentTownId, towns, routes 생성
```

`TradePrepareViewData.currentTownId`와 각 `RouteViewData.fromTownId`가 일치하지 않는 루트는 표시하거나 선택할 수 없다.

목적지를 선택하면 해당 목적지로 향하는 루트만 표시한다. 목적지를 바꾸면 이전 루트 선택을 제거한다.

### 7.3 실패 처리

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

2026-07-23 기준으로 다음 항목은 준비되어 있다.

- `CaravanBlockViewData`와 고정 4슬롯 Overview 표시 계약
- `SettingRequested(caravanId)`와 `CargoRequested(caravanId)` 전달 경로
- `TradePrepareDraft.departureCaravanId`
- `ITradePrepareCaravanOptionProvider` 계약과 테스트 Provider
- Builder의 `route.fromTownId == currentTownId` 필터
- `TownTradePreparationButton`과 `TownTradePreparationEntryController`
- `MainUICanvas.prefab`의 TradeBtn 구형 OnClick 제거 및 EntryController 연결
- 첫 `Preparation` 진입과 정산 후 `Town` 재진입 분기

다음 항목은 아직 운영 연결이 완료되지 않았다.

| 미완료 항목 | 현재 상태 | 필요한 방향 |
|---|---|---|
| `TradeBtn` 단일 사이클 재진입 | 구형 Presenter 직접 호출 제거 및 UI EntryController 연결 완료 | Play Mode에서 첫 진입과 정산 후 재진입 최종 확인 |
| 병렬 캐러밴 Preparation 진입 | 현재 Entry Command가 전역 Town 상태를 요구함 | 캐러밴 선택 UI를 먼저 열고, 선택된 `caravanId`만 대상으로 하는 Framework 진입 계약 필요 |
| 운영 Caravan Option Provider | 테스트 Provider만 준비됨 | 실제 다중 캐러밴 SaveData 조회 Provider 연결 |
| 캐러밴별 현재 도시 전달 | Option ViewData에 `currentTownId`가 없음 | Provider 결과 또는 별도 조회 계약으로 권위 위치 제공 |
| RuntimeContext 초기화 | `saveData.player.currentTownId`를 사용함 | 출발 캐러밴 선택 후 해당 캐러밴의 `currentTownId`로 갱신 |
| 출발 캐러밴 변경 | 목적지와 루트 등은 지우지만 현재 도시를 바꾸지 않음 | 새 캐러밴 위치까지 원자적으로 Draft에 반영 |
| 목적지 목록 | 모든 Town을 만든 뒤 잠금 여부만 반영함 | 현재 도시에서 유효한 Route의 도착 도시로 제한 |
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
- [ ] 캐러밴 변경 시 목적지, 루트, 용병 Draft 초기화
- [ ] 출발 성공 후 선택 캐러밴만 Traveling으로 변경
- [ ] NoticeUI의 CanvasGroup, Message Text, Fade Duration 연결
- [ ] SlidePanel과 Overlay Binding의 씬 전용 WorldMap 참조 연결
- [ ] Camera와 RawImage에 같은 RenderTexture 지정
- [ ] SceneLoader의 `Village_Home` 및 Build Settings 확인
- [ ] 한 사이클 완료 후 TradeBtn 재진입 및 새 위치 기반 루트 갱신 검증
