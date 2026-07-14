```markdown
# Trade Timing Completion Logic

## 목적

이번 구현은 Framework가 무역 시작 시간과 종료 예정 시간을 저장하고, 저장된 시간 정보를 기준으로 Core의 무역 진행도를 갱신한 뒤, 도착 또는 실패 시 정산 대기 상태로 전환하는 기능이다.

Core의 Caravan/Journey 로직은 재구현하지 않고 기존 API를 호출한다.

## 주요 흐름

```text
무역 시작
→ Core 출발 검증 및 소요 시간 계산
→ Framework 시작 시간 / 종료 예정 시간 저장
→ Caravan 상태 저장
→ 시간 경과에 따라 진행도 계산
→ Core 진행도 갱신
→ 도착 또는 실패 판정
→ Core 정산 생성
→ Framework 정산 대기 이벤트 발생
→ 정산 수령
→ 다음 무역 준비 상태로 복귀
```

## 역할 분리

### Core 담당

Core는 무역 자체의 규칙을 담당한다.

- 출발 가능 여부 검증
- 적재량 계산
- 이동 소요 시간 계산
- 진행도 반영
- 식량 고갈 실패 판정
- 도착 판정
- 정산 결과 생성
- 중복 정산 방지
- 다음 무역 준비 상태 전환

사용 API:

```csharp
JourneyRunner.TryDepart(caravan, distanceKm);
JourneyRunner.SetProgress(caravan, progress01);
JourneyRunner.IsArrived(caravan);
JourneyRunner.Settle(caravan);
JourneyRunner.ClaimSettlement(caravan);
JourneyRunner.ResetToPrepare(caravan);
```

### Framework 담당

Framework는 시간, 저장, 이벤트 연결을 담당한다.

- 무역 시작 시 현재 UTC 시간 기록
- 예상 종료 UTC 시간 기록
- 활성 Caravan 상태 저장
- 저장된 시간으로 진행도 계산
- Core에 진행도 전달
- 정산 대기 상태 저장
- 정산 준비 이벤트 발생
- 디버그 즉시 완료 요청 처리

## 저장 데이터

`SaveData`에는 다음 정보가 저장된다.

### TradeProgressSaveData

```text
activeTradeId
activeRouteId
state
tradeStartUtcTick
expectedTradeEndUtcTick
```

이 데이터는 무역 시간 판단의 기준이다.

### CaravanSaveData

`CaravanData`를 복원하거나 동기화하기 위한 최소 상태를 저장한다.

```text
wagon
animals
mercenaries
cargo
foodAmount
foodUnitWeight
state
currentDistanceKm
totalSeconds
progress01
settlementClaimed
runCargoLost
runFoodLost
runFatalReason
```

계산 가능한 값은 저장하지 않는다.

예를 들어 다음 값은 Core 계산 결과이므로 중복 저장하지 않는다.

```text
currentLoad
requiredFood
remainingFood
```

## 주요 클래스

### TradeStartService

무역 시작 진입점이다.

처리 순서:

1. `JourneyRunner.TryDepart` 호출
2. Core가 출발 가능 여부와 이동 시간 계산
3. `TradeProgressRecorder.RecordStartedTrade` 호출
4. 시작 시간과 종료 예정 시간 저장
5. `CaravanSaveDataMapper.CopyToSave`로 Caravan 상태 저장
6. 저장 서비스 호출

### TradeProgressRecorder

무역 시간 저장과 Framework 상태 변경을 담당한다.

주요 역할:

- 시작 시간 기록
- 종료 예정 시간 기록
- `Traveling` 상태 기록
- `SettlementPending` 상태 기록
- `Completed` 또는 `Failed` 상태 기록

### CaravanSaveDataMapper

`CaravanData`와 `CaravanSaveData` 사이를 변환한다.

이 클래스는 규칙을 판단하지 않는다.

- 출발 검증 없음
- 진행도 계산 없음
- 식량 판정 없음
- 정산 판정 없음

단순히 저장 데이터와 런타임 데이터 구조를 복사한다.

### TradeProgressCoordinator

이번 구현의 핵심 연결자다.

처리 순서:

1. 현재 `SaveData` 확인
2. `TradeProgressState.Traveling`인지 확인
3. 저장된 `tradeStartUtcTick`, `expectedTradeEndUtcTick` 읽기
4. 현재 UTC 시간 기준으로 진행도 계산

```text
progress01 = (now - start) / (end - start)
```

5. `JourneyRunner.SetProgress` 호출
6. Core가 식량 고갈 여부를 판단
7. 도착 또는 실패 여부 확인
8. 조건 충족 시 `JourneyRunner.Settle` 호출
9. Framework 상태를 `SettlementPending`으로 변경
10. `TradeSettlementReady` 이벤트 발생

### FrameworkEvents

정산 준비 이벤트가 추가되었다.

```csharp
TradeSettlementReady(string tradeId, JourneyResultData result)
```

이 이벤트는 추후 정산 UI 또는 InGame 화면 전환에서 사용할 수 있다.

## 즉시 완료 디버그 흐름

`FrameworkDebugCommands.CompleteTradeImmediately`는 직접 정산하지 않는다.

대신 `CompleteTradeRequested` 이벤트를 발생시킨다.

`TradeProgressCoordinator`가 이 이벤트를 구독하고 다음 순서로 처리한다.

```text
CompleteTradeRequested
→ active caravan progress = 1
→ JourneyRunner.SetProgress
→ JourneyRunner.Settle
→ SaveData state = SettlementPending
→ TradeSettlementReady event
```

따라서 일반 시간 완료와 즉시 완료는 같은 정산 경로를 사용한다.

## 테스트 씬 검증 포인트

테스트 씬에서는 `TradeStartDebugHarness`를 사용한다.

검증 순서:

1. 샘플 Caravan 생성
2. 무역 시작
3. 저장 데이터 출력
4. 진행도 갱신 확인
5. 시간 완료 또는 즉시 완료
6. 정산 결과 확인
7. 정산 수령
8. 준비 상태 복귀 확인

기대 결과:

```text
tradeStartUtcTick > 0
expectedTradeEndUtcTick > tradeStartUtcTick
state == Traveling
progress01 증가
도착 시 SettlementPending
TradeSettlementReady 이벤트 발생
정산 수령 후 Prepare 상태 복귀
중복 정산 불가
```

## 주의 사항

- Core 파일은 수정하지 않는다.
- Core 규칙을 Framework에 복사하지 않는다.
- Framework는 시간과 저장, 이벤트 연결만 담당한다.
- Unity 씬이나 프리팹 테스트 시 `.meta`와 serialized reference를 유지해야 한다.
- 기존 저장 파일은 `SaveData.CurrentVersion` 변경으로 새 버전과 호환되지 않을 수 있다.
```