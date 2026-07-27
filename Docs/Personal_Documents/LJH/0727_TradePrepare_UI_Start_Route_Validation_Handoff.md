# TradePrepare UI 및 출발 Route 검증 변경 요청

## 1. 문서 목적

이 문서는 다음 두 스크립트에 적용할 Town/Route 검증 변경을 전달하기 위한 작업 가이드다.

- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareUiRuntimeBinding.cs`
- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareStartAdapter.cs`

두 스크립트의 역할은 다음과 같이 구분한다.

```text
TradePrepareUiRuntimeBinding
-> CurrentViewData를 이용한 UI 입력 1차 검증

TradePrepareStartAdapter
-> 최신 SaveData와 원본 TownData/RouteData를 이용한 출발 최종 검증
```

`TradePrepareViewDataBuilder`의 Town/Route `canSelect` 계산 변경은 별도 브랜치에서 진행한다. 이 문서의 두 스크립트만 먼저 변경해도 현재 타입과 API 기준으로 컴파일할 수 있으며, Builder 변경이 병합되면 표시 단계부터 출발 단계까지 같은 규칙을 사용하게 된다.

---

## 2. 공통 검증 규칙

Route 선택 및 출발은 다음 조건을 모두 만족해야 한다.

```text
Route가 존재함
AND Route.fromTownId == 선택 캐러밴의 최신 currentTownId
AND Route.toTownId == 선택한 destinationTownId
AND 출발 Town이 유효하게 해금됨
AND 목적지 Town이 유효하게 해금됨
AND Route가 유효하게 해금됨
AND Route가 출발 Town의 TownData.availableRoutes에 등록됨
```

Town과 Route의 유효 해금 상태는 다음과 같이 계산한다.

```text
Town 유효 해금
= TownData.UnlockedByDefault
   OR SaveData.world.unlockedTownIds에 Town ID 존재

Route 유효 해금
= RouteData.UnlockedByDefault
   OR SaveData.world.unlockedRouteIds에 Route ID 존재
```

콘텐츠 ID 비교는 `StringComparison.Ordinal`을 사용한다.

---

## 3. TradePrepareUiRuntimeBinding 변경

### 3.1 변경 목적

기존 `CanSelectRoute()`는 `routeId`만 받아 다음 항목만 확인한다.

```text
Route ID 일치
AND Route.isUnlocked
AND Route.canSelect
```

이 방식은 UI가 서로 맞지 않는 목적지 ID와 Route ID를 전달했을 때 두 값의 관계를 검사할 수 없다.

```text
잘못된 예:

currentTownId = RiverTown
destinationTownId = BaseCamp
routeId = RiverToMount
```

변경 후에는 `destinationTownId`도 검증 함수에 전달하여 다음 연결 관계를 확인한다.

```text
currentTownId -> Route -> destinationTownId
```

### 3.2 HandleRouteSelected 교체

기존 `HandleRouteSelected()`를 다음 코드로 교체한다.

```csharp
private void HandleRouteSelected(
    string destinationTownId,
    string routeId,
    float distance)
{
    // CurrentViewData를 기준으로 다음 항목을 함께 검사한다.
    //
    // 1. 선택한 목적지 Town이 현재 선택 가능한가?
    // 2. Route가 현재 Town에서 출발하는가?
    // 3. Route가 전달받은 목적지 Town으로 향하는가?
    // 4. Route가 해금되어 있고 선택 가능한가?
    //
    // 검증에 실패하면 Draft를 변경하지 않고 현재 화면에 머문다.
    if (runtimeContext == null ||
        !CanSelectRoute(
            runtimeContext.CurrentViewData,
            destinationTownId,
            routeId))
    {
        return;
    }

    // 검증에 통과한 목적지와 Route 조합만 Draft에 반영한다.
    runtimeContext.SelectDestination(destinationTownId);
    runtimeContext.SelectRoute(routeId);
}
```

`distance`는 기존 UI 이벤트 시그니처 호환을 위해 유지한다. 실제 이동 거리의 권위 데이터는 `RouteData.Distance`이므로 UI가 전달한 `distance`를 검증 또는 출발 계산에 사용하지 않는다.

### 3.3 CanSelectRoute 교체

기존 시그니처:

```csharp
private static bool CanSelectRoute(
    TradePrepareViewData viewData,
    string routeId)
```

