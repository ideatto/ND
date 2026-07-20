# Rescue Loan Calculation Contract

- 작성일: 2026-07-20
- 담당: Progression & System / Economy
- 기준 브랜치: `dev2`
- 상태: M0 구현 계약
- 상위 문서: `0720_Progression_Economy_M0_Contract.md`
- 저장 정책: `Donation_Investment_Loan_Save_Contract.md`
- Framework 구현 요청: `0720_Progression_Requested_Framework_Integration.md`

## 1. 목적

구조 대출의 자격 판정, 고정 원금 발급, 명시적 상환, 제한 모드와 재파산 판정을 순수 계산 계약으로 고정한다. Framework Command는 이 계산 결과를 사용해 상태를 stage하고 즉시 저장한다.

구조 대출은 진행 불능 상태에서 무역을 한 번 더 시작할 기회를 제공한다. 이자 수익이나 반복 자금 공급을 위한 일반 금융 상품이 아니다.

## 2. 확정 정책

- 구조 대출 상품은 무이자 고정 원금 1종이다.
- 한 번에 하나만 활성화할 수 있다.
- 신용점수, 연체, 패널티와 복수 상품을 두지 않는다.
- 사용 가능한 거래 재화가 설정된 최소 거래 비용보다 낮을 때만 발급할 수 있다.
- 발급 원금은 부족분이 아니라 설정된 최소 거래 비용 전액이다.
- 최소 거래 비용은 말, 마차, 마차 슬롯을 모두 채우는 기준 상품 가격을 포함한 승인된 고정값이다.
- Framework는 최저가 조합이나 preset을 계산·선택하지 않는다.
- 부분 상환과 전액 상환을 허용한다.
- 정산 이익에서 자동 상환하지 않는다.
- 활성 대출 상태에서 다시 복구 필요 상태가 되면 경고 후 게임오버다.

## 3. 용어

| 용어 | 의미 |
|---|---|
| `usableTradeMoney` | 현재 플레이어가 무역 준비에 사용할 수 있는 `tradingCurrency` |
| `minimumTradeCost` | Content/Progression이 제공하는 설정 가능한 고정 최소 거래 비용 |
| `fixedPrincipal` | 발급되는 고정 원금. 이번 계약에서는 `minimumTradeCost`와 같다 |
| `needsRecovery` | `usableTradeMoney < minimumTradeCost` |
| `activeLoan` | `isActive == true && remainingPrincipal > 0` |
| `restrictedMode` | 구조 대출 발급 후 승인된 출발 성공 전까지의 거래 준비 제한 상태 |
| `rebankrupt` | 활성 대출이 있는 상태에서 `needsRecovery == true`가 된 상태 |

## 4. 계산과 상태 변경의 책임 분리

### Progression/Economy 계산기

- 입력 유효성 검사
- 발급 가능 여부 계산
- 발급 원금과 발급 후 재화 계산
- 상환 가능 여부와 상환 후 잔액 계산
- 복구 필요 및 재파산 판정
- 안정적인 도메인 실패 코드 반환
- SaveData를 직접 변경하거나 저장하지 않음

### Framework Command

- 현재 SaveData에서 입력 snapshot 생성
- 계산기 호출
- 계산 성공 시 재화·대출·제한 상태를 함께 stage
- `ISaveService.Save()` 실행
- `SaveResult.Succeeded` 확인
- 실패 시 stage 이전 상태로 rollback
- 저장 성공 후에만 committed event 발행
- 1단계 공개 반환값은 `SaveResult`

### Core와 UI

- Core는 출발 구성의 유효성과 출발 성공 여부를 제공한다.
- UI는 대출 안내, 명시적 상환 요청, 제한 모드와 재파산 경고·게임오버 화면을 담당한다.
- Core와 UI는 대출 자격이나 금액 계산식을 복제하지 않는다.

## 5. 정의 데이터 계약

```csharp
[Serializable]
public sealed class RescueLoanDefinition
{
    public string LoanId = "rescue_loan";
    public long MinimumTradeCost;
}
```

검증 규칙:

- `LoanId`는 비어 있지 않아야 한다.
- `MinimumTradeCost`는 0보다 커야 한다.
- 2차 빌드에서 유효한 구조 대출 정의는 하나만 존재한다.
- `MinimumTradeCost`의 실제 값은 기준 말·마차·상품 정의가 확정된 뒤 Content/Tools가 입력한다.
- 런타임 계산에서 시장 최저가를 탐색해 값을 다시 만들지 않는다.

