# 원자 Claim · Town 라우팅 구현 로직

- 작성일: 2026-07-21
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/atomic-claim-town-routing`
- 기준 브랜치: `dev2`
- 상태: 구현·Editor E2E 검증 완료
- 관련 인계: `Docs/Personal_Documents/CSU/Handoff/07-21_atomic_claim_town_routing_economy_handoff.md`

## 1. 목적

정산 claim을 **한 번의 원자 저장 단위**로 처리하고, 성공 시 플레이어 위치를 목적지 마을로 갱신한 뒤 **Town 화면**으로 전환한다.

이전 claim 동작의 문제:

- claim 중 저장 실패해도 런타임/SaveData가 부분 변경된 채로 남을 수 있었다.
- claim 성공 후 화면이 `Preparation`으로 돌아가 마을 도착 UX가 없었다.
- 목적지 town ID를 claim 시점에 검증·반영하지 않았다.

이번 브랜치가 담당하는 범위:

- claim 전 destination 검증 (`tradePreparationCommit` ↔ `SharedGameData` route)
- claim staging 전체를 snapshot 후, 실패 시 전량 원복
- `ISaveService.Save` 성공 여부로 커밋 확정
- `player.currentTownId` 갱신 + `InGameScreenState.Town` 전환
- 재실행 시 Town 화면 복원 규칙
- Editor E2E로 저장 실패·정상 claim·중복 claim·재실행 복원 검증

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `TradeProgressCoordinator.cs` | 원자 claim, destination 검증, snapshot rollback, Town 전환 |
| `InGameScreenState.cs` | `Town` enum 추가 |
| `InGameScreenStateRouter.cs` | Completed/Failed + 정리 완료 시 Town 매핑 |
| `FrameworkRoot.cs` | Coordinator에 `TradePrepareCommitStore`를 Source/Completion으로 주입 |
| `FrameworkM1LoopE2EEditorTests.cs` | Atomic claim E2E, post-claim Town 기대값, 실제 route ID 사용 |

## 3. 책임 분리

```text
Trade Prepare (LJH / UI)
  └─ TradePrepareCommitData stage
     (tradeId, selectedDestinationTownId, routeId, …)

SharedGameData
  └─ Route.ToTownId 조회 (destination 일치 검증 기준)

TradeProgressCoordinator
  └─ claim 검증 → stage → Save → Town 전환
  └─ 실패 시 SaveData + runtime caravan snapshot 원복

EconomyM1SettlementBridge
  └─ settle 시 금액 preview / claim 시 currency·stat 반영
  └─ apply 실패도 claim rollback 대상

InGameScreenStateRouter / UI
  └─ Town 화면 구독 (FrameworkEvents.InGameScreenChanged)
```

## 4. Claim 원자 흐름

### 4.1 성공 경로

```text
ClaimSettlementAndReset()
  ├─ CanClaimCachedSettlement
  ├─ TryResolveClaimDestination  → destinationTownId
  ├─ snapshot(SaveData, runtime caravan)
  ├─ JourneyRunner.ClaimSettlement
  ├─ EconomyM1SettlementBridge.TryApplyPendingEconomy
  ├─ MarkCompleted / MarkFailed
  ├─ JourneyRunner.ResetToPrepare
  ├─ player.currentTownId = destinationTownId
  ├─ PendingSettlement clear
  ├─ TradePrepareCommitCompletion.TryComplete(tradeId)
  ├─ CaravanSaveDataMapper.CopyToSave
  ├─ saveService.Save  → Succeeded 필수
  ├─ ClearSettlementCache
  └─ RequestScreen(Town)  → InGameScreenChanged(Town)
