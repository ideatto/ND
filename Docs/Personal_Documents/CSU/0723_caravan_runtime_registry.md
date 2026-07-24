# Caravan Runtime Registry 구현 로직

- 작성일: 2026-07-23
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/caravan-runtime-registry`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현·Editor E2E·재검증 완료(커밋 전)
- 선행 작업:
  - `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md` — SaveData v6 `caravans[]` / `tradeProgressEntries[]`
  - `Docs/Personal_Documents/CSU/0722_caravan_id_copytosave_sync.md` — `CopyToSave` 시 `caravanId` 보존
  - `Docs/Personal_Documents/CSU/0722_multi_caravan_trade_start_command.md` — caravan ID 기반 출발 command
- 관련 문서:
  - `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`
  - `Docs/Personal_Documents/CSU/0712_m3-offline-progress-pipeline.md`

---

## 1. 목적

Multi-caravan Save Cutover 이후에도 `TradeProgressCoordinator`는 **단일 `activeCaravan` 필드** 하나만 runtime을 보유했다. 이 구조에서는 다음 P0 문제가 발생할 수 있었다.

| 조건 | 기존 동작 | 문제 |
|---|---|---|
| `selectedCaravanId = A`, A가 Traveling | Tick/Offline/ForceComplete 실행 | 정상 |
| B runtime이 registry에 존재 (`GetOrCreateRuntimeCaravan(B)` 등) | `activeCaravan`이 B를 가리킬 수 있음 | Tick이 B runtime에 progress 적용 |
| runtime → save 반영 | `CopyToSave(caravan, saveData.caravan)` | **선택 caravan(A) save DTO**에 B runtime 값이 기록됨 |

즉 **Active≠selected** 상태에서 progress 계산 대상과 save target이 서로 다른 ID를 참조하는 **교차 갱신(cross-update)** 이 가능했다.

이번 브랜치는 단일 active 참조를 **caravanId 기반 Runtime Registry**로 교체하고, progress 연산·save 반영 모두 **`progress.caravanId` / `caravan.caravanId`** 로 동일 ID를 강제한다.

---

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `TradeProgressCoordinator.cs` | 단일 `activeCaravan` → `Dictionary<string, CaravanData>` registry. progress lookup·owned save copy 헬퍼 추가 |
| `TradeStartService.cs` | 출발 시 매번 `ToRuntime` 새 객체 생성 대신 registry canonical runtime 사용 |
| `FrameworkRoot.cs` | Load/Rebuild registry, 출발·생성 경로에 `GetOrCreateRuntimeCaravan` 연결 |
| `FrameworkM1LoopE2EEditorTests.cs` | `RunRuntimeCaravanRegistryChecks` 및 TestContext production wiring 정합 |

Scene / Prefab / Meta / Package / SaveData schema 변경 없음.

---

## 3. 핵심 구조

### 3.1 Before → After

```text
Before
  TradeProgressCoordinator
    └─ CaravanData activeCaravan          (전역 1개)
    └─ EnsureActiveCaravan()              (없으면 saveData.caravan에서 복원)
    └─ CopyToSave(..., saveData.caravan)  (항상 selected save DTO)

After
  TradeProgressCoordinator
    └─ Dictionary<string, CaravanData> runtimeCaravans
    └─ TryGetRuntimeCaravan / GetOrCreateRuntimeCaravan / RegisterRuntimeCaravan
    └─ RebuildRuntimeCaravans()           (Load 시 전체 재구성)
    └─ GetRuntimeForProgress(saveData)    (progress.caravanId lookup)
    └─ CopyRuntimeToOwnedSave(...)        (caravan.caravanId owned save lookup)
    └─ ActiveCaravan                      (selected facade, 진행 계산에는 미사용)
