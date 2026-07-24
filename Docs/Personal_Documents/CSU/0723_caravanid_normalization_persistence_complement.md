# CaravanId Normalization Persistence Complement 구현 로직

- 작성일: 2026-07-23
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/caravanid-normalization-persistence-complement`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현 완료(커밋 전)
- 선행 작업:
  - `feature/framework/asset-instance-id-persistence` — Save DTO·mapper `instanceId` / `caravanId` Save ↔ Runtime 왕복
  - `feature/framework/asset-instance-id-normalization` — `NewInstanceId()`, 자산 ID backfill·중복 repair, `Load()` 자동 저장
  - `fix/framework/runtime-data-caravanid-to-savedata-mapping` — `CopyToSave` caravanId 보존, 출발 전 runtime 동기화
- 관련 문서:
  - `Docs/Personal_Documents/CSU/0722_asset_instance_id_persistence.md`
  - `Docs/Personal_Documents/CSU/0722_asset_instance_id_normalization.md`
  - `Docs/Personal_Documents/CSU/0722_caravan_id_copytosave_sync.md`
  - `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`

---

## 1. 목적

선행 `asset-instance-id-normalization` 작업에서 `JsonSaveService.Load()`는 `NormalizeData`가 `true`를 반환하면 디스크에 1회 저장하도록 연결되었다. 그러나 **caravan ID·선택 포인터 정규화**는 `assetDataChanged`에 반영되지 않아, 다음 문제가 남아 있었다.

1. **빈/공백 `caravanId` backfill 후 미영속** — 메모리상으로는 ID가 발급되지만 `Load()`가 디스크에 다시 쓰지 않아, 다음 세션에서 같은 backfill을 반복한다.
2. **중복 `caravanId` repair 후 미영속** — 나중 caravan에 새 ID를 부여해도 변경 감지가 없어 save 파일에 반영되지 않는다.
3. **`selectedCaravanId` 복구 후 미영속** — 존재하지 않는 선택 ID를 첫 caravan ID로 되돌려도 디스크에 저장되지 않는다.
4. **caravan 컨테이너 생성 미감지** — `caravans == null` 또는 빈 리스트 보정도 변경으로 간주되지 않았다.

이번 브랜치는 **caravan ID 정규화 결과를 `NormalizeData`의 bool 반환값에 포함**하고, **Editor E2E로 로드→디스크 영속→재로드 안정성**을 검증한다.

---

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `JsonSaveService.cs` | caravan 컨테이너·ID·선택 포인터 정규화 시 `assetDataChanged = true` 반영. whitespace caravanId 처리·중복 repair 로직 보강 |
| `FrameworkM1LoopE2EEditorTests.cs` | `RunCaravanIdNormalizationPersistenceChecks` 추가. temp save path로 `Load()` 디스크 영속 E2E |

이번 범위에 **포함하지 않는 것**:

- SaveData schema version 변경 (v6 유지)
- child entry(`tradeProgressEntries`, `pendingSettlements`) relink
- `SaveDataLookup.TryGetCaravan`의 whitespace 처리 변경
- gameplay/UI 쪽 caravan ID 발급 경로 연결

---

## 3. 선행 작업과의 관계

| 주제 | 선행 (`asset-instance-id-normalization`) | 이번 complement |
|---|---|---|
| `NewInstanceId()` / 자산 ID backfill | 구현 | 변경 없음 |
| `Load()` → `NormalizeData` → 변경 시 `Save()` | 자산 ID 변경만 감지 | **caravan ID·선택·컨테이너 변경도 감지** |
| caravanId 빈 값 발급 | `NormalizeData` 내부 존재 | **변경 감지 + whitespace 처리 + E2E** |
| duplicate caravanId repair | 존재하나 변경 미반영 | **`assetDataChanged` 반영 + Warning 문구 정리** |
| `CopyToSave` caravanId | 선행 PR | 변경 없음 |

```text
[선행] instanceId backfill/repair  → NormalizeData true → Load() Save
[이번] caravanId/selectedCaravanId → NormalizeData true → Load() Save  ← 보완
```

---

## 4. `NormalizeData` 변경 상세

### 4.1 반환값 의미 확장

```csharp
/// <returns>저장 컨테이너, Caravan 선택/ID 또는 자산 ID가 변경되었으면 true.</returns>
public static bool NormalizeData(SaveData data)
```

`true`가 되는 조건(신규·보강 포함):

| 구분 | 조건 |
|---|---|
| caravan 컨테이너 | `data.caravans == null` → 새 리스트 생성 |
| caravan 컨테이너 | `data.caravans.Count == 0` → 기본 `CaravanSaveData` 1개 추가 |
| caravan ID | `caravanId`가 null / empty / **whitespace** |
| caravan ID | `caravanIds` 집합에 이미 등록된 **중복 ID** |
| 선택 포인터 | `SaveDataLookup.TryGetCaravan(data, selectedCaravanId)` 실패 |
| 자산 ID | wagon / animal / mercenary `instanceId` backfill 또는 중복 repair (기존) |
| 자산 컨테이너 | wagon / animals / mercenaries null 보정 (기존) |

### 4.2 caravanId 발급 조건

**변경 전**

```csharp
if (string.IsNullOrEmpty(caravan.caravanId) || !caravanIds.Add(caravan.caravanId))
```

**변경 후**

```csharp
if (string.IsNullOrWhiteSpace(caravan.caravanId) || !caravanIds.Add(caravan.caravanId))
```

공백만 있는 ID도 invalid로 취급하여 새 GUID를 발급한다.

### 4.3 중복 caravanId repair

**변경 전**

- 중복 발견 시 Warning 후 `NewCaravanId()` 1회 호출
- `assetDataChanged` 미설정

**변경 후**

```csharp
var duplicateId = caravan.caravanId;
do
{
    caravan.caravanId = SaveDataLookup.NewCaravanId();
}
while (!caravanIds.Add(caravan.caravanId));

