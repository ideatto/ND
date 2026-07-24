# TradePrepare 멀티 Caravan Provider 연결 요청

작성일: 2026-07-21
구현 정합성 갱신: 2026-07-24
외부 요청 영역: Caravan 런타임·저장 데이터 제공자 및 Framework/Integration
요청 목적: 최대 4개의 실제 Caravan을 조회할 데이터 계약과 `caravanId` 기반 출발 Command를 제공받는다. Provider 결과를 기존 TradePrepareUI에 표시하고 선택 이벤트를 연결하는 작업은 UI & Data가 담당한다.

---

## 1. 현재 준비된 UI/Data 계약

UI/Data 영역에는 다음 계약이 준비되어 있다.

```csharp
public interface ITradePrepareCaravanOptionProvider
{
    TradePrepareCaravanOptionViewData[] GetOptions();
}
```

```csharp
public sealed class TradePrepareCaravanOptionViewData
{
    public string caravanId;
    public string displayName;
    public string currentTownId;
    public JourneyState state;
    public bool canSelect;
    public string disabledReason;
}
```

`TradePrepareDraft.departureCaravanId`는 이번 출발 대상으로 선택한 Caravan ID만 의미한다.

본 문서의 `fullTradeId`는 Framework가 출발 확정 시 발급하는 최종 무역 ID를 의미한다. 저장 DTO의 필드명이 `activeTradeId` 또는 `tradeId`이더라도 같은 값을 전달·저장해야 하며, UI는 최종 ID를 생성하지 않는다.

- `CaravanBlockViewData.caravanId`: Overview의 Setting/Cargo 버튼이 해당 Caravan을 조회할 때 전달하는 ID이며 선택 상태가 아니다.
- `SaveData.selectedCaravanId`: 기존 단일 runtime 호환 접근자가 사용하는 Framework 선택 ID이다.
- `TradePrepareDraft.departureCaravanId`: TradePrepareUI 안에서 이번 출발 대상으로 선택한 ID이다.

Overview의 Caravan 정보 영역은 표시 전용이다. Overview의 표시나 편집 대상이 출발 Draft로 자동 복사되어서는 안 된다.
S_CaravanSlot에서 옵션을 선택하면 `departureCaravanId`와 그 옵션의 `currentTownId`를 출발 Draft에 함께 반영한다. 현재 Framework의 화면 전환·정산 호환 계층이 `selectedCaravanId`를 함께 사용하므로, Provider 적용이 모두 성공한 뒤 `SaveData.selectedCaravanId`도 같은 Caravan ID로 동기화한다. 장기적으로 모든 화면·정산 소비자가 명시적 ID를 사용하게 되면 이 호환 동기화를 제거할 수 있다.

---

## 2. 기준 Framework 구조

`dev2`의 멀티 Caravan Save cutover 이후 구조를 기준으로 구현한다.

- merge commit: `86aaca7`
- source commit: `232617e`
- schema version: 6

사용 가능한 저장 필드:

```csharp
public List<CaravanSaveData> caravans;
public List<TradeProgressSaveData> tradeProgressEntries;
public List<PendingSettlementSaveData> pendingSettlements;
public string selectedCaravanId;
```

조회에는 배열 위치가 아니라 `SaveDataLookup.TryGetCaravan(saveData, caravanId, out caravan)`을 사용한다.

기존 `SaveData.caravan`, `tradeProgress`, `pendingSettlement`은 `selectedCaravanId` 기반 호환 접근자이므로 새로운 명시적 ID 흐름의 원본 API로 사용하지 않는다.

---

## 3. 실제 Option Provider 또는 조회 API 제공 요청

