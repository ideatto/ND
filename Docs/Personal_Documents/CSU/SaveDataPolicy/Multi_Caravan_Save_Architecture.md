# Multi-active TradeProgressCoordinator Plan

## 목적과 범위

이 문서는 numeric version 7 목표의 multi-active runtime orchestration 계약을 정의한다. Framework는 persistence, stable-key lookup, UTC progress input, SaveResult, integration Event를 담당한다. Core/Economy 계산, UI 직접 제어, 출발·적재·식량·보상·Building·Investment 비용 계산은 담당하지 않는다.

## Current Implementation

- numeric version 6의 `caravans[]`, `tradeProgressEntries[]`, `pendingSettlements[]`가 persisted source of truth이다.
- `selectedCaravanId`와 selected compatibility properties가 있으며 coordinator는 단일 `activeCaravan`, 단일 last-settlement cache, selected entry 중심 처리에 의존한다.
- `ClaimSettlement(caravanId, tradeId)`와 ID-bearing `TradeSettlementReady`는 구현되어 있다.
- settlement finalization은 `SaveResult`를 무시한 채 Event와 화면 전환을 수행할 수 있다.
- offline recovery, Force Complete, map progress, 화면 routing은 복수 active entry 전체를 처리하지 않는다.

## Approved Target Contract

### Persisted source와 runtime cache

```text
SaveData (serialized lists)
- caravans[]
- tradeProgressEntries[]
- pendingSettlements[]

Runtime (never serialized)
- Dictionary<string, TradeRuntimeContext> keyed by caravanId
```

Context 최소 개념은 `CaravanId`, `TradeId`, `RuntimeCaravan`, `LoadedRevision`, `IsValid`이다. Context를 사용할 때 현재 `tradeId`를 함께 검증한다. SaveData List만 persisted source of truth이며 Dictionary는 load 이후 재구성 가능한 cache다.

`selectedCaravanId`는 UI 선택과 마지막 선택 복원에만 사용한다. 진행, 완료, Force Complete, settlement 생성, Claim, runtime context 대상을 추론하지 않는다. 모든 진행·완료·정산 Command는 `caravanId + tradeId`를 기준으로 한다.

### Traveling 순회

1. 모든 유효한 Traveling entry snapshot을 만든다.
2. `tradeStartUtcTicks → caravanId → tradeId` 순으로 정렬한다. 이 순서는 로그와 테스트 재현용이며 게임 우선순위가 아니다.
3. entry별 ID와 composite 관계를 검증한다.
4. runtime context를 조회하거나 SaveData에서 복구한다.
5. 현재 UTC 기반 progress input을 계산하고 Core `JourneyRunner`를 호출한다.
6. 완료·실패 결과를 메모리에 staging한다.
7. 모든 entry 처리 후 해당 tick 변경을 한 번 Save한다.
8. Save 성공 후 runtime state를 commit하고 완료 entry마다 Event를 발행한다.

Entry 오류는 해당 Caravan만 차단하고 다른 entry 처리를 계속한다. 오류 데이터를 삭제하거나 ID를 바꾸지 않는다. batch Save 실패 시 그 tick의 모든 staging을 rollback하고 Event와 Settlement 화면 전환을 금지한다.

### 완료, Pending, Event

- pending canonical key는 `(caravanId, tradeId)`이다.
- 동일 composite key pending을 두 번 만들지 않는다.
- 같은 Caravan에 다른 `tradeId` pending이 둘 이상이면 임의 선택하지 않고 validation failure로 처리한다.
- finalization 순서는 pending staging → Save → SaveResult 성공 → runtime commit → `TradeSettlementReady(caravanId, tradeId, result)`이다.
- 일반 시간 완료와 `ForceCompleteTrade(caravanId, tradeId)`는 같은 내부 finalization 경로를 쓴다.

### 화면 routing

전역 화면은 현재 selected Caravan만 반영한다.

```text
Pending 존재 → Settlement
Traveling 존재 → Traveling
그 외 → Preparation
```

비선택 Caravan 완료는 pending 저장과 Event/알림을 허용하지만 화면을 강제로 바꾸지 않는다. aggregate badge/경고 UX는 미결정이며 이 계약의 진행 대상 선택에 영향을 주지 않는다.

### Load와 offline recovery

- 모든 Traveling entry를 복구하고 entry별 UTC progress를 계산한다.
- 이미 완료된 여러 entry는 같은 batch settlement 경로로 처리한다.
- 기존 Pending의 confirmed result snapshot은 재계산하지 않는다.
- runtime Dictionary는 validated SaveData에서 다시 만든다.
- 화면은 recovery 완료 후 selected Caravan 상태로 결정한다.

### orphan과 duplicate

- duplicate Caravan ID, progress/pending composite key, same-Caravan ambiguous pending은 visible validation failure다.
- orphan과 unknown shared ID는 보존하고 해당 Caravan Command를 차단한다.
- normalization에서 자동 삭제, first/last 선택, GUID 재발급, child relink를 하지 않는다.

### 공개 API 목표

```csharp
TradeDepartureResult Depart(string caravanId, TradeDepartureRequest request);
TradeForceCompleteResult ForceCompleteTrade(string caravanId, string tradeId);
ClaimSettlementResult ClaimSettlement(string caravanId, string tradeId);
```

public finalization result의 구체 타입명은 구현 PR에서 기존 Result convention과 함께 확정한다. identity와 Save/Event timing은 확정 계약이다.

## Transition Steps

1. Stage 1 — v7 DTO, validation, v6 reset handling, shared definition 계약
2. Stage 2 — explicit-ID Depart와 compatibility adapter
3. Stage 3 — runtime context Dictionary, 전체 Traveling 순회, entry 오류 격리, ForceCompleteTrade
4. Stage 4 — tick batch Save 1회, rollback, Save 성공 후 Event, 단일 cache 제거
5. Stage 5 — 복수 Traveling/Pending load recovery, selected 화면 routing, UI subscriber migration, deprecated API 제거 준비

후속 구현 PR은 SaveData v7, Building ID, InvestmentQuest, coordinator, UI/legacy migration 단위로 분리한다.

## 테스트 매트릭스

- Caravan A/B 동시 Traveling; A Pending + B Traveling
- 동일 tick 복수 완료에서 Save 1회와 완료 수만큼 Event
- 한 entry 오류 시 다른 entry 진행
- Save 실패 시 전체 batch rollback, Event 0회, 화면 전환 없음
- selected 변경이 진행/완료 대상에 영향 없음
- 비선택 Caravan 완료 시 화면 강제 전환 없음
- 복수 Traveling offline recovery와 기존 Pending snapshot 복원
- 복수 Pending 중 exact 하나만 Claim
- duplicate/orphan/ambiguous pending 차단과 데이터 보존

## 제외 범위와 미결정

이 문서는 production C#, SaveData version 값, Scene/Prefab/ScriptableObject를 변경하지 않는다. aggregate UI 표현, retry/queue 구체 타입, JourneyResultData immutable snapshot 교체 시점은 후속 결정이다.

