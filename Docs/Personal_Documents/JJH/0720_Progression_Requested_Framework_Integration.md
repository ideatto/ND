# Progression Requested — Framework & Integration

- 작성일: 2026-07-20
- 요청자: Progression & System / Economy
- 대상: Framework & Integration
- 기준 브랜치: `dev2`
- 상태: Progression 팀 구현 요청 모음
- 계산 계약: `0720_Rescue_Loan_Calculation_Contract.md`

## 1. 구조 대출 요청 목적

Progression/Economy에 구현된 순수 `RescueLoanCalculator`를 현재 `SaveData`와 `ISaveService.Save()` 흐름에 연결한다. 발급·상환은 재화와 대출 상태를 함께 변경하고 즉시 저장하며, 저장 실패 시 변경 전 상태로 rollback해야 한다.

이번 요청은 계산식 변경 요청이 아니다. Framework는 계산기의 결과를 저장 상태에 원자적으로 반영하고 재실행 시 복구 가능하게 만든다.

## 2. Progression 제공 완료 항목

- `RescueLoanFailureReason`
- `RescueLoanDefinition`
- 복구 필요·대출 가능·재파산 판정 DTO
- 발급 입력·결과 DTO
- 상환 입력·결과 DTO
- `RescueLoanCalculator.EvaluateStatus(...)`
- `RescueLoanCalculator.Issue(...)`
- `RescueLoanCalculator.Repay(...)`
- Editor 계약 테스트

위치는 `Assets/_Project/03.Economy/08_Loan/`이다.

## 3. SaveData 추가 요청

`SaveData` 최상위에 구조 대출 상태 하나를 추가한다.

```csharp
public RescueLoanSaveData rescueLoan = new RescueLoanSaveData();
```

DTO 목표 형태:

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

필드 의미:

| 필드 | 의미 |
|---|---|
| `loanId` | 구조 대출 정의의 안정적인 ID |
| `originalPrincipal` | 최초 발급된 고정 원금 |
| `remainingPrincipal` | 현재 남은 상환 원금 |
| `isActive` | 상환 의무가 남아 있는지 여부 |
| `issuedUtcTicks` | 발급이 확정된 UTC 시각 |
| `isRestrictedPreparation` | 승인된 출발 전 제한 모드 여부 |

## 4. NormalizeData 요청

로드와 저장 직전 다음을 보장한다.

```text
rescueLoan == null
-> new RescueLoanSaveData()

originalPrincipal < 0
-> 0

remainingPrincipal < 0
-> 0

remainingPrincipal > originalPrincipal
-> originalPrincipal로 clamp + warning

remainingPrincipal == 0
-> isActive = false

isActive == false
-> isRestrictedPreparation = false

issuedUtcTicks < 0
-> 0 + warning
```

활성 대출인데 `loanId`가 비었거나 원금·잔액이 0인 손상 상태는 자동으로 새 대출을 만들지 않는다. 비활성 안전 상태로 정규화하고 경고를 남긴다.

새 필드 추가 시 `SaveData.CurrentVersion` 유지 또는 증가 여부는 Framework 정책에 따라 결정하되, 구 version 5 세이브의 null 필드가 정상 기본값으로 복구되어야 한다.

## 5. Framework 서비스 요청

권장 서비스:

```csharp
public sealed class RescueLoanCommandService
{
    public RescueLoanCommandService(
        ISaveService saveService,
        Func<SaveData> getSaveData,
        RescueLoanDefinition definition,
        Func<long> utcTicksProvider);

    public SaveResult IssueRescueLoan();
    public SaveResult RepayRescueLoan(long amount);
    public SaveResult ExitRestrictedModeAfterDeparture();
}
```

`DateTime.UtcNow.Ticks`를 서비스 내부에서 직접 읽을 수도 있지만 테스트를 위해 provider 주입을 권장한다.

## 6. 도메인 검증 결과 전달

현재 결정은 공개 Progression Command가 1단계에서 `SaveResult`를 직접 반환하는 것이다. 그러나 `SaveResult`는 저장 실패만 표현하며 `NotEligible`, `ActiveLoanExists`, `RepaymentExceedsBalance` 같은 도메인 실패를 표현하지 않는다.

따라서 다음 중 1안을 1단계 호환 방식으로 요청한다.

### 권장 1단계

- UI가 먼저 `RescueLoanCalculator` 또는 read-only validation query로 도메인 상태를 확인한다.
- Command는 동일 검증을 다시 수행한다.
- Command 검증 실패는 저장을 호출하지 않고 `SaveResult.Failure(SaveFailureReason.InvalidData, ..., "rescueLoan")`를 반환한다.
- 구체적인 플레이어 문구는 사전 validation 결과의 `RescueLoanFailureReason`을 사용한다.

### 후속 확장

도메인 실패를 Command 한 번으로 전달해야 하면 다음 통합 결과를 별도 승인 후 추가한다.

```csharp
ProgressionCommandResult<IssueRescueLoanResult>
ProgressionCommandResult<RepayRescueLoanResult>
```

이번 구현에서 `SaveFailureReason`에 Progression 도메인 값을 추가하지 않는다.

## 7. IssueRescueLoan 변경 순서

```text
1. SaveData와 하위 player/rescueLoan 존재 확인
2. 현재 tradingCurrency와 rescueLoan snapshot
3. RescueLoanCalculator.Issue 입력 조립
4. 계산 실패면 Save 호출 없이 실패 반환
5. player.tradingCurrency = result.TradeMoneyAfter
6. rescueLoan 전체 필드 stage
7. rescueLoan.isActive = true
8. rescueLoan.isRestrictedPreparation = true
9. saveService.Save(data)
10. 저장 성공이면 발급 완료
11. 저장 실패면 tradingCurrency와 rescueLoan snapshot 복원
```