## 6. 저장 데이터 계약

```csharp
[Serializable]
public sealed class RescueLoanSaveData
{
    public string loanId = string.Empty;
    public long originalPrincipal;
    public long remainingPrincipal;
    public bool isActive;
    public long issuedUtcTicks;
    public bool isRestrictedPreparation;
}
```

정규화 규칙:

- 원금과 잔액은 0 미만이면 0으로 보정한다.
- 잔액은 원금을 초과할 수 없다.
- 잔액이 0이면 `isActive = false`로 보정한다.
- 비활성 대출은 `isRestrictedPreparation = false`여야 한다.
- 활성 대출은 유효한 `loanId`와 0보다 큰 원금·잔액을 가져야 한다.
- UTC ticks가 음수이면 0으로 보정하고 경고를 기록한다.
- 제한 모드는 대출 활성 상태와 별개다. 출발 성공 후 대출이 남아 있어도 제한 모드만 해제된다.

`SaveData` 최상위에 `rescueLoan` 하나를 두는 것이 목표 계약이다. 실제 필드 추가와 버전·정규화는 Framework owner가 수행한다.

## 7. 공통 실패 코드

```csharp
public enum RescueLoanFailureReason
{
    None,
    InvalidInput,
    InvalidDefinition,
    NotEligible,
    ActiveLoanExists,
    NoActiveLoan,
    InvalidRepaymentAmount,
    RepaymentExceedsBalance,
    InsufficientCurrency,
    RepaymentWouldTriggerRecovery,
    InvalidState,
    Overflow
}
```

`SaveFailed`는 계산 실패가 아니라 Framework 저장 결과이므로 위 enum에 넣지 않는다. Command는 계산 성공 후 저장 실패 시 실패 `SaveResult`를 직접 반환한다.

## 8. 복구 필요 및 재파산 판정

### 입력

```csharp
public sealed class RescueStatusInput
{
    public long UsableTradeMoney;
    public long MinimumTradeCost;
    public bool HasActiveLoan;
}
```

### 결과

```csharp
public sealed class RescueStatusResult
{
    public bool IsValid;
    public RescueLoanFailureReason FailureReason;
    public bool NeedsRecovery;
    public bool CanOfferLoan;
    public bool IsRebankrupt;
    public long Shortfall;
}
```

### 판정식

```text
needsRecovery = usableTradeMoney < minimumTradeCost
shortfall = max(0, minimumTradeCost - usableTradeMoney)
canOfferLoan = needsRecovery && !hasActiveLoan
isRebankrupt = needsRecovery && hasActiveLoan
```

- `usableTradeMoney`가 음수이면 유효하지 않은 입력이다.
- `minimumTradeCost <= 0`이면 유효하지 않은 정의다.
- `IsRebankrupt`가 참이면 신규 대출을 제안하지 않는다.
- UI는 `IsRebankrupt` 결과로 경고를 먼저 표시한 뒤 게임오버 흐름을 실행한다.

## 9. 구조 대출 발급 계산

### 입력

```csharp
public sealed class IssueRescueLoanInput
{
    public string LoanId = string.Empty;
    public long TradeMoneyBefore;
    public long MinimumTradeCost;
    public bool HasActiveLoan;
    public long IssuedUtcTicks;
}
```

### 결과

```csharp
public sealed class IssueRescueLoanResult
{
    public bool Success;
    public RescueLoanFailureReason FailureReason;
    public string LoanId = string.Empty;
    public long Principal;
    public long TradeMoneyBefore;
    public long TradeMoneyAfter;
    public long RemainingPrincipal;
    public long IssuedUtcTicks;
    public bool EnterRestrictedMode;
}
```

### 검증 순서

1. 입력과 `LoanId`가 유효한지 확인한다.
2. 금액과 UTC ticks가 음수가 아닌지 확인한다.
3. `MinimumTradeCost > 0`인지 확인한다.
4. 활성 대출이 있으면 `ActiveLoanExists`로 실패한다.
5. `TradeMoneyBefore >= MinimumTradeCost`이면 `NotEligible`로 실패한다.
6. 덧셈 overflow를 사전 검사한다.

### 계산식

```text
principal = minimumTradeCost
tradeMoneyAfter = checked(tradeMoneyBefore + principal)
remainingPrincipal = principal
enterRestrictedMode = true
```

발급 예시:

