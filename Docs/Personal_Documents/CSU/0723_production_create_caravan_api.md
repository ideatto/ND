# Production Create Caravan API 구현 로직

- 작성일: 2026-07-23
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/production-create-caravan-api`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현 완료(커밋 전). Editor E2E(`RunProductionCaravanCreationChecks`, `RunCaravanSlotNormalizationChecks`) 포함
- 선행 작업:
  - `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md` — `caravans[]` / `tradeProgressEntries[]` ID 기반 컬렉션
  - `Docs/Personal_Documents/CSU/0723_caravanid_normalization_persistence_complement.md` — caravanId·선택 포인터 정규화 및 Load() 영속
- 관련 문서:
  - `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`
  - `Docs/Personal_Documents/CSU/0722_multi_caravan_trade_start_command.md`

---

## 1. 목적

Multi-caravan Save Cutover 이후 저장 모델은 `caravans[]` 컬렉션으로 확장되었지만, **플레이어가 새 Caravan을 추가하는 Production command**는 없었다. UI(Caravan Overview)는 고정 슬롯 개념을 전제로 하므로, 배열 인덱스가 아닌 **영속 `slotIndex`** 와 **생성 command**가 필요하다.

이번 작업은 다음을 만족한다.

- 지정한 `slotIndex`에 새 Caravan과 기본 `TradeProgressSaveData`를 추가한다
- 생성 ID는 `SaveDataLookup.NewCaravanId()`(32자 `N` 형식 GUID)를 사용한다
- 검증 실패는 SaveData를 변경하지 않는다
- 저장 실패는 snapshot rollback으로 전체 SaveData를 원복한다
- 저장 성공 후 `FrameworkEvents.CaravanCreated`를 1회 발행한다
- 구/손상 세이브의 `slotIndex` 중복·음수를 `NormalizeData`에서 repair하고 `Load()`로 디스크에 영속한다

이번 범위에 **포함하지 않는 것**:

- Caravan Overview UI에서 `CreateCaravan` 호출 연결 (후속 PR)
- 새 Caravan의 wagon/animal/mercenary 초기 구성 (빈 DTO만 생성)
- `slotIndex` 변경·삭제 API
- SaveData schema version 변경 (v6 유지)
- 생성 시 runtime `CaravanData` 동기화 또는 active caravan 전환

---

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `SaveData.cs` | `CaravanSaveData.slotIndex` 필드 추가 |
| `JsonSaveService.cs` | `NormalizeData`에 slotIndex 중복·음수 repair 및 `assetDataChanged` 반영 |
| `FrameworkRoot.cs` | `CaravanCreationFailureReason`, `CaravanCreationResult`, `CaravanManagementService`, `FrameworkRoot.CaravanManagement` |
| `FrameworkEvents.cs` | `CaravanCreated` 이벤트 및 `RaiseCaravanCreated` |
| `FrameworkM1LoopE2EEditorTests.cs` | Production 생성 계약 E2E, slot 정규화·Load 영속 E2E |

Scene / Prefab / Meta / Package 변경 없음.

---

## 3. 책임 분리

```text
CaravanSaveData.slotIndex
  └─ Caravan Overview 고정 슬롯과 1:1 매핑되는 영속 위치 (caravans[] 배열 인덱스와 무관)

CaravanManagementService.CreateCaravan(slotIndex)
  └─ 슬롯 검증 → SaveData snapshot → caravan + progress 추가 → (선택 repair) → Save → (실패 rollback / 성공 event)

JsonSaveService.NormalizeData
  └─ slotIndex 음수·중복 repair (리스트 순서 유지, caravanId·child entry는 건드리지 않음)
  └─ 변경 시 Load()가 디스크에 1회 저장

FrameworkRoot.CaravanManagement
  └─ runtime 접근: FrameworkRoot.Instance.CaravanManagement.CreateCaravan(...)

FrameworkEvents.CaravanCreated(caravanId, slotIndex)
  └─ 저장 성공 직후 1회. 저장 실패·검증 거부 시 발생하지 않음
