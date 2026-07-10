```markdown
# 정산 UI 데이터 연결 가이드

## 목적

이 문서는 정산 UI에서 사용할 수 있는 데이터, 사용해야 하는 API, 그리고 Framework 정산 흐름과 UI를 연결하는 방법을 설명한다.

정산 로직 자체는 Framework/Core가 소유한다.  
UI는 정산 데이터를 표시하고, 제공된 어댑터 API를 통해 정산 수령을 요청하는 역할만 담당한다.

---

## 전체 UI 데이터 흐름

```text
무역 완료
→ Framework가 TradeSettlementReady(tradeId, JourneyResultData) 발생
→ SettlementUiBridge가 결과 검증 및 캐시
→ SettlementUiDataAdapter가 결과를 SettlementViewData로 변환
→ ISettlementView 구현체가 데이터 표시
→ 수령 버튼이 SettlementUiDataAdapter.OnClickClaimSettlement 호출
→ Framework가 수령 처리 후 Preparation으로 복귀
```

---

## UI에서 사용할 수 있는 데이터

UI는 기본적으로 `SettlementViewData`를 사용한다.

### `SettlementViewData`

| 필드 | 타입 | 의미 | UI 사용처 |
|---|---|---|---|
| `TradeId` | `string` | 완료된 현재 무역 ID | 디버그 표시, 로그, QA 확인 |
| `Grade` | `JourneyResultGrade` | 정산 결과 등급 | 메인 결과 제목/상태 |
| `FailureReason` | `JourneyFailureReason` | 실패 시 실패 사유 | 실패 설명 텍스트 |
| `Revenue` | `int` | 판매 수익 | 정산 수치 항목 |
| `Cost` | `int` | 총 비용 | 정산 수치 항목 |
| `NetProfit` | `int` | 최종 순이익 또는 손실 | 강조 표시할 핵심 결과 값 |
| `IsFailed` | `bool` | 결과가 실패인지 여부 | 실패 결과 스타일 적용 |
| `CanClaim` | `bool` | 수령 버튼 활성화 가능 여부 | 수령 버튼 interactable 상태 |
| `StatusMessage` | `string` | 기본 상태 메시지 | 임시 상태/디버그 텍스트 |

---

## 결과 등급 값

### `JourneyResultGrade`

| 값 | 의미 | 추천 UI 표시 |
|---|---|---|
| `Success` | 무역을 정상 완료함 | 성공 제목, 긍정 색상 |
| `PartialSuccess` | 무역은 완료했지만 손실이 있음 | 경고 제목, 일부 손실 표시 |
| `Failed` | 도착 전 무역 실패 | 실패 제목, 손실/복구 표시 |

---

## 실패 사유 값

### `JourneyFailureReason`

| 값 | 의미 | 추천 UI 표시 |
|---|---|---|
| `None` | 실패 아님 | 실패 사유 숨김 또는 `None` 표시 |
| `FoodDepleted` | 식량 고갈 | 식량 부족 설명 표시 |
| `AnimalsLost` | 견인 동물 상실 | 견인 동물 손실 설명 표시 |
| `NotEnoughAnimals` | 남은 견인 동물 수 부족 | 마차 운행 불가 설명 표시 |

---

## UI 계약

실제 정산 UI 또는 테스트 UI는 `ISettlementView`를 구현해야 한다.

```csharp
public interface ISettlementView
{
    void ShowSettlement(SettlementViewData viewData);
    void ShowNoSettlement(string reason);
    void SetClaimInteractable(bool interactable);
}
```

### 메서드 역할

#### `ShowSettlement(SettlementViewData viewData)`

유효한 정산 데이터가 있을 때 호출된다.

UI에서 해야 할 일:

- 결과 등급 표시
- 필요하면 무역 ID 표시
- 판매 수익, 비용, 순이익 표시
- 실패한 경우 실패 사유 표시
- 성공/실패 스타일 적용
- `viewData.CanClaim` 값에 따라 수령 버튼 활성/비활성 처리

#### `ShowNoSettlement(string reason)`

정산 데이터가 없거나 유효하지 않을 때 호출된다.

UI에서 해야 할 일:

- 정산 수치 숨김 또는 placeholder 표시
- 테스트 UI라면 디버그/상태 메시지 표시
- 수령 버튼 비활성화

#### `SetClaimInteractable(bool interactable)`

수령 버튼 상태를 바꿔야 할 때 호출된다.

UI에서 해야 할 일:

- 수령 버튼의 interactable 상태 변경
- 처리 중 반복 입력 방지

---

## 어댑터 API

UI 버튼은 `JourneyRunner` 또는 `TradeProgressCoordinator`를 직접 호출하지 않고 `SettlementUiDataAdapter`를 호출해야 한다.

### `SettlementUiDataAdapter`

주요 역할:

- 정산 이벤트 수신
- `SettlementUiBridge`에서 데이터 읽기
- `JourneyResultData`를 `SettlementViewData`로 변환
- `ISettlementView`에 데이터 전달
- 수령 버튼 클릭 처리

### Inspector 필드

| 필드 | 타입 | 연결할 대상 |
|---|---|---|
| `settlementViewBehaviour` | `MonoBehaviour` | `ISettlementView`를 구현한 컴포넌트 |
| `refreshOnEnable` | `bool` | 보통 `true` |

---

## 수령 버튼 API

수령 버튼은 아래 메서드에 연결한다.

```csharp
SettlementUiDataAdapter.OnClickClaimSettlement()
```

아래 메서드들에는 직접 연결하지 않는다.

```csharp
JourneyRunner.ClaimSettlement(...)
TradeProgressCoordinator.ClaimSettlementAndReset()
TradeStartDebugHarness.ClaimSettlementAndReset()
```

어댑터 경로를 사용하는 이유:

- UI 레벨의 중복 입력 방어가 포함되어 있다.
- UI가 Core 로직에 직접 의존하지 않는다.
- 이후 실제 정산 UI로 교체해도 연결 구조를 유지하기 쉽다.

---

## 현재 InGame 테스트 UI 연결

`InGame.unity`에는 기존 오브젝트가 있다.

```text
InGameCanvas
└── TradeTestPannel
    ├── PreparationPanel
    ├── TravelingPanel
    └── SettlementPanel
