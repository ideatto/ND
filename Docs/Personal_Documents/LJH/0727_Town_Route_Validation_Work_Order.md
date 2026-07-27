# Town/Route 검증 작업 분담 및 적용 순서

## 1. 문서 목적

Town/Route 검증 변경을 여러 브랜치에서 나누어 작업할 때 담당 파일과 병합 순서를 잊지 않도록 기록한다.

이번 작업은 다음 세 단계로 나눈다.

```text
1. 현재 브랜치: Builder + Panel + Factory
2. 다른 작업자 브랜치: UI Binding + StartAdapter
3. 두 브랜치 병합 및 통합 검증 후 ContextProvider 계열 정리
```

Town/Route Asset 방향 수정과 신규 Route 등록은 이전 PR에서 완료됐다.

Quest/Progression 해금 저장 Command 구현은 이번 작업 범위에 포함하지 않는다.

---

## 2. 최종 목표 규칙

Route가 실제로 선택 및 출발 가능하려면 다음 조건을 모두 만족해야 한다.

```text
Route가 존재함
AND Route.fromTownId == 선택 캐러밴의 최신 currentTownId
AND Route.toTownId == 선택한 destinationTownId
AND 출발 Town이 유효하게 해금됨
AND 목적지 Town이 유효하게 해금됨
AND Route가 유효하게 해금됨
AND Route가 출발 Town의 TownData.availableRoutes에 등록됨
```

유효 해금 상태는 다음과 같다.

```text
Town 유효 해금
= TownData.UnlockedByDefault
   OR SaveData.world.unlockedTownIds에 Town ID 존재

Route 유효 해금
= RouteData.UnlockedByDefault
   OR SaveData.world.unlockedRouteIds에 Route ID 존재
```

방향성 Route는 UI가 반대 방향을 임의로 허용하지 않는다.

---

## 3. 현재 브랜치 작업

현재 브랜치에서는 다음 세 스크립트를 수정한다.

### 3.1 TradePrepareViewDataBuilder

대상:

```text
Assets/99.Sandbox/_LJH/01.Script/Runtime/Builder/TradePrepareViewDataBuilder.cs
```

작업 항목:

- [ ] 현재 Town이라는 이유로 자동 해금하는 예외 제거
- [ ] `TownViewData.isUnlocked`에는 Town의 실제 유효 해금 상태만 반영
- [ ] `RouteViewData.isUnlocked`에는 Route 자체의 유효 해금 상태만 반영
- [ ] `RouteViewData.canSelect`에 Route 해금과 목적지 Town 해금을 함께 반영
- [ ] Route의 `fromTownId`가 현재 Town ID와 같은 경우만 후보로 사용
- [ ] `TownViewData.canSelect`은 해당 Town으로 향하는 선택 가능한 Route가 하나 이상 있을 때만 `true`
- [ ] 현재 Town은 목적지로 다시 선택할 수 없도록 유지
- [ ] Route 후보 생성 시 `context.routes`와 `TownData.availableRoutes` 병합 제거
- [ ] 현재 Town의 `TownData.availableRoutes`만 후보로 사용

핵심 변경:

```text
기존:
context.routes + currentTown.availableRoutes

변경:
currentTown.availableRoutes
```

주의:

- 현재 Town인데 실제로 잠겼다면 `isCurrent`로 해금 상태를 덮지 않는다.
- 현재 Town 잠금은 SaveData 또는 콘텐츠 무결성 오류로 드러내야 한다.
- 출발 조건 계산에서도 목적지 Town 잠금이 누락되지 않도록 확인한다.

### 3.2 TownRoutePanel

대상:

```text
Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/TownRoutePanel.cs
```

작업 항목:

- [ ] `IsRouteForTown()`의 역방향 허용 제거
- [ ] 현재 Town에서 출발하고 선택 Town으로 도착하는 Route만 표시
- [ ] `currentTownId`가 비었을 때 Route 양 끝 중 하나를 허용하는 fallback 제거

허용 조건:

```text
route.fromTownId == currentTownId
AND route.toTownId == townId
```

제거할 조건:

```text
route.toTownId == currentTownId
AND route.fromTownId == townId
```

### 3.3 TradePrepareCaravanFactory

대상:

```text
Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareCaravanFactory.cs
```

작업 항목:

- [ ] `ResolveSelectedRoute()`의 전역 Route 병합 제거
- [ ] 현재 Town의 `AvailableRoutes`에서만 선택 Route 조회
- [ ] `selected.FromTownId == currentTownId` 검증 유지
- [ ] `selected.ToTownId == selectedDestinationTownId` 검증 유지

핵심 변경:

```text
기존:
MergeUnique(context.routes, currentTown.AvailableRoutes)

변경:
currentTown.AvailableRoutes
```

현재 Town이 없거나 `AvailableRoutes`가 비어 있으면 Route를 찾지 못한 것으로 처리한다.

---

## 4. 다른 작업자 브랜치

다른 작업자에게 다음 전달 문서를 제공한다.

```text
Docs/Personal_Documents/LJH/0727_TradePrepare_UI_Start_Route_Validation_Handoff.md
```

다른 작업자 브랜치에서는 다음 두 스크립트를 수정한다.

### 4.1 TradePrepareUiRuntimeBinding

대상:

```text
Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareUiRuntimeBinding.cs
```

작업 항목:

- [ ] `CanSelectRoute()`에 `destinationTownId` 전달
- [ ] 목적지 Town의 존재 여부 확인
- [ ] 목적지 Town의 `isUnlocked` 및 `canSelect` 확인
- [ ] Route ID 일치 확인
- [ ] `route.fromTownId == viewData.currentTownId` 확인
- [ ] `route.toTownId == destinationTownId` 확인
- [ ] Route의 `isUnlocked` 및 `canSelect` 확인
- [ ] 검증 실패 시 Destination 및 Route Draft를 변경하지 않음

역할:

```text
CurrentViewData를 이용한 UI 입력 1차 방어
```

### 4.2 TradePrepareStartAdapter

대상:

```text
Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareStartAdapter.cs
```

작업 항목:

- [ ] 최신 SaveData에 선택 캐러밴이 존재하는지 확인
- [ ] 저장된 캐러밴 위치와 Draft의 `currentTownId` 일치 확인
- [ ] 출발 Town과 목적지 Town 존재 확인
- [ ] 출발 Town과 목적지 Town의 유효 해금 확인
- [ ] Route 유효 해금 확인
- [ ] Route 출발지와 최신 캐러밴 위치 일치 확인
- [ ] Route 목적지와 선택 Destination 일치 확인
- [ ] Route가 출발 Town의 `availableRoutes`에 등록됐는지 확인
- [ ] 검증 실패 시 Commit 데이터를 stage하지 않음
- [ ] 검증 실패 시 출발 Gateway를 호출하지 않음

역할:

```text
최신 SaveData와 원본 SO를 이용한 출발 최종 방어
```

---

## 5. 개별 브랜치 확인

### 5.1 현재 브랜치 확인

- [ ] Builder가 현재 Town을 자동 해금하지 않음
- [ ] 잠긴 목적지 Town으로 향하는 Route의 `canSelect`이 `false`
- [ ] 잠긴 Route의 `canSelect`이 `false`
- [ ] 선택 가능한 Route가 없는 Town의 `canSelect`이 `false`
- [ ] 역방향 Route가 Panel에 표시되지 않음
- [ ] Factory가 `currentTown.AvailableRoutes`에 없는 Route를 찾지 않음
- [ ] 정상 방향 Route는 기존처럼 표시 및 해석됨
- [ ] Unity 컴파일 오류 없음

### 5.2 다른 작업자 브랜치 확인

- [ ] 목적지 ID와 Route ID가 어긋나면 Binding에서 거부
- [ ] 역방향 Route 입력이 Binding에서 거부
- [ ] 잠긴 Town 또는 Route 입력이 Binding에서 거부
- [ ] UI가 열린 후 캐러밴 위치가 변경되면 Adapter에서 출발 거부
- [ ] Route가 출발 Town의 `availableRoutes`에 없으면 Adapter에서 출발 거부
- [ ] 검증 실패 시 Commit stage와 Gateway 호출이 발생하지 않음
- [ ] 정상 Route는 기존 출발 흐름 유지
- [ ] Unity 컴파일 오류 없음