```

---

## 4. 공개 API

### 4.1 Command

```csharp
// FrameworkRoot.Instance.CaravanManagement
public CaravanCreationResult CreateCaravan(int slotIndex);
```

### 4.2 결과

```csharp
public sealed class CaravanCreationResult
{
    public bool Succeeded { get; }
    public string CaravanId { get; }           // 성공 시 32자 GUID(N), 실패 시 ""
    public int SlotIndex { get; }                // 요청한 slotIndex
    public CaravanCreationFailureReason FailureReason { get; }
    public SaveResult SaveResult { get; }        // 저장 시도 시에만
}
```

### 4.3 실패 사유 (`CaravanCreationFailureReason`)

| 값 | 발생 조건 | SaveData 변경 |
|---|---|---|
| `SaveDataUnavailable` | `getSaveData()`가 null 반환 | 없음 |
| `InvalidSlotIndex` | `slotIndex < 0` | 없음 |
| `SlotAlreadyOccupied` | 기존 caravan 중 동일 `slotIndex` 존재 | 없음 |
| `SaveFailed` | `ISaveService.Save` 실패 또는 예외 | snapshot rollback |
| `None` | 생성·저장 모두 성공 | 확정 반영 |

**호출자 판정 가이드**

- `Succeeded == false` && `FailureReason != SaveFailed` → 검증 거부. 저장 시도 없음
- `Succeeded == false` && `FailureReason == SaveFailed` → 메모리는 snapshot 이전 상태로 복원됨
- `Succeeded == true` → `SaveResult.Succeeded == true`이며 `CaravanCreated` 이벤트가 이미 발행됨

### 4.4 이벤트

```csharp
public static event Action<string, int> CaravanCreated;
// 인자: caravanId, slotIndex
// 발생: Save 성공 직후 1회
// 미발생: 검증 거부, Save 실패(rollback 후)
```

---

## 5. `CreateCaravan` 처리 흐름

### 5.1 개요

```text
CreateCaravan(slotIndex)
  ├─ [1] saveData = getSaveData()
  │     └─ null → SaveDataUnavailable
  ├─ [2] slotIndex < 0 → InvalidSlotIndex
  ├─ [3] caravans[] 순회: existing.slotIndex == slotIndex → SlotAlreadyOccupied
  ├─ [4] snapshot = JsonUtility.ToJson(saveData)
  ├─ [5] selectedWasValid = SaveDataLookup.TryGetSelectedCaravan(saveData, out _)
  ├─ [6] caravans / tradeProgressEntries null 보정
  ├─ [7] 새 CaravanSaveData
  │     ├─ caravanId = SaveDataLookup.NewCaravanId()
  │     └─ slotIndex = 요청값
  ├─ [8] caravans.Add(caravan)
  ├─ [9] tradeProgressEntries.Add({ caravanId, state = None })
  ├─ [10] selectedWasValid == false → selectedCaravanId = caravan.caravanId
  ├─ [11] saveService.Save(saveData)
  │     ├─ 실패/예외 → FromJsonOverwrite(snapshot) → SaveFailed
  │     └─ 성공 → RaiseCaravanCreated → Success
  └─ (반환) CaravanCreationResult
```

### 5.2 성공 시 SaveData 불변식

1. **caravan 추가** — `caravans`에 새 항목 1개. 기존 항목은 변경하지 않는다.
2. **progress 1:1** — 동일 `caravanId`로 `tradeProgressEntries`에 `state == None` 항목 1개.
3. **pending 미생성** — `pendingSettlements`는 건드리지 않는다.
4. **선택 보존** — `TryGetSelectedCaravan`이 이미 성공이면 `selectedCaravanId`를 바꾸지 않는다.
5. **선택 repair** — 선택 ID가 비어 있거나 존재하지 않는 caravan을 가리키면, 새로 만든 caravan ID로 설정한다.
6. **legacy 접근** — `saveData.caravan`(단수) 레거시 필드는 직접 갱신하지 않는다. 직렬화 후 `NormalizeData`가 선택 caravan 기준으로 legacy mirror를 유지한다.
7. **저장 1회** — 성공 경로에서 `Save()`는 정확히 1회 호출된다.

### 5.3 Snapshot / Rollback

저장 실패 또는 `Save()` 예외 시:

```csharp
JsonUtility.FromJsonOverwrite(snapshot, saveData);
```

- **전체 SaveData**를 snapshot 시점으로 되돌린다 (caravan 추가, progress 추가, selectedCaravanId repair 모두 취소).
- `CaravanCreated` 이벤트는 발행하지 않는다.

검증 거부(`InvalidSlotIndex`, `SlotAlreadyOccupied`, `SaveDataUnavailable`)는 snapshot을 캡처하기 **전에** 반환하므로 SaveData·저장 호출 모두 없음.

### 5.4 ID 형식

- `SaveDataLookup.NewCaravanId()` → `Guid.NewGuid().ToString("N")` (32자, 하이픈 없음)
- E2E는 `Guid.TryParseExact(..., "N", ...)`로 검증한다.

---

## 6. `slotIndex` 저장 모델

### 6.1 필드 의미

```csharp
// CaravanSaveData
public int slotIndex;
```

- Caravan Overview UI의 **고정 슬롯 번호**와 연결되는 영속 값이다.
- `caravans[]`의 **배열 인덱스로 대체하지 않는다**.
- sparse 슬롯(예: `0`, `3`)은 유효하다. 연속성을 강제하지 않는다.

### 6.2 기본값·마이그레이션

| 상황 | 동작 |
|---|---|
| 새 `SaveData()` 생성 | 기본 caravan 1개, `slotIndex == 0` (int 기본값) |
| v6 이전 세이브 (필드 없음) | JsonUtility 역직렬화 시 `slotIndex == 0`으로 채워짐 → 다 caravan이면 NormalizeData가 `0,1,2,...`로 repair |
| 음수 slotIndex | NormalizeData가 사용되지 않은 최소 non-negative 슬롯으로 교체 |
| 중복 slotIndex | 리스트 순서대로 첫 항목 유지, 이후 항목만 교체 슬롯 할당 |

### 6.3 `NormalizeData` slot repair 상세

```text
usedCaravanSlots = HashSet<int>

