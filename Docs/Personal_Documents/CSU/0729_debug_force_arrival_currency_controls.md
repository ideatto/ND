# Debug Force Arrival · Currency Controls 구현 로직

**작성일:** 2026-07-29  
**브랜치:** `feature/debug/debug-tool-time-multiplier-currency-addition`  
**HEAD (검증 시점):** `a2b54a546f2a7ef552a23d8d792e06c7d4a66bfc`  
**베이스:** `dev2`  
**범위:** Work E Force Arrival backend/UI · Work F Currency grant backend · Work G Currency Debug Controls UI

---

## 1. 목적

다중 카라반 환경에서 디버그 도구가 다음을 안전하게 수행할 수 있어야 한다.

1. **선택 Caravan의 특정 Traveling trade만** 도착 정산 대기로 강제 전환한다.
2. **플레이어 공용 Trading / Development Currency**를 양수만 추가하고, Save 트랜잭션과 롤백을 보장한다.
3. `ProjectDebugPanel`은 SaveData를 직접 쓰지 않고, Reflection으로 Framework debug API만 호출한다.

레거시 `CompleteTradeImmediately()`는 전역 active trade 추론에 의존하므로, 이번 작업에서는 exact `(caravanId, tradeId)` 경로만 사용한다.

---

## 2. 변경 파일

| 영역 | 파일 | 역할 |
|------|------|------|
| Work E backend | `TradeProgressCoordinator.cs` | `TryForceCompleteTrade` + `ForcedTradeCompletionResult` |
| Work E/F facade | `FrameworkDebugCommands.cs` | Force Arrival 위임, Currency grant API, Save 주입 생성자 |
| Work G UI | `ProjectDebugPanel.cs` | Force Arrival / Currency Controls Reflection UI |
| Docs | `ProjectDebugPanelGuide.md` | 패널 사용·제약 문서 |
| Tests | `FrameworkM1LoopE2EEditorTests.cs` | Force Arrival / Currency focused Editor fixtures |

Scene / Prefab / meta / asmdef / ProjectSettings / Packages는 변경하지 않는다.

---

## 3. 전체 아키텍처

```text
ProjectDebugPanel (IMGUI, UNITY_EDITOR || DEVELOPMENT_BUILD)
  │  Monitoring: read-only Reflection snapshot
  │  Mutation: explicit button click only
  │
  ├─ Force Arrival
  │    Resolve selected Traveling entry
  │    → Reflection Invoke
  │         FrameworkDebugCommands.TryForceCompleteTrade(caravanId, tradeId)
  │              → TradeProgressCoordinator.TryForceCompleteTrade(...)
  │                   validate → settle → Save
  │                   fail 시 SaveData / runtime / settlement cache rollback
  │
  └─ Currency Controls
       Resolve current long values from SaveData.player
       → Reflection Invoke
            FrameworkDebugCommands.TryAddTradingCurrency(long)
            FrameworkDebugCommands.TryAddDevelopmentCurrency(long)
                 → candidate write → Save
                 → Save 실패 시 previousValue rollback
                 → Trading만 성공 후 TradingCurrencyChanged 발행
```

핵심 원칙:

```text
ProjectDebugPanel
  → Reflection
  → FrameworkDebugCommands
  → Save transaction
  → SaveResult / ForcedTradeCompletionResult
  → panel Last Result feedback
```

금지 경로:

```text
ProjectDebugPanel → direct SaveData currency / trade write
```

---

## 4. Work E — Exact Force Arrival

### 4-1. 공개 API

```csharp
ForcedTradeCompletionResult TryForceCompleteTrade(string caravanId, string tradeId)
```

진입점:

- `FrameworkDebugCommands.TryForceCompleteTrade` → coordinator 위임
- coordinator 부재 시 `RequiredDependencyMissing` 실패

### 4-2. 결과 타입

`ForcedTradeCompletionResult`

| 필드 | 의미 |
|------|------|
| `Succeeded` | Save까지 성공했는지 |
| `FailureReason` | 구조화 실패 사유 |
| `CaravanId` / `TradeId` | 요청 식별자 |
| `SaveResult` | Save 실패 시 하위 결과 |

주요 `ForcedTradeCompletionFailureReason`:

| Reason | 의미 |
|--------|------|
| `InvalidCaravanId` / `InvalidTradeId` | 공백 ID |
| `TradeProgressNotFound` | progress entry 없음 |
| `NotTraveling` | Traveling이 아님 |
| `TradeIdentityMismatch` | `activeTradeId` 불일치 |
| `CaravanNotFound` / `RuntimeCaravanNotFound` | Save/runtime 부재 |
| `RuntimeIdentityMismatch` | Save/runtime ID 불일치 |
| `DuplicatePendingSettlement` | 이미 exact pending 존재 |
| `RequiredDependencyMissing` | Save/time/recorder 등 부재 |
| `SettlementFailed` | Settle 실패 후 rollback |
| `SaveFailed` | Save 실패 후 rollback |
| `RollbackFailed` | rollback 자체 실패 |

### 4-3. 성공 경로

```text
1. caravanId / tradeId normalize + 비공백 검증
2. SaveDataLookup.TryGetTradeProgress
3. state == Traveling
4. progress.activeTradeId == tradeId
5. Caravan Save + runtime Caravan 존재/일치
6. exact pending 중복 없음
7. 의존성 확인
8. SaveData / runtime JSON snapshot 보관
9. SyncElapsed → ArrivalProgress → CopyToSave
10. SettleTrade (알림은 deferred)
11. saveService.Save
12. Save 성공 시에만 PublishSettlementNotifications
13. Success 반환
```

의도된 lifecycle:

```text
Traveling
→ SettlementPending / Settling
→ Arrival Sale
→ Claim
```

이 명령은 **화물 판매·Claim 보상·Completed 직접 전환을 하지 않는다.**

### 4-4. 실패 시 롤백

Settle 또는 Save 실패 시 `TryRestoreForcedTradeCompletion`이:

- SaveData JSON overwrite
- runtime Caravan JSON overwrite
- `LastSettlementTradeId` / `LastSettlementResult` 복원
- `economySettlementBridge.ClearPending(caravanId, tradeId)`

성공 알림은 Save 성공 전에 발행하지 않는다.

### 4-5. UI 계약 (`ProjectDebugPanel`)

- 버튼: `Force Selected Trade to Arrival`
- 활성 조건: selected Caravan의 exact matching Traveling entry + non-empty `activeTradeId`
- 클릭 시 현재 Framework/SaveData를 다시 resolve한 뒤 Reflection 호출
- 대상 시그니처: `TryForceCompleteTrade(String, String)`
- `CompleteTradeImmediately()`는 호출하지 않음
- `Last Force Arrival Result`는 Currency 결과와 독립 필드

---

## 5. Work F — Currency Grant Backend

### 5-1. 공개 API

```csharp
SaveResult TryAddTradingCurrency(long amount)
SaveResult TryAddDevelopmentCurrency(long amount)
```

공통 구현: `TryAddCurrency(category, amount, read, write, publishSuccess)`

| API | Save 필드 | 성공 이벤트 |
|-----|-----------|-------------|
| Trading | `player.tradingCurrency` | `FrameworkEvents.RaiseTradingCurrencyChanged(next)` |
| Development | `player.developmentCurrency` | 없음 (`null`) |

### 5-2. 생성자 DI

테스트/롤백 fixture를 위해 생성자가 확장됐다.

```csharp
FrameworkDebugCommands(
    GameTimeService gameTimeService,
    Func<SaveData> getCurrentSaveData = null,
    ISaveService saveService = null)
```

- Play Mode: null이면 `FrameworkRoot.Instance`의 SaveData / SaveService 사용
- Editor fixture: `ConfigurableSaveService` 주입으로 WriteFailed 재현

### 5-3. 트랜잭션 규칙

```text
1. amount <= 0 → InvalidData 실패 (상태 변경 없음)
2. SaveData / player / ISaveService 없으면 InvalidData
3. checked(previous + amount) overflow → InvalidData
4. candidate write
5. persistence.Save(data) 1회
6. Save 실패 → previousValue rollback, TradingCurrencyChanged 미발행
7. Save 성공 → Trading만 이벤트 발행, SaveResult 성공 반환
```

격리 규칙:

- Trading 명령은 Development를 바꾸지 않는다.
- Development 명령은 Trading을 바꾸지 않고 Trading 이벤트도 발행하지 않는다.
- 차감 / set / reset API는 제공하지 않는다.

### 5-4. Editor focused checks

