# Caravan Activity Log Framework Integration

## Purpose

- Framework 팀이 수정 중인 SaveData 관련 파일을 덮어쓰지 않고 Caravan 활동 로그 기능을 인계한다.
- 이 인계본의 신규 `.cs` 파일은 컴파일 충돌 방지를 위해 파일 전체가 `/* ... */`로 비활성화되어 있다.
- 아래 Framework 통합이 완료된 뒤 신규 파일의 맨 앞 `/*`와 맨 뒤 `*/`를 제거한다.

## Copied Files

현재 전체 주석 처리된 신규 파일:

- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/CaravanActivityLog.cs`
- `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/CaravanActivityLogItemView.cs`
- `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/CaravanActivityLogPanel.cs`
- `Assets/_Project/11.CoreServices/Editor/CaravanActivityLogPrefabBuilder.cs`
- `Assets/_Project/11.CoreServices/Editor/CaravanActivityLogTests.cs`

각 파일의 `.meta`도 함께 복사되어 GUID를 유지한다.

## Existing Framework Files To Modify

### 1. `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs`

`SaveData` 클래스에 다음 필드를 추가한다.

```csharp
public List<CaravanActivityLogEntrySaveData> caravanActivityLogs =
    new List<CaravanActivityLogEntrySaveData>();
```

같은 `ND.Framework` namespace 안에 다음 DTO와 enum을 추가한다. 기존 직렬화 enum은 수정하지 않는다.

```csharp
[Serializable]
public sealed class CaravanActivityLogEntrySaveData
{
    public long sequence;
    public long occurredUtcTicks;
    public string caravanId = string.Empty;
    public string tradeId = string.Empty;
    public string routeId = string.Empty;
    public string townId = string.Empty;
    public string routeEventId = string.Empty;
    public CaravanActivityLogType eventType;
}

public enum CaravanActivityLogType
{
    Departure = 0,
    CombatEncounter = 1,
    CombatVictory = 2,
    CombatDefeat = 3,
    Arrival = 4
}
```

주의:

- 기존 save version을 올리지 않는 확장형 version 6 필드로 사용한다.
- 과거 version 6 JSON에는 필드가 없으므로 `JsonSaveService.NormalizeData`에서 빈 목록으로 보정해야 한다.

### 2. `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`

`NormalizeData(SaveData data)`의 다른 최상위 리스트 정규화 구간에 다음을 추가한다.

```csharp
if (data.caravanActivityLogs == null)
{
    data.caravanActivityLogs = new List<CaravanActivityLogEntrySaveData>();
}
CaravanActivityLog.TrimToLimit(data);
```

의존 함수:

- 신규 파일 `CaravanActivityLog.TrimToLimit(SaveData, int)`
- 기본 최대 보관 수: `CaravanActivityLog.DefaultMaxEntries`, 현재 100

### 3. `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeStartService.cs`

수정 대상 함수:

- `Depart(...)`
- `TryStartTrade(...)`
- 신규 private helper `ResolveDestinationTownId(...)`

출발 처리의 `CaravanSaveDataMapper.CopyToSave(...)` 이후, `saveService.Save(...)` 이전에 추가한다.

```csharp
var activityLogSnapshot = saveData.caravanActivityLogs != null
    ? new List<CaravanActivityLogEntrySaveData>(saveData.caravanActivityLogs)
    : new List<CaravanActivityLogEntrySaveData>();

CaravanActivityLog.Add(
    saveData,
    CaravanActivityLogType.Departure,
    caravanId,
    tradeId,
    routeId,
    ResolveDestinationTownId(route, caravanSave.currentTownId));
```

legacy `TryStartTrade(...)`에서는 `targetCaravanId`, `targetCaravanSave`를 사용하고 route를 provider에서 조회한다.

```csharp
SharedRouteDefinition departureRoute = null;
getSharedGameData?.Invoke()?.TryGetRoute(routeId, out departureRoute);
```

저장 서비스 누락 또는 저장 실패 분기에서 다른 snapshot 복구와 함께 로그 목록도 복구한다.

```csharp
saveData.caravanActivityLogs = activityLogSnapshot;
```

목적지 helper:

```csharp
private static string ResolveDestinationTownId(
    SharedRouteDefinition route,
    string departureTownId)
{
    if (route == null)
    {
        return string.Empty;
    }

    return string.Equals(route.FromTownId, departureTownId, StringComparison.Ordinal)
        ? route.ToTownId ?? string.Empty
        : route.FromTownId ?? string.Empty;
}
```

### 4. `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`

수정 대상 함수:

- `TryProcessTravelingEntry(...)`
- `ProcessRouteEvents(...)`
- `SettleTrade(...)`
- `TryProcessForcedRouteEvent(...)`
- 신규 `RecordRouteEventLogs(...)`
- 신규 `ResolveDestinationTownId(...)`

`TryProcessTravelingEntry(...)`에서 `SaveData`를 전달하도록 호출을 변경한다.

```csharp
ProcessRouteEvents(saveData, progress, caravan);
```

`ProcessRouteEvents` signature:

```csharp
private void ProcessRouteEvents(
    SaveData saveData,
    TradeProgressSaveData progress,
    CaravanData caravan)