변경 시그니처 및 구현:

```csharp
private static bool CanSelectRoute(
    TradePrepareViewData viewData,
    string destinationTownId,
    string routeId)
{
    // 현재 Town, 목적지, Route 중 하나라도 없으면
    // 유효한 이동 조합을 검사할 수 없으므로 선택을 거부한다.
    if (viewData == null ||
        viewData.towns == null ||
        viewData.routes == null ||
        string.IsNullOrWhiteSpace(viewData.currentTownId) ||
        string.IsNullOrWhiteSpace(destinationTownId) ||
        string.IsNullOrWhiteSpace(routeId))
    {
        return false;
    }

    TownViewData destinationTown = null;

    // UI가 전달한 목적지 ID에 해당하는 TownViewData를 찾는다.
    foreach (TownViewData town in viewData.towns)
    {
        if (town != null &&
            string.Equals(
                town.townId,
                destinationTownId,
                StringComparison.Ordinal))
        {
            destinationTown = town;
            break;
        }
    }

    // 목적지 Town이 없거나 잠겼거나 선택 불가능하면
    // 그 Town으로 향하는 Route도 선택할 수 없다.
    if (destinationTown == null ||
        !destinationTown.isUnlocked ||
        !destinationTown.canSelect)
    {
        return false;
    }

    foreach (RouteViewData route in viewData.routes)
    {
        if (route == null)
        {
            continue;
        }

        // Route ID만 검사하지 않고 다음 관계를 함께 검사한다.
        //
        // currentTownId -> Route -> destinationTownId
        //
        // 이를 통해 목적지 ID와 Route ID가 서로 어긋난 Draft가
        // 생성되는 것을 UI 입력 단계에서 방지한다.
        bool isValidRoute =
            string.Equals(
                route.routeId,
                routeId,
                StringComparison.Ordinal) &&
            string.Equals(
                route.fromTownId,
                viewData.currentTownId,
                StringComparison.Ordinal) &&
            string.Equals(
                route.toTownId,
                destinationTownId,
                StringComparison.Ordinal) &&
            route.isUnlocked &&
            route.canSelect;

        if (isValidRoute)
        {
            return true;
        }
    }

    return false;
}
```

### 3.4 UI 검증의 한계

`CurrentViewData`는 Builder가 만든 UI 표시용 스냅샷이다. UI가 열린 뒤 SaveData의 해금 상태나 캐러밴 위치가 변경되면 오래된 상태가 될 수 있다.

따라서 이 검증은 사용자 입력을 조기에 차단하는 1차 방어이며, 출발 가능 여부의 최종 권위 판정이 아니다.

---

## 4. TradePrepareStartAdapter 변경

### 4.1 변경 목적

`TradePrepareStartAdapter`는 출발 비용과 구매 데이터를 stage하고 출발 Gateway를 호출하기 전에 다음 항목을 최신 데이터로 다시 검사해야 한다.

- 선택 캐러밴이 최신 SaveData에 존재하는가
- 저장된 캐러밴 위치와 Draft의 `currentTownId`가 같은가
- 출발 Town과 목적지 Town이 존재하는가
- 출발 Town과 목적지 Town이 유효하게 해금됐는가
- Route가 유효하게 해금됐는가
- Route의 방향과 선택 목적지가 일치하는가
- Route가 출발 Town의 `availableRoutes`에 등록됐는가

검증은 반드시 `commitSink.TryStage()` 및 출발 Gateway 호출보다 먼저 실행한다.

### 4.2 using 추가

파일 상단에 다음 using을 추가한다.

```csharp
using System.Collections.Generic;
```

### 4.3 오류 코드 추가

기존 오류 코드 선언부에 다음 코드를 추가한다.

```csharp
// Route는 존재하지만 Town 해금, 방향, 목적지 또는
// availableRoutes 소속 검증에 실패한 경우 사용한다.
public const string ErrorRouteValidationFailed =
    "ROUTE_VALIDATION_FAILED";
```

### 4.4 TryStartTrade에 최종 검증 추가

`ResolveSelectedRoute()` 성공 후, `TryCreateDeparture()`를 호출하기 전에 다음 검증을 추가한다.

