# Route Event API 및 Production 데이터 운영 가이드

- 문서 상태: 구현 완료 / 조건부 검증 완료
- 작성일: 2026-07-24
- 적용 범위: `Assets/_Project/11.CoreServices/`
- 대상 기능: Multi-active Route Event Processor, Forced Route Event API, Production Route Event 데이터
- 기준 브랜치: `Feature/framework/multi-active-route-events`
- 기준 검증 커밋: `fd2f18279371c8cc5fa9b50eabc487df7e998c94`

---

## 1. 문서 목적

이 문서는 다음 두 영역의 팀 공통 기준을 정리한다.

1. Framework에 구현된 Route Event 처리 API와 실행 계약
2. Production Route 데이터에 Route Event 콘텐츠를 입력할 때 지켜야 할 데이터 규칙과 후속 처리

이번 구현은 기존 Multi-active 무역 진행 구조를 유지하면서 Online Tick, Offline Restore, Forced Route Event를 동일한 `caravanId + tradeId + routeId` 식별 경로에 연결한다.

Production 데이터의 이벤트 콘텐츠 부족이나 잘못된 ID는 Framework 코드 결함과 분리하여 Content 데이터 품질 문제로 관리한다.

---

## 2. 구현 상태 요약

### 2.1 완료된 기능

- 거리 기반 `TradeRouteEventProcessor`
- FNV-1a stable hash 기반 결정적 이벤트 처리
- Online Tick 자동 Route Event 처리
- Offline Restore 자동 Route Event 처리
- 처리 완료 check cursor 저장 및 복원
- 비선택 Caravan 자동 이벤트 처리
- Multi-active Caravan별 독립 처리
- 명시적 Forced Route Event API
- Forced 저장 실패 시 대상 Caravan rollback
- Route Event table의 Shared Data 전달
- EditMode 결정성 테스트
- Editor Runtime Verification

### 2.2 검증 결과

```text
전체 판정: 조건부 PASS

자동 Online: PASS
자동 Offline: PASS
결정성: PASS
cursor 영속화: PASS
재실행 중복 방지: PASS
Multi-active: PASS
비선택 Caravan: PASS
Forced 성공: PASS
Forced cursor 격리: PASS
Forced rollback: PASS
Settlement 순서: PASS
기존 회귀: PASS
```

조건부 항목은 다음과 같다.

- Lucky/Weather 전용 Runtime 효과 API 없음
- `RollbackFailed` 예외 강제 재현 미실행
- Production Route 대부분 이벤트 콘텐츠 없음
- Play Mode 전체 InGame 수동 세션 미실행

위 항목은 현재 Framework API 병합을 막는 결함이 아니다.

---

# 3. Route Event 데이터 구조

## 3.1 Route 정의

Framework 공용 Route 정의는 다음 이벤트 컬렉션을 사용한다.

```csharp
SharedRouteDefinition.Events
```

이벤트 정의 타입:

```csharp
SharedRouteEventDefinition
```

주요 필드 의미:

| 필드 | 의미 |
|---|---|
| `Id` | Route Event의 stable ID |
| `RouteEvent` | 이벤트 종류 |
| Route 배열 순서 | 결정적 이벤트 선택 입력 중 하나 |

현재 확인된 이벤트 종류:

```text
Combat
Lucky
Weather
```

## 3.2 이벤트 빈도 설정

자동 이벤트 체크 간격은 Route의 다음 값으로 계산한다.

```text
eventIntervalKm = Distance / MaxEventCount
```

발생 확률은 다음 값을 사용한다.

```text
BaseRiskLevel
```

`BaseRiskLevel`은 처리 시 `0..1` 범위로 clamp된다.

### 자동 이벤트 비활성 조건

다음 중 하나이면 자동 Route Event가 실질적으로 발생하지 않는다.

```text
MaxEventCount <= 0
Events가 null 또는 empty
Distance <= 0
BaseRiskLevel <= 0
```

Forced Route Event는 자동 발생 확률과 별개의 명시적 Command다. 다만 대상 event가 해당 Route의 `Events`에 존재해야 한다.

---

# 4. Runtime 및 Save 상태