Framework/Caravan 기능 제작자는 실제 저장 상태, Caravan별 현재 위치, 진행·정산 상태와 출발 가능 판정 결과를 읽을 수 있는 권위 있는 조회 API를 제공한다. UI & Data는 이 API를 `ITradePrepareCaravanOptionProvider`에 맞게 조립하고 화면에 표시한다. Framework가 UI 소유 인터페이스를 직접 구현해 제공해도 되지만 필수는 아니며, 어느 방식을 선택해도 아래 데이터 의미와 판정 책임은 Framework/Caravan 기능 측에 남는다.

Provider 규칙:

1. `SaveData.caravans`에서 최대 4개의 실제 Caravan을 조회한다.
2. UI가 생성하거나 배열 위치로 추론한 ID·슬롯·현재 위치를 사용하지 않는다.
3. 모든 항목은 Framework가 저장한 안정적인 `caravanId`와 해당 Caravan의 권위 있는 위치를 사용한다.
   `currentTownId`는 UI가 추론하지 않고 같은 Option 스냅샷에 포함한다.
4. 배열과 항목은 `null`이 아니어야 한다.
5. 호출할 때마다 UI가 변경해도 원본이 오염되지 않는 새 스냅샷을 반환한다.
6. 같은 `caravanId`를 두 번 반환하지 않는다.
7. 출발 가능 판정과 차단 사유의 원본은 Framework/Caravan 기능이 제공하며 UI Provider는 그 결과를 투영한다.
8. `canSelect == true`일 때 `disabledReason`은 빈 문자열이어야 한다.
9. `canSelect == false`일 때 사용자에게 전달 가능한 차단 사유를 제공한다.
10. `currentTownId`가 비어 있는 항목은 선택 가능 상태로 반환하지 않는다.

최소 차단 대상:

- `Traveling`
- `Settling`
- 정산 claim 대기
- 저장 처리 중
- 현재 위치에서 출발할 수 없는 Caravan
- 손상·구성 오류 등 Framework 출발 전제조건을 충족하지 못한 Caravan

UI는 `JourneyState`를 보고 `canSelect`를 재계산하지 않는다.

---

## 4. UI & Data 작업: `TradePrepareRuntimeContextProvider` 연결

이 절은 외부 구현 요청이 아니다. Provider 또는 조회 API가 제공된 뒤 UI & Data가 기존 RuntimeContext에 다음 접점을 연결한다.

1. `ITradePrepareCaravanOptionProvider` 구현체 주입 필드
2. 초기화 및 Framework refresh 시 `GetOptions()` 호출
3. 결과를 `TradePrepareBuildContext.caravanOptions`에 전달
4. UI 선택을 `TradePrepareFlowController.SelectDepartureCaravan(caravanId)`로 전달하는 public wrapper
5. 잘못된 MonoBehaviour가 주입된 경우 `OnValidate()` 오류

선택 wrapper의 필수 처리:

- SaveData에 실제 존재하는 `caravanId`인지 먼저 검증한다.
- Provider가 반환한 같은 Option의 `currentTownId`를 `departureCaravanId`와 함께 Draft에 반영한다.
- Caravan별 S3 구성과 S4 Cargo 계획을 같은 ID로 조회한다.
- Provider 적용이 실패하면 클릭 전 Draft 전체를 복원한다.
- 현재 호환 계층을 위해 Provider 적용 성공 후 `SaveData.selectedCaravanId`도 동일 ID로 동기화한다.

RuntimeContext는 Overview 블록의 `caravanId`를 출발 Draft에 자동으로 넣지 않는다. 사용자가 TradePrepareUI의 Caravan 선택 단계에서 선택했을 때만 `departureCaravanId`를 갱신한다.
Route 위치는 S3 `CaravanSettingViewData`에서 가져오지 않는다. S3 Provider는 마차·동물 구성만 담당하며 빠지거나 교체되어도 Option의 위치 스냅샷 의미는 바뀌지 않는다.

---

## 5. UI & Data 작업: `CaravanSlotPanel` 연결

현재 패널은 문자열 목록과 슬롯 index만 사용한다. 실제 Provider 모드의 표시와 이벤트 연결은 UI & Data가 다음 계약으로 구현한다.

