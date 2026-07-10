```markdown
# InGame Scene State Routing 정리

## 1. 작업 목적

이번 작업의 목적은 Core/Framework가 관리하는 무역 진행 상태를 InGame UI 화면 상태로 연결하는 것입니다.

기존 흐름은 다음 단계까지 구현되어 있었습니다.

```text
JourneyRunner.Settle
→ TradeProgressState.SettlementPending
→ TradeSettlementReady 이벤트 발생
```

하지만 실제 InGame 화면을 어떤 상태로 보여줄지 결정하는 라우팅 계층이 필요했습니다.

이번 작업은 Framework가 화면 상태를 결정하고, UI는 해당 상태를 받아 패널을 켜고 끄는 구조로 분리했습니다.

```text
Framework
→ 현재 보여줄 InGame 화면 상태 결정

UI
→ 패널 활성화, 텍스트 갱신, 버튼 연결
```

---

## 2. 추가된 주요 개념

### InGameScreenState

InGame에서 표시할 화면을 3가지 상태로 단순화했습니다.

```csharp
public enum InGameScreenState
{
    Preparation,
    Traveling,
    Settlement
}
```

각 상태의 의미는 다음과 같습니다.

| Screen State | 의미 |
|---|---|
| `Preparation` | 무역 준비 화면 |
| `Traveling` | 무역 이동 진행 화면 |
| `Settlement` | 정산 화면 |

---

## 3. 저장 상태와 화면 상태 매핑

`SaveData.tradeProgress.state` 값을 기준으로 화면 상태를 결정합니다.

| TradeProgressState | InGameScreenState |
|---|---|
| `None` | `Preparation` |
| `Preparing` | `Preparation` |
| `Traveling` | `Traveling` |
| `SettlementPending` | `Settlement` |
| `Completed` | `Preparation` |
| `Failed` | `Preparation` |

주의할 점은 실패한 무역도 Core의 `JourneyState`에서는 별도 실패 상태가 아니라 정산 흐름을 거친다는 점입니다.

실패 정산 화면은 보통 다음 조합으로 판단하는 것이 맞습니다.

```text
TradeProgressState.SettlementPending
+ JourneyResultData.grade == Failed
```

즉, `TradeProgressState.Failed` 자체를 정산 화면으로 직접 연결하지 않고, 이미 정산 수령 후 완료된 실패 상태로 보고 `Preparation`으로 보냅니다.

---

## 4. 추가된 파일

### `InGameScreenState.cs`

위치:

```text
Assets/_Project/11.CoreServices/Scripts/SceneFlow/InGameScreenState.cs
```

역할:

- InGame 화면 상태 enum 정의
- UI가 구독할 수 있는 공용 화면 상태 타입 제공

---

### `InGameScreenStateRouter.cs`

위치:

```text
Assets/_Project/11.CoreServices/Scripts/SceneFlow/InGameScreenStateRouter.cs
```

역할:

- `SaveData`에서 현재 무역 진행 상태 확인
- `TradeProgressState`를 `InGameScreenState`로 변환
- 현재 화면 상태를 저장
- 같은 화면 상태 요청은 중복 이벤트로 보내지 않음
- 화면 상태가 바뀌면 `FrameworkEvents.InGameScreenChanged` 발생

핵심 동작:

```text
RefreshFromSaveData(saveData)
→ SaveData.tradeProgress.state 확인
→ InGameScreenState로 매핑
→ RequestScreen(screenState)
→ 중복 여부 확인
→ InGameScreenChanged 이벤트 발생
```

---

## 5. 수정된 파일과 역할

### `FrameworkEvents.cs`

위치:

```text
Assets/_Project/11.CoreServices/Scripts/Events/FrameworkEvents.cs
```

추가된 이벤트:

```csharp
public static event Action<InGameScreenState> InGameScreenChanged;
```

추가된 호출 메서드:

```csharp
public static void RaiseInGameScreenChanged(InGameScreenState screenState)
```

UI는 이 이벤트를 구독해서 실제 패널 전환을 처리하면 됩니다.

---

### `FrameworkRoot.cs`

위치:

```text
Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs
```

역할:

- `InGameScreenStateRouter` 생성
- `TradeStartService`, `TradeProgressCoordinator`에 라우터 전달
- 저장 데이터를 불러온 뒤 현재 화면 상태를 초기화
- Loading 완료 후 InGame 진입 전 현재 저장 상태를 기준으로 화면 상태 갱신

---

### `TradeStartService.cs`

위치:

```text
Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeStartService.cs
```

추가된 동작:

```text
무역 출발 성공
→ TradeProgressState.Traveling 기록
→ InGameScreenState.Traveling 요청
→ InGameScreenChanged 이벤트 발생
```

즉, 무역 출발에 성공하면 UI는 이동 진행 화면으로 전환할 수 있습니다.

---

### `TradeProgressCoordinator.cs`

위치:

```text
Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs
```

추가된 동작 1:

```text
무역 도착 또는 실패 확정
→ JourneyRunner.Settle
→ TradeProgressState.SettlementPending
→ TradeSettlementReady 이벤트 발생
→ InGameScreenState.Settlement 요청
```

추가된 동작 2:

```text
정산 수령
→ JourneyRunner.ClaimSettlement
→ JourneyRunner.ResetToPrepare
→ InGameScreenState.Preparation 요청
```

---

### `InGameSceneController.cs`

위치:

```text
Assets/_Project/11.CoreServices/Scripts/SceneFlow/InGameSceneController.cs
```

추가된 동작:

```text
InGame 씬 Start
→ 현재 SaveData 기준으로 화면 상태 갱신
→ InGameScreenChanged 이벤트 강제 발생
```

이 처리는 InGame 씬에 새로 로드된 UI 오브젝트가 현재 화면 상태를 받을 수 있도록 하기 위한 것입니다.

---

## 6. 전체 로직 흐름

```text
InGame 씬 진입
→ FrameworkRoot.CurrentSaveData 확인
→ InGameScreenStateRouter.RefreshFromSaveData
→ TradeProgressState를 InGameScreenState로 매핑
→ FrameworkEvents.InGameScreenChanged 발생
→ UI가 해당 화면 패널 표시
```

실시간 상태 변화 흐름은 다음과 같습니다.

```text
무역 출발 성공
→ InGameScreenState.Traveling
→ 이동 진행 화면 표시