Route Event의 중복 실행을 방지하기 위해 Caravan Runtime과 Save DTO에 다음 상태를 유지한다.

```csharp
runEventChecksProcessed
runEventsOccurred
runFatalReason
```

| 필드 | 역할 |
|---|---|
| `runEventChecksProcessed` | 현재 Trade에서 이미 소비한 자동 이벤트 check 수 |
| `runEventsOccurred` | 현재 Trade에서 실제 발생한 이벤트 수 |
| `runFatalReason` | fatal Route Event가 발생한 경우 최종 실패 원인 |

## 4.1 Mapper 계약

다음 양방향 복사가 유지되어야 한다.

```text
CaravanSaveDataMapper.ToRuntime
CaravanSaveDataMapper.CopyToSave
```

필수 대응:

```text
Save.runEventChecksProcessed ↔ Runtime.runEventChecksProcessed
Save.runEventsOccurred       ↔ Runtime.runEventsOccurred
Save.runFatalReason          ↔ Runtime.runFatalReason
```

## 4.2 초기화 시점

새 Trade가 정상 출발할 때 `JourneyRunner`가 다음 상태를 초기화한다.

```csharp
runEventChecksProcessed = 0;
runEventsOccurred = 0;
runFatalReason = string.Empty;
```

다음 시점에는 초기화하면 안 된다.

- Save Load
- Runtime Registry rebuild
- 씬 재진입
- Online Tick
- Offline Restore
- 같은 Trade의 재조회
- SettlementPending 복구

---

# 5. 결정성 계약

## 5.1 stable hash 입력

자동 Route Event는 FNV-1a stable hash를 사용한다.

기본 입력:

```text
tradeId
checkIndex
purpose
```

purpose는 최소 다음 단계에서 분리한다.

```text
occur  : 이벤트 발생 여부
select : 이벤트 선택
apply  : 이벤트 효과 내부 결정
```

이 분리를 통해 발생 판정과 선택 판정이 서로 영향을 주지 않도록 한다.

## 5.2 금지 입력

다음 값은 결정성 입력으로 사용하지 않는다.

```text
UnityEngine.Random
공유 System.Random
현재 시간
프레임 수
호출 횟수
selectedCaravanId
string.GetHashCode()
```

## 5.3 결과 동등성

동일한 초기 Caravan 상태와 동일한 다음 입력을 사용하면 처리 방식에 관계없이 결과가 같아야 한다.

```text
tradeId
Route Event 배열과 순서
BaseRiskLevel
MaxEventCount
최종 이동 거리
```

비교 대상:

```text
Online 분할 Tick
Online 일괄 Tick
Offline Restore
```

동일해야 하는 결과:

- 처리 cursor
- 이벤트 발생 check
- 선택 event ID
- cargo/food/durability 변화
- fatal 여부
- fatal reason

## 5.4 Route 배열 순서 주의

Route Event 선택은 Route의 이벤트 배열 순서를 결정성 입력으로 사용한다.

따라서 Production 데이터에서 이벤트 배열 순서를 변경하면 기존과 같은 `tradeId + checkIndex`라도 선택되는 이벤트가 달라질 수 있다.

이미 배포된 저장 데이터와 재현성을 중시하는 단계에서는 Route Event 배열 순서를 임의 변경하지 않는다.

---

# 6. 자동 Route Event API

## 6.1 Processor 역할

`TradeRouteEventProcessor`는 다음을 담당한다.

- 현재 이동 거리에서 완료된 check 수 계산
- 미처리 check index 순차 처리
- 이벤트 발생 여부 판정
- Route Event 선택
- Runtime Caravan에 이벤트 효과 적용
- cursor 및 발생 수 갱신
- fatal 결과 기록
- 처리 결과 반환

다음은 담당하지 않는다.

- SaveData lookup
- Runtime Registry lookup
- selected Caravan 선택
- Save 호출
- rollback
- Framework Event 발행
- Settlement 생성
- UI 전환

## 6.2 자동 처리 API

현재 구현의 개념적 API는 다음과 같다.

```csharp
TradeRouteEventProcessor.Process(
    CaravanData caravan,
    SharedRouteDefinition route,
    string tradeId,
    float eventIntervalKm,
    float eventChancePerCheck)
```

