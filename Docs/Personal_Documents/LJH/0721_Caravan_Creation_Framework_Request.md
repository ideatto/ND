# Caravan Creation Framework 요청서

작성일: 2026-07-21  
요청 부문: UI & Data → Framework & Integration

## 1. 요청 목적

Caravan Overview는 최대 4개의 고정 슬롯을 표시한다. `Empty` 슬롯에서 사용자가 전용 생성 버튼을 누르면 Framework가 새로운 Caravan을 원자적으로 생성·저장하고 안정적인 `caravanId`를 반환해야 한다.

UI는 `caravanId`를 생성하지 않으며 `SaveData`를 직접 변경하지 않는다. `slotIndex`는 화면과 저장 슬롯을 연결하는 위치 정보이며 Caravan의 영구 식별자로 사용하지 않는다.

## 2. UI & Data 구현 범위

- `CaravanSlotView`는 `CaravanSlotState.Empty`일 때만 `CreateButton`을 표시한다.
- Setting/Cargo/Journey 버튼은 Empty와 생성 처리 중 상태에서 숨겨지고 입력도 비활성화된다. 남은 CreateButton은 블록 전체 폭을 사용한다.
- 생성 버튼 클릭 시 `CreateRequested(slotIndex)`가 발생한다.
- `CaravanOverviewPresenter`는 같은 이벤트를 외부 연결 계층으로 전달한다.
- 생성 요청 직후 해당 슬롯은 UI 전용 생성 처리 중 상태가 되며 생성 버튼의 중복 입력을 차단한다.
- 생성 결과를 받은 UI Binding은 처리 중 상태를 해제하고 성공 시 Provider를 재조회한다.
- 성공 후 반환된 `caravanId`의 Setting 화면을 여는 작업은 UI & Data가 담당한다.
- 실패 시 Empty 상태를 유지하고 Framework 결과를 NoticeUI로 표시하는 작업은 UI & Data가 담당한다.
- UI는 생성 성공을 가정해 슬롯을 임의로 `Occupied`로 바꾸지 않는다.
- 잠긴 슬롯은 Empty 기본 화면과 비활성 CreateButton 위에 LockOverlay를 표시하고 `unlockHintText`를 NoticeUI로 전달한다.
- 실제 Framework Command 연결 전에는 `CreateRequested`를 처리할 소비자가 없다.

## 3. Framework에 요청하는 저장 구조

현재 브랜치의 `SaveData.caravan`은 단일 Caravan DTO이다. 최대 4개 Caravan과 Empty/Locked 슬롯을 지원하려면 Framework가 Caravan별 영속 상태를 제공해야 한다.

필수 데이터 예시는 다음과 같다. 실제 타입명은 Framework 규칙에 맞게 조정할 수 있다.

```csharp
public sealed class CaravanSaveData
{
    public string caravanId;
    public int slotIndex;
    public string currentTownId;
    public string destinationTownId;
    public string activeRouteId;
    public JourneyState state;

    // Existing wagon, animals, cargo, mercenaries, food and journey fields follow.
}
```

요구 규칙:

- 저장 가능한 슬롯 범위는 `0..3`이다.
- `caravanId`는 Caravan 생성 시 Framework가 한 번만 발급한다.
- `caravanId`는 `slotIndex`나 `tradeId`로부터 파생하지 않는다.
- 저장과 재실행 이후에도 같은 Caravan은 같은 ID를 유지한다.
- 슬롯 재배치 기능이 생겨도 Caravan의 ID는 변경되지 않는다.
- `CaravanSaveDataMapper`는 `caravanId`, 슬롯 및 위치·경로 상태를 누락 없이 왕복 복사해야 한다.

## 4. Framework 생성 Command 요청 계약

UI가 Framework에 전달하는 최소 입력은 `slotIndex`이다. 초기 위치가 UI 책임이 되지 않도록 `initialTownId`는 Framework가 현재 게임 상태에서 결정하는 방식을 우선 권장한다. Framework는 UI 오브젝트를 직접 갱신하거나 Setting 화면을 열지 않는다.

```csharp
public sealed class CreateCaravanRequest
{
    public int slotIndex;
}

public sealed class CreateCaravanResult
{
    public bool success;
    public string caravanId;
    public string errorCode;
    public SaveResult saveResult;
}
```

Command 이름 예시:

```csharp
CreateCaravanResult CreateCaravan(CreateCaravanRequest request);
```

`SaveResult`의 실제 타입과 표현 방식은 Framework의 기존 저장 계약을 따른다.

## 5. 생성 처리 규칙

Framework Command는 다음 순서로 처리해야 한다.

