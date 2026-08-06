# InGame Scene Loading Optimization — 구현 로직 정리

> 브랜치: `feature/framework/in-game-scene-loading-optimization`  
> 커밋: `fix(framework): wait for required additive scene before in-game ready`  
> 작성일: 2026-08-06

---

## 1. 작업 목적

기존 Loading flow는 **InGame Single scene**이 활성화되면 로딩 화면을 바로 제거했다.  
그러나 InGame 진입 직후 `Treadmill_Preview` additive 씬이 아직 준비되지 않은 상태에서 UI가 노출되면, 트레드밀 RT·캐러밴 표시가 비어 있거나 초기화 전에 상호작용이 가능해지는 문제가 있었다.

이번 작업의 목표는 다음과 같다.

1. InGame Single scene 활성화 **이후에도** 로딩 오버레이를 유지한다.
2. 필수 additive 씬(`Treadmill_Preview`)의 비동기 로드와 **첫 프레임 초기화 완료**를 기다린 뒤에만 로딩 화면을 제거한다.
3. 로딩 중 InGame 씬의 입력이 새어 나가지 않도록 입력 차단을 유지한다.
4. 진행률 UI를 Single / Additive 단계에 맞게 분할 표시한다.

---

## 2. 전체 흐름

```text
Loading Scene 진입
  ↓
FrameworkRoot.PrepareGameForInGame()
  ↓ (성공 시 progress 0.2)
SceneFlow.BeginLoadScene(InGame, deferActivation=true)
  ↓ (progress 0.2 → 0.75)
최소 표시 시간 + post-load hold 대기
  ↓
Loading EventSystem 비활성화
  ↓
sceneLoadOperation.AllowActivation()
  ↓ (InGame Single scene 활성화, progress 0.75 유지)
sceneLoadOperation.IsCompleted 대기
  ↓
AdditiveSceneLoader.LoadRequiredSceneAsync("Treadmill_Preview")
  ↓ (progress 0.75 → 0.95)
GetIsReady("Treadmill_Preview") 확인
  ↓ (progress 1.0)
LoadingScreenPresenter Destroy
```

### 핵심 변경 요약

| 구분 | 기존 | 변경 후 |
|---|---|---|
| 로딩 오버레이 수명 | InGame 활성화 직전/직후 종료 | `Treadmill_Preview` Ready까지 유지 |
| progress 구간 | 0.2 → 1.0 (Single만) | 0.2→0.75 (Single), 0.75→0.95 (Additive), 1.0 (완료) |
| Additive 로드 시점 | InGame 내부 `AdditiveSceneLoader.Start()` | Loading flow가 `Treadmill_Preview`를 명시적으로 선행 로드 |
| 입력 차단 | Loading scene EventSystem | `DontDestroyOnLoad` + 최상위 Canvas + InGame 활성화 직전 EventSystem off |

---

## 3. 변경 파일

| 파일 | 역할 |
|---|---|
| `Assets/_Project/05.UI/11_Loading/Scripts/LoadingScreenPresenter.cs` | 로딩 flow orchestration, 오버레이 유지, additive 대기 |
| `Assets/_Project/05.UI/11_Loading/Scripts/LoadingProgress.cs` | Single / Additive progress mapping |
| `Assets/_Project/05.UI/04_InGame/YHY/Scripts/AdditiveSceneLoader.cs` | additive 씬 비동기 로드 + Ready 상태 공유 |
| `Assets/_Project/05.UI/11_Loading/Tests/Editor/LoadingProgressTests.cs` | progress mapping 단위 테스트 |

---

## 4. LoadingScreenPresenter

### 4.1 오버레이 지속 (`DontDestroyOnLoad`)

`Awake()`에서 다음을 수행한다.

- `DontDestroyOnLoad(gameObject)` — InGame Single scene 활성화 후에도 로딩 Canvas와 `RunLoading()` 코루틴을 유지한다.
- Canvas `sortingOrder = short.MaxValue`, `overrideSorting = true` — InGame UI 위에 로딩 화면이 계속 덮인다.
- 자식 `EventSystem` / `BaseInputModule` 참조를 캐시한다.

