# 구조 대출 Framework 통합 로직 정리

- 작성일: 2026-07-21
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/rescue-loan-integration`
- 기준 브랜치: `dev2`
- 상태: 구현·Editor 통합 검증 완료 (Economy E2E는 별도 이슈)
- 상위 계약: `Docs/Personal_Documents/JJH/0720_Rescue_Loan_Calculation_Contract.md`
- 저장 정책: `Docs/Personal_Documents/CSU/SaveDataPolicy/Donation_Investment_Loan_Save_Contract.md`

## 1. 목적

Progression/Economy가 제공한 **구조 대출(Rescue Loan) 순수 계산기**를 Framework에 연결한다.

이번 브랜치가 담당하는 범위:

- `SaveData`에 구조 대출 영속 필드 추가
- 발급·상환 **Command**와 저장 rollback
- 무역 출발 시 **출발 전 제한 모드(restricted preparation)** 해제
- 저장 성공 후 **Framework event** 발행
- Editor E2E로 통합 경로 검증

계산식·자격 판정·재파산 규칙 자체는 Economy 쪽 계약을 그대로 사용하며, Framework는 **stage → Save → event**만 담당한다.

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `SaveData.cs` | `RescueLoanSaveData`, `SaveData.rescueLoan` 추가 |
| `JsonSaveService.cs` | `NormalizeRescueLoan()` — 구 세이브·손상 데이터 보정 |
| `TradeStartService.cs` | 출발 snapshot/rollback, 제한 모드 해제, `RescueLoanCommandService` |
| `FrameworkRoot.cs` | `RescueLoan` command service 노출, definition 주입 API |
| `FrameworkEvents.cs` | 발급·상환·종료·제한 모드 진입/해제 event |
| `FrameworkEconomyM1InputBuilder.cs` | 정산 입력 `LoanRepayment = 0L` 고정 |
| `FrameworkM1LoopE2EEditorTests.cs` | 구조 대출 integration + happy path 검증 |

## 3. 책임 분리

```text
Progression/Economy (RescueLoanCalculator)
  └─ 자격·발급·상환·재파산 계산 (SaveData 미변경)

Framework (RescueLoanCommandService, TradeStartService)
  └─ snapshot → 계산기 호출 → stage → ISaveService.Save()
  └─ 실패 시 rollback, 성공 시 event

Core / UI
  └─ 출발 검증, 제한 UI, 명시적 상환 요청 (계산 복제 금지)
```

## 4. 저장 데이터

### 4.1 필드 (`RescueLoanSaveData`)

```csharp
public sealed class RescueLoanSaveData
{
    public string loanId = string.Empty;
    public long originalPrincipal;
    public long remainingPrincipal;
    public bool isActive;
    public long issuedUtcTicks;          // UTC DateTime.Ticks
    public bool isRestrictedPreparation;   // 출발 전 제한 모드
}
```

`SaveData` 최상위에 `rescueLoan` 하나를 둔다. **schema version은 5를 유지**하고, 필드 누락은 `NormalizeData`가 보정한다.

### 4.2 정규화 (`JsonSaveService.NormalizeRescueLoan`)

- `rescueLoan == null` → 빈 비활성 DTO 생성
- 원금·잔액 음수 → 0
- 잔액 > 원금 → 원금으로 clamp (경고 로그)
- `issuedUtcTicks < 0` → 0 (경고 로그)
- `remainingPrincipal == 0` → `isActive = false`
- 활성인데 `loanId` 비어 있거나 원금/잔액 ≤ 0 → 비활성으로 강제 보정
- **비활성 대출** → `isRestrictedPreparation = false`

## 5. Command: `RescueLoanCommandService`

위치: `TradeStartService.cs` 하단 (동일 feature, command 계층).

### 5.1 공개 API

| API | 반환 | 설명 |
|---|---|---|
| `IsRestrictedPreparation` | `bool` | 현재 저장 상태가 출발 전 제한 모드인지 |
| `EvaluateStatus()` | `RescueStatusResult` | 복구 필요·대출 제안·재파산 상태 계산 |
| `IssueRescueLoan()` | `SaveResult` | 고정 원금 발급 + 즉시 저장 |
| `RepayRescueLoan(long amount)` | `SaveResult` | 명시적 상환 + 즉시 저장 |

의존성: `ISaveService`, `Func<SaveData>`, `RescueLoanDefinition`, `Func<long>`(UTC ticks).

### 5.2 발급 (`IssueRescueLoan`)

```text
GetValidData()
→ RescueLoanCalculator.Issue(...)
→ 실패: SaveResult.Failure(InvalidData, ...)
→ 성공 시 stage:
    tradingCurrency = TradeMoneyAfter
    rescueLoan.* = 계산 결과 (isActive=true, isRestrictedPreparation=true)
