```markdown
# M1 Settlement UI Entry & Data Adapter Summary

## 목적

이번 작업의 목적은 Core/Framework에서 이미 생성한 정산 결과(`JourneyResultData`)를 실제 UI에서 안정적으로 받을 수 있도록 연결 구조를 정리하는 것이다.

핵심 목표는 다음과 같다.

- `TradeSettlementReady(tradeId, result)` 이벤트를 UI 진입 흐름에 연결한다.
- 정산 결과가 claim 전까지 사라지지 않도록 런타임 캐시 수명주기를 정리한다.
- UI가 `JourneyRunner`나 Core 로직을 직접 호출하지 않도록 한다.
- 테스트 UI와 향후 실제 정산 UI가 같은 데이터 연결 구조를 사용할 수 있게 한다.

---

## 변경된 주요 구조

### 1. `TradeProgressCoordinator`

역할:

- 무역 진행 완료 감지
- `JourneyRunner.Settle` 호출
- 정산 결과 캐시
- 정산 claim/reset 실행

정리된 사항:

- `CheckProgressAndCompletion()` 시작 시 `LastSettlementResult = null` 하던 로직 제거
- `LastSettlementTradeId` 추가
- 정산 결과 캐시를 `tradeId + JourneyResultData`로 관리
- claim 시 다음 조건 검증:
  - `LastSettlementResult != null`
  - `LastSettlementTradeId == activeTradeId`
  - `tradeProgress.state == SettlementPending`
- claim 성공 후 캐시 제거

---

### 2. `TradeStartService`

역할:

- 무역 시작 처리
- 출발 성공 시 저장 상태 갱신
- 화면 상태를 `Traveling`으로 전환

정리된 사항:

- 새 무역 시작이 성공했을 때 이전 정산 캐시를 명시적으로 제거
- 실패한 출발 시도에서는 캐시를 제거하지 않음

---

### 3. `SettlementUiBridge`

역할:

- `TradeSettlementReady` 이벤트를 지속적으로 구독하는 Framework-side bridge
- 정산 이벤트 수신 시 유효성 검증
- 현재 세션의 정산 결과 캐시
- UI claim 요청을 `TradeProgressCoordinator.ClaimSettlementAndReset()`으로 전달

검증 조건:

- `result != null`
- 이벤트의 `tradeId`가 현재 `activeTradeId`와 일치
- 현재 상태가 `SettlementPending`

UI는 이 bridge를 통해 정산 데이터를 읽고 claim을 요청한다.

```csharp
FrameworkRoot.Instance.SettlementUiBridge.TryGetPendingSettlement(
    out string tradeId,
    out JourneyResultData result);
```

```csharp
FrameworkRoot.Instance.SettlementUiBridge.ClaimSettlementAndReset();
```

---

## UI 데이터 어댑터 구조

### 1. `SettlementViewData`

UI 표시용 데이터 모델이다.

포함 필드:

- `TradeId`
- `Grade`
- `FailureReason`
- `Revenue`
- `Cost`
- `NetProfit`
- `IsFailed`
- `CanClaim`
- `StatusMessage`

목적:

- UI가 raw `JourneyResultData`에 과하게 의존하지 않도록 한다.
- 이후 실제 UI, 로컬라이징, 상세 정산 항목 추가 시 변경 범위를 줄인다.

---

### 2. `ISettlementView`

정산 UI가 구현해야 하는 최소 계약이다.

```csharp
public interface ISettlementView
{
    void ShowSettlement(SettlementViewData viewData);
    void ShowNoSettlement(string reason);
    void SetClaimInteractable(bool interactable);
}
```

테스트 UI와 실제 UI 모두 이 인터페이스를 구현할 수 있다.

---

### 3. `SettlementUiDataAdapter`

역할:

- `SettlementUiBridge`에서 정산 결과를 읽음
- `JourneyResultData`를 `SettlementViewData`로 변환
- `ISettlementView`에 표시 요청
- claim 버튼 클릭 처리
- 중복 claim 입력 방지

Unity Button에는 다음 메서드를 연결한다.

```csharp
SettlementUiDataAdapter.OnClickClaimSettlement()
```

이 어댑터는 TextMeshPro, Panel, Button 같은 구체 UI 요소를 직접 알지 않는다.

---

### 4. `InGameSettlementTestView`

현재 `InGame.unity` 테스트 UI 전용 View 구현이다.

연결 대상:

- `PreparationPanel`
- `TravelingPanel`
- `SettlementPanel`
- `Txt_tradeId`
- `Txt_grade`
- `Txt_failureReason`
- `Txt_netProfit`
- `Claim Settlement Button`

역할:

- `ISettlementView` 구현
- 패널 전환
- 정산 결과 텍스트 표시
- claim 버튼 interactable 제어

---

## InGame 씬 연결 방식

현재 `InGame.unity`에는 다음 테스트 UI가 존재한다.

```text
InGameCanvas
├── Button Horizontal Canvas
│   ├── Start Trade Button
│   ├── CompleteTrade Button
│   └── Claim Settlement Button
└── TradeTestPannel
    ├── PreparationPanel
    ├── TravelingPanel
    └── SettlementPanel