```

`TradeRouteEventProcessor.Process(...)` 성공 후 호출한다.

```csharp
RecordRouteEventLogs(saveData, progress, route, result);
```

`RecordRouteEventLogs(...)`가 수행해야 하는 동작:

1. `result.Occurrences`를 순회한다.
2. `occurrence.EventId`로 `route.Events`의 정의를 찾는다.
3. `definition.EventType != RouteEvent.Combat`이면 로그를 만들지 않는다.
4. Combat이면 `CombatEncounter`를 먼저 추가한다.
5. `occurrence.IsFatal`이면 `CombatDefeat`, 아니면 `CombatVictory`를 추가한다.
6. 반환형은 forced event 저장 실패 시 제거 또는 복구할 수 있도록 `List<CaravanActivityLogEntrySaveData>`로 둔다.

`SettleTrade(...)`에서 pending settlement와 caravan save 반영 후, settlement notification 추가 전에 성공 도착 로그를 추가한다.

```csharp
if (result.grade != JourneyResultGrade.Failed)
{
    CaravanActivityLog.Add(
        saveData,
        CaravanActivityLogType.Arrival,
        progress.caravanId,
        settlementTradeId,
        settlementRouteId,
        ResolveDestinationTownId(
            sharedGameData,
            settlementRouteId,
            caravanSave.currentTownId));
}
```

목적지 helper:

```csharp
private static string ResolveDestinationTownId(
    ISharedGameDataProvider sharedGameData,
    string routeId,
    string departureTownId)
{
    if (sharedGameData == null
        || !sharedGameData.TryGetRoute(routeId, out var route)
        || route == null)
    {
        return string.Empty;
    }

    return string.Equals(route.FromTownId, departureTownId, StringComparison.Ordinal)
        ? route.ToTownId ?? string.Empty
        : route.FromTownId ?? string.Empty;
}
```

`TryProcessForcedRouteEvent(...)`에서는 로그 추가 전 목록 snapshot을 만들고 저장 실패 시 복구한다.

```csharp
var activityLogSnapshot = saveData.caravanActivityLogs != null
    ? new List<CaravanActivityLogEntrySaveData>(saveData.caravanActivityLogs)
    : new List<CaravanActivityLogEntrySaveData>();

RecordRouteEventLogs(saveData, progress, route, processResult);
```

저장 실패 분기:

```csharp
saveData.caravanActivityLogs = activityLogSnapshot;
```

일반 online/offline progress는 이미 전체 `SaveData` JSON snapshot을 복구하므로 그 rollback에 로그도 포함된다.

## Activation Order

1. 위 4개 기존 Framework 파일의 현재 팀 변경을 먼저 확정한다.
2. 이 문서의 필드와 호출을 해당 최신 코드에 수동 병합한다.
3. `CaravanActivityLog.cs`의 파일 전체 주석을 해제한다.
4. Framework 컴파일을 확인한다.
5. UI 두 파일의 전체 주석을 해제한다.
6. `CaravanActivityLogPrefabBuilder.cs`와 테스트 파일의 전체 주석을 해제한다.
7. Unity 메뉴 `ND/UI/Create Caravan Activity Log Prefabs`를 실행한다.
8. 생성된 `CaravanActivityLogPanel.prefab`을 원하는 Canvas 아래에 배치한다.

## UI Behavior

- 로그는 오래된 항목부터 위에, 최신 항목을 아래에 표시한다.
- 패널·Viewport·말풍선·아이콘·텍스트는 Raycast를 받지 않는다.
- Scrollbar 트랙과 Handle만 Raycast를 받는다.
- Scrollbar를 조작한 뒤 5초 동안 위치를 유지한다.
- 5초 동안 추가 조작이 없으면 0.2초 동안 최하단으로 부드럽게 복귀한다.
- `Auto Return Delay`와 `Auto Return Duration`은 Inspector에서 변경할 수 있다.

## Check

통합 후 실행:

```text
Unity EditMode test filter: CaravanActivityLogTests
```

기대 결과:

- 테스트 3개 통과
- 신규/구버전 save에서 `caravanActivityLogs` null 없음
- 최대 100개 유지
- 출발 저장 실패 시 로그 없음
- forced combat 저장 실패 시 로그 rollback
- 출발 → 전투 중 → 승리/패배 → 도착 순서 확인

## Risk

- Scene 변경: No
- Prefab 변경: 통합 완료 후 생성 필요
- Meta 변경: Yes, 신규 C# GUID 유지용
- SaveData 구조 변경: Yes, 확장형 로그 목록 추가
- 직렬화 필드 변경: Yes
- 기존 데이터 마이그레이션 필요: No
- 원천 소유자 리뷰 필요: Yes, Framework & Integration 담당자의 최신 SaveData 변경과 병합 필요

