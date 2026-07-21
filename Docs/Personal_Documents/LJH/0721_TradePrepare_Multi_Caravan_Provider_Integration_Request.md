# TradePrepare 멀티 Caravan Provider 연결 요청

작성일: 2026-07-21
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
    public JourneyState state;
    public bool canSelect;
    public string disabledReason;
}
```

`TradePrepareDraft.departureCaravanId`는 이번 출발 대상으로 선택한 Caravan ID만 의미한다.

- `CaravanBlockViewData.caravanId`: Overview의 Setting/Cargo 버튼이 해당 Caravan을 조회할 때 전달하는 ID이며 선택 상태가 아니다.
- `SaveData.selectedCaravanId`: 기존 단일 runtime 호환 접근자가 사용하는 Framework 선택 ID이다.
- `TradePrepareDraft.departureCaravanId`: TradePrepareUI 안에서 이번 출발 대상으로 선택한 ID이다.

Overview의 Caravan 정보 영역은 표시 전용이다. Overview의 표시나 편집 대상이 출발 Draft로 자동 복사되어서는 안 된다.

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

Caravan 런타임·저장 데이터 제공자는 `ITradePrepareCaravanOptionProvider` 실제 구현을 제공한다. 팀 구조상 UI 소유 인터페이스를 직접 구현하기 어렵다면, UI & Data가 Provider를 조립할 수 있는 동등한 읽기 전용 Caravan 조회 API를 제공한다.

Provider 규칙:

1. `SaveData.caravans`에서 최대 4개의 실제 Caravan을 조회한다.
2. UI가 생성하거나 배열 위치로 추론한 ID를 사용하지 않는다.
3. 모든 항목은 Framework가 저장한 안정적인 `caravanId`를 제공한다.
4. 배열과 항목은 `null`이 아니어야 한다.
5. 호출할 때마다 UI가 변경해도 원본이 오염되지 않는 새 스냅샷을 반환한다.
6. 같은 `caravanId`를 두 번 반환하지 않는다.
7. 출발 가능 판정과 차단 사유는 Provider가 결정한다.
8. `canSelect == true`일 때 `disabledReason`은 빈 문자열이어야 한다.
9. `canSelect == false`일 때 사용자에게 전달 가능한 차단 사유를 제공한다.

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

권장 wrapper:

```csharp
public bool SelectDepartureCaravan(string caravanId)
{
    return flowController != null
        && flowController.SelectDepartureCaravan(caravanId);
}
```

RuntimeContext는 Overview 블록의 `caravanId`를 출발 Draft에 자동으로 넣지 않는다. 사용자가 TradePrepareUI의 Caravan 선택 단계에서 선택했을 때만 `departureCaravanId`를 갱신한다.

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

## 7. Framework 요청: 출발 API 및 저장 경계

현재 멀티 Caravan Save cutover는 컬렉션 저장 구조와 호환 접근자를 제공하지만, 완전한 multi-active runtime은 범위 밖이다.

출발 시 반드시 확인할 사항:

1. `departureCaravanId`로 대상 `CaravanSaveData`를 찾는다.
2. 찾은 저장 데이터와 출발용 runtime `CaravanData.caravanId`가 동일해야 한다.
3. 다른 `SaveData.selectedCaravanId` 대상에 출발 데이터를 기록하지 않는다.
4. `TradeProgressSaveData.caravanId`를 같은 ID로 기록한다.
5. Pending settlement도 같은 `caravanId`와 `tradeId`로 연결한다.
6. ID가 없거나 중복이면 저장 전에 실패한다.

기존 `SaveData.caravan` 호환 property에만 기록하면 선택된 다른 Caravan을 덮어쓸 수 있으므로, 명시적 ID 기반 Command 또는 안전한 Framework 선택·복원 경계가 필요하다.

---

## 8. UI & Data 구현 범위

Provider API가 제공된 뒤 UI/Data 측에서 다음을 수행한다.

1. `TradePrepareUiRuntimeBinding`에 옵션 표시와 선택 이벤트 연결
2. `TradePrepareViewDataBuilder`에서 `departureCaravanId` 대상 저장 스냅샷 조회
3. 선택 Caravan의 마차·동물·Cargo·내구도 기반 Preview 계산
4. `TradePrepareCaravanFactory.TryCreateDeparture`에서 저장된 Caravan을 출발 runtime 객체로 복원
5. `TradePrepareStartAdapter`에서 동일 ID를 Commit과 Gateway에 전달

---

## 9. 완료 조건

- 최대 4개 실제 Caravan이 stable ID로 표시된다.
- Overview 블록의 표시·설정·적재 대상이 출발 대상으로 자동 사용되지 않는다.
- Traveling/Settling Caravan을 선택할 수 없다.
- 선택 가능한 Caravan만 Draft의 `departureCaravanId`에 들어간다.
- 잘못된 ID와 중복 ID가 Gateway 호출 전에 차단된다.
- 선택한 Caravan의 구성·Cargo·내구도가 Preview와 출발 객체에 사용된다.
- 출발·진행·정산 데이터가 동일한 `caravanId`에 기록된다.
- 다른 Caravan의 저장 데이터는 변하지 않는다.
- 저장 실패 시 해당 Caravan과 거래 준비 Commit이 이전 상태로 복구된다.

---

## 10. 외부 담당자 인계 요청

- 실제 Provider 클래스 또는 UI Provider가 사용할 읽기 전용 조회 API와 배치 위치
- `canSelect` 판정 규칙과 안정적인 실패 코드
- Provider refresh가 필요한 Framework 이벤트
- `caravanId` 기반 출발 Command 또는 Gateway API
- 선택 Caravan의 설정·Cargo 스냅샷 조회 API
- 동시 Traveling/SettlementPending 지원 범위
- Provider/Command 단위 테스트 또는 SmokeTest 실행 결과

다음 항목은 외부 인계 대상이 아니라 UI & Data가 직접 수행한다.

- RuntimeContext 직렬화 필드 및 Provider 주입
- CaravanSlotPanel 카드 생성과 선택 이벤트 연결
- TradePrepareUIManager의 실제 Provider 모드와 Demo 모드 분리
- `disabledReason` 및 실패 결과의 NoticeUI 표시
- Provider refresh 후 UI 재표시와 Next 버튼 상태 갱신