```

어느 단계든 실패하면 `RestoreClaimSnapshot`으로 claim 직전 상태로 되돌리고 `false`를 반환한다.  
**화면은 Town으로 바꾸지 않는다.**

### 4.2 Destination 검증 (`TryResolveClaimDestination`)

필수 조건:

1. `player` 존재
2. `ITradePrepareCommitSource.TryGet(activeTradeId)` 성공
3. `commit.selectedDestinationTownId` 비어 있지 않음
4. `tradeProgress.activeRouteId` 비어 있지 않음
5. `SharedGameData.TryGetRoute(activeRouteId)` 성공, `route.ToTownId` 존재
6. `selectedDestinationTownId == route.ToTownId` (Ordinal)

불일치·누락 시 claim을 **시작하지 않는다** (snapshot 전 차단).

### 4.3 저장 실패 rollback

`saveService.Save`가 null이거나 `Succeeded == false`이면:

- SaveData JSON snapshot 복구
- runtime caravan JSON snapshot 복구
- `LastSettlementResult`가 있으면 Economy pending을 `TryCalculateAndFill`로 재구성
- `SettlementPending` / pendingSettlement / commit 유지
- 화면은 Settlement 유지

즉 UI에서 claim을 다시 누를 수 있는 상태가 보존된다.

## 5. 화면 상태

### 5.1 `InGameScreenState.Town`

정산 claim과 목적지 위치 저장이 완료된 마을 화면이다.

### 5.2 `MapFromSaveData` 규칙

`tradeProgress.state`이 `Completed` 또는 `Failed`일 때:

| 조건 | 결과 |
|---|---|
| pendingSettlement 정리됨 **그리고** tradePreparationCommit 정리됨 **그리고** `currentTownId` 비어 있지 않음 | `Town` |
| 위 조건 미충족 | `Preparation` (안전 폴백) |

재실행(로드 후 `RefreshFromSaveData`)도 같은 규칙을 쓴다.  
claim이 정상 저장된 세이브는 Town으로 복원된다.

### 5.3 이벤트

성공 claim 직후:

```text
FrameworkEvents.InGameScreenChanged(InGameScreenState.Town)
```

구독자는 중복 발행에 대비해야 한다 (`forceNotify` 또는 씬 재진입 시 재발행 가능).

## 6. 이전 동작과의 차이

| 항목 | 이전 | 현재 |
|---|---|---|
| claim 후 화면 | `Preparation` | `Town` |
| `currentTownId` | claim에서 갱신 안 함 | destination으로 갱신 |
| Economy apply 실패 | 경고 후 claim 계속 가능했음 | **rollback + false** |
| commit complete 실패 | 경고만 | **rollback + false** |
| Save 실패 | 무시하고 진행 가능 | **rollback + false** |
| destination 검증 | 없음 | commit ↔ route 필수 |

## 7. 조립 (`FrameworkRoot`)

```csharp
TradePrepareCommitStore = new FrameworkTradePrepareCommitStore(() => CurrentSaveData);
TradeProgressCoordinator = new TradeProgressCoordinator(
    ...,
    TradePrepareCommitStore,  // ITradePrepareCommitCompletion
    TradePrepareCommitStore); // ITradePrepareCommitSource
```

동일 store가 Sink / Source / Completion을 모두 구현한다.  
**출발 전 `TryStage`가 되어 있지 않으면 claim이 차단된다.**

## 8. 검증

Editor 메뉴: `ND/Framework/Run M1 Loop + Economy E2E Checks`

Atomic claim 체크리스트 (E2E 내 `RunAtomicSettlementClaimE2E`):

1. 저장 실패 강제 → 상태 원복
2. 정상 claim → `currentTownId` = route.ToTownId (`RiverTown` / `BaseToRiver`)
3. Town 이벤트 발행
4. 중복 claim 거부
5. 새 router로 재실행 시 Town 복원
6. destination mismatch 시 claim 거부·상태 불변

참고: E2E `RouteId`는 SharedGameData catalog에 실제 존재하는 `BaseToRiver`를 사용한다.  
(`BaseRoute`는 catalog에 없어 destination 검증이 실패한다.)

## 9. 알려진 주의점

- Play Mode `TradeStartDebugHarness` 기본 `routeId`가 `BaseRoute`이면 claim destination 검증에 실패할 수 있다. 실제 route ID로 맞춰야 한다.
- `MapFromTradeProgressState(Completed/Failed)`는 여전히 `Preparation`을 반환한다. **재실행 복원은 `MapFromSaveData`를 써야 한다.**
- Town UI panel 자체는 이번 브랜치 범위 밖이다. Framework는 상태·이벤트만 제공한다.

## 10. 관련 문서

- Settlement claim 연결: `Docs/Personal_Documents/CSU/0715_settlement_claim_framework_connection.md`
- Pending settlement 영속: `Docs/Personal_Documents/CSU/0712_m3-pending-settlement-persist.md`
- Economy 후속 handoff: `Docs/Personal_Documents/CSU/Handoff/07-21_atomic_claim_town_routing_economy_handoff.md`