### 4.2 RunLoading() 단계

#### Phase A — Framework 준비

```csharp
var preparation = frameworkRoot.PrepareGameForInGame();
targetProgress = 0.2f;
```

- `PrepareGameForInGame()` 실패 시 `Fail()` 후 중단.
- `refreshSeasonAfterPreparation`이 true이면 공식 시즌 ID로 배경 갱신.

#### Phase B — InGame Single scene deferred load

```csharp
sceneLoadOperation = frameworkRoot.SceneFlow.BeginLoadScene(SceneNames.InGame, true);
```

- `deferActivation=true`이므로 Unity는 activation boundary(0.9 plateau)까지 로드한 뒤 대기한다.
- 매 프레임 `sceneLoadOperation.Progress01`을 `LoadingProgress.MapInGameSceneProgress()`로 0.2~0.75에 매핑한다.
- `IsReadyForActivation`이 true가 되면 progress target을 0.75로 고정하고 표시 progress가 따라잡을 때까지 대기한다.

#### Phase C — 표시 타이밍 gate

- `minimumDisplaySeconds` — 로딩 화면 최소 노출 시간.
- `postLoadHoldSeconds` — 75% 도달 후 추가 hold.

#### Phase D — InGame 활성화

```csharp
DisableLoadingEventSystem();
sceneLoadOperation.AllowActivation();
```

- InGame의 EventSystem과 충돌하지 않도록 **Loading 쪽 EventSystem만** 먼저 비활성화한다.
- Canvas는 그대로 남아 raycast 차단 역할을 계속한다.
- `AllowActivation()` 후 `sceneLoadOperation.IsCompleted`까지 대기한다.

#### Phase E — 필수 additive scene 로드

```csharp
StartCoroutine(AdditiveSceneLoader.LoadRequiredSceneAsync("Treadmill_Preview"));
```

- `RequiredAdditiveSceneName = "Treadmill_Preview"` (상수).
- 로드 중 `LoadingProgress.MapRequiredAdditiveProgress()`로 0.75~0.95 매핑.
- `GetIsLoading()`이 false가 된 뒤 `GetIsReady()`를 확인한다.
- Ready가 아니면 `GetError()` 상세와 함께 `Fail()`.

#### Phase F — 완료

- target progress 1.0까지 표시 progress를 올린 뒤 `Destroy(gameObject)`.

### 4.3 입력 차단 정책

| 시점 | Loading Canvas | Loading EventSystem | InGame 입력 |
|---|---|---|---|
| Single 로드 중 | ON | ON | 차단 |
| Single 활성화 직전 | ON | OFF | Canvas로 차단 |
| Additive 로드 중 | ON | OFF | Canvas로 차단 |
| Additive Ready 후 | Destroy | — | 정상 |

`DisableLoadingEventSystem()`은 idempotent하다. 반복 호출해도 추가 부작용 없음.

### 4.4 실패 처리

- `Fail(detail)`은 사용자 메시지(`UserErrorMessage`)를 error panel에 표시한다.
- Editor / Development Build에서만 `detail`을 error text에 append한다.
- `ReturnToTitle()` 호출 시 `Destroy(gameObject)` 후 `SceneFlow.GoToTitle()`을 호출한다.

---

## 5. LoadingProgress

순수 함수로 progress mapping과 표시 progress smoothing을 담당한다. Presenter와 EditMode test가 공유한다.

### 5.1 구간 매핑

| 메서드 | 입력 | 출력 구간 | 용도 |
|---|---|---|---|
| `MapInGameSceneProgress` | scene 0~1 | 0.2 ~ 0.75 | InGame Single deferred load |
| `MapRequiredAdditiveProgress` | scene 0~1 | 0.75 ~ 0.95 | `Treadmill_Preview` additive load |
| `MapSceneProgress` | scene 0~1 | 0.2 ~ 1.0 | 기존 호출부 호환용 (현재 presenter는 미사용) |

