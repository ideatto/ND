```markdown
# 무역 시작 시간 기록 로직 정리

## 1. 작업 목적

이번 작업의 목적은 **무역이 실제로 시작되는 순간의 게임 시간**을 저장 데이터에 기록하는 것이다.

이를 통해 이후 다음 기능의 기반을 마련한다.

- 진행 중 무역의 시작 시각 복구
- 종료 예정 시각 계산
- 저장 후 이어하기
- 오프라인 진행 및 완료 판정
- 중복 보상 방지

## 2. 전체 흐름

```text
무역 출발 요청
→ JourneyRunner.TryDepart(...)
→ 출발 검증 성공
→ TradeStartService.TryStartTrade(...)
→ TradeProgressRecorder.RecordStartedTrade(...)
→ SaveData.tradeProgress에 시작 시간 기록
→ SaveService.Save(...)
→ 저장 파일에 반영
```

## 3. 주요 책임 분리

### JourneyRunner

`JourneyRunner`는 무역 진행 상태만 변경한다.

담당하는 것:

- 출발 가능 여부 검증
- 무역 상태를 `Traveling`으로 변경
- 이동 거리와 총 소요 시간 계산
- 진행률, 정산, 완료 상태 관리

담당하지 않는 것:

- 현재 시간 조회
- 저장 데이터 접근
- 파일 저장
- 게임 시간 정책 결정

즉, `JourneyRunner`는 계속 시간 출처를 모르는 구조로 유지한다.

### GameTimeService

`GameTimeService`는 현재 게임 시간 제공자 역할을 한다.

현재 구현에서는 다음 값을 제공한다.

```csharp
public DateTime CurrentUtc => DateTime.UtcNow;
```

또한 `IGameTimeProvider`를 구현하여, 다른 시스템이 구체 클래스에 강하게 의존하지 않고 현재 시간을 받을 수 있게 했다.

```csharp
public interface IGameTimeProvider
{
    DateTime CurrentUtc { get; }
}
```

### TradeStartService

`TradeStartService`는 무역 시작을 Framework 쪽에서 연결하는 서비스다.

역할:

- `JourneyRunner.TryDepart(...)` 호출
- 출발 성공 여부 확인
- 출발 성공 후 시작 시간 기록 요청
- 필요 시 저장 실행
- 마지막 기록 성공 여부 보관

핵심 상태:

```csharp
public bool LastRecordSucceeded { get; private set; }
```

이 값은 디버그 하네스가 “출발은 성공했지만 기록은 실패했는지”를 구분하기 위해 사용한다.

### TradeProgressRecorder

`TradeProgressRecorder`는 저장 데이터에 무역 진행 정보를 기록한다.

기록 대상:

```csharp
SaveData.tradeProgress
```

주요 저장 필드:

```csharp
public string activeTradeId;
public string activeRouteId;
public TradeProgressState state;
public long tradeStartUtcTick;
public long expectedTradeEndUtcTick;
```

기록 시점:

- `JourneyRunner.TryDepart(...)`가 성공한 직후
- 즉, 무역 상태가 실제로 `Traveling`이 된 이후

## 4. 저장되는 데이터

무역 시작 시 다음 값이 저장된다.

```csharp
progress.activeTradeId = tradeId;
progress.activeRouteId = routeId;
progress.state = TradeProgressState.Traveling;
progress.tradeStartUtcTick = startUtc.Ticks;
progress.expectedTradeEndUtcTick = expectedEndUtc.Ticks;
```

각 필드 의미:

| 필드 | 의미 |
|---|---|
| `activeTradeId` | 현재 진행 중인 무역 ID |
| `activeRouteId` | 현재 무역 경로 ID |
| `state` | 무역 진행 상태 |
| `tradeStartUtcTick` | 무역 시작 UTC 시간 |
| `expectedTradeEndUtcTick` | 무역 종료 예정 UTC 시간 |

## 5. 중복 기록 방지 로직

이미 같은 무역이 진행 중이고 시작 시간이 기록되어 있으면 다시 덮어쓰지 않는다.

```csharp
progress.state == TradeProgressState.Traveling
&& progress.activeTradeId == tradeId
&& progress.tradeStartUtcTick > 0
```

이 조건이 참이면 기록하지 않는다.

다른 무역이 이미 `Traveling` 상태인 경우에도 기존 기록을 덮어쓰지 않는다.

```csharp
progress.state == TradeProgressState.Traveling
&& !string.IsNullOrEmpty(progress.activeTradeId)
&& progress.activeTradeId != tradeId
```

이 경우 경고 로그를 출력하고 기록을 중단한다.

## 6. 음수 시간 방어

`caravan.totalSeconds`가 음수가 될 가능성에 대비해 두 단계에서 방어한다.

### TradeStartService

`TimeSpan`을 만들기 전에 0 이상으로 보정한다.

```csharp
var expectedDuration = TimeSpan.FromSeconds(Math.Max(0f, caravan.totalSeconds));
```

### TradeProgressRecorder

외부에서 음수 `TimeSpan`이 직접 들어와도 다시 보정한다.

```csharp
if (expectedDuration < TimeSpan.Zero)
{
    FrameworkLog.Warning("Negative trade duration was clamped to zero.");
    expectedDuration = TimeSpan.Zero;
}
```

이를 통해 다음 조건을 보장한다.

```text
expectedTradeEndUtcTick >= tradeStartUtcTick
```

## 7. Null 방어 로직

### SaveData가 없는 경우

```csharp
if (saveData == null)
{
    FrameworkLog.Warning("Trade start time was not recorded because save data is null.");
    return false;
}
```

저장 데이터가 없으면 기록하지 않는다.

### GameTimeProvider가 없는 경우

```csharp
if (gameTimeProvider == null)
{
    FrameworkLog.Warning("Trade start time was not recorded because game time provider is null.");
    return false;
}
```

현재 시간을 가져올 수 없으면 기록하지 않는다.

### TradeProgressRecorder가 없는 경우

```csharp
if (tradeProgressRecorder == null)
{
    FrameworkLog.Warning("Trade start time was not recorded because trade progress recorder is null.");
    return result;
}
```

출발은 성공했더라도 기록 서비스가 없으면 기록하지 않고 안전하게 반환한다.

### SaveService가 없는 경우

```csharp
if (saveService == null)
{
    FrameworkLog.Warning("Trade start time was recorded but save was skipped because save service is null.");
    return result;
}
```

시작 시간 기록은 성공했지만 저장 서비스가 없으면 파일 저장만 건너뛴다.

## 8. 저장 데이터 정규화

기존 저장 파일에 `tradeProgress`가 없을 수 있으므로 로드/저장 전에 누락된 객체를 보정한다.

```csharp
if (data.tradeProgress == null)
{
    data.tradeProgress = new TradeProgressSaveData();
}
```

같은 방식으로 다음 필드도 null이면 새로 생성한다.

- `player`
- `caravan`
- `caravan.inventory`
- `tradeProgress`
- `world`
- `tutorial`

이 로직은 기존 version 1 저장 파일에 새 필드가 없을 때 NullReferenceException을 방지한다.

## 9. 디버그 테스트 로직

### TradeStartDebugHarness

테스트 씬에서 수동으로 무역 시작을 실행하기 위한 컴포넌트다.

Context Menu:

```text
Framework/Fill Sample Caravan
Framework/Start Trade And Record Time
```

동작:

1. 샘플 상단 데이터를 만든다.
2. `TradeStartService.TryStartTrade(...)`를 호출한다.
3. 출발 실패 시 실패 사유를 출력한다.
4. 출발 성공 후 `LastRecordSucceeded`를 확인한다.
5. 기록 성공 여부에 따라 로그를 다르게 출력한다.

기록 성공 시:

```text
Debug trade started and recorded.
```

출발은 성공했지만 기록 실패 시:

```text
Debug trade departed, but start time was not recorded.
```

### SaveDataDebugPrinter

현재 저장 데이터를 Unity Console에 출력하는 읽기 전용 디버그 컴포넌트다.

Context Menu:

```text
Framework/Print Full Save Data
Framework/Print Trade Progress Save Data
```

무역 진행 데이터 출력 항목:

- `ActiveTradeId`
- `ActiveRouteId`
- `State`
- `TradeStartUtcTick`
- `TradeStartUtc`
- `ExpectedTradeEndUtcTick`
- `ExpectedTradeEndUtc`

이 컴포넌트는 저장 데이터를 변경하지 않는다.

## 10. 테스트 씬에서 확인할 흐름

테스트 씬:

```text
Assets/_Project/11.CoreServices/TestScene/time-recode-in-trade_TestScene.unity
```

테스트 순서:

```text
1. 빈 GameObject 생성
2. TradeStartDebugHarness 추가
3. SaveDataDebugPrinter 추가
4. Play Mode 진입
5. Framework/Fill Sample Caravan 실행
6. Framework/Start Trade And Record Time 실행
7. Framework/Print Trade Progress Save Data 실행
```

확인할 값:

```text
State: Traveling
ActiveTradeId: debug_trade_001
ActiveRouteId: debug_route_001
TradeStartUtcTick: 0이 아닌 값
ExpectedTradeEndUtcTick: TradeStartUtcTick 이상
```

## 11. 이번 작업에서 의도적으로 하지 않은 것

다음 항목은 후속 기획 및 별도 PR 대상으로 남겼다.

### TradeStartResult 도입

현재는 기존 API 호환을 위해 `TryStartTrade(...)`가 계속 `DepartureValidationResult`를 반환한다.

기록 성공 여부는 임시로 다음 속성에서 확인한다.

```csharp
public bool LastRecordSucceeded { get; private set; }
```

향후에는 다음 정보를 포함하는 별도 결과 타입을 고려할 수 있다.

```text
canDepart
recordedSuccessfully
savedSuccessfully
reasons
```

### 저장 버전 마이그레이션

현재 버전이 맞지 않으면 새 저장 데이터를 만든다.

버전별 저장 데이터 변환은 후속 작업으로 남긴다.

### Atomic Save

현재 저장은 파일을 직접 덮어쓴다.

임시 파일, 백업 파일, 교체 저장 방식은 후속 저장 안정화 작업에서 다룬다.

### 게임 시간 정책 정리

현재 `GameTimeService`에는 두 개념이 공존한다.

```text
TimeScale: Unity 시뮬레이션 배속
CurrentUtc: 현실 UTC 시간
```

이번 작업은 무역 시작 시각 저장에 `CurrentUtc`를 사용한다.

향후 `JourneyRunner` 진행률 계산에서 배속 시간과 현실 시간을 어떻게 사용할지는 별도 기획이 필요하다.

## 12. 최종 구조 요약

```text
GameTimeService
    └─ 현재 UTC 시간 제공

IGameTimeProvider
    └─ 시간 제공 인터페이스

TradeStartService
    ├─ JourneyRunner.TryDepart 호출
    ├─ 출발 성공 후 기록 요청
    ├─ 음수 duration 보정
    ├─ 저장 요청
    └─ LastRecordSucceeded 제공

TradeProgressRecorder
    ├─ SaveData.tradeProgress 기록
    ├─ 중복 기록 방지
    ├─ 다른 진행 중 무역 덮어쓰기 방지
    └─ expected end time 계산

JsonSaveService
    └─ 저장 데이터 null 필드 정규화

TradeStartDebugHarness
    └─ 테스트 씬에서 수동 출발 실행

SaveDataDebugPrinter
    └─ 저장 데이터 읽기 전용 출력
```
```