```

권장 연결:

1. `TradeTestPannel`에 `InGameSettlementTestView` 추가
2. 같은 오브젝트에 `SettlementUiDataAdapter` 추가
3. `SettlementUiDataAdapter.settlementViewBehaviour`에 `InGameSettlementTestView` 연결
4. `Claim Settlement Button`의 기존 OnClick 제거
5. `Claim Settlement Button` OnClick에 `SettlementUiDataAdapter.OnClickClaimSettlement` 연결

기존 버튼 연결은 유지한다.

- `Start Trade Button` → `TradeStartDebugHarness.StartTradeAndRecordTime`
- `CompleteTrade Button` → `InGameSceneController.CompleteTradeImmediately`

---

## 전체 흐름

```text
Start Trade Button
→ TradeStartService.TryStartTrade
→ TradeProgressState.Traveling
→ InGameScreenState.Traveling

CompleteTrade Button
→ CompleteTradeImmediately
→ TradeProgressCoordinator.ForceCompleteActiveTrade
→ JourneyRunner.Settle
→ LastSettlementTradeId + LastSettlementResult 캐시
→ TradeSettlementReady(tradeId, result)
→ SettlementUiBridge 캐시
→ SettlementUiDataAdapter 수신
→ InGameSettlementTestView 표시
→ InGameScreenState.Settlement

Claim Settlement Button
→ SettlementUiDataAdapter.OnClickClaimSettlement
→ SettlementUiBridge.ClaimSettlementAndReset
→ TradeProgressCoordinator.ClaimSettlementAndReset
→ JourneyRunner.ClaimSettlement
→ JourneyRunner.ResetToPrepare
→ Save
→ 캐시 제거
→ InGameScreenState.Preparation
```

---

## 확인해야 할 테스트 항목

### 기본 성공 흐름

- `Start Trade Button` 클릭 시 Traveling 상태로 전환된다.
- `CompleteTrade Button` 클릭 시 Settlement 상태로 전환된다.
- `Txt_tradeId`에 현재 trade ID가 표시된다.
- `Txt_grade`에 결과 등급이 표시된다.
- `Txt_failureReason`에 실패 사유 또는 `None`이 표시된다.
- `Txt_netProfit`에 정산 순이익이 표시된다.

### Claim 흐름

- `Claim Settlement Button` 클릭 시 claim이 한 번만 처리된다.
- 빠르게 여러 번 클릭해도 중복 보상이 발생하지 않는다.
- claim 성공 후 Preparation 상태로 돌아온다.
- claim 성공 후 정산 캐시가 제거된다.
- 이후 두 번째 무역을 정상적으로 시작할 수 있다.

### 방어 로직

- 정산 결과가 없으면 claim이 차단된다.
- trade ID가 현재 active trade와 다르면 claim이 차단된다.
- 상태가 `SettlementPending`이 아니면 claim이 차단된다.
- `SettlementUiBridge`가 없으면 UI에 `No settlement bridge.` 상태가 표시된다.

---

## 현재 한계

- M1에서는 정산 결과를 세션 메모리 캐시로만 유지한다.
- 앱 재시작 또는 저장 복구 후 `SettlementPending` 상태의 상세 결과 복원은 M3에서 별도 처리해야 한다.
- 실제 최종 정산 UI의 레이아웃, 연출, 상세 항목 표시, 로컬라이징은 UI 담당 영역으로 남긴다.
- `.unity` YAML은 직접 편집하지 않고 Unity Editor에서 연결해야 한다.
```