→ saveService.Save()
→ 저장 실패: RestoreLoan(currencyBefore, loanBefore) 후 SaveResult 반환
→ 저장 성공:
    RaiseRescueLoanIssued
    EnterRestrictedMode 이면 RaiseRescueRestrictedModeEntered
```

발급 예 (MinimumTradeCost=1000):

| 발급 전 재화 | 발급 후 재화 | 원금 | 제한 모드 |
|---:|---:|---:|:---:|
| 400 | 1,400 | 1,000 | 진입 |

### 5.3 상환 (`RepayRescueLoan`)

```text
GetValidData()
→ RescueLoanCalculator.Repay(...)
→ 실패: SaveResult.Failure(InvalidData, ...)
→ 성공 시 stage:
    tradingCurrency -= RepaidAmount
    remainingPrincipal 갱신, 전액이면 isActive=false
→ saveService.Save()
→ 저장 실패: RestoreLoan rollback
→ 저장 성공:
    RaiseRescueLoanRepaid
    비활성화되면 RaiseRescueLoanClosed
```

Economy 계약상 거부되는 대표 케이스:

- `RequestedAmount <= 0`
- 잔액·보유 재화 초과
- **제한 모드 중** 상환 (`IsRestrictedPreparation == true`)
- 상환 후 `tradeMoneyAfter < minimumTradeCost` (`RepaymentWouldTriggerRecovery`)

마지막 규칙 때문에, 출발 직후 재화 1,400 / 잔액 1,000 / MinimumTradeCost 1,000 상태에서 **전액 상환(1,000)은 거부**된다. (1,400 - 1,000 = 400 < 1,000)

### 5.4 FrameworkRoot 연결

```csharp
// InitializeServices()
ConfigureRescueLoanDefinition(new RescueLoanDefinition());

// Content/Progression이 유효한 MinimumTradeCost를 줄 때까지
// MinimumTradeCost=0 → command가 InvalidDefinition으로 안전 거부
public void ConfigureRescueLoanDefinition(RescueLoanDefinition definition)
{
    RescueLoan = new RescueLoanCommandService(
        SaveService,
        () => CurrentSaveData,
        definition,
        () => DateTime.UtcNow.Ticks);
}
```

런타임 접근: `FrameworkRoot.Instance.RescueLoan`

## 6. 무역 출발과 제한 모드 해제

`TradeStartService.TryStartTrade` 변경 핵심:

1. 출발 **직전** `tradeProgress`, `caravan`(save), runtime `caravan`, `isRestrictedPreparation` snapshot
2. `RecordStartedTrade` → Core `TryDepart` → `CaravanSaveDataMapper.CopyToSave`
3. `saveImmediately == true`일 때:
   - `rescueLoan.isRestrictedPreparation = false` stage
   - `saveService.Save(saveData)` — **한 저장 경계**에서 tradeProgress + caravan + 제한 해제 확정
   - 저장 실패 → `RestoreDepartureSnapshot` (제한 모드 포함 rollback) → `canDepart=false`
   - 저장 성공 && 이전에 제한 모드였음 → `RaiseRescueRestrictedModeExited`
4. 저장 성공 후에만 `clearSettlementCache`, `SetActiveCaravan`, `Traveling` 화면 전환

```text
[제한 모드] ──IssueRescueLoan──▶ isRestrictedPreparation=true
       │
       │  TryStartTrade (Core 출발 OK)
       ▼
  stage: tradeProgress + caravan + isRestrictedPreparation=false
       │
       │  Save 성공
       ▼
  RescueRestrictedModeExited
  Traveling 화면
  (대출 잔액·isActive는 유지)
```

**중요:** preview·버튼 클릭·Core 검증만으로는 제한 모드가 해제되지 않는다. **저장 성공**이 조건이다.

## 7. Event (`FrameworkEvents`)

저장 성공 **이후에만** 발행:

| Event | 발생 조건 |
|---|---|
| `RescueLoanIssued` | 발급 저장 성공 |
| `RescueRestrictedModeEntered` | 발급 시 `EnterRestrictedMode` |
| `RescueLoanRepaid` | 상환 저장 성공 |
| `RescueLoanClosed` | 전액 상환으로 `isActive=false` |
| `RescueRestrictedModeExited` | 출발 저장 성공 && 이전에 제한 모드 |

UI는 event 수신만으로 재화·대출 상태를 다시 변경하지 않는다. refresh/query는 `CurrentSaveData` 또는 ViewData builder를 사용한다.

## 8. 정산과의 정렬

`FrameworkEconomyM1InputBuilder`에서 M1 정산 입력에 **`LoanRepayment = 0L`** 을 명시한다.

- 구조 대출 상환은 정산 자동 차감 경로를 사용하지 않는다.
- 상환은 `RepayRescueLoan` Command만 사용한다.

## 9. 통합 흐름 (Happy Path)

Editor 테스트 `RunRestrictedDepartureHappyPathCheck` 기준:

```text
1. tradingCurrency=400, MinimumTradeCost=1000
2. IssueRescueLoan()
   → currency=1400, active, restricted, RescueRestrictedModeEntered