### 5.2 표시 progress 이동

```csharp
MoveDisplayedProgress(displayed, target, maximumDelta)
```

- NaN / Infinity 입력은 0으로 sanitize.
- displayed는 target보다 **절대 감소하지 않는다**.
- target은 0~1 clamp, displayed보다 작으면 displayed를 유지.
- `maximumDelta`가 비유한수이면 0으로 처리해 progress freeze.

Presenter는 `progressVisualSpeed * Time.unscaledDeltaTime`를 delta로 사용하므로, timeScale과 무관하게 로딩 바가 움직인다.

---

## 6. AdditiveSceneLoader

기존에는 `Start()`에서 `SceneManager.LoadScene(..., Additive)`를 **동기**로 호출했다.  
이번 변경으로 **씬별 static state dictionary**를 두고, Loading flow와 InGame 내부 로더가 같은 진행 상태를 공유할 수 있게 했다.

### 6.1 SceneLoadState

씬 이름별로 다음을 보관한다.

- `Operation` — 진행 중 `AsyncOperation`
- `IsLoading` — 로드 코루틴 실행 중
- `IsReady` — 활성화 + 첫 프레임 초기화 완료
- `Error` — 마지막 실패 메세지

### 6.2 LoadRequiredSceneAsync(string)

```text
1. scene name empty → LogError, yield break
2. 이미 loaded → IsReady=true, 즉시 종료
3. 다른 caller가 IsLoading → while 대기 후 종료 (중복 로드 없음)
4. LoadSceneAsync(additive) 시작
5. operation 완료 후 scene.isLoaded 재검증
6. yield return null  ← Awake/OnEnable/Start/sceneLoaded callback 이후
7. IsLoading=false, IsReady=true
```

**Ready의 의미:** Unity operation 완료 직후가 아니라, **한 프레임 더 기다린 뒤** true로 설정한다.  
동기 `Awake` / `OnEnable` / `Start` / `sceneLoaded` callback과 즉시 실행되는 Rebuild가 끝난 다음 프레임을 보장하기 위함이다.

### 6.3 static 조회 API

| API | 반환 의미 |
|---|---|
| `GetIsLoading(name)` | 해당 씬 로드 코루틴 진행 중 |
| `GetIsReady(name)` | Ready flag + `SceneManager.GetSceneByName().isLoaded` |
| `GetProgress01(name)` | Ready면 1, operation 있으면 `progress/0.9`, 없으면 0 |
| `GetError(name)` | 마지막 실패 detail, 없으면 빈 문자열 |

### 6.4 InGame 씬과의 관계

`InGame.unity`에는 `AdditiveSceneLoader` 컴포넌트가 `sceneName = "Treadmill_Preview"`로 배치되어 있다.  
`Start()`에서 `LoadRequiredSceneAsync()`를 호출하지만, Loading flow가 먼저 같은 static API로 로드를 시작하면 **기존 작업에 합류**한다.

`Village_Home` 등 다른 additive 씬은 별도 `AdditiveSceneLoader` 인스턴스의 serialized `sceneName`으로 기존처럼 InGame Start 시 로드된다.  
이번 변경의 **필수 대기 대상은 `Treadmill_Preview`만**이다.

---

## 7. Treadmill_Preview가 필수인 이유

InGame UI는 트레드밀 RenderTexture를 `Treadmill_Preview` additive 씬의 카메라 출력에 의존한다.  
관련 배경 문서: `Docs/Personal_Documents/YHY/0731_treadmill_ingame_sync.md`

- `DisableWhenAdditive` — additive로 얹힐 때 프리뷰 전용 조명·디버그 UI 자동 비활성
- `TreadmillLane` / `TreadmillLaneManager` — 마차별 독립 레인
- `TreadmillProgressSync` — 캐러밴 진행도와 트레드밀 동기화