assetDataChanged = true;
```

| 항목 | 정책 |
|---|---|
| 최초 등록 caravan | 기존 ID 유지 |
| 이후 중복 caravan | 새 ID 발급 |
| child relink | **하지 않음**. `TradeProgress` / `PendingSettlement`는 원본 ID에 남음 |
| Warning | duplicateId가 whitespace가 아닐 때만 출력 |

Warning 예:

```text
Duplicate caravan ID was found and a later Caravan received a new ID: {duplicateId}.
Existing TradeProgress/PendingSettlement entries remain assigned to the original ID.
```

### 4.4 `selectedCaravanId` 복구

```csharp
if (!SaveDataLookup.TryGetCaravan(data, data.selectedCaravanId, out selected))
{
    data.selectedCaravanId = data.caravans[0].caravanId;
    assetDataChanged = true;
}
```

| invalid selected ID 예 | 복구 결과 |
|---|---|
| `""` | 첫 caravan ID |
| `"   "` | 첫 caravan ID (`TryGetCaravan`은 empty만 거부) |
| `"missing"` | 첫 caravan ID |

---

## 5. `Load()` 자동 저장 흐름

```text
save_data.json 읽기
  → version 5면 MigrateVersion5 + Save (기존)
  → version != 6이면 CreateNewGameData (기존)
  → normalized = NormalizeData(data)
  → normalized == true 이면 Save(data) 1회   ← caravan ID 변경도 이제 트리거
  → data 반환
```

| 시나리오 | 1차 Load 동작 | 2차 Load 동작 |
|---|---|---|
| legacy empty caravanId | backfill + 디스크 Save | NormalizeData false, JSON 불변 |
| whitespace caravanId | repair + Save | idempotent |
| duplicate caravanId | 나중 항목 repair + Save | idempotent |
| invalid selectedCaravanId | 복구 + Save | idempotent |

저장 실패 시: `FrameworkLog.Error`만 남기고 메모리상 정규화 결과는 유지한다 (기존 정책 동일).

---

## 6. E2E 검증 (`RunCaravanIdNormalizationPersistenceChecks`)

메뉴: `ND/Framework/Run M1 Loop + Economy E2E Checks` — `RunMultiCaravanSaveDataChecks` 직후 실행.

### 6.1 검증 시나리오

| 단계 | 검증 내용 |
|---|---|
| empty caravanId | `NormalizeData` → 32자 GUID, `selectedCaravanId` 동기화 |
| whitespace caravanId | 동일 |
| duplicate caravanId | 첫 항목 유지, 두 번째 항목 새 ID, `NormalizeData` → `true` |
| idempotent 2nd pass | duplicate repair 후 재호출 → `false`, ID 불변 |
| invalid selected | `""`, `"missing"` → `"caravan_a"`로 복구 |
| **Load persistence** | temp path JSON + reflection으로 `JsonSaveService.Load()` 2회 → 1차 Load 후 디스크 JSON 고정, 2차 Load ID 동일 |

### 6.2 Load persistence 테스트 구조

```text
CreateNormalizedSaveData("")          // empty caravanId
  → temp/save_data.json 직접 기록
  → JsonSaveService.savePath (reflection) = temp path
  → firstLoad = service.Load()          // backfill + Save
  → firstJson = File.ReadAllText(...)
  → secondLoad = service.Load()
  → secondJson == firstJson           // 재정규화·재저장 없음
  → secondLoad.caravans[0].caravanId == firstId
