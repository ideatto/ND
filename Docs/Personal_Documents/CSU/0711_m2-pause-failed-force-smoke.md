# M2 Pause / Failed / Force* 통합 검증 기록

**작성일:** 2026-07-11  
**브랜치:** `chore/integration/m2-pause-failed-force-smoke`  
**Base:** `dev2`  
**Feature root:** `Assets/_Project/11.CoreServices/`  
**목적:** Framework M2 완료 기준 중 Pause 식량 정지 · Failed 정산 화면 · Force* 재현을 smoke로 닫고 결과를 한곳에 기록한다.

---

## 1. M2 완료 기준 매핑

| Milestone 완료 기준 | 검증 수단 | 상태 |
|---------------------|-----------|------|
| 게임 시간이 정지되면 식량 계산용 경과 인게임 시간도 증가하지 않는다 | `Run Pause Food Freeze Smoke` / Editor E2E | **Pass** (2026-07-11) |
| 성공과 실패가 올바른 화면으로 이동한다 (실패 → Settlement) | `Run Failed Settlement Screen Smoke` / Editor E2E | **Pass** (2026-07-11) |
| 디버그 메뉴로 주요 조건을 재현한다 | Force* ContextMenu + `Run Force World Debug Smoke` | **Pass** (2026-07-11) |

관련 milestone: [`Docs/Planning_Milestone/02_Framework_Integration_Milestone.md`](../../Planning_Milestone/02_Framework_Integration_Milestone.md) M2 완료 기준.

---

## 2. Pause 식량 정지

### ContextMenu

`Framework/Run Pause Food Freeze Smoke` (`TradeStartDebugHarness`)

### 절차

1. Trade start + `SetActiveCaravan`
2. baseline `CheckProgress` → `elapsed0`, `food0`
3. `PauseGameTime()`
4. `BackdateActiveTradeStart(30s)` 후 `CheckProgress`
5. 통과: check=`false`, elapsed/food 불변
6. `ResumeGameTime()`

### 통과 로그 (기대)

```text
Pause food freeze smoke passed. Elapsed held at ...s, Food held at ....
[Framework M1 E2E] Pause food freeze passed. ...
```

### 실패 시 증상

- Pause 중에도 elapsed/food가 증가·감소 → `TradeProgressCoordinator` pause early-return 또는 `GameTimeService.IsGameTimePaused` 연동 문제

### Editor

`FrameworkM1LoopE2EEditorTests.RunPauseFoodFreezeE2E` — `ND/Framework/Run M1 Loop + Economy E2E Checks`에 포함.

---

## 3. Failed 정산 화면

### ContextMenu

`Framework/Run Failed Settlement Screen Smoke`

### 절차

1. `foodAmount=0`(int), `starveGraceSeconds=0f`로 `FoodDepleted` fatal 즉시 재현
2. Trade start → backdate 1s → `CheckProgress` (ForceComplete 성공 경로 미사용)
3. 통과 조건:
   - `LastSettlementResult.grade == Failed`
   - `failureReason == FoodDepleted`
   - `tradeProgress.state == SettlementPending`
   - screen `Settlement`
   - `SettlementViewData.IsFailed == true`
4. claim 성공, 중복 claim 실패
5. post-claim: `tradeProgress.state == Failed`, caravan `Prepare`, screen `Preparation`

### 통과 로그 (기대)

```text
Failed settlement screen smoke passed. Failed grade -> Settlement -> claim -> Preparation/Failed state.
[Framework M1 E2E] Failed settlement screen passed. ...
```

### 실패 시 증상

- grade가 Success/Partial → 식량 fatal이 아닌 도착 정산 경로
- Settlement 미진입 → `SettleActiveTrade` / router 미연결
- claim 후 state ≠ Failed → `MarkFailed` 미기록

---

## 4. Force* 통합 기록

API 구현은 [`feature/framework/world-force-debug-commands`](world-force-debug-commands.md)에서 완료. 본 작업은 **재현 smoke + 결과 기록**만 추가한다.

### 참조 가이드

- [`Docs/Guide/Framework_World_Force_Debug_API_Guide.md`](../../Guide/Framework_World_Force_Debug_API_Guide.md)
- [`Docs/Personal_Documents/CSU/0711_world-force-debug-commands.md`](0711_world-force-debug-commands.md)

### ContextMenu

| 메뉴 | 역할 |
|------|------|
| `Framework/Force Season` | `WorldSaveData.currentSeasonId` + Save |
| `Framework/Force Disaster` | `WorldSaveData.currentDisasterId` + Save |
| `Framework/Force Route Event` | Traveling 1회 inject hook |
| `Framework/Run Force World Debug Smoke` | Season/Disaster 저장 assert + RouteEvent Traveling 전/후 |