Loading flow가 이 씬 Ready를 기다리지 않으면, InGame UI가 먼저 보이는 순간 트레드밀 영역이 비거나 초기화 중 상태가 노출될 수 있다.

---

## 8. EditMode 테스트

`LoadingProgressTests`에서 검증하는 항목:

- `MapInGameSceneProgress` — 0→0.2, 0.5→0.475, 1→0.75
- `MapRequiredAdditiveProgress` — 0→0.75, 0.5→0.85, 1→0.95
- `MapSceneProgress` — clamp 및 legacy 전체 구간
- `MoveDisplayedProgress` — non-finite 입력, 역행 방지, delta=0 freeze

Play Mode / Scene integration test는 이번 커밋 범위에 포함되지 않았다.

---

## 9. 실패 조건 정리

| 조건 | 처리 |
|---|---|
| `FrameworkRoot` 없음 | Fail |
| `PrepareGameForInGame()` 실패 | Fail + preparation error |
| `SceneFlow` 없음 | Fail |
| `BeginLoadScene(InGame)` null | Fail |
| Single load operation 유실 | Fail |
| `Treadmill_Preview` additive load 실패 | Fail + `GetError()` |
| `Treadmill_Preview` load 완료 but not Ready | Fail |

실패 후 로딩 오버레이는 error panel을 표시하고 유지된다. 사용자는 `ReturnToTitle()`로 타이틀 복귀 가능.

---

## 10. 주의사항 및 리스크

### 10.1 Build Profile 등록

`Treadmill_Preview`는 Build Settings / Build Profile에 등록되어 있어야 한다.  
미등록 시 additive load가 실패하고 loading error panel이 표시된다.

### 10.2 중복 로드 방지

동일 씬 이름에 대해 `LoadRequiredSceneAsync`를 여러 번 호출해도 dictionary state로 **하나의 로드만** 진행된다.  
Loading presenter와 InGame `AdditiveSceneLoader.Start()`가 동시에 호출해도 안전하다.

### 10.3 Ready 판정의 한계

Ready는 "operation 완료 + 1 frame" 기준이다.  
코루틴이나 async 초기화를 **다음 프레임 이후**에 시작하는 컴포넌트까지 보장하지는 않는다.  
추가 게이트가 필요하면 `AdditiveSceneLoader` 또는 트레드밀 쪽 explicit ready signal을 검토해야 한다.

### 10.4 Village_Home 등 기타 additive

`Village_Home`은 Loading flow 필수 대기 대상이 아니다.  
마을 RT가 늦게 뜨는 UX가 문제가 되면 별도 작업으로 확장해야 한다.

### 10.5 External modification

`AdditiveSceneLoader.cs`는 `Assets/_Project/05.UI/04_InGame/YHY/`에 있으며 YHY 영역 코드다.  
Loading flow 연동을 위해 static API와 async load로 확장했다.  
InGame 씬의 serialized `sceneName` 동작은 유지된다.

---

## 11. 검증 체크리스트

- [ ] Title → Loading → InGame 진입 시 로딩 바가 75%에서 잠시 유지된 뒤 additive 구간으로 진행하는지
- [ ] InGame UI 노출 시점에 트레드밀 RT가 이미 렌더되는지
- [ ] 로딩 중 InGame 버튼 클릭이 차단되는지
- [ ] `Treadmill_Preview` Build Profile 미등록 시 error panel + 타이틀 복귀 동작
- [ ] EditMode `LoadingProgressTests` 통과
- [ ] `ReturnToTitle()` 호출 시 DontDestroyOnLoad 오브젝트 정리

---

## 12. Related

- 트레드밀 InGame 연동: `Docs/Personal_Documents/YHY/0731_treadmill_ingame_sync.md`
- InGame scene routing: `Docs/Personal_Documents/CSU/0710_InGame_Scene_Route_UI_Guide.md`
- Framework scene load API: `Assets/_Project/11.CoreServices/Scripts/SceneFlow/ISceneLoadOperation.cs`
- 관련 Issue: 없음