```

사용자 `persistentDataPath`의 실제 세이브는 건드리지 않는다.

### 6.3 검증하지 못한 항목

- Unity Editor 메뉴 실 실행 및 Console Error 0 확인
- duplicate repair 후 orphan `tradeProgressEntries` / `pendingSettlements` runtime 동작
- multi-caravan 3개 이상 동시 duplicate repair
- gameplay UI 경로에서의 caravan ID 발급 end-to-end

---

## 7. 책임 분리

```text
SaveDataLookup
  └─ NewCaravanId() / NewInstanceId() — ID 발급
  └─ TryGetCaravan — selectedCaravanId 유효성 판별 (empty만 invalid)

JsonSaveService.NormalizeData
  └─ caravan 컨테이너·caravanId·selectedCaravanId·instanceId 정규화
  └─ 변경 여부 bool 반환

JsonSaveService.Load
  └─ NormalizeData 변경 시 디스크 1회 Save

CaravanSaveDataMapper
  └─ Save ↔ Runtime 값 보존 (ID 생성 없음)

FrameworkM1LoopE2EEditorTests
  └─ caravan ID 정규화 + temp path Load persistence smoke
```

---

## 8. 호환성·리스크

### 8.1 호환성

- schema version 6 유지
- 기존 valid caravanId·selectedCaravanId 세이브는 Load 시 변경 없음 (`NormalizeData` → `false`)
- whitespace-only caravanId는 이제 invalid → 첫 Load에서 repair·Save

### 8.2 리스크

| 항목 | 내용 |
|---|---|
| Load 시 자동 Save | 빈/중복 caravanId 세이브는 **로드만으로 파일 수정**. QA·백업 정책 주의 |
| duplicate child 미연동 | 중복 repair caravan의 progress/settlement는 원본 ID에 남을 수 있음. orphan Warning은 `ValidateChildData`에서 기존과 동일 |
| whitespace vs TryGetCaravan | `SaveDataLookup.TryGetCaravan`은 whitespace ID를 invalid로 보지 않을 수 있음. Normalize 단계에서 repair되므로 실질 영향은 제한적 |
| reflection seam | Load E2E가 `JsonSaveService.savePath` private field에 의존. 필드 rename 시 테스트 수정 필요 |

---

## 9. 데이터 흐름 요약

### 9.1 문제였던 경로

```text
[디스크] caravanId = ""
  → Load → NormalizeData (메모리 backfill)
  → assetDataChanged == false  ← 버그
  → Save() 생략
  → [디스크] caravanId = ""   ← 다음 실행도 반복
```

### 9.2 수정 후 경로

```text
[디스크] caravanId = ""
  → Load → NormalizeData → assetDataChanged == true
  → Save(data)
  → [디스크] caravanId = "abc...32자..."
  → 재Load → NormalizeData == false, JSON 불변
```

---

## 10. 후속 후보

1. duplicate caravanId repair 시 child entry relink 정책 검토 (필요 시 version 7)
2. `SaveDataLookup.TryGetCaravan` whitespace 처리와 Normalize 정책 통일
3. gameplay caravan 생성 경로에서 `NewCaravanId()` 명시적 호출 (Normalize fallback 최소화)
4. orphan progress/settlement 자동 정리 또는 수동 복구 도구
5. `JsonSaveService` save path를 테스트용으로 주입 가능한 internal seam 공개 (reflection 제거)

---

## 11. 관련 문서

- Asset Instance ID Persistence: `Docs/Personal_Documents/CSU/0722_asset_instance_id_persistence.md`
- Asset Instance ID Normalization: `Docs/Personal_Documents/CSU/0722_asset_instance_id_normalization.md`
- CaravanId CopyToSave 동기화: `Docs/Personal_Documents/CSU/0722_caravan_id_copytosave_sync.md`
- Multi-Caravan Save Cutover: `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
