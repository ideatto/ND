# 2026-07-24 충돌 교정 및 무역 화면 복구 기록

## Purpose

- 병합 과정에서 손실된 무역 프리팹의 데이터 참조를 다시 연결한다.
- `TradeProgressCoordinator`의 데이터 조회 경로를 무역 진행 및 정산 연결이 동작하는 구성으로 교정한다.
- 나머지 파일은 현재 작업 브랜치에 반영된 상태를 유지한다.

## 기준

- 작업 브랜치: `featuel/conflictresolution/part2/jjh`
- 기준 브랜치: `dev2`
- 기준 커밋: `be4efe9`

## Changes

### TradeProgressCoordinator

- 파일: `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`
- `TradeRouteEventProcessor`와 `TryProcessForcedRouteEvent(...)`가 포함되지 않은 Coordinator 구현을 적용했다.
- 다중 `tradeProgressEntries` 순회 처리 대신 선택 캐러밴 호환 접근 경로를 사용한다.

### 무역 UI 프리팹

다음 무역 UI 프리팹의 직렬화 데이터와 참조를 수정했다.

#### `TradePrepareRuntimeContext.prefab`

- 비어 있던 마을 4개 참조를 복구했다.
- 비어 있던 경로 6개 참조를 복구했다.
- 비어 있던 무역 상품 6개 참조를 복구했다.
- 비어 있던 마차 3개 참조를 복구했다.
- 비어 있던 짐 동물 2개 참조를 복구했다.
- 비어 있던 용병 5개 참조를 복구했다.

#### `MercenarySelectionPanel.prefab`

- 용병 선택 패널의 UI 계층과 레이아웃을 변경했다.
- 확인 버튼 문구와 높이를 변경하고 TextMeshPro 폰트 참조를 연결했다.

#### `TradeTravelingPanel.prefab`

- 진행 화면의 TextMeshPro 폰트 및 공유 머티리얼 참조 4개를 변경했다.

## 요청 사항

현재 적용한 Coordinator에는 경로 이벤트 기능이 포함되어 있지 않다. 다음 기능은 최신 다중 캐러밴 데이터 구조에 맞춰 별도 교정 후 다시 반영해야 한다.

### Route Event Processor

- 거리 기반 경로 이벤트를 처리하는 `TradeRouteEventProcessor`를 복구한다.
- 동일한 `tradeId`와 진행 거리에서 호출 횟수와 관계없이 같은 결과가 나오도록 결정적 처리를 유지한다.
- 처리 대상 캐러밴은 `selectedCaravanId`가 아니라 해당 무역 진행 데이터의 `caravanId`로 조회한다.
- 자동 이벤트 처리 시 이미 처리한 체크 인덱스를 저장하여 중복 발생을 막는다.

### Forced Route Event API 호출 메서드

다음 공개 호출 메서드 또는 동등한 API를 Coordinator에 추가한다.

```csharp
public bool TryProcessForcedRouteEvent(string tradeId, string eventId)
```

요구 동작:

- `tradeId`로 실제 Traveling 상태의 `TradeProgressSaveData`를 찾는다.
- 해당 진행 데이터의 `caravanId`로 runtime caravan과 저장 caravan을 찾는다.
- `activeRouteId`로 경로 정의를 조회한다.
- `TradeRouteEventProcessor.ProcessForced(...)`를 호출한다.
- 성공 시 runtime caravan 결과를 같은 `caravanId`의 저장 데이터에 반영한다.
- 저장 성공 여부를 반환하고, ID 누락이나 상태 불일치 시 다른 캐러밴 데이터는 변경하지 않는다.

## Check

- `TradePrepareRuntimeContext.prefab`의 데이터 참조를 연결한 뒤 무역 선택 화면이 다시 표시됨을 확인했다.
- Coordinator를 넣고 빼는 비교로 선택 화면 미표시의 직접 원인이 프리팹 참조 손실임을 확인했다.
- `Assembly-CSharp.csproj` 빌드 결과:
  - 오류: 0
  - 경고: 14

추가 확인 항목:

- 무역 준비 화면에서 마을, 경로, 상품, 마차, 동물, 용병 목록 확인
- 출발 후 Traveling 화면 전환 확인
- 저장 및 재로드 후 진행 데이터 복구 확인
- 도착 및 정산 화면 전환 확인
- Route Event Processor와 Forced Route Event API 재반영 후 자동·강제 이벤트 각각 검증

## Risk

- Scene 변경: No
- Prefab 변경: Yes
- Meta 변경: No
- Package 변경: No