---

## 6. 병합 순서

두 작업 브랜치는 파일 소유 범위를 분리하므로 어느 PR을 먼저 병합해도 컴파일 가능하도록 유지한다.

권장 순서:

```text
1. 현재 브랜치 PR
   Builder + Panel + Factory

2. 다른 작업자 PR
   UI Binding + StartAdapter

3. 최신 dev2에서 통합 검증

4. ContextProvider 계열 정리용 후속 브랜치 생성
```

다른 작업자 PR이 먼저 병합되더라도 다음 상태로 동작한다.

```text
Binding 및 Adapter의 방어는 적용됨
Builder의 표시용 canSelect 계산은 기존 방식 유지
```

현재 브랜치 PR이 먼저 병합되더라도 다음 상태로 동작한다.

```text
Builder와 Panel의 UI 후보 계산은 강화됨
StartAdapter의 최신 SaveData 최종 검증은 아직 없음
```

두 PR이 모두 병합되어야 표시, 입력, Route 해석 및 최종 출발이 같은 규칙을 사용한다.

---

## 7. 병합 후 통합 검증

최신 `dev2`에서 다음 항목을 함께 확인한다.

- [ ] Builder의 `canSelect`과 Binding 검증 결과가 일치
- [ ] Factory의 Route 해석 결과와 Adapter 최종 검증 결과가 일치
- [ ] BaseCamp에서 BaseCamp 출발 Route만 표시
- [ ] RiverTown에서 RiverTown 출발 Route만 표시
- [ ] MountTown에서 MountTown 출발 Route만 표시
- [ ] WindyTown에서 WindyTown 출발 Route만 표시
- [ ] 잠긴 Town으로 향하는 해금 Route가 선택되지 않음
- [ ] 해금 Town으로 향하는 잠긴 Route가 선택되지 않음
- [ ] 역방향 Route가 표시, 선택 또는 출발되지 않음
- [ ] Draft에 잘못된 목적지와 Route ID를 직접 넣어도 출발되지 않음
- [ ] UI가 열린 뒤 캐러밴 위치가 바뀌면 출발되지 않음
- [ ] 정상 Route의 선택, 비용 계산, Commit stage 및 출발이 유지됨

---

## 8. 통합 검증 후 ContextProvider 계열 정리

다음 정리는 두 PR 병합 및 통합 검증이 끝난 후 별도 브랜치에서 진행한다.

대상:

```text
TradePrepareRuntimeContextProvider.routes
TradePrepareBuildContext.routes
TradePrepareRuntimeContext.prefab의 직렬화 Route 배열
TradePrepareTemporaryUI의 Route 배열 및 BuildContext 할당
관련 Smoke Test와 테스트용 BuildContext
```

작업 순서:

```text
1. 저장소 전체에서 context.routes 및 BuildContext.routes 사용처 검색
2. 운영 코드 사용처가 0개인지 확인
3. 임시 UI와 테스트의 Route 주입 방식 변경
4. TradePrepareRuntimeContextProvider.routes 제거
5. TradePrepareBuildContext.routes 제거
6. 프리팹의 직렬화 Route 배열 제거
7. Unity 직렬화 및 Missing Reference 확인
8. TradePrepare UI 회귀 테스트
```

주의:

- 전역 Route Catalog 자체는 제거하지 않는다.
- 제거 대상은 Town별 후보 생성에 사용하던 RuntimeContext의 임시 Route 배열이다.
- 전역 Catalog는 Route 등록, ID 조회, 중복 검사 및 참조 무결성 검증에 계속 사용한다.

---

## 9. 이번 작업에서 제외

- Quest/Progression 해금 저장 Command 구현
- TownRuntime 및 RouteRuntime 도입
- 전역 Route Catalog 제거
- 건물 데이터 추가 및 건물 Progression 연결

위 항목은 각각 별도 작업 범위로 관리한다.