실제 반환 타입과 접근 제한자는 구현 코드 기준을 따른다.

## 6.3 check 처리 규칙

완료된 check 수는 이동 거리와 이벤트 간격으로 계산한다.

```text
completedCheckCount
= floor(currentDistanceKm / eventIntervalKm)
```

처리 범위:

```text
runEventChecksProcessed
부터
completedCheckCount - 1
까지
```

이벤트가 발생하지 않은 check도 cursor를 소비한다.

```text
미발생 check
→ runEventChecksProcessed 증가
→ 같은 거리에서 재실행해도 다시 판정하지 않음
```

fatal 이벤트가 발생하면 이후 check 처리를 중단한다.

---

# 7. Online Tick 통합 계약

Online Tick의 entry별 권장 실행 순서는 다음과 같다.

```text
1. TradeProgress entry 조회
2. caravanId로 Save Caravan 조회
3. caravanId로 Runtime Caravan 조회
4. elapsed 및 progress 계산
5. JourneyRunner.SetProgress
6. TradeRouteEventProcessor.Process
7. fatal 판정
8. 도착 판정
9. 필요 시 Settlement 생성
10. Runtime을 동일 CaravanSaveData에 복사
11. batch dirty 표시
12. 전체 entry 처리 후 Save 최대 1회
```

핵심 순서:

```text
이동 거리 갱신
→ Route Event
→ fatal
→ arrival
→ Settlement
```

Settlement를 먼저 생성하면 마지막 거리 구간의 이벤트가 누락될 수 있으므로 순서를 바꾸지 않는다.

## 7.1 Multi-active 대상 식별

자동 처리 대상은 각 `tradeProgressEntries`의 다음 값으로 식별한다.

```text
progress.caravanId
progress.activeTradeId
progress.activeRouteId
```

다음 값은 canonical 처리에 사용하지 않는다.

```text
selectedCaravanId
ActiveCaravan
saveData.tradeProgress
saveData.caravan
EnsureActiveCaravan()
```

## 7.2 오류 격리

하나의 잘못된 entry가 다른 Caravan 처리를 막으면 안 된다.

```text
entry 오류
→ Warning
→ 해당 entry skip
→ 다음 entry 계속 처리
```

Save는 변경된 모든 정상 entry를 묶어 Tick당 최대 1회 수행한다.

---

# 8. Offline Restore 통합 계약

Offline Restore는 모든 Traveling entry를 snapshot 순회한다.

실행 순서:

```text
1. Offline elapsed 계산
2. 최종 progress 반영
3. JourneyRunner.SetProgress
4. 저장 cursor 이후의 check 순차 처리
5. fatal 발생 시 이후 check 중단
6. fatal 또는 arrival Settlement 생성
7. 동일 CaravanSaveData 갱신
8. 전체 entry 처리 후 Save 최대 1회
```

Online과 Offline은 동일 Processor를 사용해야 한다.

## 8.1 재실행 중복 방지

```text
Offline Restore에서 cursor 0→4 처리
→ Save
→ Runtime Registry rebuild
→ 같은 거리에서 재실행
→ check 0~3 재처리 금지
```

새로운 거리 구간을 통과한 경우에만 새 check를 처리한다.

---

# 9. Forced Route Event API

## 9.1 공개 진입점

Coordinator에 다음 명시적 API가 추가됐다.

```csharp
TryProcessForcedRouteEvent(
    string caravanId,
    string tradeId,
    string eventId)
```

실제 반환 타입은 구현된 `ForcedRouteEventResult`를 기준으로 한다.

기존 `TryProcessForcedRouteEvent(string tradeId, string eventId)` 호출부가 없었으므로 selected 또는 전체 검색 기반 호환 wrapper는 추가하지 않았다.

## 9.2 사용 목적

Forced API는 다음 용도로 제한한다.

- Editor Debug
- QA fixture
- Content 검증 도구
- 특정 Route Event 재현 테스트

플레이어 UI가 임의로 호출하는 일반 게임 Command로 노출하지 않는다.

## 9.3 필수 입력