```csharp
public void PopulateCaravanOptions(
    IReadOnlyList<TradePrepareCaravanOptionViewData> options);

public event Action<string> OnDepartureCaravanSelected;
```

필수 동작:

- 표시명과 Journey 상태 표시
- `canSelect == false`인 항목 클릭 차단
- 차단 항목의 `disabledReason` 전달 또는 표시
- 선택 결과로 index가 아닌 `caravanId` 전달
- 선택 성공 시 Option의 `currentTownId`가 Draft와 이후 Route ViewData에 반영
- 같은 카드를 다시 눌렀을 때 UI만 해제되어 Draft와 달라지지 않도록 처리
- Provider refresh 후 기존 선택 ID가 사라지거나 차단되면 Next 비활성화

---

## 6. UI & Data 작업: `TradePrepareUIManager` 연결

현재 다음 필드는 UI 내부 임시 프리셋 저장소이다.

```csharp
private string[] caravanSlots;
private SlotComp[] slotData;
```

실제 Provider 모드에서는 위 데이터를 실제 Caravan 원본처럼 사용하지 않는다.

UI & Data가 수행할 변경:

1. Caravan Option Provider delegate 또는 Runtime binding 접점 추가
2. `GoCaravanSlot()`에서 Provider 옵션 표시
3. 선택 결과 `caravanId`를 RuntimeContext로 전달
4. Provider 모드와 Demo 임시 슬롯 모드 분리
5. `FromSlotNext()`가 빈 `slotData`를 읽고 구성 단계를 잘못 건너뛰지 않도록 분기
6. UI의 구성 저장 Toggle이 실제 저장 Command 없이 저장된 것처럼 표시되지 않도록 처리

Demo 호환이 필요하면 Provider가 없을 때만 기존 `caravanSlots` 경로를 사용한다.

---

## 7. Framework 요청: 동시 무역 출발 API 및 저장 경계

목표 기능은 최대 4개 Caravan이 서로 독립적인 무역 상태를 가지며, 서로 다른 Caravan이 동시에 `Traveling` 또는 `SettlementPending` 상태에 존재하는 것이다. version 6 컬렉션은 저장 그릇을 제공하지만, 실제 Command·runtime·이벤트도 명시적 ID 흐름을 지원해야 한다.

출발 시 반드시 확인할 사항:

1. `departureCaravanId`로 대상 `CaravanSaveData`를 찾는다.
2. 찾은 저장 데이터와 출발용 runtime `CaravanData.caravanId`가 동일해야 한다.
3. 다른 `SaveData.selectedCaravanId` 대상에 출발 데이터를 기록하지 않는다.
4. `TradeProgressSaveData.caravanId`를 같은 ID로 기록한다.
5. Pending settlement도 같은 `caravanId`와 `fullTradeId`로 연결한다. 저장 필드명이 `tradeId`이면 해당 필드에 동일한 `fullTradeId` 값을 기록한다.
6. ID가 없거나 중복이면 저장 전에 실패한다.
7. 같은 Caravan의 중복 출발은 차단하지만 다른 Caravan의 출발·진행·정산은 허용한다.
8. 출발·진행·정산·Claim은 대상 `(caravanId, fullTradeId)`만 변경하고 다른 컬렉션 항목을 유지한다.
9. `selectedCaravanId` 전환을 정확성 보장의 수단으로 사용하지 않는다. 호환 접근자가 필요하면 호출 전후 선택값을 복구하더라도 실제 쓰기는 명시적 ID로 검증한다.
10. 진행·정산 관련 Framework 이벤트는 UI가 정확한 Caravan을 갱신할 수 있도록 `caravanId`와 `fullTradeId`를 제공한다.
11. Claim 완료 후 도착 도시는 해당 Caravan의 현재 위치로 저장되며, 전역 `SaveData.player.currentTownId`만으로 여러 Caravan의 위치를 표현하지 않는다.
12. 출발 Command는 실행 시점의 실제 Cargo 중량과 사용 슬롯이 현재 Caravan 구성의 용량을 넘지 않는지 재검증한다. 초과 시 출발을 차단하고 Cargo를 삭제·축소하지 않는다.