```csharp
RouteData route =
    TradePrepareCaravanFactory.ResolveSelectedRoute(draft, context);

if (route == null)
{
    return CreateFailure(
        ErrorRouteNotFound,
        "Selected route could not be resolved.",
        tradeId,
        viewData.startCondition,
        null);
}

// CurrentViewData는 UI용 스냅샷이므로 출발의 최종 권위로 사용하지 않는다.
// 출발 직전에 최신 SaveData와 원본 TownData/RouteData를 기준으로
// Town, Route, 방향과 소속 관계를 다시 검사한다.
//
// 이 검증은 CommitSink.TryStage()보다 먼저 실행해야 한다.
// 검증에 실패한 출발의 비용이나 구매 데이터가 stage되면 안 된다.
if (!TryValidateRouteForStart(
    draft,
    context,
    route,
    out string routeValidationMessage))
{
    return CreateFailure(
        ErrorRouteValidationFailed,
        routeValidationMessage,
        tradeId,
        viewData.startCondition,
        null);
}
```

### 4.5 최종 Route 검증 함수 추가

```csharp
private static bool TryValidateRouteForStart(
    TradePrepareDraft draft,
    TradePrepareBuildContext context,
    RouteData route,
    out string errorMessage)
{
    errorMessage = string.Empty;

    if (draft == null || context == null || route == null)
    {
        errorMessage = "Route validation input is missing.";
        return false;
    }

    string caravanId =
        draft.departureCaravanId?.Trim() ?? string.Empty;

    string currentTownId =
        draft.currentTownId?.Trim() ?? string.Empty;

    string destinationTownId =
        draft.selectedDestinationTownId?.Trim() ?? string.Empty;

    // 선택 캐러밴의 최신 저장 위치를 다시 확인한다.
    // UI가 열린 이후 캐러밴 위치가 바뀌었을 수 있으므로
    // Draft의 currentTownId만 신뢰하지 않는다.
    if (!string.IsNullOrEmpty(caravanId))
    {
        if (!ND.Framework.SaveDataLookup.TryGetCaravan(
            context.saveData,
            caravanId,
            out ND.Framework.CaravanSaveData savedCaravan))
        {
            errorMessage =
                "The selected departure Caravan does not exist.";
            return false;
        }

        if (!string.Equals(
            savedCaravan.currentTownId,
            currentTownId,
            StringComparison.Ordinal))
        {
            errorMessage =
                "The Caravan location changed. Refresh the preparation screen.";
            return false;
        }
    }

    TownData currentTown =
        TradePrepareViewDataBuilder.FindTown(
            context.towns,
            currentTownId);

    if (currentTown == null)
    {
        errorMessage = "The departure town does not exist.";
        return false;
    }

    TownData destinationTown =
        TradePrepareViewDataBuilder.FindTown(
            context.towns,
            destinationTownId);

    if (destinationTown == null)
    {
        errorMessage = "The destination town does not exist.";
        return false;
    }

    // 현재 위치라는 이유만으로 출발 Town의 잠금을 우회하지 않는다.
    if (!IsTownUnlocked(currentTown, context.saveData))
    {
        errorMessage = "The departure town is locked.";
        return false;
    }

    // Route가 해금되어 있더라도 목적지 Town이 잠겼다면 출발할 수 없다.
    if (!IsTownUnlocked(destinationTown, context.saveData))
    {
        errorMessage = "The destination town is locked.";
        return false;
    }

    if (!IsRouteUnlocked(route, context.saveData))
    {
        errorMessage = "The selected route is locked.";
        return false;
    }

    // 선택 Route는 캐러밴의 현재 Town에서 출발해야 한다.
    if (!string.Equals(
        route.FromTownId,
        currentTownId,
        StringComparison.Ordinal))
    {
        errorMessage =
            "The selected route does not depart from the current town.";
        return false;
    }

    // Route의 실제 목적지와 UI에서 선택한 목적지가 같아야 한다.
    if (!string.Equals(
        route.ToTownId,
        destinationTownId,
        StringComparison.Ordinal))
    {
        errorMessage =
            "The selected route does not lead to the selected destination.";
        return false;
    }

    // 전역 Route 배열에 존재하는 것만으로는 출발할 수 없다.
    // 출발 Town의 availableRoutes에 등록된 Route만 유효하다.
    if (!ContainsRoute(
        currentTown.AvailableRoutes,
        route.RouteId))
    {
        errorMessage =
            "The selected route is not registered in the departure town.";
        return false;
    }

    return true;
}
```