### Force World Debug Smoke 기대 결과

1. Season → Inspector `debugSeasonId` (기본 `winter`)가 Save에 반영
2. Disaster → `debugDisasterId` (기본 `drought`) 반영
3. Traveling **전** ForceRouteEvent → `false`
4. Trade start 후 ForceRouteEvent → `true`, `TryConsumeForcedRouteEvent`로 ID 소모

### 수동 체크리스트 (Play)

```text
Boot → Title → New Game → Loading → InGame
```

| 순서 | 동작 | 기대 |
|------|------|------|
| 1 | Force Season | 로그 + Print Save `currentSeasonId` |
| 2 | Force Disaster | 로그 + `currentDisasterId` |
| 3 | Force Route Event (무역 전) | Traveling required Warning |
| 4 | Fill Sample → Start Trade | Traveling |
| 5 | Force Route Event | `RouteEventForced` + inject hook 로그 |
| 6 | (선택) Run Force World Debug Smoke | 한 번에 동일 assert |

### 실행 결과 (이번 PR)

| 항목 | 결과 |
|------|------|
| ForceSeason / ForceDisaster / ForceRouteEvent 코드 경로 | 기존 구현 유지 (재구현 없음) |
| `Run Force World Debug Smoke` | **Pass** (Play ContextMenu) |
| Unity Play 수동 Force Season/Disaster/Route | **Pass** |
| Editor batchmode Force smoke | 미포함 (`FrameworkRoot` 의존) — Play harness로 검증 |

---

## 5. 통합 실행 결과 (2026-07-11)

검증자: 로컬 Unity Editor / Play Mode  
브랜치: `chore/integration/m2-pause-failed-force-smoke`  
Console 오류: 없음 (`foodAmount` int 할당 수정 후 CS0266 해소)

### 5-1. Editor E2E

메뉴: `ND/Framework/Run M1 Loop + Economy E2E Checks`

| 항목 | 결과 |
|------|------|
| Loop integrity 3사이클 | Pass |
| Economy E2E 3사이클 | Pass |
| InGame food consumption E2E | Pass |
| Pause food freeze E2E | Pass |
| Failed settlement screen E2E | Pass |
| 전체 `All checks passed.` | Pass |

### 5-2. Play Mode (`TradeStartDebugHarness`)

경로: Boot → Title → New Game → Loading → InGame

| ContextMenu | 결과 |
|-------------|------|
| `Run Pause Food Freeze Smoke` | Pass |
| `Run Failed Settlement Screen Smoke` | Pass |
| `Run Force World Debug Smoke` | Pass |
| Force Season / Disaster / Route Event (수동) | Pass |

### 5-3. 참고

- Failed smoke의 `CaravanData.foodAmount`는 **int** (`0`). `0f` 할당은 CS0266을 유발하므로 사용하지 않는다.
- Editor E2E에 Force World smoke는 포함하지 않는다. Force*는 Play harness로 검증한다.

---

## 6. 변경 파일

| 경로 | 변경 |
|------|------|
| `Scripts/Debug/TradeStartDebugHarness.cs` | Pause / Failed / Force World smoke ContextMenu |
| `Editor/FrameworkM1LoopE2EEditorTests.cs` | Pause / Failed Editor 회귀 |
| `Docs/Personal_Documents/CSU/0711_m2-pause-failed-force-smoke.md` | 본 문서 |
| `Docs/Guide/Framework_CoreServices_Team_Usage_Guide.md` | 팀 가이드 smoke 메뉴·검증 경로 |
| `Docs/Policy/GitRules.md` | 기본 통합 브랜치 `dev2` 명시 |
| `.cursor/rules/git-base-branch-dev2.mdc` | Agent alwaysApply base=`dev2` |
| `Docs/Personal_Documents/CSU/0711_user-rule-dev2-base-branch-patch.md` | User Rules 수동 패치 안내 |

---

## 7. 범위 밖

- Core 로드/약탈 실제 적용, `expectedTradeEndUtcTick` 갱신
- 계절·재난 자동 시간 전환
- M3 `PendingSettlementSaveData` / 오프라인 정산 복구
- Scene / Prefab YAML 직접 편집
- Force* API 재구현

---

## 8. 검증 방법 (리뷰어 재현)

1. Unity: `ND/Framework/Run M1 Loop + Economy E2E Checks`
2. Play: Boot → InGame → `TradeStartDebugHarness`에서  
   - `Run Pause Food Freeze Smoke`  
   - `Run Failed Settlement Screen Smoke`  
   - `Run Force World Debug Smoke`
3. Console에 각 `... passed` 로그 확인