for each caravan in data.caravans (순서 유지):
  if slotIndex < 0 OR slotIndex already in usedCaravanSlots:
    replacement = 0부터 usedCaravanSlots에 없는 최솟값
    caravan.slotIndex = replacement
    usedCaravanSlots.Add(replacement)
    assetDataChanged = true
    Warning 로그
  else:
    usedCaravanSlots.Add(slotIndex)
```

**의도적으로 하지 않는 것**

- `caravans[]` 리스트 재정렬 (caravanId·tradeProgress·pending은 ID 기준이므로 순서 변경 불필요)
- `tradeProgressEntries` / `pendingSettlements`의 `caravanId` relink (slot과 무관)
- sparse 슬롯을 contiguous하게 압축 (예: `[0,3]` → `[0,1]` 변환 없음)

**repair 예시**

| 입력 slotIndex (리스트 순) | 결과 slotIndex |
|---|---|
| `0, 0, 2` | `0, 1, 2` |
| `-1, 1, 1` | `0, 1, 2` |
| `0, 1, 2` | 변경 없음 |
| `0, 3` | 변경 없음 (sparse 허용) |

### 6.4 Load() 영속

선행 caravanId complement와 동일하게, `NormalizeData`가 `true`를 반환하면 `JsonSaveService.Load()`가 디스크에 1회 저장한다.

E2E `RunCaravanSlotLoadPersistenceCheck`:

1. temp save 파일에 `slotIndex` 필드를 제거한 JSON 기록 (구 세이브 시뮬레이션)
2. 첫 `Load()` → slot backfill → Save 1회
3. 두 번째 `Load()` → 추가 Save 없음, JSON 동일 (idempotent)

---

## 7. FrameworkRoot 연동

```csharp
// InitializeServices()
CurrentSaveData = SaveService.HasSaveData() ? SaveService.Load() : SaveService.CreateNewGameData();
CaravanManagement = new CaravanManagementService(() => CurrentSaveData, SaveService);
```

- `CaravanManagementService`는 `CurrentSaveData` 참조를 공유한다.
- UI/gameplay는 `FrameworkRoot.Instance.CaravanManagement.CreateCaravan(slotIndex)`로 호출한다.
- `StartNewGame()` / `ContinueGame()` 이후에도 동일 `CurrentSaveData`를 사용한다.

---

## 8. Editor E2E 검증 범위

### 8.1 `RunProductionCaravanCreationChecks`

| 케이스 | 기대 |
|---|---|
| `CreateCaravan(-1)` | `InvalidSlotIndex`, Save 0회, event 0회 |
| `CreateCaravan(0)` (기본 caravan 점유) | `SlotAlreadyOccupied` |
| `CreateCaravan(1)` | 성공, GUID 32자, progress None, selected 유지, Save 1회, event 1회 |
| JSON 직렬화·복원 | slotIndex·caravanId·legacy `caravan` mirror 유지 |
| `CreateCaravan(2)` 연속 | 고유 ID, selected 유지, Save 2회, event 2회 |
| Save 실패 시뮬레이션 | `SaveFailed`, SaveData JSON 동일, event 증가 없음 |
| `selectedCaravanId` 공백 | 생성 성공 시 새 caravan ID로 repair |

### 8.2 `RunCaravanSlotNormalizationChecks`

| 케이스 | 기대 |
|---|---|
| duplicate / negative+duplicate / contiguous / sparse | slot repair 결과·Warning 횟수 |
| progress·pending·selected·legacy caravan | Normalize 후 caravanId 기반 데이터 불변 |
| 손상 slot repair 후 `CreateCaravan` | normalized slot 기준 occupancy |
| Load persistence (2·4 caravan) | slot backfill 디스크 영속·재로드 idempotent |

---

## 9. 후속 연동 시 주의

1. **UI는 `slotIndex`로 생성 요청** — 배열 인덱스나 화면 순서를 slot으로 쓰지 않는다.
2. **생성 후 runtime 동기화** — 이번 API는 Save DTO만 갱신한다. Caravan Overview가 runtime caravan 목록을 쓰면 별도 refresh가 필요할 수 있다.
3. **선택 caravan** — 새 caravan 생성만으로 active/selected가 바뀌지 않는다(선택이 이미 유효한 경우).
4. **이벤트 구독** — `OnDisable`/`OnDestroy`에서 `CaravanCreated` 구독 해제. 중복 UI 갱신 방지.
5. **NormalizeData와 CreateCaravan 순서** — Load 직후 NormalizeData가 slot을 repair할 수 있으므로, UI occupancy 표시는 normalized slot 기준으로 맞춘다.

---

## 10. 관련 PR·Issue

- 관련 Issue: 없음
- 선행 PR: multi-caravan save cutover, caravanId normalization persistence complement
- 후속 PR: Caravan Overview UI → `CreateCaravan` 연결 (예상)
