# InGame UI, World Map, and SceneLoader Assembly Guide

## 문서 목적

이 문서는 `Assets/_Project/07.Scenes/04_InGame/InGame.unity`의 현재 조립 상태를 기준으로 작성되었다.
새 InGame 계열 씬을 만들거나 프리팹을 다시 배치할 때 UI, 무역 준비 화면, 월드맵, Additive 마을 씬이 같은 방식으로 동작하도록 만드는 것이 목적이다.

> Revision reason (2026-07-16): `TradePrepareUI`, `FrameworkTradeScreenPresenter`, and `TradePrepareRuntimeContext` were bundled into `TradeFeature.prefab` so scene-specific trade references are no longer required. SceneLoader placement and verification were added to prevent incomplete scene copies.
>
> Revision reason (2026-07-16): the current InGame scene now has a scene-added `NoticeUI` for departure failures. Its CanvasGroup, TMP text, and `TradePrepareUiRuntimeBinding.departureWarning` reference are documented because this object is not yet part of `MainUICanvas.prefab`.

## 사용해야 하는 프리팹

| 역할 | 프리팹 경로 |
|---|---|
| 메인 UI와 무역 기능 | `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab` |
| 월드맵 렌더링 | `Assets/_Project/08.Prefabs/UI/Maps/WorldMapRenderRoot.prefab` |
| 마을 씬 Additive 로더 | `Assets/_Project/08.Prefabs/UI/Maps/SceneLoader.prefab` |

`TradeFeature.prefab`, `TradePrepareUI.prefab`, `TradePrepareRuntimeContext.prefab`은 `MainUICanvas.prefab` 내부에 중첩되어 있다. 일반적인 씬 조립에서는 이 세 프리팹을 별도로 배치하지 않는다.

## 최종 Hierarchy

```text
InGame Scene
├─ Main Camera
├─ Directional Light
├─ EventSystem
├─ InGameSceneController
├─ MainUICanvas                         <- MainUICanvas.prefab
│  ├─ InfoPanel
│  │  └─ CurrencyBar
│  │     └─ TradeBtn
│  ├─ WorldMapPanel
│  │  └─ RawImage
│  │     └─ WorldUiCanvas
│  │        ├─ ProgressPercentLabel
│  │        └─ RiskLabel
│  ├─ TradeFeature                      <- nested TradeFeature.prefab
│  │  ├─ TradePrepareUI                 <- 화면이 닫히면 비활성
│  │  │  └─ UIManager
│  │  │     └─ TradePrepareUiRuntimeBinding
│  │  └─ Runtime                        <- 항상 활성
│  │     ├─ FrameworkTradeScreenPresenter
│  │     └─ TradePrepareRuntimeContext
│  └─ NoticeUI                          <- InGame 씬에서 추가한 Override, 최초 비활성
│     ├─ Panel                          <- 경고 배경 Image
│     └─ Text (TMP)                     <- 출발 실패 메시지
├─ WorldMapRenderRoot                   <- WorldMapRenderRoot.prefab
│  ├─ WorldMapCamera
│  └─ WorldMapRoot
│     └─ WorldMapPresenter
└─ SceneLoader                          <- SceneLoader.prefab
```

`WorldMapRenderRoot`는 Canvas 자식으로 넣지 않는다. `MainUICanvas`, `WorldMapRenderRoot`, `SceneLoader`는 모두 씬의 최상위 오브젝트로 배치한다.

## 조립 순서

### 1. MainUICanvas 배치

1. `MainUICanvas.prefab`을 씬 최상위에 배치한다.
2. RectTransform이 전체 화면 Stretch인지 확인한다.
3. 씬에 EventSystem이 정확히 하나만 있는지 확인한다.
4. `MainUICanvas` 내부에 `TradeFeature`가 하나 존재하는지 확인한다.

다음 무역 연결은 프리팹 내부 연결이므로 Inspector에서 다시 지정하지 않는다.

| 호출 위치 | 내부 대상 |
|---|---|
| `TradeBtn.onClick` | `FrameworkTradeScreenPresenter.OpenTradeScreen()` |
| `Backdrop.onClick` | `FrameworkTradeScreenPresenter.CloseTradeScreen()` |
| `FrameworkTradeScreenPresenter.viewBehaviour` | `TradePrepareUI/UIManager` |
| `TradePrepareUiRuntimeBinding.runtimeContext` | `TradePrepareRuntimeContext` |

`Runtime`은 `TradePrepareUI`의 형제이다. 준비 화면이 닫혀도 RuntimeContext와 Draft를 유지하기 위한 구조이므로 `Runtime`을 `TradePrepareUI` 아래로 이동하지 않는다.

