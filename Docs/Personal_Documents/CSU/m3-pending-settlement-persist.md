# M3 Pending Settlement 영속화 로직

**작성일:** 2026-07-12  
**브랜치:** `feature/framework/pending-settlement-save`  
**Base:** `dev2`  
**Feature root:** `Assets/_Project/11.CoreServices/`  
**목적:** `SettlementPending` 상태에서 앱 종료·재실행 후에도 동일 무역 ID·동일 정산 결과로 UI·Claim이 가능하도록 `PendingSettlementSaveData`를 영속화한다.

---

## 1. 배경

M1/M2까지는 다음만 저장했다.

- `tradeProgress.state = SettlementPending`
- `caravan.state = Settling` / `settlementClaimed`

`JourneyResultData`는 `TradeProgressCoordinator.LastSettlementResult`와 `SettlementUiBridge` 세션 캐시에만 있었다.

```text
정산 생성 → 캐시 + Save(상태만)
앱 종료 / Continue
→ 화면은 Settlement로 라우팅
→ LastSettlementResult / Bridge / Economy pending = null
→ UI 비어 있음, Claim 차단
```

---

## 2. 저장 스키마

`SaveData.CurrentVersion = 5`

| 필드 | 설명 |
|------|------|
| `pendingSettlement` | `PendingSettlementSaveData` |

### PendingSettlementSaveData

| 필드 | 역할 |
|------|------|
| `hasResult` | 유효 결과 존재 |
| `tradeId`, `routeId` | `activeTradeId`와 대조 |
| `resultVersion` | 현재 지원 `1` (`CurrentResultVersion`) |
| `grade`, `failureReason` | 표시·최종 Completed/Failed |
| `cargoLost`, `durabilityLost`, travel/food/load metrics | Core 결과 |
| `revenue`, `cost`, `netProfit` | UI 표시용 확정 금액 |
| `claimed` | 이미 수령한 pending 복구 차단 |

Economy 내부 타입(`EconomyM1LoopResult`)은 저장하지 않는다.

버전 불일치(v4 이하) 세이브는 기존 `JsonSaveService` 정책대로 **마이그레이션 없이 새 게임 데이터**로 복구될 수 있다.

---

## 3. 시퀀스

### 3-1. Settle (쓰기)

```text
JourneyRunner.Settle
→ MarkSettlementPending
→ Economy TryCalculateAndFill (preview)
→ pendingSettlement = Mapper.ToSave(result, tradeId, routeId)
→ Caravan CopyToSave + SaveService.Save
→ RaiseTradeSettlementReady
→ Settlement 화면
```

`SettlementPending`과 `pendingSettlement`는 **같은 저장 단위**에 기록된다.

### 3-2. Load / Continue (복구)

호출 시점: `FrameworkRoot.CompleteLoadingAndEnterGame`  
조건: **SharedGameData 로드 이후**

```text
RestorePendingSettlement(CurrentSaveData)
→ 검증
→ LastSettlementTradeId / LastSettlementResult 재구성
→ TryCalculateAndFill로 Economy pending 재구성
→ UI 금액은 저장값 우선 덮어쓰기
→ RaiseTradeSettlementReady (Bridge 캐시 갱신)
→ RefreshFromSaveData → Settlement 화면
→ RaiseLoadCompleted
```

### 3-3. Claim (수령)

```text
CanClaimCachedSettlement
  (runtime cache + SettlementPending + pending.claimed==false + tradeId 일치)
→ JourneyRunner.ClaimSettlement
→ Economy TryApplyPendingEconomy
→ MarkCompleted / MarkFailed
→ ResetToPrepare
→ PendingSettlementSaveDataMapper.Clear
→ Save + Preparation 화면 + ClearSettlementCache
```

### 3-4. 새 무역 출발

`TradeStartService` 출발 성공 시:

- runtime settlement cache clear
- `pendingSettlement` clear

---

## 4. 복구 검증 · 실패 분기

`RestorePendingSettlement`가 **false**를 반환하는 경우 (Completed로 강등하지 않음):