| 입력 | 의미 |
|---|---|
| `caravanId` | 이벤트를 적용할 명시적 Caravan |
| `tradeId` | 해당 Caravan의 현재 Traveling Trade |
| `eventId` | 해당 Route에 등록된 Event ID |

## 9.4 변경 전 검증

다음 조건을 모두 확인한 후에만 Runtime을 변경한다.

```text
caravanId 유효
tradeId 유효
eventId 유효
해당 caravanId의 progress 존재
progress.state == Traveling
progress.activeTradeId == tradeId
progress.activeRouteId 유효
해당 caravanId의 CaravanSaveData 존재
해당 caravanId의 Runtime Caravan 존재
Runtime/Save/Progress ID 일치
activeRouteId Route 존재
Route.Events에 eventId 존재
이미 fatal 상태가 아님
```

검증 실패 시:

- Runtime 변경 없음
- SaveData 변경 없음
- Save 호출 없음
- 다른 Caravan 변경 없음
- 성공 로그 없음

## 9.5 Forced cursor 계약

Forced Event는 자동 check cursor를 소비하지 않는다.

```text
Forced 실행 전 runEventChecksProcessed = N
Forced 실행 후 runEventChecksProcessed = N
```

실제 이벤트가 적용되면 `runEventsOccurred`는 증가할 수 있다.

Forced 실행 여부가 이후 자동 이벤트의 `checkIndex` 또는 seed를 변경하면 안 된다.

---

# 10. Forced 저장 및 rollback 계약

Forced Route Event는 개별 중요 Command로 처리한다.

```text
입력 검증
→ 대상 Runtime/Save snapshot
→ ProcessForced
→ Runtime을 같은 CaravanSaveData에 복사
→ Save
```

## 10.1 Save 성공

- 성공 결과 반환
- 발생 성공 로그 출력
- 대상 Caravan 변경 확정

## 10.2 Save 실패

- 대상 Runtime Caravan 복구
- 대상 `CaravanSaveData` 복구
- Runtime Registry가 동일 Runtime 객체를 계속 참조하도록 유지
- 성공 결과 반환 금지
- 성공 로그 출력 금지
- 다른 Caravan 무변경

확인된 실패 이유:

```text
SaveFailed
RollbackFailed
```

`SaveFailed` rollback은 검증 완료됐다.

`RollbackFailed`는 예외 강제 fixture가 없어 아직 직접 재현하지 않았다.

---

# 11. Route Event 효과 적용 범위

## 11.1 Combat

Combat 이벤트는 다음 Core API를 사용한다.

```csharp
JourneyRunner.ResolveBanditRaid(...)
```

발생 가능한 결과:

- cargo 감소
- food 감소
- durability 감소
- fatal 상태
- `runFatalReason` 기록

Combat 효과는 Online/Offline/Forced 모두 같은 Core API를 사용해야 한다.

## 11.2 Lucky

현재 Lucky 전용 Runtime 효과 API가 없다.

현재 처리:

```text
이벤트 발생 판정
→ runEventsOccurred 증가
→ cursor 소비
→ 별도 cargo/food/durability 효과 없음
```

## 11.3 Weather

현재 Weather 전용 Runtime 효과 API가 없다.

현재 처리:

```text
이벤트 발생 판정
→ runEventsOccurred 증가
→ cursor 소비
→ 별도 Runtime 효과 없음
```

Lucky/Weather 효과는 기획과 Core API가 확정된 후 별도 작업으로 추가한다. Framework Processor에서 임의 효과를 설계하지 않는다.

---

# 12. Shared Game Data 변환 계약

`SharedGameDataService`는 원본 `RouteData.RouteEvents`를 Framework 공용 Route 정의로 복사한다.

```text
RouteData.RouteEvents
→ SharedRouteDefinition.Events
```

복사 시 보장할 항목:

- event 개수 유지
- event ID 유지
- event type 유지
- 배열 순서 유지
- null collection은 empty collection으로 안전 변환
- 기존 Route 필드 유지

기존 Route 필드 예시:

```text
FromTownId
ToTownId
Distance
BaseRiskLevel
MaxEventCount
```