무역 도착 또는 실패 정산 준비
→ InGameScreenState.Settlement
→ 정산 화면 표시

정산 수령 및 리셋
→ InGameScreenState.Preparation
→ 무역 준비 화면 표시
```

---

## 7. 중복 이벤트 방지

`InGameScreenStateRouter`는 현재 화면 상태를 기억합니다.

따라서 같은 화면 상태를 다시 요청하면 이벤트를 다시 발생시키지 않습니다.

예시:

```text
현재 상태: Traveling
요청 상태: Traveling
→ 무시
```

단, InGame 씬에 새로 진입했을 때는 UI가 현재 상태를 다시 받아야 하므로 강제 알림이 발생할 수 있습니다.

```text
InGameSceneController.Start
→ RefreshFromSaveData(forceNotify: true)
```

---

# 테스트 방법

## 1. 컴파일 확인

Unity 에디터를 열고 Console에 C# 컴파일 에러가 없는지 확인합니다.

확인 대상:

```text
InGameScreenState.cs
InGameScreenStateRouter.cs
FrameworkEvents.cs
FrameworkRoot.cs
TradeStartService.cs
TradeProgressCoordinator.cs
InGameSceneController.cs
```

---

## 2. InGame 씬 준비

Unity에서 InGame 씬을 엽니다.

```text
Assets/_Project/07.Scenes/04_InGame/InGame.unity
```

씬 안에 `InGameSceneController`가 붙은 GameObject가 있어야 합니다.

없다면 Unity 에디터에서 빈 GameObject를 만들고 `InGameSceneController` 컴포넌트를 붙여 테스트합니다.

---

## 3. 이벤트 로그 확인

UI 연결 전에도 Console 로그로 라우팅 이벤트를 확인할 수 있습니다.

예상 로그:

```text
InGameScreenChanged event raised. ScreenState: Preparation
InGameScreenChanged event raised. ScreenState: Traveling
InGameScreenChanged event raised. ScreenState: Settlement
```

---

## 4. InGame 진입 테스트

### 절차

1. Play Mode 실행
2. InGame 씬 진입
3. Console 로그 확인

### 기대 결과

저장 상태에 따라 다음 이벤트가 발생해야 합니다.

| 저장 상태 | 기대 화면 이벤트 |
|---|---|
| `None` | `Preparation` |
| `Preparing` | `Preparation` |
| `Completed` | `Preparation` |
| `Failed` | `Preparation` |
| `Traveling` | `Traveling` |
| `SettlementPending` | `Settlement` |

---

## 5. 무역 출발 테스트

### 절차

1. InGame 씬에서 무역 출발 기능 실행
2. `TradeStartService.TryStartTrade(...)`가 성공하도록 유효한 상단 데이터 사용
3. Console 로그 확인

### 기대 흐름

```text
TradeStartService.TryStartTrade
→ TradeProgressRecorder.RecordStartedTrade
→ TradeProgressState.Traveling
→ InGameScreenState.Traveling
```

### 기대 로그

```text
InGameScreenChanged event raised. ScreenState: Traveling
```

---

## 6. 정산 화면 진입 테스트

### 절차

1. 무역이 `Traveling` 상태인 상태에서 무역 완료 처리
2. 디버그 버튼 또는 즉시 완료 기능 사용
3. Console 로그 확인

### 기대 흐름

```text
CompleteTradeImmediately
→ TradeProgressCoordinator.ForceCompleteActiveTrade
→ JourneyRunner.Settle
→ TradeProgressState.SettlementPending
→ TradeSettlementReady
→ InGameScreenState.Settlement
```

### 기대 로그

```text
TradeSettlementReady event raised.
InGameScreenChanged event raised. ScreenState: Settlement
```

---

## 7. 정산 완료 후 준비 화면 복귀 테스트

### 절차

1. 정산 화면 상태에서 정산 수령 실행
2. `TradeProgressCoordinator.ClaimSettlementAndReset()` 호출
3. Console 로그 확인

### 기대 흐름

```text
ClaimSettlementAndReset
→ JourneyRunner.ClaimSettlement
→ JourneyRunner.ResetToPrepare
→ InGameScreenState.Preparation
```

### 기대 로그

```text
InGameScreenChanged event raised. ScreenState: Preparation
```

---

## 8. 씬 재진입 복구 테스트

### 절차

1. 특정 무역 상태를 저장
2. Title 또는 Loading을 거쳐 InGame 씬에 다시 진입
3. Console 로그 확인

### 기대 결과

| 저장된 상태 | InGame 재진입 시 기대 화면 |
|---|---|
| `Traveling` | `Traveling` |
| `SettlementPending` | `Settlement` |
| `Completed` | `Preparation` |
| `Preparing` | `Preparation` |

이 테스트는 저장 상태를 기준으로 올바른 화면이 복구되는지 확인하는 핵심 테스트입니다.

---

## 9. 중복 이벤트 테스트

### 절차

1. 이미 `Traveling` 상태인 상태에서 다시 `Traveling` 요청
2. 이미 `Settlement` 상태인 상태에서 반복 완료 체크
3. 이미 `Preparation` 상태인 상태에서 반복 리셋 시도
4. Console 로그 개수 확인

### 기대 결과

같은 화면 상태에 대한 이벤트가 반복해서 발생하지 않아야 합니다.

예외:

```text
InGame 씬 진입 시 forceNotify: true
```

이 경우는 새로 로드된 UI가 현재 상태를 받아야 하므로 같은 상태라도 이벤트가 한 번 더 발생할 수 있습니다.

---

## 10. UI 연결 후 확인

UI 담당자는 다음 이벤트를 구독합니다.

```csharp
FrameworkEvents.InGameScreenChanged += OnInGameScreenChanged;
```

예상 처리:

```text
Preparation
→ 준비 화면 패널 활성화

Traveling
→ 이동 진행 화면 패널 활성화

Settlement
→ 정산 화면 패널 활성화
```

주의:

- Framework는 어떤 화면을 보여줄지만 결정합니다.
- UI는 실제 GameObject 활성화, 텍스트 갱신, 버튼 연결을 담당합니다.
- Scene/Prefab 수정은 Unity 에디터에서 직접 수행합니다.
```
