# M3 Offline Progress Pipeline

**작성일:** 2026-07-12  
**브랜치:** `feature/framework/offline-progress-pipeline`  
**Base:** `dev2`  
**Feature root:** `Assets/_Project/11.CoreServices/`  
**목적:** Continue/Load 시 Traveling 무역의 오프라인 경과·식량 복구와 완료→`pendingSettlement` 저장을 연결하고, 역행 감지·최대 오프라인 상한을 적용한다.

---

## 1. 배경

PendingSettlement 영속화([`m3-pending-settlement-persist.md`](m3-pending-settlement-persist.md))는 **이미 SettlementPending인 세션**만 복구한다.

Traveling 중 종료하면:

```text
Traveling 저장
→ Continue
→ (이전) RestorePendingSettlement no-op
→ Traveling 화면만 복귀
→ CheckProgress가 돌 때까지 경과·식량이 로드 시점에 확정되지 않음
→ 오프라인 완료여도 Settlement로 바로 가지 않을 수 있음
```

본 파이프라인은 Loading 완료 시점에 Traveling을 한 번 해석한다.

---

## 2. 핵심 규칙

| 규칙 | 내용 |
|------|------|
| 배율 | `inGameTimeMultiplierAtStart` (출발 스냅샷). 온라인과 동일 |
| 경과 공식 | `(evaluationUtc - tradeStartUtc) × multiplier` — **절대값 overwrite** |
| 상한 | `evaluationUtc = min(loadUtc, lastSavedUtc + maxOfflineRealSeconds)` |
| 역행 | `loadUtc < lastSavedUtc` → `TimeRollbackDetected`, 적용 스킵 |
| 완료 | 도착/fatal → `SettleActiveTrade` → `TradeOfflineCompleted` 1회 |
| Pause | Save에 영속화하지 않음. 재실행 시 pause 해제된 것으로 취급 |

기본 상한: `InGameTimePolicyConfig.maxOfflineRealSeconds = 259200` (72시간).

---

## 3. 시퀀스

호출 시점: `FrameworkRoot.CompleteLoadingAndEnterGame`  
조건: SharedGameData 로드 이후

```text
ApplyOfflineProgressOnLoad(CurrentSaveData)
  state ≠ Traveling → false (no-op)
  역행 → RaiseTimeRollbackDetected → false
  SyncElapsed + SetProgress(evaluationUtc)
  미도착 → Save → false
  도착/fatal → SettleActiveTrade
    → pendingSettlement + SettlementPending (동일 저장 단위)
    → RaiseTradeSettlementReady
    → RaiseTradeOfflineCompleted(tradeId)
    → true

RestorePendingSettlement(CurrentSaveData)
  (오프라인 settle 직후 또는 이전 세션 SettlementPending)

RefreshFromSaveData → RaiseLoadCompleted → InGame
```

### 중복 적용 방지

- elapsed/food는 delta 누적이 아니라 tradeStart 기준 절대값
- 완료 후 state가 SettlementPending이므로 Traveling 분기 재진입 없음
- `TradeOfflineCompleted`는 offline settle 성공 경로에서만 발행

---

## 4. 주요 API · 파일

| 경로 | 역할 |
|------|------|
| `TradeProgressCoordinator.ApplyOfflineProgressOnLoad` | Load 훅 |
| `GameTimeService.TryResolveOfflineEvaluationUtc` | 역행·상한 |
| `InGameTimeConversionPolicy.GetOfflineElapsedInGameSeconds` | 경과 공식 |
| `InGameTimePolicyConfig.maxOfflineRealSeconds` | 상한 config |
| `FrameworkRoot.CompleteLoadingAndEnterGame` | Offline → Pending 순서 |
| `FrameworkEvents.TradeOfflineCompleted` | 오프라인 완료 통지 |
| `FrameworkEvents.TimeRollbackDetected` | 역행 통지 |

---

## 5. 상세 테스트 계획

> Play smoke는 **하나의 `CurrentSaveData`**를 연속 사용한다.  
> 따라서 Case B는 새 `TryStartTrade`를 하지 않고 Case A Traveling을 재사용하며,  
> Case D는 claim으로 SettlementPending을 비운 뒤 새 Traveling을 시작한다.

### T1 — Editor 회귀

1. `ND/Framework/Run M1 Loop + Economy E2E Checks`
2. 기존 Pending/Pause/Failed 통과 유지
3. Offline incomplete / complete / rollback 통과 (각 case는 별도 `TestContext`)
4. Console: `All checks passed.`

### T2 — Play Offline Smoke

1. Boot → Title → **New Game** → InGame (이전 Traveling 잔여 상태 제거)
2. `TradeStartDebugHarness` → `Framework/Run Offline Progress Smoke`
3. 통과 로그: `Offline progress smoke passed.`