### 1-1. 출발 실패 NoticeUI 조립

현재 `InGame.unity`의 `NoticeUI`는 `MainUICanvas.prefab` 원본에 포함된 자식이 아니라 씬에서 추가된 Prefab Override이다. 따라서 다른 씬에 `MainUICanvas.prefab`을 새로 배치하면 아래 구성과 참조를 수동으로 추가해야 한다.

```text
MainUICanvas
└─ NoticeUI                           <- 최초 비활성, UI Layer
   ├─ RectTransform                  <- 화면 상단 배치
   ├─ CanvasGroup
   ├─ NoticeUI (Script)
   ├─ Panel                          <- Image, 경고 배경
   └─ Text (TMP)                     <- 메시지 출력
```

| Component | Field | 값 |
|---|---|---|
| `NoticeUI`의 `CanvasGroup` | `Alpha` | `1` |
| `NoticeUI`의 `CanvasGroup` | `Interactable` | `false` |
| `NoticeUI`의 `CanvasGroup` | `Blocks Raycasts` | `false` |
| `NoticeUI` 스크립트 | `Canvas Group` | 같은 오브젝트의 CanvasGroup |
| `NoticeUI` 스크립트 | `Message Text` | 자식 `Text (TMP)` |
| `NoticeUI` 스크립트 | `Fade Duration` | `3` |
| `TradePrepareUI/UIManager`의 `TradePrepareUiRuntimeBinding` | `Departure Warning` | `MainUICanvas/NoticeUI`의 NoticeUI 컴포넌트 |

`NoticeUI.Show(message)`가 호출되면 오브젝트가 활성화되고 `SetAsLastSibling()`으로 MainUICanvas의 가장 앞쪽 렌더 순서로 이동한다. 이후 CanvasGroup 알파가 3초 동안 1에서 0으로 감소하고 자동으로 비활성화된다. 같은 시간 안에 다시 호출하면 기존 코루틴을 중단하고 알파 1부터 다시 시작한다.

`NoticeUI`를 `TradePrepareUI` 아래로 이동하지 않는다. 현재 구조에서는 알림이 화면 패널과 분리된 MainUICanvas 직속 자식이며, `Blocks Raycasts = false`로 뒤쪽 UI 입력을 차단하지 않는다.

장기적으로 모든 InGame 계열 씬에서 동일한 알림을 사용하게 되면 `NoticeUI`를 별도 프리팹으로 만들거나 `MainUICanvas.prefab` 원본에 포함하는 편이 안전하다. 그 작업 전까지는 이 절의 수동 연결이 필수이다.

### 2. WorldMapRenderRoot 배치

1. `WorldMapRenderRoot.prefab`을 씬 최상위에 배치한다.
2. 씬에 `WorldMapRoot`, `WorldMapPresenter`, `WorldMapCamera`가 각각 하나만 존재하는지 확인한다.
3. `WorldMapCamera`가 `MainUICanvas`의 자식이 아닌지 확인한다.

### 3. 월드맵의 씬 전용 참조 연결

`MainUICanvas.prefab`과 `WorldMapRenderRoot.prefab`은 서로 다른 프리팹이므로 두 개의 씬 인스턴스 참조는 자동 저장되지 않는다.

| Component 위치 | Field | 할당할 씬 오브젝트 |
|---|---|---|
| `MainUICanvas/WorldMapPanel`의 `SlidePanel` | `Rend Cam` | `WorldMapRenderRoot/WorldMapCamera` |
| `MainUICanvas/WorldMapPanel/RawImage/WorldUiCanvas`의 `WorldMapOverlayLabelBinding` | `Presenter` | `WorldMapRenderRoot/WorldMapRoot/WorldMapPresenter` |

Progress 및 Risk Text 참조는 `MainUICanvas.prefab` 내부 연결이다. 별도로 다시 지정하지 않는다.

### 4. RenderTexture 확인

아래 두 필드는 동일한 `WorldMapRenderTexture` 에셋을 사용해야 한다.

| Component | Field |
|---|---|
| `WorldMapCamera` | `Target Texture` |
| `MainUICanvas/WorldMapPanel/RawImage` | `Texture` |

두 에셋이 다르면 버튼과 UI는 작동해도 지도 영상은 비어 보인다.

### 5. SceneLoader 배치

1. `SceneLoader.prefab`을 씬 최상위에 하나만 배치한다.
2. `AdditiveSceneLoader.sceneName`이 `Village_Home`인지 확인한다.
3. Build Settings의 `Scenes In Build`에 다음 씬이 활성 상태로 들어 있는지 확인한다.

```text
Assets/_Project/07.Scenes/04_InGame/Village_Home.unity
```