기존 `SaveData.caravan` 호환 property에만 기록하면 선택된 다른 Caravan을 덮어쓸 수 있으므로, 명시적 ID 기반 Command 또는 안전한 Framework 선택·복원 경계가 필요하다.

---

## 8. UI & Data 구현 범위

Provider API가 제공된 뒤 UI/Data 측에서 다음을 수행한다.

1. `TradePrepareUiRuntimeBinding`에 옵션 표시와 선택 이벤트 연결
2. Framework 조회 API를 통해 `departureCaravanId` 대상의 변경 불가능한 출발 스냅샷 조회
3. `TradePrepareViewDataBuilder`는 해당 스냅샷과 Core/Calculator 결과를 ViewData로 조립하며 이동 시간·비용·적재·위험 공식을 다시 구현하지 않음
4. `TradePrepareCaravanFactory.TryCreateDeparture`에서 조회된 스냅샷으로 출발 runtime 객체를 복원
5. `TradePrepareStartAdapter`에서 동일 ID를 Commit과 Gateway에 전달

---

## 9. 완료 조건

- 최대 4개 실제 Caravan이 stable ID로 표시된다.
- Overview 블록의 표시·설정·적재 대상이 출발 대상으로 자동 사용되지 않는다.
- Traveling/Settling Caravan을 선택할 수 없다.
- 선택 가능한 Caravan만 Draft의 `departureCaravanId`에 들어간다.
- 선택한 Option의 `currentTownId`가 같은 Draft에 들어가며 플레이어 위치로 대체되지 않는다.
- S_CaravanSlot 선택 전후 `SaveData.selectedCaravanId`는 유지된다.
- 잘못된 ID와 중복 ID가 Gateway 호출 전에 차단된다.
- 선택한 Caravan의 구성·Cargo·내구도가 Preview와 출발 객체에 사용된다.
- 출발·진행·정산 데이터가 동일한 `caravanId`에 기록된다.
- 서로 다른 두 Caravan이 동시에 출발·진행·정산 대기 상태를 유지할 수 있다.
- 한 Caravan의 출발·진행·Claim이 다른 Caravan의 구성·Cargo·진행·정산을 변경하지 않는다.
- Claim 후 Caravan별 도착 위치가 저장되고 재실행 후 복구된다.
- 다른 Caravan의 저장 데이터는 변하지 않는다.
- 저장 실패 시 해당 Caravan과 거래 준비 Commit이 이전 상태로 복구된다.

---

## 10. 외부 담당자 인계 요청

- UI Provider가 사용할 읽기 전용 조회 API, Caravan별 위치 정보와 배치 위치
- `canSelect` 판정 규칙과 안정적인 실패 코드
- Provider refresh가 필요한 Framework 이벤트
- `caravanId` 기반 출발 Command 또는 Gateway API
- 선택 Caravan의 설정·Cargo 스냅샷 조회 API
- 최대 4개 Caravan의 동시 Traveling/SettlementPending 지원 API와 이벤트 계약
- 서로 다른 두 Caravan의 동시 출발·진행·정산·재실행 복구 검증 결과
- Provider/Command 단위 테스트 또는 SmokeTest 실행 결과

다음 항목은 외부 인계 대상이 아니라 UI & Data가 직접 수행한다.

- RuntimeContext 직렬화 필드 및 Provider 주입
- CaravanSlotPanel 카드 생성과 선택 이벤트 연결
- TradePrepareUIManager의 실제 Provider 모드와 Demo 모드 분리
- `disabledReason` 및 실패 결과의 NoticeUI 표시
- Provider refresh 후 UI 재표시와 Next 버튼 상태 갱신
