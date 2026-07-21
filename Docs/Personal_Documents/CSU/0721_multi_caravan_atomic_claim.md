# Multi-Caravan Atomic Claim 구현 로직

- 작성일: 2026-07-21
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/multi-caravan-atomic-claim`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현·Editor E2E 검증 완료(커밋 전)
- 선행 작업:
  - `feature/framework/atomic-claim-town-routing` — 원자 claim + Town 라우팅
  - `feature/framework/multi-caravan-save-cutover` — SaveData v6 컬렉션 컷오버
- 관련 문서:
  - `Docs/Personal_Documents/CSU/0721_atomic_claim_town_routing.md`
  - `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
  - `Docs/Personal_Documents/CSU/Handoff/07-21_atomic_claim_town_routing_economy_handoff.md`
  - `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`

---

## 1. 목적

SaveData v6 multi-caravan 스키마 위에서, 기존 원자 claim을 **`(caravanId, tradeId)` 명시 키**로 수행하도록 전환한다.

선행 원자 claim(`ClaimSettlementAndReset`)은 `selectedCaravanId` 호환 접근자에 의존했다.  
컬렉션 컷오버 이후에는 다음이 필요하다.

1. claim 대상을 `selectedCaravanId` 암시가 아니라 **caravan + trade ID로 지정**
2. pending settlement를 `pendingSettlements[]`에서 **정확한 항목만 제거**
3. settle 이벤트를 `(caravanId, tradeId, result)`로 전달해 bridge cache가 올바른 caravan을 추적
4. 실패 사유를 `bool`이 아닌 **열거형 + SaveResult**로 반환해 호출부가 분기 가능
5. 기존 원자성(snapshot rollback, destination 검증, Town 전환)은 유지

이번 브랜치 범위:

- `ClaimSettlement(caravanId, tradeId)` 공개 API
- `ClaimSettlementResult` / `ClaimSettlementFailureReason`
- `TradeSettlementReady` 시그니처를 `(caravanId, tradeId, JourneyResultData)`로 확장
- `SettlementUiBridge` pending cache에 `pendingCaravanId` 추가
- UI adapter / debug harness / Editor E2E를 새 API에 맞춤
- multi-caravan null 호환 접근자에 맞춘 E2E null-safe 검증

이번 범위에 **포함하지 않는 것**:

- 동시에 여러 caravan이 Traveling/SettlementPending인 완전 multi-active runtime UI
- Town UI panel 자체 구현
- Economy 계산기 변경
- SaveData schema version 추가 변경 (v6 유지)

---

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `TradeProgressCoordinator.cs` | `ClaimSettlement` / Result / FailureReason, legacy wrapper Obsolete화 |
| `FrameworkEvents.cs` | `TradeSettlementReady(caravanId, tradeId, result)` |
| `FrameworkRoot.cs` (`SettlementUiBridge`) | pending caravanId cache, claim 시 명시 ID 전달 |
| `SettlementUiDataAdapter.cs` | bridge 새 `TryGetPendingSettlement` 시그니처 대응 |
| `TradeStartDebugHarness.cs` | pending restore smoke에서 caravanId 검증 |
| `FrameworkM1LoopE2EEditorTests.cs` | Atomic claim E2E를 새 API·null-safe assertion으로 갱신 |

---

## 3. 책임 분리

```text
SaveData v6
  └─ caravans[] / tradeProgressEntries[] / pendingSettlements[]
  └─ selectedCaravanId (선택 포인터, claim 키의 기본값이 될 수 있음)

TradeProgressCoordinator.ClaimSettlement(caravanId, tradeId)
  └─ 컬렉션에서 대상 caravan / progress / pending 조회
  └─ 원자 stage → Save → Town
  └─ ClaimSettlementResult 반환

FrameworkEvents.TradeSettlementReady
  └─ (caravanId, tradeId, JourneyResultData)

SettlementUiBridge
  └─ pendingCaravanId + pendingTradeId + pendingResult cache
  └─ Claim 시 Coordinator.ClaimSettlement(pendingCaravanId, pendingTradeId)

UI / Debug / E2E
  └─ Succeeded / FailureReason / SaveResult로 분기
```

---

## 4. 공개 API

### 4.1 `ClaimSettlementResult`

| 멤버 | 의미 |
|---|---|
| `Succeeded` | Economy apply·상태 기록·Save·Town 전환까지 모두 성공 |
| `FailureReason` | 실패 단계. 성공 시 `None` |
| `SaveResult` | Save를 시도한 경우의 저장 결과. Save 이전 실패면 `null`일 수 있음 |

팩토리:

- `ClaimSettlementResult.Success(SaveResult)`
- `ClaimSettlementResult.Failure(ClaimSettlementFailureReason, SaveResult = null)`

### 4.2 `ClaimSettlementFailureReason`

| 값 | 의미 |
|---|---|
| `InvalidCaravanId` / `InvalidTradeId` | 인자 누락 |
| `CaravanNotFound` | `caravans[]`에 대상 없음 |
| `TradeProgressNotFound` | `tradeProgressEntries[]`에 대상 없음 |
| `PendingSettlementNotFound` | `(caravanId, tradeId)` pending 없음 |
| `AmbiguousPendingSettlement` | 동일 키 pending이 2개 이상 (데이터 손상) |
| `TradeIdMismatch` | progress.activeTradeId ≠ tradeId |
| `InvalidTradeState` | state ≠ `SettlementPending` |
| `AlreadyClaimed` | pending.claimed == true |
| `SettlementDataInvalid` | pending → runtime 변환 실패 또는 caravan runtime 부재 |
| `TownApplyFailed` | destination 검증 또는 commit complete 실패 |
| `EconomyApplyFailed` | Economy calculate/apply 실패 |
| `CoreClaimRejected` | JourneyRunner claim/reset 거부 |
| `SaveFailed` | `ISaveService.Save` 실패 (SaveResult 동봉) |
| `RollbackFailed` | (예약) snapshot 원복 실패용 |

### 4.3 `ClaimSettlement(caravanId, tradeId)`

성공 경로 요약:

```text
ClaimSettlement(caravanId, tradeId)
  ├─ ID / caravan / progress / pending 조회·검증
  ├─ TryResolveClaimDestination (commit ↔ route.ToTownId)
  ├─ snapshot(SaveData, runtime caravan)
  ├─ selectedCaravanId를 claim 대상 caravan으로 임시 전환
  ├─ JourneyRunner.ClaimSettlement
  ├─ Economy TryCalculateAndFill + TryApplyPendingEconomy
  ├─ progress.state = Completed | Failed
  ├─ JourneyRunner.ResetToPrepare
  ├─ player.currentTownId = destinationTownId
  ├─ TradePrepareCommitCompletion.TryComplete(tradeId)
  ├─ pendingSettlements.Remove(exact pending)
  ├─ CaravanSaveDataMapper.CopyToSave
  ├─ selectedCaravanId 복원
  ├─ saveService.Save → Succeeded 필수
  ├─ (필요 시) ClearSettlementCache
  └─ RequestScreen(Town)
```

어느 stage든 실패하면 `RestoreClaimSnapshot`으로 claim 직전 상태로 되돌리고  
해당 `FailureReason`을 담은 `ClaimSettlementResult`를 반환한다.  
**화면은 Town으로 바꾸지 않는다.**

### 4.4 호환 API

```csharp
[Obsolete("Use ClaimSettlement(caravanId, tradeId).")]
public bool ClaimSettlementAndReset()
```

동작: `selectedCaravanId`의 pending을 찾아 `ClaimSettlement(...).Succeeded`만 반환한다.  
신규 호출부는 Result API를 사용해야 한다.

구 구현 본문은 `ClaimSettlementAndResetLegacy()`로 남겨 두었으나,  
공개 진입은 Obsolete wrapper → 새 `ClaimSettlement` 경로다.

---

## 5. 이벤트·Bridge 변경

### 5.1 `FrameworkEvents.TradeSettlementReady`

| 이전 | 현재 |
|---|---|
| `Action<string, JourneyResultData>` `(tradeId, result)` | `Action<string, string, JourneyResultData>` `(caravanId, tradeId, result)` |

발행 지점(Coordinator settle / restore)도 caravanId를 함께 올린다.

### 5.2 `SettlementUiBridge`

| 항목 | 내용 |
|---|---|
| cache | `pendingCaravanId`, `pendingTradeId`, `pendingResult` |
| `TryGetPendingSettlement` | `(out caravanId, out tradeId, out result)` |
| `ClaimSettlementAndReset` | pending 검증 후 `ClaimSettlement(pendingCaravanId, pendingTradeId).Succeeded` |
| 화면 진입 검증 | `SaveDataLookup.TryGetTradeProgress(saveData, caravanId, ...)`로 caravan별 state/tradeId 확인 |

---

## 6. 선행 원자 Claim과의 차이