현재 프로젝트에서 `Village_Home`은 Build Index 5로 등록되어 있다. Build Index 번호 자체보다 씬 이름과 활성 상태가 중요하다.

Play Mode가 시작되면 `AdditiveSceneLoader.Start()`가 `Village_Home`을 Additive로 로드한다. 이미 같은 이름의 씬이 로드되어 있으면 다시 로드하지 않는다.

SceneLoader는 다음 기능을 담당하지 않는다.

- `Village_Home` 언로드
- 다른 마을로 교체
- MainUICanvas 또는 WorldMap 프리팹 생성
- Framework SaveData 초기화

따라서 씬 전환 시 마을 언로드나 교체가 필요하면 별도의 흐름에서 처리해야 한다.

## Framework 전제 조건

`FrameworkRoot`는 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`에서 준비되는 전역 시스템이다. 씬에 별도의 FrameworkRoot 프리팹을 중복 배치하지 않는다.

무역 UI의 RuntimeContext는 다음 데이터를 FrameworkRoot에서 사용한다.

- 현재 SaveData
- 현재 마을 ID
- 무역 시작 서비스
- 진행 및 정산 화면 상태

`InGame.unity`만 직접 실행하여 테스트할 때도 FrameworkRoot와 SaveData가 초기화됐는지 Console에서 확인한다. RuntimeContext는 존재하지만 SaveData가 없다면 무역 ViewData가 생성되지 않을 수 있다.

## 프리팹 편집 규칙

씬 Hierarchy에서 프리팹 내부 자식을 다른 위치로 이동하거나 형제 순서를 바꾸면 `Cannot restructure Prefab instance` 안내가 표시된다.

- 구조를 바꿀 때는 `Open Prefab`을 눌러 Prefab Mode에서 수정한다.
- `Unpack Prefab`은 사용하지 않는다.
- 씬별 위치와 크기만 바꿀 때는 프리팹 인스턴스의 최상위 RectTransform을 조정한다.
- `TradeFeature`, `TradePrepareUI`, `Runtime`의 부모 관계를 씬에서 변경하지 않는다.

Unpack하면 내부 연결이 씬 전용으로 바뀌어 다른 씬에 배치했을 때 동일한 구성을 보장할 수 없다.

## 추가하면 안 되는 항목

- 두 번째 EventSystem
- 두 번째 WorldMapRoot 또는 WorldMapPresenter
- UI 프리팹 내부의 WorldMapCamera
- `ND.UI.WorldMap.WorldMapPanel` 컴포넌트
- Progress/Risk 라벨용 중첩 Canvas, CanvasScaler 또는 GraphicRaycaster
- 별도로 배치한 TradeFeature, TradePrepareUI, RuntimeContext 또는 FrameworkTradeScreenPresenter
- 두 번째 SceneLoader

`ND.UI.WorldMap.WorldMapPanel`을 다시 추가하면 Awake에서 두 번째 WorldMapRoot가 생성될 수 있다.

## Play Mode 검증 순서

### 시작 상태

1. Console에 Missing Prefab, Missing Script, NullReference 오류가 없는지 확인한다.
2. Loaded Scenes에 `InGame`과 `Village_Home`이 함께 표시되는지 확인한다.
3. 월드맵이 닫힌 상태에서는 `WorldMapCamera`가 비활성인지 확인한다.
4. `TradePrepareUI`는 비활성이지만 `TradeFeature/Runtime`은 활성인지 확인한다.

### 월드맵

1. WorldMapButton을 누른다.
2. 패널이 움직이기 전에 WorldMapCamera가 활성화되는지 확인한다.
3. 지도, Progress, Risk가 함께 움직이는지 확인한다.
4. 지도를 닫고 슬라이드가 끝난 뒤 WorldMapCamera가 비활성화되는지 확인한다.

### 무역 UI

1. `MainUICanvas/InfoPanel/CurrencyBar/TradeBtn`을 누른다.
2. `TradePrepareUI`가 활성화되고 S1 준비 화면이 표시되는지 확인한다.
3. Backdrop을 눌러 준비 화면을 닫는다.
4. `TradeFeature/Runtime/TradePrepareRuntimeContext`가 계속 활성인지 확인한다.
5. 다시 열었을 때 기존 Draft 선택 내용이 유지되는지 확인한다.
6. 출발 불가 상태에서 `Depart` 버튼을 눌러 `NoticeUI`가 활성화되는지 확인한다.
7. Core 출발 실패 사유가 메시지로 표시되고 약 3초 뒤 `NoticeUI`가 비활성화되는지 확인한다.
8. 경고가 사라지기 전에 다시 누르면 알파가 1부터 다시 감소하는지 확인한다.

### 예상 오브젝트 수

```text
MainUICanvas:                       1
TradeFeature:                      1
TradePrepareUI:                    1
FrameworkTradeScreenPresenter:     1
TradePrepareRuntimeContext:        1
NoticeUI:                          1
WorldMapCamera:                    1
WorldMapRoot:                      1
WorldMapPresenter:                 1
SceneLoader:                       1
EventSystem:                       1
```

## 문제 해결표

| 증상 | 가능한 원인 | 확인 및 해결 |
|---|---|---|
| TradeBtn을 눌러도 열리지 않음 | MainUICanvas 내부 TradeFeature 누락 또는 Prefab Override 손상 | MainUICanvas 원본을 열어 TradeFeature와 TradeBtn 내부 listener 확인 |
| 준비 UI를 닫은 뒤 선택값이 초기화됨 | RuntimeContext가 TradePrepareUI 아래로 이동됨 | RuntimeContext를 `TradeFeature/Runtime` 아래에 유지 |
| 출발 실패 로그는 있지만 경고가 보이지 않음 | `TradePrepareUiRuntimeBinding.departureWarning`이 None이거나 NoticeUI 내부 참조 누락 | Binding의 Departure Warning, NoticeUI의 Canvas Group과 Message Text를 확인 |
| 경고가 다른 UI 뒤에 가려짐 | NoticeUI가 MainUICanvas 직속 자식이 아니거나 별도 Canvas Sort Order가 더 높음 | MainUICanvas 직속 자식으로 유지하고 `Show()`의 `SetAsLastSibling()` 호출 확인 |
| 경고가 뒤쪽 버튼 클릭을 막음 | CanvasGroup의 Blocks Raycasts가 활성화됨 | `Blocks Raycasts = false`로 설정 |
| 경고가 자동으로 사라지지 않음 | NoticeUI가 비활성 부모 아래에 있거나 Fade Duration/참조 누락 | MainUICanvas 직속 배치, Fade Duration 3, CanvasGroup 참조 확인 |
| Missing Nested Prefab 오류 | TradePrepareUI 또는 TradePrepareRuntimeContext 프리팹 삭제 | 삭제한 `.prefab`과 `.meta`를 함께 복구하고 TradeFeature 재임포트 |
| 지도가 비어 있음 | RenderTexture 불일치 또는 Camera 미연결 | RawImage Texture, Camera Target Texture, SlidePanel Rend Cam 확인 |
| 지도는 보이지만 라벨이 갱신되지 않음 | Overlay Binding Presenter가 None | 씬의 WorldMapPresenter 할당 |
| 지도를 닫아도 Camera가 계속 켜짐 | SlidePanel Rend Cam이 None | WorldMapCamera 할당 |
| WorldMapRoot가 두 개 생성됨 | 생성형 WorldMapPanel 컴포넌트 또는 Render prefab 중복 | 중복 컴포넌트와 프리팹 제거 |
| Village_Home이 로드되지 않음 | sceneName 오타 또는 Build Settings 누락 | 정확히 `Village_Home`인지와 Scenes In Build 활성 상태 확인 |
| SceneLoader 실행 시 scene load 오류 | 대상 씬이 Build Settings에 없음 | `Village_Home.unity`를 Scenes In Build에 추가 |
| 프리팹 자식을 이동할 수 없음 | 씬에서 Prefab instance 구조 변경 시도 | `Open Prefab`으로 원본 편집. Unpack하지 않음 |

## 다른 씬으로 복사할 때의 최소 체크리스트

- [ ] `MainUICanvas.prefab` 배치
- [ ] `WorldMapRenderRoot.prefab` 배치
- [ ] `SceneLoader.prefab` 배치
- [ ] EventSystem 하나 유지
- [ ] SlidePanel의 Rend Cam 연결
- [ ] WorldMapOverlayLabelBinding의 Presenter 연결
- [ ] Camera와 RawImage에 동일 RenderTexture 할당
- [ ] SceneLoader의 sceneName을 `Village_Home`으로 설정
- [ ] `Village_Home.unity`가 Build Settings에 활성 등록
- [ ] MainUICanvas 내부 TradeFeature를 별도로 Unpack하거나 이동하지 않음
- [ ] `MainUICanvas/NoticeUI`가 하나만 존재하고 최초 비활성 상태인지 확인
- [ ] NoticeUI의 CanvasGroup, Message Text, Fade Duration 3 연결
- [ ] `TradePrepareUiRuntimeBinding.departureWarning`에 NoticeUI 연결
- [ ] 출발 실패 메시지 표시, 재호출 타이머 초기화, 3초 후 비활성화 확인
- [ ] Play Mode에서 UI, 지도, Additive scene, Console을 순서대로 검증