메뉴: `ND/Framework/Run Work F Currency Command Focused Checks`

검증 항목:

- Trading/Development 성공 트랜잭션
- 0 / 음수 / overflow validation
- Save WriteFailed rollback
- missing dependency rejection

---

## 6. Work G — Currency Debug Controls UI

### 6-1. 표시

`[Currency Controls]`

- Trading Currency Current (`N0` 포맷, Reflection read)
- Development Currency Current
- Availability / DisabledReason

읽기 경로:

```text
FrameworkRoot.Instance
→ CurrentSaveData
→ player
→ tradingCurrency / developmentCurrency   // long
```

### 6-2. 입력

각 화폐 그룹:

| UI | 전달 값 |
|----|---------|
| `+100` | `100L` |
| `+1,000` | `1000L` |
| `+10,000` | `10000L` |
| Custom TextField + `Add` | parse된 `long` |

커스텀 파싱:

```csharp
long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out amount)
```

로컬 거부 (backend 미호출):

- empty / whitespace
- non-numeric / decimal
- `<= 0`
- `long.MaxValue` 초과 텍스트

### 6-3. 호출

```text
ExecuteCurrencyGrant
  → FindCurrencyMethod(TryAddTradingCurrency | TryAddDevelopmentCurrency)
  → Method.Invoke(debugCommands, new object[] { amount })
  → FormatCurrencyResult(Succeeded / FailedDataCategory / FailureReason / Message)
  → RefreshSnapshot() 1회
```

Reflection 메서드 조건:

- public instance
- 이름 정확 일치
- 파라미터 정확히 `long` 1개
- 반환형 `ND.Framework.SaveResult`

### 6-4. 비변이 경로

아래는 화폐/무역/Save를 변경하지 않는다.

- Layout / Repaint
- F12 open·close
- scroll
- periodic `RefreshSnapshot` (0.25s)
- custom TextField 편집만

변이 가능한 경로는 preset 버튼과 `Add` 버튼 분기뿐이다.

### 6-5. 결과 필드 독립성

| 필드 | 용도 |
|------|------|
| `lastForceArrivalResult` | Force Arrival만 |
| `lastCurrencyResult` | Currency grant만 |

한쪽 명령이 다른쪽 Last Result를 덮어쓰지 않는다.

---

## 7. 검증 요약 (2026-07-29 Runtime)

| 항목 | 결과 |
|------|------|
| Trading/Development presets exact amount | PASS |
| Valid custom inputs | PASS |
| Invalid / zero / negative / overflow text local reject | PASS |
| Currency isolation | PASS |
| Success feedback + refresh | PASS |
| Backend overflow structured failure UI | PASS |
| Save-failure UI formatter + Editor rollback fixture | CONDITIONAL PASS |
| Physical F12 | CONDITIONAL (equivalent visibility path PASS) |
| Work E API discoverable + result independence | PASS |
| Preferred Traveling→Force Arrival click | 미실행 (fixture trade가 Traveling 아님) |
| Scene / Prefab integrity | PASS |
| Small fix | None |

전체 Work G UI 판정: **CONDITIONAL PASS**  
권장 다음 단계: commits and PR 준비

---

## 8. 사용 시 주의

1. Force Arrival은 Traveling exact match에서만 성공한다. Settling/Pending에서는 버튼이 비활성 또는 `NotTraveling` 실패다.
2. Currency는 추가만 가능하다. 실수 부여 시 차감 디버그 API는 없다.
3. 패널은 Release 빌드에 포함되지 않는다 (`UNITY_EDITOR || DEVELOPMENT_BUILD`).
4. 모니터링 스냅샷은 표시용이며, 명령 클릭 시마다 현재 SaveData를 다시 resolve한다.
5. 알려진 기존 full-suite E2E 실패(online tick isolation)는 Work E/F/G와 별개로 분류한다. Route asset을 약화하거나 UI로 우회하지 않는다.

---

## 9. Related

* 패널 가이드: `Assets/_Project/98.DebugTools/Documentation/ProjectDebugPanelGuide.md`
* 선행 Force\* 문서: `Docs/Personal_Documents/CSU/0711_world-force-debug-commands.md`
* 다중 Pending/Claim identity 계열: `0725_multi_pending_restore_targeted_claim_backend.md`, `0727_multi_Caravan_Arrival_Sale_Identity_Integration_Guide.md`