```

### 3.2 책임 분리

```text
SaveData v6
  └─ caravans[].caravanId                 (영구 식별자)
  └─ selectedCaravanId                    (UI·legacy facade 포인터)
  └─ tradeProgressEntries[].caravanId     (caravan별 progress)
  └─ saveData.tradeProgress (facade)      → selectedCaravanId progress entry

Runtime Registry (TradeProgressCoordinator)
  └─ runtimeCaravans[id]                  (세션 동안 canonical CaravanData 참조)

진행 계산 (Tick / Offline / ForceComplete / Legacy claim restore)
  └─ saveData.tradeProgress.caravanId     → GetRuntimeForProgress
  └─ runtime 변경                        → CopyRuntimeToOwnedSave (동일 id save DTO)

UI·legacy 호환
  └─ ActiveCaravan                        → selectedCaravanId registry entry
  └─ SetActiveCaravan(caravan)            → registry[id] 교체 등록 (save에 존재하는 ID만)
```

---

## 4. Registry API

### 4.1 `TryGetRuntimeCaravan(caravanId, out caravan)`

- registry에 등록된 **공유 참조**를 반환한다.
- ID가 비어 있거나 미등록이면 `false`.

### 4.2 `GetOrCreateRuntimeCaravan(caravanId)`

1. registry hit → 기존 참조 반환
2. miss → `SaveDataLookup.TryGetCaravan`으로 save snapshot 조회
3. `CaravanSaveDataMapper.ToRuntime` 후 `RegisterRuntimeCaravan`
4. save에 caravan이 없거나 register 실패 → `null`

출발·claim·생성 command가 **동일 ID에 대해 항상 같은 runtime 객체**를 재사용하도록 하는 진입점이다.

### 4.3 `RegisterRuntimeCaravan(caravanId, caravan)`

등록 조건 (하나라도 실패하면 `false`):

- `caravanId`, `caravan` non-null
- `caravanId == caravan.caravanId` (ordinal)
- saveData에 해당 caravan save 존재

이미 동일 ID가 있으면 **ReferenceEquals**일 때만 성공. 다른 객체로 교체하려면 `SetActiveCaravan`을 사용한다.

### 4.4 `SetActiveCaravan(caravan)`

- 기존 호출자 호환용 **명시적 registry 교체**.
- save에 존재하지 않는 ID는 무시한다.
- 진행 계산 경로(Tick 등)는 이 메서드를 사용하지 않는다.

### 4.5 `RebuildRuntimeCaravans()`

- registry를 `Clear()`한 뒤 `saveData.caravans[]` 전체를 순회해 `ToRuntime` + `RegisterRuntimeCaravan`.
- `FrameworkRoot.InitializeServices`에서 Load 직후 1회 호출.
- selected 변경만으로는 rebuild하지 않는다.

---

## 5. Progress lookup · Save target

### 5.1 `GetRuntimeForProgress(SaveData saveData)`

```csharp
var caravanId = saveData?.tradeProgress?.caravanId;
return GetOrCreateRuntimeCaravan(caravanId);
```

- Tick / Offline / ForceComplete / Legacy claim / Pending restore가 **runtime 대상 결정**에 사용한다.
- `ActiveCaravan` 또는 `selectedCaravanId`를 직접 사용하지 않는다.
- `saveData.tradeProgress` facade는 selected caravan progress를 가리키므로, Offline·Tick은 **selected caravan이 Traveling일 때만** 진입한다(기존 단일 selected 경로 유지).

### 5.2 `CopyRuntimeToOwnedSave(SaveData saveData, CaravanData caravan)`

```csharp
SaveDataLookup.TryGetCaravan(saveData, caravan.caravanId, out var caravanSave);
CaravanSaveDataMapper.CopyToSave(caravan, caravanSave);
```

- 기존 `CopyToSave(caravan, saveData.caravan)` 을 전면 교체.
- runtime이 가리키는 **자기 owned save DTO**에만 기록한다.
- lookup 실패 시 warning 후 `false` — fallback으로 selected save를 쓰지 않는다.

적용 위치:

- `CheckProgressAndCompletion`
- `ApplyOfflineProgressOnLoad`
- `ClaimSettlementAndResetLegacy`
- `ForceCompleteActiveTrade`
- `SettleActiveTrade`

### 5.3 `EnsureActiveCaravan()` (ActiveCaravan getter)

- `TryGetRuntimeCaravan(selectedCaravanId)` 우선.
- 없으면 `GetOrCreateRuntimeCaravan(selectedCaravanId)`.
- **UI·legacy facade 전용**. progress 계산 주석에도 "진행 계산은 progress caravan ID"라고 명시.

---

## 6. 연동 지점

### 6.1 `FrameworkRoot.InitializeServices`

```text
TradeStartService 생성
  └─ setActiveCaravan: TradeProgressCoordinator.SetActiveCaravan
  └─ getRuntimeCaravan: TradeProgressCoordinator.GetOrCreateRuntimeCaravan   (신규)