Route Event 변환 오류가 Shared Data 전체 로드를 막거나 InGame 진입을 회귀시키면 안 된다. 단, 명시적으로 invalid 데이터 차단 정책이 추가될 경우 별도 계약 변경이 필요하다.

---

# 13. Production 데이터 현황

검증 시 Production Route 데이터는 다음 상태였다.

- 총 Route load: 8개
- 대부분 `Events = []`
- 대부분 `MaxEventCount = 0`
- 대부분 `BaseRiskLevel = 0`

따라서 Framework 기능은 구현되어 있지만 실제 Production 플레이에서는 자동 Route Event가 대부분 비활성 상태다.

## 13.1 확인된 dummyroute 문제

```text
Route: dummyroute
Events: 1
Events[0].Id: 빈 문자열
Events[0].Type: Combat
MaxEventCount: 0
```

현재 `MaxEventCount = 0`이므로 자동 이벤트는 실행되지 않는다.

하지만 다음 문제는 남는다.

- Forced API에서 유효한 eventId로 지정할 수 없음
- 로그 추적이 불가능함
- 향후 MaxEventCount 활성화 시 invalid 콘텐츠가 Production에 노출됨
- 이벤트 결과 저장·분석 시 식별자가 없음

판정:

```text
심각도: Low
현재 Framework PR 차단: No
Content 데이터 수정 필요: Yes
```

---

# 14. Production Route Event 데이터 규칙

Content 담당자는 Route Event 데이터를 입력할 때 다음 규칙을 지켜야 한다.

## 14.1 Event ID

모든 이벤트는 비어 있지 않은 stable ID를 가져야 한다.

권장 형식:

```text
route_event_{category}_{name}
```

예시:

```text
route_event_combat_bandit_raid
route_event_weather_heavy_rain
route_event_lucky_abandoned_cart
```

규칙:

- null/empty/whitespace 금지
- 같은 Route 내 중복 금지
- 가능하면 전체 프로젝트에서도 중복 금지
- 표시 이름과 분리
- 한 번 사용한 ID는 이름 변경 없이 유지
- 대소문자 규칙 통일

## 14.2 MaxEventCount

`MaxEventCount`는 Route 한 번 이동 중 자동 check 최대 수를 의미한다.

```text
MaxEventCount = 0
→ 자동 Route Event 비활성
```

활성화하려면 1 이상으로 설정한다.

입력 시 확인:

- `Distance > 0`
- `MaxEventCount > 0`
- `Distance / MaxEventCount`가 지나치게 짧지 않은지
- 한 Tick 또는 Offline Restore에서 과도한 이벤트가 발생하지 않는지

## 14.3 BaseRiskLevel

```text
0   : 자동 발생 없음
0~1 : check당 발생 확률
1   : 모든 check에서 이벤트 발생
```

Production 밸런스 값은 Progression 담당과 협의한다.

테스트용 fixture에서만 1을 사용할 수 있으며, 검증 목적으로 Production asset에 임시 입력했다면 반드시 원복한다.

## 14.4 Events 배열

- empty면 자동/Forced 이벤트 대상 없음
- 배열 순서는 결정성에 영향을 줌
- 배포 이후 무분별한 순서 변경 금지
- 삭제·삽입·재정렬 시 기존 결정 결과 변화 가능성을 기록
- Combat/Lucky/Weather 비율은 기획과 밸런스 정책에 맞춤

## 14.5 이벤트 종류별 준비 상태

| 종류 | Production 활성 권장 상태 |
|---|---|
| Combat | Core 효과 API와 데이터가 준비되면 활성 가능 |
| Lucky | 효과 API 확정 전에는 발생 기록만 가능 |
| Weather | 효과 API 확정 전에는 발생 기록만 가능 |

Lucky/Weather가 플레이어에게 아무 효과 없이 로그상 발생하는 것이 문제라면, 효과 API가 준비될 때까지 Production Route의 Events에서 제외하거나 `BaseRiskLevel/MaxEventCount`로 비활성화한다.

---

# 15. Production 데이터 수정 절차

## 15.1 담당

주 담당:

```text
Content & Tools
```

협의 담당:

```text
Core Gameplay: 이벤트 효과와 fatal 계약
Progression & System: 발생 확률·간격·손실 밸런스
Framework & Integration: ID·Shared Data·저장 재현성 검토
UI & Data: 표시 문구와 이벤트 결과 표현
```

## 15.2 수정 순서

```text
1. 대상 Route 목록 작성
2. 각 Route의 Distance 확인
3. MaxEventCount 결정
4. BaseRiskLevel 결정
5. Events 구성
6. 모든 Event ID 검증
7. event type 확인
8. Shared Data load 검증
9. Runtime Verification 실행
10. Online/Offline 결과 확인
11. PR에서 배열 순서 변경 명시
```

## 15.3 최소 Production 활성 조건

한 Route에서 자동 이벤트를 실제 활성화하려면 최소 다음을 만족해야 한다.

```text
Distance > 0
MaxEventCount >= 1
BaseRiskLevel > 0
Events.Count >= 1
모든 Events[i].Id가 유효
지원되는 event type
```

## 15.4 비활성 Route 처리

이벤트 콘텐츠가 아직 준비되지 않은 Route는 명시적으로 비활성 상태를 유지한다.

권장:

```text
MaxEventCount = 0
BaseRiskLevel = 0
Events = []
```

다음과 같은 반쪽 상태는 피한다.

```text
Events는 존재하지만 ID가 비어 있음
Events는 존재하지만 MaxEventCount = 0
MaxEventCount > 0인데 Events = []
BaseRiskLevel > 0인데 MaxEventCount = 0
```

기획상 미래 콘텐츠를 미리 연결해야 한다면 문서나 주석으로 비활성 사유를 명시한다.

---

# 16. Production 데이터 검증 체크리스트

## 16.1 Shared Data

- [ ] Route가 정상 로드된다.
- [ ] `FromTownId`, `ToTownId`, `Distance`가 유지된다.
- [ ] `BaseRiskLevel`이 의도한 값이다.
- [ ] `MaxEventCount`가 의도한 값이다.
- [ ] 원본 RouteEvents 수와 Shared Events 수가 같다.
- [ ] event ID가 모두 비어 있지 않다.
- [ ] event type이 일치한다.
- [ ] event 배열 순서가 유지된다.
- [ ] Shared load Error가 없다.
- [ ] InGame 진입이 차단되지 않는다.

## 16.2 Runtime

- [ ] selected Caravan에서 자동 이벤트가 처리된다.
- [ ] non-selected Caravan에서도 처리된다.
- [ ] Preparing Caravan은 처리되지 않는다.
- [ ] 미발생 check도 cursor가 증가한다.
- [ ] 같은 거리에서 중복 발생하지 않는다.
- [ ] Save 후 재실행해도 cursor가 복원된다.
- [ ] Online/Offline 결과가 같다.
- [ ] fatal 이벤트가 이후 check를 중단한다.
- [ ] 마지막 구간 이벤트가 Settlement 전에 처리된다.

## 16.3 Forced

- [ ] 유효한 `caravanId + tradeId + eventId`로 성공한다.
- [ ] non-selected Caravan을 명시적으로 처리할 수 있다.
- [ ] 잘못된 ID에서 다른 Caravan이 변경되지 않는다.
- [ ] Forced가 자동 cursor를 소비하지 않는다.
- [ ] Save 실패 시 대상 Runtime과 Save DTO가 복구된다.
- [ ] 성공 로그는 Save 성공 후에만 출력된다.

---

# 17. 테스트 및 디버그 진입점

## 17.1 EditMode 테스트

```text
TradeRouteEventProcessorTests
```

현재 검증 항목:

- 분할 거리와 일괄 거리의 결정성
- 처리 완료 check 재실행 방지
- Forced 자동 cursor 미소비

검증 결과:

```text
3/3 PASS
```

## 17.2 Runtime Verification

Unity 메뉴:

```text
ND/Framework/Run Route Event Runtime Verification
```

이 검증 도구는 다음을 재검증하는 용도로 유지한다.

- Multi-active Online
- 비선택 Caravan
- cursor 저장·복원
- Online/Offline parity
- Forced 성공
- Forced Save 실패 rollback
- 기존 Multi-active 회귀