| 거래 재화 | 최소 거래 비용 | 발급 원금 | 발급 후 재화 |
|---:|---:|---:|---:|
| 0 | 1,000 | 1,000 | 1,000 |
| 400 | 1,000 | 1,000 | 1,400 |
| 999 | 1,000 | 1,000 | 1,999 |
| 1,000 | 1,000 | 발급 불가 | 1,000 |

부족분만 지급하지 않는 것이 확정 정책이다.

## 10. 명시적 상환 계산

### 입력

```csharp
public sealed class RepayRescueLoanInput
{
    public long TradeMoneyBefore;
    public long MinimumTradeCost;
    public long OriginalPrincipal;
    public long RemainingPrincipalBefore;
    public bool IsActive;
    public bool IsRestrictedPreparation;
    public long RequestedAmount;
}
```

### 결과

```csharp
public sealed class RepayRescueLoanResult
{
    public bool Success;
    public RescueLoanFailureReason FailureReason;
    public long RequestedAmount;
    public long RepaidAmount;
    public long TradeMoneyBefore;
    public long TradeMoneyAfter;
    public long RemainingPrincipalBefore;
    public long RemainingPrincipalAfter;
    public bool IsActiveAfter;
    public bool IsRestrictedPreparationAfter;
}
```

### 검증 순서

1. 활성 대출과 0보다 큰 잔액이 있는지 확인한다.
2. `RequestedAmount > 0`인지 확인한다.
3. 요청액이 남은 원금을 초과하지 않는지 확인한다.
4. 요청액이 현재 거래 재화를 초과하지 않는지 확인한다.
5. 제한 모드에서는 상환 요청을 거부한다. 제한 모드의 목적은 승인된 출발 준비다.
6. 상환 후 거래 재화가 최소 거래 비용보다 낮아지는 요청은 거부한다.

### 계산식

```text
tradeMoneyAfter = tradeMoneyBefore - requestedAmount
remainingPrincipalAfter = remainingPrincipalBefore - requestedAmount
isActiveAfter = remainingPrincipalAfter > 0
isRestrictedPreparationAfter = isActiveAfter && isRestrictedPreparation
```

상환 때문에 즉시 재파산·게임오버가 발생하지 않도록 `tradeMoneyAfter >= minimumTradeCost`를 요구한다. 이 보호 규칙을 변경하려면 별도 정책 승인이 필요하다.

전액 상환 시:

```text
remainingPrincipalAfter = 0
isActiveAfter = false
isRestrictedPreparationAfter = false
```

## 11. 제한 모드 해제 계약

제한 모드는 승인된 출발의 영속 저장이 성공한 뒤에만 해제한다.

```text
Core 출발 검증 성공
-> Framework가 출발 상태와 isRestrictedPreparation=false를 stage
-> 즉시 저장
-> SaveResult.Succeeded 확인
-> Traveling 전환 및 제한 모드 해제 event
```

- 구성 preview, 버튼 클릭이나 출발 검증 성공만으로 해제하지 않는다.
- 저장 실패 시 제한 모드를 유지하고 화면 전환을 중단한다.
- 제한 모드가 해제되어도 대출 잔액과 활성 상태는 유지된다.
- Title·종료·재접속 시 Framework가 제한 상태를 저장·복구한다.
- 화면 이동과 차단 UI는 UI & Data 팀 계약을 따른다.

## 12. 정산 계약과의 정렬

현재 `SettlementInput.LoanRepayment`와 `SettlementCalculator`에는 정산 비용으로 대출 상환액을 자동 차감할 수 있는 경로가 있다.

확정 정책에서는 자동 상환을 사용하지 않는다.

- 일반 정산에서 `LoanRepayment`는 반드시 0이어야 한다.
- `SettlementCalculator`는 대출 잔액을 직접 변경하지 않는다.
- 상환은 별도 `RepayRescueLoan` Command에서만 수행한다.
- `SettlementEntryType.LoanRepayment`는 호환성을 위해 당장 삭제하지 않고 미사용 처리할 수 있다.
- 모든 호출부가 0을 전달하는지 inventory와 테스트로 확인한 뒤 제거 여부를 결정한다.

정산 완료 후 `TradeMoneyAfter < MinimumTradeCost`이면:

```text
활성 대출 없음 -> 구조 대출 제안 가능
활성 대출 있음 -> 재파산 경고 후 게임오버
```

## 13. Command 상태 변경 순서

### IssueRescueLoan

```text
현재 SaveData snapshot
-> IssueRescueLoan 계산
-> tradingCurrency 증가 stage
-> rescueLoan 생성·활성화 stage
-> restrictedPreparation=true stage
-> 즉시 Save
-> 성공: LoanIssued event 및 UI refresh
-> 실패: snapshot rollback, 성공 event 금지
```