CurrentSaveData = Load() | CreateNewGameData()
TradeProgressCoordinator.RebuildRuntimeCaravans()                               (신규)

CaravanManagementService
  └─ registerRuntimeCaravan: GetOrCreateRuntimeCaravan                        (신규)
```

### 6.2 `TradeStartService.DepartInternal`

```text
기존: runtimeCaravan = CaravanSaveDataMapper.ToRuntime(caravanSave)   // 매번 새 객체
변경: runtimeCaravan = getRuntimeCaravan(caravanId)                   // registry canonical
      null 또는 caravanId 불일치 → CaravanNotFound
```

출발 성공 후 `setActiveCaravan(runtimeCaravan)`으로 동일 참조를 registry에 유지한다.

### 6.3 `CaravanManagementService.CreateCaravan`

- save 성공 후 `registerRuntimeCaravan(caravan.caravanId)` 호출.
- registry 등록 실패 시 snapshot rollback + `SaveFailed` 반환.
- reload 없이 새 caravan runtime이 세션에 즉시 등록된다.

---

## 7. 경로별 동작 요약

| 경로 | Runtime lookup | Save target | selected 변경 영향 |
|---|---|---|---|
| Online Tick (`CheckProgressAndCompletion`) | `progress.caravanId` | `caravan.caravanId` owned | 없음 |
| Offline (`ApplyOfflineProgressOnLoad`) | `progress.caravanId` | owned | 없음 (selected progress만 처리) |
| ForceComplete | `progress.caravanId` | owned | 없음 |
| SettleActiveTrade | 인자 caravan | owned | 없음 |
| Atomic Claim (`ClaimSettlement(id, tradeId)`) | `GetOrCreateRuntimeCaravan(caravanId)` | `caravanSave` by id | claim 중 selected 임시 변경 후 복구(기존) |
| Map progress (pause) | `GetRuntimeForProgress` | 없음 (read) | 없음 |
| ActiveCaravan getter | `selectedCaravanId` | 없음 | facade만 전환 |

---

## 8. P0 교차 갱신 차단 메커니즘

재현 시나리오:

```text
A: selectedCaravanId = A, Traveling, progress.caravanId = A
B: registry 존재, Prepare, progress01 = 0.42
SetActiveCaravan(B)  // 구조상 B를 registry에 올릴 수 있음
```

기대·검증 결과:

| 항목 | 결과 |
|---|---|
| Tick runtime 대상 | A (`GetRuntimeForProgress` → A 참조) |
| A runtime progress | 변경 (예: 0.10 → 0.30) |
| A save progress | A runtime과 일치 |
| B runtime / save | 불변 |
| `selectedCaravanId` | A 유지 |
| missing ID lookup | null (B fallback 없음) |

핵심: Tick 결정에 `ActiveCaravan` 필드를 쓰지 않고, save 반영에 `saveData.caravan`(selected facade)을 쓰지 않는다.

---

## 9. Registry 수명

```text
Load / CreateNewGameData
  → RebuildRuntimeCaravans()        // caravans[] 전체 등록