1. `slotIndex`가 지원 범위인지 검증한다.
2. 해당 슬롯이 해금되었고 아직 Empty인지 최신 저장 상태로 재검증한다.
3. 전체 Caravan 수가 최대 4개를 넘지 않는지 검증한다.
4. 중복 실행 또는 처리 중인 동일 요청을 차단한다.
5. Framework가 전역적으로 고유한 `caravanId`를 발급한다.
6. 현재 게임 기준 도시를 초기 `currentTownId`로 기록한다.
7. `JourneyState.Prepare`와 빈 구성으로 Caravan을 생성한다.
8. 저장 성공 후에만 성공 결과와 `caravanId`를 반환한다.
9. 저장 실패 시 생성 전 상태로 완전히 rollback한다.

초기 Caravan은 마차·동물·화물이 없는 상태여도 되지만, Provider와 Setting 화면이 이를 정상적인 미설정 상태로 표현할 수 있어야 한다.

## 6. 필수 검증 및 실패 코드

최소한 다음 실패 상황을 구분해 안정적인 `errorCode`로 반환해야 한다.

- 슬롯 인덱스 범위 오류
- 잠긴 슬롯
- 이미 Caravan이 존재하는 슬롯
- 최대 Caravan 수 초과
- 초기 도시 또는 런타임 상태 없음
- 중복 생성 요청
- ID 발급 실패
- 저장 실패

실패 시 다음 조건을 만족해야 한다.

- 새 Caravan이나 ID가 SaveData에 남지 않는다.
- 기존 Caravan 배열과 슬롯 상태가 변하지 않는다.
- UI가 NoticeUI로 표시할 수 있는 안정적인 실패 코드 또는 표시 문구를 제공한다.

## 7. UI & Data가 구현할 통합 흐름

```text
CreateButton 클릭
→ CaravanSlotView.CreateRequested(slotIndex)
→ CaravanOverviewPresenter.CreateRequested(slotIndex)
→ UI Binding이 해당 슬롯을 생성 처리 중으로 전환하고 중복 입력 차단
→ Framework CreateCaravan Command
→ 저장 성공
→ UI Binding이 생성 처리 중 상태 해제
→ UI Binding이 ICaravanOverviewViewDataProvider 재조회
→ Provider 결과에 따라 해당 슬롯 Occupied 표시
→ UI가 반환된 caravanId의 Caravan Setting 화면 열기
```

실패 시 UI Binding은 생성 처리 중 상태를 해제하고 Overview를 임의로 Occupied로 바꾸지 않는다. 기존 Empty 상태를 유지한 채 Framework가 반환한 실패 코드 또는 SaveResult를 NoticeUI로 표시한다.

## 8. 담당 구분

| 항목 | 담당 |
|---|---|
| Empty 전용 CreateButton 표시 | UI & Data, 완료 |
| `CreateRequested(slotIndex)` 이벤트 전달 | UI & Data, 완료 |
| 생성 처리 중 표시 및 Setting/Cargo/Journey 입력 차단 | UI & Data |
| 결과 수신 후 Provider Refresh 및 Setting 화면 열기 | UI & Data |
| `caravanId` 발급 | Framework & Integration 요청 |
| 최대 4개 Caravan 저장 구조 | Framework & Integration 요청 |
| 슬롯 잠금·중복·최대 개수 검증 | Framework & Integration 요청 |
| 생성 transaction, 저장 및 rollback | Framework & Integration 요청 |
| 실제 저장 데이터를 읽는 Overview Provider | 기능 제작자/Framework 협의 필요 |
| Command 호출 Binding과 성공·실패 UI 처리 | Framework Command 제공 후 UI & Data 연결 |

## 9. 완료 조건

- Empty 슬롯 클릭 한 번으로 Caravan이 정확히 하나 생성된다.
- 생성 처리 중에는 Create/Setting/Cargo/Journey 입력을 다시 받을 수 없다.
- 생성된 Caravan은 비어 있지 않은 고유 `caravanId`를 가진다.
- 생성 성공 후 동일 슬롯이 Occupied로 조회된다.
- 재실행 후에도 ID, 슬롯 및 초기 위치가 복구된다.
- Locked 또는 Occupied 슬롯에는 생성 Command가 적용되지 않는다.
- 저장 실패와 중복 요청에서는 유령 Caravan이나 중복 ID가 남지 않는다.
- Framework는 생성 결과와 `caravanId`만 반환하며 UI 오브젝트를 직접 조작하지 않는다.
- UI는 생성 성공 후 Setting 화면을 반환된 `caravanId` 대상으로 연다.