### RepayRescueLoan

```text
현재 SaveData snapshot
-> RepayRescueLoan 계산
-> tradingCurrency 차감 stage
-> remainingPrincipal 차감 stage
-> 전액이면 active=false stage
-> 즉시 Save
-> 성공: LoanRepaid 또는 LoanClosed event 및 UI refresh
-> 실패: snapshot rollback, 성공 event 금지
```

공개 Command가 1단계에서 `SaveResult`를 직접 반환하므로 UI는 도메인 실패 상세를 query/ViewData 또는 별도 검증 API에서 먼저 받는다. 이 방식이 부족해질 때 통합 결과 타입으로 확장한다.

## 14. Event 계약

저장 성공 후에만 발행할 event 후보:

- `RescueLoanIssued`
- `RescueLoanRepaid`
- `RescueLoanClosed`
- `RescueRestrictedModeEntered`
- `RescueRestrictedModeExited`
- `RescueRebankruptcyDetected`

payload에는 `loanId`, 변경 전후 잔액, 변경 전후 거래 재화, UTC 시각과 저장 revision을 가능한 범위에서 포함한다. event 수신만으로 재화를 다시 변경하지 않는다.

## 15. 테스트 매트릭스

### 자격 판정

- 재화 0, 최소 비용 1,000이면 대출 제안
- 재화 999, 최소 비용 1,000이면 대출 제안
- 재화 1,000 이상이면 `NotEligible`
- 최소 비용 0 또는 음수이면 `InvalidDefinition`
- 활성 대출이 있으면 신규 발급 거부

### 발급

- 재화 400, 최소 비용 1,000이면 원금 1,000 및 최종 재화 1,400
- 발급 후 원금과 잔액이 동일
- 발급 후 활성·제한 모드 진입
- 금액 덧셈 overflow 시 상태 불변
- 저장 실패 시 재화·대출·제한 상태 rollback
- 저장 성공 후에만 발급 event 발행

### 상환

- 부분 상환 후 활성 상태와 잔액 유지
- 전액 상환 후 잔액 0, 비활성, 제한 상태 해제
- 0·음수 상환 거부
- 잔액 초과 상환 거부
- 보유 재화 초과 상환 거부
- 제한 모드 중 상환 거부
- 최소 거래 비용 미만을 만드는 상환 거부
- 저장 실패 시 재화와 대출 잔액 rollback
- 정산 결과에서 자동 상환이 발생하지 않음

### 재파산

- 활성 대출 없이 기준 미달이면 대출 제안
- 활성 대출이 있고 기준 미달이면 대출 제안 없이 재파산
- 기준과 같은 재화는 재파산이 아님
- UI가 경고 완료 전 게임오버 화면을 확정하지 않음

### 복구

- 발급 직후 종료·재실행 시 대출과 제한 모드 유지
- 출발 저장 실패 후 재실행 시 제한 모드 유지
- 출발 저장 성공 후 재실행 시 제한 모드 해제, 대출 잔액 유지
- 전액 상환 저장 성공 후 재실행 시 비활성 상태 유지

## 16. 기존 코드 영향

| 위치 | 현재 상태 | 후속 작업 |
|---|---|---|
| `Assets/_Project/03.Economy/08_Loan/` | 구현 없음 | 정의·입출력 DTO·순수 계산기·Editor 테스트 추가 |
| `SaveData.cs` | 구조 대출 필드 없음 | Framework가 `rescueLoan` DTO와 정규화 추가 |
| `SettlementModels.cs` | `LoanRepayment` 존재 | 일반 정산 입력 0 고정, 추후 제거 검토 |
| `SettlementCalculator.cs` | 상환액을 비용에 포함 | 자동 상환 경로 미사용 보장 및 테스트 |
| UI | 제한 모드 화면 계약 필요 | UI & Data 팀 handoff |

## 17. 구현 완료 기준

- 자격·발급·상환·재파산 계산기가 SaveData 변경 없이 결정적으로 동작한다.
- 경계값과 overflow 테스트가 통과한다.
- Framework가 재화와 대출 상태를 원자적으로 저장하고 실패 시 rollback한다.
- 일반 정산에서 자동 상환이 발생하지 않는다.
- 제한 모드는 출발 저장 성공 후에만 해제된다.
- 활성 대출 중 재파산 시 신규 대출 없이 UI 경고·게임오버 흐름으로 연결된다.