| 조건 | 로그 |
|------|------|
| state ≠ SettlementPending | (조용히 false, 정상 non-pending) |
| `hasResult == false` | Error |
| `claimed == true` | Error |
| `resultVersion` 불일치 | Error |
| `tradeId` ≠ `activeTradeId` | Error |
| caravan ≠ Settling 또는 `settlementClaimed` | Error |

실패 시 Claim도 계속 차단된다. UI는 pending이 없으면 기존처럼 비어 있을 수 있다.

---

## 5. Economy 재계산 정책

- 저장: settle 시점의 `revenue` / `cost` / `netProfit`
- 복구: `TryCalculateAndFill`로 Economy pending 재구성 (Claim 화폐 반영용)
- 재계산 금액이 저장값과 다르면 Warning 후 **UI 표시는 저장값 유지**
- Claim apply는 재계산된 Economy pending의 `FinalCurrencyState` 사용
- SharedGameData 미로드 시 Economy rebuild는 skip (Claim 화폐 실패 가능)

---

## 6. 주요 파일

| 경로 | 역할 |
|------|------|
| `Scripts/Save/PendingSettlementSaveData.cs` | DTO |
| `Scripts/Save/PendingSettlementSaveDataMapper.cs` | ↔ JourneyResultData |
| `Scripts/Save/SaveData.cs` | v5 + `pendingSettlement` |
| `Scripts/Save/JsonSaveService.cs` | Normalize |
| `Scripts/TradeProgress/TradeProgressCoordinator.cs` | 쓰기·복구·Claim |
| `Scripts/Bootstrap/FrameworkRoot.cs` | CompleteLoading 훅 |
| `Editor/FrameworkM1LoopE2EEditorTests.cs` | Editor E2E |
| `Scripts/Debug/TradeStartDebugHarness.cs` | Play smoke |

---

## 7. 검증 절차

### Editor

```text
ND → Framework → Run M1 Loop + Economy E2E Checks
```

포함: success restore + claim · Failed restore · corrupt(hasResult/tradeId/claimed/resultVersion) · progress recheck keeps cache

### Play Mode

1. Boot → Title → New Game → InGame  
2. `TradeStartDebugHarness` → `Framework/Run Pending Settlement Restore Smoke`  
3. 로그: `Pending settlement restore smoke passed.`

### 수동

1. 무역 → Force Complete → InGame → Title → Continue  
2. Settlement에 동일 결과 → Claim → 화폐·상태 확인  
3. Failed(식량 고갈) 경로 동일  
4. `Framework/Print Pending Settlement Save Data`로 필드 확인

---

## 8. 실행 결과 (Pass)

**검증일:** 2026-07-12  
**브랜치:** `feature/framework/pending-settlement-save` (이후 `dev2` 병합)  
**검증자:** 로컬 Unity Editor / Play Mode

| 항목 | 결과 |
|------|------|
| Editor: `ND/Framework/Run M1 Loop + Economy E2E Checks` (Pending restore 포함) | **Pass** |
| Play: `Framework/Run Pending Settlement Restore Smoke` | **Pass** |
| Console 오류 | 없음 |

---

## 9. 범위 밖

- AtomicSave (temp/backup)
- AutoSave / Dirty Flag
- Offline 완료 파이프라인 (`TradeOfflineCompleted` 연동) — [`m3-offline-progress-pipeline.md`](m3-offline-progress-pipeline.md) / 브랜치 `feature/framework/offline-progress-pipeline`
- HMAC / 저장 암호화
- Core / Economy 패키지 직접 수정

---

## 10. 관련 문서

- [`Docs/Planning_Milestone/02_Framework_Integration_Milestone.md`](../../Planning_Milestone/02_Framework_Integration_Milestone.md) M3
- [`Docs/Guide/Framework_CoreServices_Team_Usage_Guide.md`](../../Guide/Framework_CoreServices_Team_Usage_Guide.md)
- [`m3-offline-progress-pipeline.md`](m3-offline-progress-pipeline.md) — Traveling 오프라인 복구·완료
