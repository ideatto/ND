# Framework CoreServices 팀 사용 설명서

## 목적

이 문서는 `Assets/_Project/11.CoreServices/`(Framework & Integration)가 제공하는 **주요 기능을 팀원이 빠르게 찾아 쓰는** 통합 설명서이다.

- 상세 정책·API 계약은 각 전용 Guide / CSU 문서를 따른다.
- 이 문서는 **어디를 열고, 무엇을 호출하고, 어떤 ContextMenu를 쓰는지**에 초점을 둔다.

**네임스페이스:** `ND.Framework`  
**진입점:** `FrameworkRoot.Instance`  
**Play 시작:** `Assets/_Project/07.Scenes/01_Boot/Boot.unity`

---

## 목차

1. [5분 퀵스타트](#1-5분-퀵스타트)
2. [FrameworkRoot 서비스 맵](#2-frameworkroot-서비스-맵)
3. [씬 흐름](#3-씬-흐름)
4. [저장 데이터](#4-저장-데이터)
5. [공용 기준 데이터 (SharedGameData)](#5-공용-기준-데이터-sharedgamedata)
6. [시간 · 배율 · Pause](#6-시간--배율--pause)
7. [무역 루프 (출발 → 진행 → 정산 → Claim)](#7-무역-루프-출발--진행--정산--claim)
8. [인게임 화면 라우팅](#8-인게임-화면-라우팅)
9. [정산 UI 연결](#9-정산-ui-연결)
10. [Framework 이벤트](#10-framework-이벤트)
11. [디버그 · Smoke](#11-디버그--smoke)
12. [팀별 빠른 경로](#12-팀별-빠른-경로)
13. [상세 문서 링크](#13-상세-문서-링크)

---

## 1. 5분 퀵스타트

```text
Boot → Title → New Game → Loading → InGame
```

| 단계 | 조작 |
|------|------|
| 1 | Hierarchy에서 Play Mode로 `Boot` 실행 |
| 2 | Title에서 **New Game** (`TitleSceneController.StartNewGame`) |
| 3 | Loading 완료 후 InGame 진입 |
| 4 | Hierarchy에서 `TradeStartDebugHarness` 선택 |
| 5 | `Framework/Fill Sample Caravan` → `Framework/Start Trade And Record Time` |
| 6 | `Framework/Check Trade Progress And Completion` 또는 `Force Complete Active Trade` |
| 7 | `Framework/Claim Settlement And Reset` |

자동 검증(Editor):

```text
메뉴 ND → Framework → Run M1 Loop + Economy E2E Checks
```

포함 항목: loop integrity · Economy E2E · 인게임 식량 · Pause 식량 정지 · Failed 정산 화면 · PendingSettlement 복구 · **Offline 진행 복구**.  
M3 대기 정산 로직: [`Docs/Personal_Documents/CSU/m3-pending-settlement-persist.md`](../Personal_Documents/CSU/m3-pending-settlement-persist.md)  
M3 오프라인 진행: [`Docs/Personal_Documents/CSU/m3-offline-progress-pipeline.md`](../Personal_Documents/CSU/m3-offline-progress-pipeline.md)  
M2 통합 검증 기록: [`Docs/Personal_Documents/CSU/m2-pause-failed-force-smoke.md`](../Personal_Documents/CSU/m2-pause-failed-force-smoke.md)
---

## 2. FrameworkRoot 서비스 맵

다른 feature는 서비스를 **직접 new 하지 말고** root에서 가져온다.

| 프로퍼티 | 용도 |
|----------|------|
| `GameTime` | UTC / 인게임 배율 / pause |
| `SaveService` | Load / Save / Reset / HasSaveData |
| `CurrentSaveData` | 현재 공유 SaveData 참조 |
| `SharedGameData` | ID → 기준 데이터 조회 (`ISharedGameDataProvider`) |
| `SceneFlow` | Boot/Title/Loading/InGame 전환 |
| `TradeStart` | 무역 출발 |
| `TradeProgressCoordinator` | 진행률 · 정산 생성 · claim |
| `TradeProgressRecorder` | tradeProgress 상태 기록 |
| `InGameScreenRouter` | Preparation / Traveling / Settlement |
| `SettlementUiBridge` | 정산 결과 → UI |
| `DebugCommands` | debug Force* · time · complete trade |

```csharp
var root = FrameworkRoot.Instance;
var save = root.CurrentSaveData;
var shared = root.SharedGameData;
```

주의:

- `CurrentSaveData`는 **공유 mutable 참조**이다. 바꾸면 다른 서비스에도 바로 보인다.
- 영속화가 필요하면 `SaveService.Save(CurrentSaveData)`를 호출한다(많은 흐름은 이미 내부에서 저장한다).

---

## 3. 씬 흐름

씬 이름 상수: `SceneNames.Boot` / `Title` / `Loading` / `InGame`  
(Unity Build Settings 이름과 일치해야 한다.)

| UI / 컨트롤러 | 호출 |
|---------------|------|
| Title New Game | `TitleSceneController.StartNewGame()` → `FrameworkRoot.StartNewGame()` |
| Title Continue | `ContinueGame()` |
| Title Reset Save | `ResetSaveData()` |
| Loading 완료 | `LoadingSceneController.CompleteLoading()` → `CompleteLoadingAndEnterGame()` |
| InGame → Title | `InGameSceneController.ReturnToTitle()` |

Loading → InGame 시 이벤트 순서:

```text
SharedGameDataLoaded → LoadCompleted → InGameScreenChanged
```

SharedGameData 검증 실패 시 InGame 진입이 막힐 수 있다.

---

## 4. 저장 데이터

| 항목 | 내용 |
|------|------|
| 구현 | `JsonSaveService` → `persistentDataPath/save_data.json` |
| 스키마 | `SaveData` version **5** |
| 주요 섹션 | `player`, `caravan`, `tradeProgress`, `pendingSettlement`, `world`, `tutorial` |

자주 보는 필드:

| 경로 | 의미 |
|------|------|
| `tradeProgress.state` | None / Preparing / Traveling / SettlementPending / Completed / Failed |
| `tradeProgress.activeTradeId` | 현재 무역 ID |
| `pendingSettlement` | SettlementPending 대기 정산 결과 (재실행 복구용) |
| `pendingSettlement.hasResult` | 유효 정산 결과 존재 |
| `pendingSettlement.claimed` | 이미 수령한 pending (복구·재수령 차단) |
| `world.currentSeasonId` | 계절 (Economy 입력) |
| `world.currentDisasterId` | 재난 (빈 문자열 = 없음) |
| `caravan.elapsedInGameSeconds` | 인게임 경과(식량) |
| `player.tradingCurrency` | 무역 화폐 |

디버그 출력:

- `TradeStartDebugHarness` → `Framework/Print Save Data`
- `SaveDataDebugPrinter` → `Framework/Print Full Save Data` / `Print Pending Settlement Save Data`

버전 불일치 시 마이그레이션 없이 새 게임 데이터가 될 수 있다.  
**v4 이하 세이브는 v5 코드에서 새 게임으로 복구될 수 있으므로**, M3 통합 전에는 New Game으로 맞추는 것을 권장한다.

---

## 5. 공용 기준 데이터 (SharedGameData)

SaveData의 ID를 **도시·상품·마차·동물·루트** 정의로 해석할 때 사용한다.

```csharp
var data = FrameworkRoot.Instance.SharedGameData;
if (data != null && data.IsLoaded)
{
    data.TryGetTradeItem(itemId, out var item);
    data.TryGetRoute(routeId, out var route);
}
```

또는 `FrameworkEvents.SharedGameDataLoaded` 구독.

| 항목 | 내용 |
|------|------|
| 카탈로그 | `Resources/SandboxSharedGameDataCatalog.asset` |
| 규칙 | Sandbox SO 타입을 다른 feature에서 직접 참조하지 말 것 |
| 상세 | [`Framework_Shared_Game_Data_Guide.md`](./Framework_Shared_Game_Data_Guide.md) |

---

## 6. 시간 · 배율 · Pause

| 구분 | 용도 |
|------|------|
| `IGameTimeProvider.CurrentUtc` | 현실 UTC (도착 `progress01`) |
| `IInGameTimeProvider` | 인게임 배율 · pause · 경과 변환 |
| `Time.timeScale` | 연출/debug 재생 속도 (**식량 축과 다름**) |

이중 시간 모델:

```text
현실 UTC  → progress01, 도착 시각
인게임 초 → elapsedInGameSeconds, 식량 소모
```

정책 에셋: `Resources/InGameTimePolicyConfig.asset`

Debug (InGame `FrameworkDebugBridge`):

| ContextMenu | 효과 |
|-------------|------|
| Set Debug Time Scale | Unity `timeScale` |
| Set / Reset In-Game Time Multiplier | 게임플레이 배율 |
| Pause / Resume In-Game Time | 인게임 경과 정지/재개 |

Pause 중에는 `TradeProgressCoordinator.CheckProgressAndCompletion()`이 `progress01`과 `elapsedInGameSeconds`를 **모두** 갱신하지 않는다.  
식량 잔량도 증가·감소하지 않는다. 검증: `Framework/Run Pause Food Freeze Smoke`.

Release 빌드에서는 runtime 배율 변경이 제한될 수 있다.  
상세: [`Framework_InGame_Time_Multiplier_API_Guide.md`](./Framework_InGame_Time_Multiplier_API_Guide.md)

---

## 7. 무역 루프 (출발 → 진행 → 정산 → Claim)

```text
Prepare → Traveling → SettlementPending → claim → Completed/Failed → Prepare
```

### 7-1. 출발

```csharp
var result = FrameworkRoot.Instance.TradeStart.TryStartTrade(
    caravan, distanceKm, tradeId, routeId);

// canDepart 와 LastRecordSucceeded 둘 다 확인할 것
```

성공 시 `tradeProgress`가 Traveling으로 기록되고 화면이 Traveling으로 바뀐다.

### 7-2. 진행 · 도착

```csharp
var coordinator = FrameworkRoot.Instance.TradeProgressCoordinator;
coordinator.SetActiveCaravan(caravan); // runtime caravan이 따로 있으면
bool settlementReady = coordinator.CheckProgressAndCompletion();
```

- `progress01`: UTC 도착 비율
- `elapsedInGameSeconds`: SetProgress **전에** Framework가 동기화 (식량)

### 7-3. Economy M1

| 단계 | 동작 |
|------|------|
| Settle (preview) | `JourneyResultData` 금액 채움, **화폐 불변** |
| Claim | `SaveData` 화폐·growth 반영 (`PurchaseGrowth`는 현재 false) |

입력에 `world.currentSeasonId` / `currentDisasterId`가 포함된다.

### 7-4. Claim

UI는 coordinator를 직접 부르지 말고:

```text
SettlementUiDataAdapter.OnClickClaimSettlement()
→ SettlementUiBridge → ClaimSettlementAndReset
```

중복 claim · trade ID 불일치 · `pendingSettlement.claimed`는 Framework가 차단한다.  
Claim 성공 시 `pendingSettlement`는 clear되고 `Completed`/`Failed`로 기록된다.

### 7-5. Continue / Load 복구 (Offline + PendingSettlement)

Title Continue 또는 Loading 완료 시:

```text
CompleteLoadingAndEnterGame
→ SharedGameData 로드
→ ApplyOfflineProgressOnLoad   (Traveling만: 역행 검사 · 상한 clamp · 경과/식량 · 오프라인 완료)
→ RestorePendingSettlement     (SettlementPending만: runtime cache · TradeSettlementReady)
→ RefreshFromSaveData
→ RaiseLoadCompleted
→ InGame
```

- Traveling + 오프라인 미완료: 진행·식량 갱신 후 Traveling 유지
- Traveling + 오프라인 완료: `SettlementPending` + `pendingSettlement` 저장 + `TradeOfflineCompleted` 1회
- 이미 SettlementPending: Offline 분기 no-op → Pending 복구만 수행
- `loadUtc < lastSavedUtcTicks`: `TimeRollbackDetected`, 오프라인 적용 스킵

상세:

- Offline: [`m3-offline-progress-pipeline.md`](../Personal_Documents/CSU/m3-offline-progress-pipeline.md)
- Pending: [`m3-pending-settlement-persist.md`](../Personal_Documents/CSU/m3-pending-settlement-persist.md)

검증: `Framework/Run Offline Progress Smoke`, `Framework/Run Pending Settlement Restore Smoke`

---

## 8. 인게임 화면 라우팅

상태: `InGameScreenState` = Preparation | Traveling | Settlement

```csharp
FrameworkEvents.InGameScreenChanged += screen => { /* 패널 전환 */ };
// OnDisable에서 구독 해제
```

| `tradeProgress.state` | 화면 |
|----------------------|------|
| Traveling | Traveling |
| SettlementPending | Settlement (Success / Failed grade 모두) |
| Completed / Failed (claim 후) | Preparation |
| 그 외 | Preparation |

실패 무역도 정산 전에는 `SettlementPending` + Settlement 화면이다.  
claim 후 `Failed`로 기록되면 Preparation으로 돌아온다. 검증: `Framework/Run Failed Settlement Screen Smoke`.

서비스가 화면을 바꿀 때: `InGameScreenRouter.RequestScreen(...)`.

---

## 9. 정산 UI 연결

```text
TradeSettlementReady
→ SettlementUiBridge 캐시 + Settlement 화면
→ SettlementUiDataAdapter → ISettlementView(SettlementViewData)
→ OnClickClaimSettlement
```

재실행·Continue 시에도 `RestorePendingSettlement`가 `TradeSettlementReady`를 다시 발행하므로, UI는 기존 이벤트 구독만으로 복구 결과를 받을 수 있다.

UI 팀 할 일:

1. `ISettlementView` 구현
2. `SettlementUiDataAdapter`에 view 연결
3. 수령 버튼을 `OnClickClaimSettlement`에 연결

상세: [`Settlement_UI_Data_Connection_Guide.md`](./Settlement_UI_Data_Connection_Guide.md)

테스트 뷰: InGame의 `InGameSettlementTestView` (프로덕션 UI 대체용).

---

## 10. Framework 이벤트

구독은 `OnEnable` / 해제는 `OnDisable`. 발행은 `Raise*`만 사용.

| 이벤트 | 언제 |
|--------|------|
| `SharedGameDataLoaded` | 공용 데이터 준비 |
| `LoadCompleted` | SaveData 준비 |
| `SceneChanged` | 씬 로드 완료 |
| `TradeSettlementReady` | 정산 결과 준비 `(tradeId, JourneyResultData)`. Settle뿐 아니라 **RestorePendingSettlement** 시에도 재발행 |
| `InGameScreenChanged` | 인게임 패널 전환 |
| `CompleteTradeRequested` | debug 즉시 완료 요청 |
| `RouteEventForced` | debug ForceRouteEvent `(tradeId, eventId)` |
| `TradeOfflineCompleted` | Offline settle 성공 시 1회 `(tradeId)`. Traveling→SettlementPending 전환 직후. 재로드 시 중복 발행하지 않음 |
| `TimeRollbackDetected` | Continue/Load 시 `CurrentUtc < lastSavedUtcTicks`이면 발행. Offline 적용은 스킵 |

인자는 공유 참조일 수 있으므로 구독자가 함부로 수정하지 않는다.

---

## 11. 디버그 · Smoke

InGame에 이미 배치된 컴포넌트:

- `FrameworkDebugBridge`
- `TradeStartDebugHarness`

### 11-1. FrameworkDebugBridge

| ContextMenu | 용도 |
|-------------|------|
| Set / Reset Time Scale | Unity 배속 |
| Set / Reset In-Game Time Multiplier | 인게임 배율 |
| Pause / Resume In-Game Time | pause |
| Complete Trade Immediately | 즉시 도착 |
| Force Load Completed | LoadCompleted 재발행 |
| Log Shared Game Data Summary | 공용 데이터 요약 |
| Force Season / Disaster / Route Event | 월드 Force* |

### 11-2. TradeStartDebugHarness

| ContextMenu | 용도 |
|-------------|------|
| Fill Sample Caravan | 샘플 카라반 |
| Start Trade And Record Time | 출발 |
| Check Trade Progress And Completion | 진행·정산 |
| Force Complete Active Trade | 즉시 완료 |
| Claim Settlement And Reset | claim |
| Set Low Food Failure Case | 식량 실패 케이스 |
| Run M1 Loop Integrity Smoke | 3사이클 loop |
| Run Economy E2E Smoke | settle/claim 화폐 검증 |
| Run InGame Food Consumption Smoke | 인게임 식량 연동 |
| Run Pause Food Freeze Smoke | Pause 중 식량 elapsed 정지 |
| Run Failed Settlement Screen Smoke | Failed → Settlement → claim → Preparation |
| Run Pending Settlement Restore Smoke | pending 저장 → 캐시 소실 → 복구 → claim |
| Run Offline Progress Smoke | Traveling 오프라인 미완료 · 완료 · 재호출 no-op · 역행 |
| Run Force World Debug Smoke | ForceSeason/Disaster/RouteEvent 일괄 검증 |
| Force Season / Disaster / Route Event | 월드 Force* |
| Print Save Data | JSON 확인 |

Failed smoke는 `foodAmount = 0`(int)과 `starveGraceSeconds = 0f`로 `FoodDepleted`를 즉시 재현한다.  
`CaravanData.foodAmount`는 int이므로 `0f`를 넣지 않는다.

### 11-3. World Force* 요약

| API | 저장 | 조건 |
|-----|------|------|
| ForceSeason | `world.currentSeasonId` + Save | seasonId 필수 |
| ForceDisaster | `world.currentDisasterId` + Save | 빈 문자열 = 재난 해제 |
| ForceRouteEvent | 런타임 pending만 | **Traveling** 필수, Core 적용은 stub |

상세: [`Framework_World_Force_Debug_API_Guide.md`](./Framework_World_Force_Debug_API_Guide.md)

### 11-4. M2 / M3 통합 Smoke 체크리스트

경로: Boot → Title → New Game → Loading → InGame

| 순서 | ContextMenu / 메뉴 | 기대 |
|------|-------------------|------|
| 1 | Editor: `ND/Framework/Run M1 Loop + Economy E2E Checks` | `All checks passed.` (Pending + Offline 포함) |
| 2 | `Run Pause Food Freeze Smoke` | elapsed/food 불변 |
| 3 | `Run Failed Settlement Screen Smoke` | Failed → Settlement → Preparation |
| 4 | `Run Pending Settlement Restore Smoke` | pending 복구 → claim → Preparation |
| 5 | `Run Offline Progress Smoke` | incomplete / complete / re-apply / rollback |
| 6 | `Run Force World Debug Smoke` | Season/Disaster Save + RouteEvent Traveling |

검증 기록:

- M3 Offline: [`m3-offline-progress-pipeline.md`](../Personal_Documents/CSU/m3-offline-progress-pipeline.md)
- M3 Pending: [`m3-pending-settlement-persist.md`](../Personal_Documents/CSU/m3-pending-settlement-persist.md) (2026-07-12 **Pass**)
- M2 Pause/Failed/Force: [`m2-pause-failed-force-smoke.md`](../Personal_Documents/CSU/m2-pause-failed-force-smoke.md) (2026-07-11 **Pass**)
---

## 12. 팀별 빠른 경로

| 팀 | 먼저 볼 것 | 바로 쓰기 |
|----|------------|-----------|
| **UI** | §8 화면 라우팅, §9 정산 | `InGameScreenChanged`, `SettlementUiDataAdapter` |
| **Economy / Progression** | §5 SharedData, §7 Economy | season/disaster는 Force* 또는 Save `world` |
| **Core** | §6 시간, §7 무역 | `elapsedInGameSeconds`는 Framework가 채움; `progress01`은 UTC |
| **Content** | §5 SharedData 카탈로그 | ID가 Save / Force / smoke와 일치하는지 |
| **QA / 통합** | §1 퀵스타트, §11 Smoke | Harness M2 smoke + Editor E2E 메뉴 |

---

## 13. 상세 문서 링크

### Docs/Guide (팀 공용)

| 문서 | 내용 |
|------|------|
| [`Framework_Shared_Game_Data_Guide.md`](./Framework_Shared_Game_Data_Guide.md) | 공용 기준 데이터 |
| [`Framework_InGame_Time_Multiplier_API_Guide.md`](./Framework_InGame_Time_Multiplier_API_Guide.md) | 인게임 시간 배율 |
| [`Settlement_UI_Data_Connection_Guide.md`](./Settlement_UI_Data_Connection_Guide.md) | 정산 UI |
| [`Framework_World_Force_Debug_API_Guide.md`](./Framework_World_Force_Debug_API_Guide.md) | ForceSeason/Disaster/RouteEvent |
| 이 문서 | 통합 사용 설명서 |

### 참고 (개인/상세 로직)

| 문서 | 내용 |
|------|------|
| `Docs/Personal_Documents/CSU/Core-services-M1-sync.md` | M1/M2 아키텍처 동기화 |
| `Docs/Personal_Documents/CSU/M1_Trade_Loop_Integrity.md` | 무역 loop 무결성 |
| `Docs/Personal_Documents/CSU/caravan-ingame-food-sync.md` | 식량·인게임 시간 |
| `Docs/Personal_Documents/CSU/world-force-debug-commands.md` | Force* 구현 로직 |
| `Docs/Personal_Documents/CSU/m2-pause-failed-force-smoke.md` | M2 Pause / Failed / Force* 통합 검증 (Pass) |
| `Docs/Personal_Documents/CSU/m3-pending-settlement-persist.md` | M3 PendingSettlement 영속화·복구 로직 |
| `Docs/Personal_Documents/CSU/m3-offline-progress-pipeline.md` | M3 Traveling 오프라인 복구·완료·역행/상한 |

### 테스트 씬 (선택)

| 씬 | 용도 |
|----|------|
| `11.CoreServices/Scenes/SharedDataTest.unity` | SharedData |
| `11.CoreServices/Scenes/InGameTimeMultiplierTest.unity` | 시간 배율 |
| `11.CoreServices/Scenes/TradeTimingCompletionScene.unity` | 무역 타이밍 |

일반 통합은 **Boot → Title → InGame** 경로를 우선한다.

---

## 하지 말 것

- `FrameworkRoot` / Save / SharedData를 feature마다 복제하지 말 것
- Scene / Prefab YAML을 직접 편집하지 말 것
- UI에서 Core `JourneyRunner`나 Save 화폐를 직접 건드리지 말 것 (정산은 Bridge/Adapter)
- `Time.timeScale`로 식량·월드 시뮬을 맞추려 하지 말 것
- ForceRouteEvent를 Core 로드/약탈 **완전 적용**으로 오해하지 말 것
- AtomicSave / AutoSave 범위를 Pending·Offline smoke에 섞지 말 것
- Offline smoke와 Pending restore smoke를 한 ContextMenu에 섞지 말 것
- v4 세이브를 v5에서 그대로 이어하기 가능하다고 가정하지 말 것