selected A → B → A 변경
  → Rebuild 호출 없음
  → ActiveCaravan facade만 selected entry 반환
  → A/B CaravanData 참조 동일 유지

CreateCaravan 성공
  → GetOrCreateRuntimeCaravan       // registry 추가

CreateCaravan save/registry 실패
  → snapshot rollback, registry 미등록
```

---

## 10. Editor E2E — `RunRuntimeCaravanRegistryChecks`

`ND/Framework/Run M1 Loop + Economy E2E Checks`에 포함.

검증 항목:

1. `RebuildRuntimeCaravans` 후 selected·second caravan ID별 registry 존재, `ActiveCaravan == selected`
2. selected A→B→A 변경 시 **동일 runtime 참조** 유지 (재생성 없음)
3. **Non-selected departure** (B 출발, selected=A): A runtime/state 불변, B만 Traveling
4. `CreateCaravan` 성공 시 reload 없이 registry 등록
5. `CreateCaravan` save 실패 시 save·registry 불변

TestContext production wiring도 정합:

- `StageTestCommit(..., caravan.caravanId)` — non-selected 출발 commit staging
- `TradeStartService`에 `GetOrCreateRuntimeCaravan` 전달

---

## 11. 재검증 요약 (2026-07-23)

| 테스트 | 결과 |
|---|---|
| Active≠selected Online Tick | PASS |
| Offline ID 일치 | PASS |
| Force Complete / Settlement ID 일치 | PASS |
| selected 변경 Runtime 보존 | PASS |
| M1 Loop + Economy E2E | PASS |
| Unity Editor 컴파일 | PASS (Console Error 0) |
| `Assembly-CSharp.csproj` build | 오류 0 |
| `git diff --check` | PASS (CRLF 경고만) |

분리 기록: `ND.sln` / `Assembly-CSharp-Editor.csproj`의 기존 NUnit 참조 오류는 본 변경과 무관.

---

## 12. 의도적으로 범위 밖인 것

- **Offline 전체 entry 순회**: 이번 PR에서는 제외. selected caravan Traveling에 대해서만 기존 Offline 경로 유지.
- **`ActiveCaravan` API 제거**: UI·legacy 호환 facade로 유지. 진행 계산에서만 배제.
- **Core `CaravanRuntimeList` 변경**: Framework registry가 canonical runtime 소유. Core 측은 기존 contract 유지.
- **SaveData schema / version 변경**: 없음.

---

## 13. 후속 PR 시 주의

1. progress 연산을 추가할 때 `EnsureActiveCaravan()` / `saveData.caravan` 직접 사용 금지 → `GetRuntimeForProgress` + `CopyRuntimeToOwnedSave`.
2. non-selected caravan Traveling 동시 처리(Offline 전체 순회 등)는 **별도 설계** 필요. `tradeProgress` facade가 selected 전용임을 전제로 한다.
3. UI가 `ActiveCaravan`만 구독하는 경우 selected 변경과 progress caravan 불일치 가능 — 화면별로 `caravanId` 명시 조회(`TryGetRuntimeCaravan`) 검토.

---

## 14. 참조 코드 위치

| 심볼 | 파일 |
|---|---|
| `runtimeCaravans`, Registry API | `TradeProgressCoordinator.cs` |
| `GetRuntimeForProgress`, `CopyRuntimeToOwnedSave` | `TradeProgressCoordinator.cs` |
| `RebuildRuntimeCaravans` 호출 | `FrameworkRoot.cs` — `InitializeServices` |
| `getRuntimeCaravan` | `TradeStartService.cs` — `DepartInternal` |
| `registerRuntimeCaravan` | `FrameworkRoot.cs` — `CaravanManagementService` |
| `RunRuntimeCaravanRegistryChecks` | `FrameworkM1LoopE2EEditorTests.cs` |