| Case | 절차 | 기대 |
|------|------|------|
| A 미완료 | 새 무역 출발 → start 10s backdate → `ApplyOfflineProgressOnLoad` | Traveling 유지, elapsed 증가, `TradeOfflineCompleted` 0 |
| B 완료 | **같은** Traveling의 `expectedTradeEndUtcTick`을 과거로 → 재 Apply | SettlementPending + pending.hasResult + 동일 tradeId, OfflineCompleted 1 |
| C 재호출 | SettlementPending에서 Apply 재호출 | no-op, OfflineCompleted 추가 없음 |
| D 역행 | `ClaimSettlementAndReset` → 새 Traveling 출발 → `lastSaved`를 미래로 → Apply | `TimeRollbackDetected` 1, Traveling·식량·elapsed 불변 |

실패 시 흔한 원인:

- Case B에서 새 `TryStartTrade` 시도 → `Trade start time was not overwritten` (수정 전 버그)
- New Game 없이 Continue 상태에서 실행 → 기존 Traveling과 충돌

### T3~T6 — 수동 (선택, 이번 브랜치 필수 아님)

이번 브랜치 완료 기준으로 **T1 + T2 Pass면 충분**하다.  
T3~T6은 Continue 실경로·체감 확인용이며, 아래처럼 T1/T2·기존 Pending smoke로 대체한다.

| 항목 | 대체 검증 | 이번 실행 |
|------|-----------|-----------|
| T3 수동 Continue (미완료) | T2 Case A + `ApplyOfflineProgressOnLoad` | **미실행 (스모크로 대체)** |
| T4 수동 Continue (오프라인 완료) | T2 Case B | **미실행 (스모크로 대체)** |
| T5 역행 | T2 Case D | **미실행 (스모크로 대체)** |
| T6 Pending 경계 | T1 Pending E2E + `Run Pending Settlement Restore Smoke` (Pass) | **미실행 (스모크로 대체)** |

시간 여유가 있으면 T3 또는 T4 중 하나만 수동 Continue로 보면 된다.

<details>
<summary>T3~T6 수동 절차 (참고용)</summary>

**T3 — 수동 Continue (미완료)**

1. New Game → 무역 출발 → Title 복귀(저장)
2. 수분 대기 또는 harness로 start backdate 후 Continue
3. Traveling 화면, 식량/경과가 온라인과 동일 배율로 반영
4. 재 Continue → 이중 소모 없음

**T4 — 수동 Continue (오프라인 완료)**

1. Traveling 저장에서 `expectedTradeEndUtcTick`을 과거로 만든 뒤 Continue
2. Settlement 진입, pending과 동일 tradeId/결과
3. Claim 1회 성공 → 재실행 후 중복 보상 없음
4. Console에 `TradeOfflineCompleted` 1회

**T5 — 역행**

1. Traveling 상태에서 `lastSavedUtcTicks`를 미래로 조작 후 Apply/Continue
2. `TimeRollbackDetected` 로그, Traveling·식량 동결

**T6 — Pending 경계**

1. SettlementPending에서 Title → Continue → Offline Traveling 분기 미진입
2. `Run Pending Settlement Restore Smoke` Pass

</details>

---

## 6. 실행 결과

**검증일:** 2026-07-12  
**브랜치:** `feature/framework/offline-progress-pipeline`  
**검증자:** 로컬 Unity Editor / Play Mode

| 항목 | 결과 |
|------|------|
| T1 Editor Offline E2E | **Pass** (2026-07-12) |
| T2 Play Offline Smoke | **Pass** (2026-07-12) |
| T3 수동 Continue (미완료) | **미실행 (스모크로 대체)** |
| T4 수동 Continue (오프라인 완료) | **미실행 (스모크로 대체)** |
| T5 역행 | **미실행 (스모크로 대체)** — T2 Case D로 커버 |
| T6 Pending 경계 | **미실행 (스모크로 대체)** — Pending Restore Smoke / T1 Pending E2E Pass로 커버 |
| Console 오류 | 없음 (Case D 역행 Warning/Info는 정상) |

### T2 통과 로그 (요약)

```text
TimeRollbackDetected event raised.
Offline progress skipped because load UTC is earlier than lastSavedUtcTicks.
Offline progress smoke passed. Incomplete elapsed advanced (food was 30), complete settled once, re-apply no-op, rollback skipped.
```

- Case A~C: 미완료 elapsed 증가 → 동일 Traveling offline settle → 재호출 no-op
- Case D: `TimeRollbackDetected` + 적용 스킵 후 smoke 전체 Pass

**브랜치 검증 기준:** T1 + T2 Pass. T3~T6은 필수 아님.

---

## 7. 범위 밖

- AtomicSave (temp/backup)
- AutoSave / Dirty Flag
- HMAC / 저장 암호화
- Pause 상태 Save 영속화
- Core / Economy 패키지 직접 수정

---

## 8. 관련 문서

- [`m3-pending-settlement-persist.md`](m3-pending-settlement-persist.md)
- [`Docs/Guide/Framework_CoreServices_Team_Usage_Guide.md`](../../Guide/Framework_CoreServices_Team_Usage_Guide.md)
- [`Docs/Guide/Framework_InGame_Time_Multiplier_API_Guide.md`](../../Guide/Framework_InGame_Time_Multiplier_API_Guide.md)
- [`Docs/Planning_Milestone/02_Framework_Integration_Milestone.md`](../../Planning_Milestone/02_Framework_Integration_Milestone.md)