검증 도구는 Production asset을 영구 변경하지 않아야 하며 반복 실행 가능해야 한다.

---

# 18. 로그 계약

현재 실제 소비자가 없어 별도 Framework Event는 추가하지 않았다.

Route Event 알림은 저장 성공 후 로그로 기록한다.

로그에 포함할 권장 필드:

```text
caravanId
tradeId
routeId
eventId
checkIndex
처리 경로: Online / Offline / Forced
fatal 여부
```

Save 실패 또는 rollback 발생 시 성공 로그를 출력하지 않는다.

Forced rollback 로그 예시 의미:

```text
Forced route event rolled back because save failed.
```

향후 UI 또는 분석 시스템이 Route Event occurrence를 직접 구독해야 할 때만 별도 Framework Event 계약을 추가한다.

---

# 19. API 사용 예시

## 19.1 Forced Combat 이벤트 재현

개념적 예시:

```csharp
var result = FrameworkRoot.Instance.TradeProgressCoordinator
    .TryProcessForcedRouteEvent(
        caravanId,
        tradeId,
        "route_event_combat_bandit_raid");

if (!result.Succeeded)
{
    // result.FailureReason 확인
}
```

호출 전 전제:

- 해당 Caravan이 Traveling
- `tradeId`가 현재 active trade와 일치
- 해당 Route의 Events에 event ID가 존재
- 이미 fatal 상태가 아님

## 19.2 사용하면 안 되는 방식

```csharp
// selected Caravan을 암묵적으로 대상으로 삼는 API 금지
TryProcessForcedRouteEvent(tradeId, eventId);
```

```csharp
// UI 선택값을 canonical 식별자로 사용 금지
var caravanId = saveData.selectedCaravanId;
```

항상 실제 명령 대상의 명시적 `caravanId`와 `tradeId`를 전달한다.

---

# 20. 후속 작업

## 20.1 Content 데이터 정리

- `dummyroute`의 빈 event ID 수정 또는 event 제거
- 실제 Production Route별 `MaxEventCount` 설정
- 실제 Production Route별 `BaseRiskLevel` 설정
- Route별 Events 구성
- 배열 순서 확정
- ID 중복 검사 도구 보강

권장 이슈 제목:

```text
fix(content): assign valid route event IDs and activation settings
```

## 20.2 Lucky/Weather API

별도 기획·Core 계약이 필요하다.

확정할 항목:

- Lucky 보상 종류
- Weather의 속도·식량·위험 영향
- 일시 효과와 누적 효과 구분
- Settlement snapshot 기록 방식
- Online/Offline 동일 재현 방식
- fatal 가능 여부
- UI 표시 데이터

## 20.3 RollbackFailed 테스트

현재 Save 실패 rollback은 PASS다.

향후 rollback 과정에서 예외를 주입할 수 있는 test double이 마련되면 다음을 추가 검증한다.

```text
FailureReason == RollbackFailed
성공 로그 없음
Registry 상태 오염 없음
다른 Caravan 무변경
```

---

# 21. 최종 운영 결론

Framework Route Event 구현은 현재 Multi-active 계약에 맞게 완료됐다.

핵심 원칙:

```text
1. 자동 이벤트 대상은 progress.caravanId로 식별한다.
2. Online과 Offline은 같은 결정적 Processor를 사용한다.
3. 처리 cursor를 저장해 재실행 중복을 막는다.
4. Forced API는 caravanId + tradeId + eventId를 명시한다.
5. Forced 저장 실패 시 대상 Caravan만 rollback한다.
6. Route Event는 fatal·arrival·Settlement 판정보다 먼저 처리한다.
7. Production 콘텐츠 문제는 Framework 코드 문제와 분리한다.
8. 모든 Production Event는 유효한 stable ID를 가져야 한다.
9. Route Event 배열 순서는 결정성 계약의 일부다.
10. Lucky/Weather 효과는 Core API 확정 전 임의 구현하지 않는다.
```

현재 Framework PR은 Production 데이터 부족과 별개로 병합 가능하다.

Production Route Event 활성화는 Content 데이터 정리와 이벤트 종류별 효과 계약을 완료한 뒤 별도 PR로 진행한다.