### 4.6 해금 및 Route 소속 검사 함수 추가

```csharp
private static bool IsTownUnlocked(
    TownData town,
    ND.Framework.SaveData saveData)
{
    if (town == null)
    {
        return false;
    }

    // Town의 유효 해금 상태:
    // SO 기본 해금 또는 SaveData 해금 목록에 ID 존재
    return town.UnlockedByDefault ||
        ContainsId(
            saveData?.world?.unlockedTownIds,
            town.TownId);
}

private static bool IsRouteUnlocked(
    RouteData route,
    ND.Framework.SaveData saveData)
{
    if (route == null)
    {
        return false;
    }

    // Route의 유효 해금 상태:
    // SO 기본 해금 또는 SaveData 해금 목록에 ID 존재
    return route.UnlockedByDefault ||
        ContainsId(
            saveData?.world?.unlockedRouteIds,
            route.RouteId);
}

private static bool ContainsId(
    IList<string> ids,
    string expectedId)
{
    if (ids == null ||
        string.IsNullOrWhiteSpace(expectedId))
    {
        return false;
    }

    foreach (string id in ids)
    {
        // 콘텐츠 ID는 대소문자를 구분해 정확히 비교한다.
        if (string.Equals(
            id,
            expectedId,
            StringComparison.Ordinal))
        {
            return true;
        }
    }

    return false;
}

private static bool ContainsRoute(
    RouteData[] routes,
    string routeId)
{
    if (routes == null ||
        string.IsNullOrWhiteSpace(routeId))
    {
        return false;
    }

    foreach (RouteData availableRoute in routes)
    {
        // ScriptableObject 참조가 아니라 안정적인 Route ID를 기준으로
        // availableRoutes 소속 여부를 검사한다.
        if (availableRoute != null &&
            string.Equals(
                availableRoute.RouteId,
                routeId,
                StringComparison.Ordinal))
        {
            return true;
        }
    }

    return false;
}
```

---

## 5. 별도 브랜치 의존 사항

다음 변경은 별도 브랜치에서 진행한다.

- `TradePrepareViewDataBuilder`
  - 현재 Town 자동 해금 예외 제거
  - 목적지 Town 해금을 `RouteViewData.canSelect`에 반영
  - 선택 가능한 Route 존재 여부를 `TownViewData.canSelect`에 반영
- `TradePrepareCaravanFactory`
  - `context.routes`와 `TownData.availableRoutes` 병합 제거
  - 현재 Town의 `availableRoutes`만 사용하여 선택 Route 해석
- `TownRoutePanel`
  - 역방향 Route 표시 fallback 제거

이 문서의 두 스크립트만 먼저 적용해도 컴파일할 수 있다. 다만 Builder 변경 전에는 ViewData 표시 상태 일부가 기존 계산을 사용한다.

`TradePrepareStartAdapter`가 `TradePrepareViewDataBuilder.FindTown()`을 호출하므로 다음 접근 범위를 유지해야 한다.

```csharp
internal static TownData FindTown(...)
```

`FindTown()`을 `private`으로 변경해야 한다면 Adapter 내부에 별도 조회 함수를 추가하거나 공용 Route Validator로 분리해야 한다.

---

## 6. 확인 항목

- [ ] 올바른 현재 Town, Route, 목적지 조합은 선택 가능
- [ ] Route ID와 목적지 ID가 어긋나면 Draft가 변경되지 않음
- [ ] 역방향 Route는 UI Binding에서 거부됨
- [ ] 잠긴 목적지 Town으로 향하는 Route는 UI Binding에서 거부됨
- [ ] UI가 열린 후 캐러밴 위치가 변경되면 출발이 거부됨
- [ ] 출발 Town 또는 목적지 Town이 잠겼으면 출발이 거부됨
- [ ] Route가 잠겼으면 출발이 거부됨
- [ ] Route가 출발 Town의 `availableRoutes`에 없으면 출발이 거부됨
- [ ] 검증 실패 시 Commit 데이터가 stage되지 않음
- [ ] 검증 실패 시 출발 Gateway가 호출되지 않음
- [ ] 정상 출발 시 기존 비용 stage 및 출발 흐름이 유지됨