3. TryStartTrade(...)
   → Traveling, restricted=false, RescueRestrictedModeExited
4. RepayRescueLoan(400)          // 허용 (1400→1000, 잔액 600)
5. tradingCurrency=2500 (정산 등으로 확보 가정)
6. RepayRescueLoan(600)          // 전액 상환, RescueLoanClosed
```

## 10. Editor 검증

메뉴: `ND/Framework/Run M1 Loop + Economy E2E Checks`

구조 대출 전용 검증 (`RunRescueLoanIntegrationChecks`):

| 항목 | 내용 |
|---|---|
| Normalize | null·손상·잔액 clamp |
| 발급 | 재화·원금·제한 모드·event·저장 1회 |
| 중복 발급 | 활성 대출 시 거부 |
| 부분/전액 상환 | 제한 해제 후 |
| 저장 실패 rollback | 발급·상환 각각 |
| 재파산/제안 | `EvaluateStatus` |
| Happy path | 발급→제한→출발→해제→상환 |
| 출발 저장 실패 rollback | 제한 모드 유지 |

2026-07-21 MCP 검증 결과:

- 컴파일 오류: 없음
- Rescue Loan integration: **PASS**
- Loop integrity smoke (3 cycle): **PASS**
- Economy E2E: **FAIL** (`claim` 후 `tradingCurrency` 미변동 — 구조 대출 diff와 무관한 기존 경로 의심)

## 11. UI / Content 후속 연결

Framework만으로는 다음이 아직 비어 있다:

- `ConfigureRescueLoanDefinition`에 **실제 MinimumTradeCost** 주입 (Content/Progression)
- 제한 모드 UI 차단 (UI & Data 계약)
- 재파산 경고·게임오버 (`RescueRebankruptcyDetected` event는 계약에 있으나 이번 브랜치 미구현)
- `TradeStartDebugHarness` 등 debug harness에 대출 command shortcut (선택)

호출 예:

```csharp
var root = FrameworkRoot.Instance;
var status = root.RescueLoan.EvaluateStatus();
if (status.CanOfferLoan)
    root.RescueLoan.IssueRescueLoan();

// 제한 해제 후, UI에서 명시적 금액
root.RescueLoan.RepayRescueLoan(amount);
```

## 12. 설계 메모

### 12.1 왜 출발과 제한 해제를 같은 Save에 묶는가

제한 모드는 “승인된 출발이 영속화되기 전까지 준비 화면을 제한”하는 상태다. 출발 기록·caravan·제한 해제가 분리 저장되면, 크래시/저장 실패 시 **Traveling인데 제한 UI가 남는** 불일치가 생길 수 있다.

### 12.2 rollback 범위

| 실패 지점 | rollback 대상 |
|---|---|
| `IssueRescueLoan` / `RepayRescueLoan` 저장 실패 | `tradingCurrency`, `rescueLoan` 전 필드 |
| `TryStartTrade` 저장 실패 | `tradeProgress`, save `caravan`, runtime `caravan`, `isRestrictedPreparation` |

### 12.3 schema version을 올리지 않은 이유

`rescueLoan`은 version 5 세이브에 필드가 없어도 `NormalizeRescueLoan`이 안전한 기본값으로 보정한다. migration 없이 additive field로 처리한다.

## 13. 관련 문서

- `Docs/Personal_Documents/JJH/0720_Rescue_Loan_Calculation_Contract.md` — 계산·정책 원본
- `Docs/Personal_Documents/JJH/0720_Progression_Requested_Framework_Integration.md` — Framework 구현 요청
- `Docs/Personal_Documents/CSU/SaveDataPolicy/Donation_Investment_Loan_Save_Contract.md` — 저장 계약
- `Docs/Personal_Documents/CSU/0710_M1_Trade_Loop_Integrity.md` — 무역 루프 무결성
- `Assets/_Project/03.Economy/08_Loan/RescueLoanCalculator.cs` — 순수 계산기 구현