```

기존 테스트 텍스트 오브젝트:

```text
Txt_tradeId
Txt_grade
Txt_failureReason
Txt_netProfit
```

기존 버튼:

```text
Start Trade Button
CompleteTrade Button
Claim Settlement Button
```

---

## 테스트 View 컴포넌트

현재 InGame 테스트 씬에서는 `InGameSettlementTestView`를 사용한다.

### 연결할 참조

| 필드 | 연결할 오브젝트 |
|---|---|
| `preparationPanel` | `PreparationPanel` |
| `travelingPanel` | `TravelingPanel` |
| `settlementPanel` | `SettlementPanel` |
| `tradeIdText` | `Txt_tradeId` |
| `gradeText` | `Txt_grade` |
| `failureReasonText` | `Txt_failureReason` |
| `netProfitText` | `Txt_netProfit` |
| `claimSettlementButton` | `Claim Settlement Button` |

---

## Unity Editor 설정 절차

1. `Assets/_Project/07.Scenes/04_InGame/InGame.unity`를 연다.
2. `TradeTestPannel`을 선택한다.
3. `InGameSettlementTestView` 컴포넌트를 추가한다.
4. `SettlementUiDataAdapter` 컴포넌트를 추가한다.
5. `InGameSettlementTestView`에 모든 panel/text/button 참조를 연결한다.
6. `SettlementUiDataAdapter`에 다음을 연결한다.
   - `settlementViewBehaviour` → `InGameSettlementTestView`
   - `refreshOnEnable` → `true`
7. `Claim Settlement Button`을 선택한다.
8. 기존 OnClick 연결을 제거한다.
   - `TradeStartDebugHarness.ClaimSettlementAndReset`
9. 새 OnClick 연결을 추가한다.
   - Target: `SettlementUiDataAdapter`가 붙은 오브젝트
   - Method: `SettlementUiDataAdapter.OnClickClaimSettlement`

기존 연결은 유지한다.

| 버튼 | 기존 메서드 |
|---|---|
| `Start Trade Button` | `TradeStartDebugHarness.StartTradeAndRecordTime` |
| `CompleteTrade Button` | `InGameSceneController.CompleteTradeImmediately` |

---

## 추천 UI 표시 매핑

| UI 요소 | 사용할 데이터 |
|---|---|
| 결과 제목 | `SettlementViewData.Grade` |
| 무역 ID 텍스트 | `SettlementViewData.TradeId` |
| 실패 사유 텍스트 | `SettlementViewData.FailureReason` |
| 판매 수익 항목 | `SettlementViewData.Revenue` |
| 비용 항목 | `SettlementViewData.Cost` |
| 순이익 강조 표시 | `SettlementViewData.NetProfit` |
| 수령 버튼 활성화 | `SettlementViewData.CanClaim` |
| 실패 스타일 적용 | `SettlementViewData.IsFailed` |
| 임시 상태 텍스트 | `SettlementViewData.StatusMessage` |

---

## 수동 테스트 흐름

1. Play Mode에 진입한다.
2. `Start Trade Button`을 클릭한다.
3. UI가 Traveling 상태로 변경되는지 확인한다.
4. `CompleteTrade Button`을 클릭한다.
5. UI가 Settlement 상태로 변경되는지 확인한다.
6. 정산 필드가 갱신되는지 확인한다.
   - `Txt_tradeId`
   - `Txt_grade`
   - `Txt_failureReason`
   - `Txt_netProfit`
7. `Claim Settlement Button`을 빠르게 여러 번 클릭한다.
8. 수령 처리가 한 번만 실행되는지 확인한다.
9. UI가 Preparation 상태로 돌아오는지 확인한다.
10. `Start Trade Button`을 다시 클릭한다.
11. 두 번째 무역이 정상적으로 시작되는지 확인한다.

---

## 문제 해결

### Settlement 패널은 열리는데 텍스트가 비어 있음

확인할 것:

- `SettlementUiDataAdapter.settlementViewBehaviour`가 연결되어 있는가
- 연결된 컴포넌트가 `ISettlementView`를 구현하는가
- `InGameSettlementTestView`의 텍스트 참조가 모두 연결되어 있는가

### 수령 버튼이 동작하지 않음

확인할 것:

- 버튼 OnClick이 `SettlementUiDataAdapter.OnClickClaimSettlement`를 가리키는가
- `claimSettlementButton`이 `InGameSettlementTestView`에 연결되어 있는가
- `FrameworkRoot` 아래에 `SettlementUiBridge`가 존재하는가

### Console에 `No settlement result`가 표시됨

가능한 원인:

- 무역이 아직 완료되지 않음
- `TradeSettlementReady`가 발생하지 않음
- 정산 이벤트의 trade ID가 현재 active trade ID와 일치하지 않음
- 현재 상태가 `SettlementPending`이 아님

### 중복 수령이 발생함

확인할 것:

- 버튼이 아직 `TradeStartDebugHarness.ClaimSettlementAndReset`에 연결되어 있지 않은가
- 버튼이 `SettlementUiDataAdapter.OnClickClaimSettlement`만 호출하는가

---

## 소유권 정리

Framework 소유:

- `SettlementUiBridge`
- `SettlementUiDataAdapter`
- `SettlementViewData`
- `ISettlementView`

UI 소유:

- 최종 정산 화면 레이아웃
- 텍스트 포맷
- 아이콘, 색상, 애니메이션
- 로컬라이징
- 실제 서비스용 `ISettlementView` 구현체

테스트 전용:

- `InGameSettlementTestView`

---

## 현재 M1 한계

M1에서는 정산 결과 데이터를 현재 세션의 메모리 안에서만 유지한다.

플레이어가 `SettlementPending` 상태에서 앱을 종료하면 상세 정산 결과 복원은 아직 보장되지 않는다.  
저장 기반 정산 결과 복구는 M3에서 처리할 예정이다.
```