발급 원금은 부족분이 아니라 `definition.MinimumTradeCost` 전액이다.

저장 성공 전에는 다음을 실행하지 않는다.

- 성공 UI
- 대출 발급 event
- 제한 모드 화면 확정
- 다른 경제 기능 차단 event

## 8. RepayRescueLoan 변경 순서

```text
1. SaveData와 활성 대출 확인
2. 현재 tradingCurrency와 rescueLoan snapshot
3. RescueLoanCalculator.Repay 입력 조립
4. 계산 실패면 Save 호출 없이 실패 반환
5. player.tradingCurrency = result.TradeMoneyAfter
6. rescueLoan.remainingPrincipal = result.RemainingPrincipalAfter
7. rescueLoan.isActive = result.IsActiveAfter
8. rescueLoan.isRestrictedPreparation = result.IsRestrictedPreparationAfter
9. saveService.Save(data)
10. 저장 성공이면 상환 완료
11. 저장 실패면 tradingCurrency와 rescueLoan snapshot 복원
```

- 제한 모드 중 상환 요청은 거부한다.
- 상환 후 재화가 최소 거래 비용 미만이 되는 요청은 거부한다.
- 전액 상환 후 잔액 0, 비활성, 제한 모드 해제를 저장한다.
- 정산 수익에서 자동으로 상환하지 않는다.

## 9. 출발 성공과 제한 모드 해제

`isRestrictedPreparation`은 출발 검증 성공이 아니라 출발 상태의 즉시 저장 성공 뒤 해제되어야 한다.

권장 원자적 처리:

```text
Core 출발 검증 성공
-> TradeStartService가 Traveling/Trade ID/준비 상태 stage
-> isRestrictedPreparation=false stage
-> 한 번의 즉시 Save
-> 성공: Traveling 전환과 제한 모드 해제
-> 실패: 출발 상태와 제한 모드 모두 rollback
```

별도의 두 번 저장으로 출발과 제한 해제를 분리하지 않는다.

대출 잔액과 `isActive`는 출발 후에도 유지한다.

## 10. 재파산 연결

정산 완료 후 현재 재화와 대출 상태로 다음 판정을 호출한다.

```csharp
RescueLoanCalculator.EvaluateStatus(new RescueStatusInput
{
    UsableTradeMoney = saveData.player.tradingCurrency,
    MinimumTradeCost = definition.MinimumTradeCost,
    HasActiveLoan = saveData.rescueLoan.isActive
});
```

- `CanOfferLoan == true`: UI에 구조 대출 제안 가능 상태 전달
- `IsRebankrupt == true`: 신규 대출 금지, UI에 재파산 경고·게임오버 상태 전달
- Framework는 게임오버 화면을 직접 제어하지 않는다.

재파산 상태 자체를 SaveData에 별도 저장할지는 UI 재접속 흐름과 함께 후속 결정한다. 저장하지 않는 경우 로드 직후 동일 입력으로 결정적으로 재계산해야 한다.

## 11. 정산 자동 상환 제거 요청

현재 Economy 모델에 `SettlementInput.LoanRepayment`가 있고 계산기가 이를 비용으로 차감한다.

Framework/Core 연결부는 다음을 보장해야 한다.

- 일반 정산 조립 시 `LoanRepayment = 0`
- pending settlement snapshot에 자동 상환액을 확정하지 않음
- Claim 시 대출 잔액을 변경하지 않음
- 상환은 `RepayRescueLoan(amount)`만 사용

기존 필드 삭제는 호출부 inventory 후 별도 작업으로 진행한다.

## 12. Event 요청

저장 성공 뒤에만 다음 event를 발행한다.

- `RescueLoanIssued`
- `RescueLoanRepaid`
- `RescueLoanClosed`
- `RescueRestrictedModeEntered`
- `RescueRestrictedModeExited`
- `RescueRebankruptcyDetected`

같은 상태 변경에 대해 legacy/new event를 동시에 구독한 UI가 두 번 처리하지 않도록 canonical event를 하나로 정한다.

## 13. Framework 테스트 요청

### 저장·복구

- 구 세이브의 null `rescueLoan` 정규화
- 발급 저장 후 재실행 시 원금·잔액·활성·제한 상태 유지
- 부분 상환 저장 후 잔액 유지
- 전액 상환 저장 후 비활성 유지
- 출발 저장 성공 후 제한 상태 해제 및 대출 활성 유지

### rollback

- 발급 저장 실패 시 재화·대출 상태 원복
- 부분 상환 저장 실패 시 재화·잔액 원복
- 전액 상환 저장 실패 시 활성 상태 원복
- 출발 저장 실패 시 제한 상태 유지
- 저장 실패 시 committed event 미발행

### 중복·안전성

- 활성 대출 중 두 번째 발급 거부
- 동일 UI 요청 연속 입력 시 중복 발급 없음
- 음수·0·초과 상환 거부
- 정산/Claim에서 자동 상환 없음
- null·손상 DTO 정규화 후 예외 없이 로드

## 14. 완료 조건

- `SaveData.rescueLoan`과 정규화가 구현된다.
- 발급·상환 Command가 계산기를 사용한다.
- 재화와 대출 상태가 한 저장 경계에서 처리된다.
- 저장 실패 rollback 테스트가 통과한다.
- 출발 저장 성공 후에만 제한 모드가 해제된다.
- 자동 정산 상환이 발생하지 않는다.
- UI가 읽을 수 있는 대출·제한·재파산 snapshot/query가 제공된다.