| 항목 | atomic-claim-town-routing | 이번 브랜치 |
|---|---|---|
| claim 식별 | selected caravan 암시 | `(caravanId, tradeId)` 명시 |
| pending 정리 | 호환 property clear | `pendingSettlements.Remove(exact)` |
| 반환값 | `bool` | `ClaimSettlementResult` |
| settle 이벤트 | `(tradeId, result)` | `(caravanId, tradeId, result)` |
| 원자성·Town 라우팅 | 유지 | 유지 |
| destination 검증 | 유지 | 유지 (progress의 activeRouteId 사용) |
| selectedCaravanId | claim 중 변경 없음 | claim 중 임시 전환 후 Save 전 복원 |

`selectedCaravanId` 임시 전환 이유:

- Economy / 호환 접근자 / ActiveCaravan 경로가 선택 caravan을 읽는 구간이 남아 있다.
- claim 대상이 선택 caravan과 다를 때를 대비해 staging 구간만 맞춘 뒤, Save 직전에 원래 선택을 복원한다.

---

## 7. E2E 검증

Editor 메뉴: `ND/Framework/Run M1 Loop + Economy E2E Checks`

### 7.1 Atomic claim (`RunAtomicSettlementClaimE2E`)

1. `ClaimSettlement` + 저장 실패 강제 → `FailureReason.SaveFailed`, 상태 원복
2. 정상 claim → `Succeeded` + `SaveResult.Succeeded`
3. `currentTownId` = route destination (`RiverTown` / `BaseToRiver`)
4. `InGameScreenChanged(Town)` 발행
5. 중복 claim 거부 (`ClaimSettlementAndReset` / 재호출)
6. 재실행 router가 Town 복원

null-safe 보강:

- `tradeProgress == null`일 때 Traveling 판정 NRE 방지 (Rescue Loan rollback 체크)
- claim 성공 후 `pendingSettlement == null`을 “정리됨”으로 인정 (컬렉션 remove 후 호환 getter가 null)

### 7.2 검증 결과 (2026-07-21)

| 항목 | 결과 |
|---|---|
| Editor 스크립트 컴파일 | 통과 (Console compile error 0) |
| Full E2E | **PASS** — `[Framework M1 E2E] All checks passed.` |
| Atomic claim 세부 로그 | save-fail rollback / normal claim / duplicate reject / relaunch Town 모두 확인 |

의도적 Error 로그(실패 아님):

- orphan trade progress / pending settlement preserved (multi-caravan Normalize 검증)
- pending settlement corrupt restore blocked 케이스

---

## 8. 호출부 가이드

### 권장

```csharp
var result = coordinator.ClaimSettlement(caravanId, tradeId);
if (!result.Succeeded)
{
    // result.FailureReason / result.SaveResult 로 분기
    return;
}
// Town 화면·currentTownId는 Framework가 처리
```

### 비권장

- 신규 코드에서 `ClaimSettlementAndReset()`만으로 실패 사유를 구분하는 것
- settle 이벤트에서 tradeId만 보고 caravan을 추정하는 것
- claim 성공 후 `pendingSettlement.hasResult`를 null 검사 없이 읽는 것

---

## 9. 알려진 주의점

- `ClaimSettlementAndResetLegacy`는 파일에 남아 있을 수 있으나 공개 경로는 Obsolete wrapper다. 신규 로직은 `ClaimSettlement`만 본다.
- claim 중 `selectedCaravanId`가 잠시 바뀔 수 있다. Save 실패 rollback은 snapshot으로 함께 원복된다.
- `pendingSettlements` 중복 키는 `AmbiguousPendingSettlement`로 거부한다. Normalize는 orphan을 삭제하지 않고 로그만 남긴다.
- Town UI·완전 multi-active caravan 동시 진행 UX는 후속 작업이다.
- Economy apply 실패 시 전체 claim rollback 계약은 선행 원자 claim과 동일하다.

---

## 10. 후속 후보

1. UI/외부 호출부를 Obsolete `ClaimSettlementAndReset`에서 Result API로 완전 이전
2. settle/claim 이벤트를 소비하는 Economy·UI 문서를 `(caravanId, tradeId)` 기준으로 갱신
3. 비선택 caravan claim에 대한 Play Mode smoke 추가
4. `ClaimSettlementAndResetLegacy` 제거 시점 결정

---

## 11. 관련 문서

- 원자 Claim · Town 라우팅: `Docs/Personal_Documents/CSU/0721_atomic_claim_town_routing.md`
- Multi-Caravan Save Cutover: `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
- Economy handoff: `Docs/Personal_Documents/CSU/Handoff/07-21_atomic_claim_town_routing_economy_handoff.md`
- Multi-Caravan 아키텍처 계약: `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`
- Settlement claim 연결: `Docs/Personal_Documents/CSU/0715_settlement_claim_framework_connection.md`
- Pending settlement 영속: `Docs/Personal_Documents/CSU/0712_m3-pending-settlement-persist.md`
)
