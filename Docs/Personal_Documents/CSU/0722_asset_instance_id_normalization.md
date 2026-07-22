# Asset Instance ID Normalization 구현 로직

- 작성일: 2026-07-22
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/asset-instance-id-normalization`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현 완료(커밋 전)
- 선행 작업:
  - `feature/framework/asset-instance-id-persistence` (PR #187) — SaveData v6에 `instanceId` 필드 및 `CaravanSaveDataMapper` Save ↔ Runtime 왕복
  - `fix/framework/runtime-data-caravanid-to-savedata-mapping` (PR #187) — `CopyToSave`에서 `caravanId` 보존
- 관련 문서:
  - `Docs/Personal_Documents/CSU/0722_asset_instance_id_persistence.md`
  - `Docs/Personal_Documents/CSU/0722_caravan_id_copytosave_sync.md`
  - `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
  - `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`

---

## 1. 목적

선행 PR에서 Save DTO·mapper가 `instanceId`를 **보존**할 수 있게 되었으나, 다음 문제는 아직 남아 있었다.

1. **legacy 세이브 backfill 부재** — 기존 v6 세이브의 wagon·animal·mercenary `instanceId`는 `string.Empty`로 로드된다.
2. **ID 발급 API 부재** — debug/sample runtime이 ID 없이 생성되면 저장 후에도 empty 상태가 유지된다.
3. **중복 ID 방어 부재** — 복사·수동 편집·마이그레이션 과정에서 동일 `instanceId`가 여러 자산에 할당될 수 있다.
4. **정규화 결과 미영속** — 메모리상 backfill/repair 후 디스크에 다시 쓰지 않으면 다음 로드 때 같은 작업을 반복한다.

이번 브랜치는 **Framework 소유의 instance ID 생성·정규화·로드 시 자동 저장** 경로를 추가하여, 영속 자산 ID가 저장 파일에 안정적으로 존재하도록 한다.

---

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `SaveDataLookup.cs` | `NewInstanceId()` public API 추가. `NewCaravanId()`와 동일한 N-format GUID 생성 |
| `JsonSaveService.cs` | `NormalizeData`가 `bool` 반환. 자산 `instanceId` backfill·중복 repair. `Load()` 시 정규화 변경분 자동 저장 |
| `FrameworkM1LoopE2EEditorTests.cs` | ID 생성·정규화·mapper 왕복 Editor smoke 추가. 샘플 caravan에 ID 부여 |
| `TradeStartDebugHarness.cs` | 샘플 wagon·animal·mercenary 생성 시 `NewInstanceId()` 부여 |

이번 범위에 **포함하지 않는 것**:

- SaveData schema version 변경 (v6 유지)
- Economy / UI / `CaravanAssetLock` 소비측 구현
- 자산 구매·배치 등 gameplay-owned ID 발급 경로 전면 연결
- `CaravanSaveDataMapper` 동작 변경 (선행 PR에서 완료)

---

## 3. 책임 분리

```text
SaveDataLookup
  └─ NewInstanceId(): 영속 자산 ID 발급 (public)
  └─ NewCaravanId(): caravan ID 발급 (internal, 동일 GUID 형식)

JsonSaveService.NormalizeData
  └─ 기존 null 컨테이너·caravanId·child data 정규화 (기존 책임 유지)
  └─ wagon / animal / mercenary instanceId backfill 및 전역 중복 제거 (신규)
  └─ 변경 여부를 bool로 반환

JsonSaveService.Load
  └─ NormalizeData 후 변경이 있으면 Save(data) 1회 호출 (신규)

CaravanSaveDataMapper
  └─ ID 생성 없음. Save ↔ Runtime 값 보존만 (선행 PR)

Debug / E2E harness
  └─ 샘플 runtime 생성 시 NewInstanceId()로 ID 선부여
```

---

## 4. ID 생성 API

### 4.1 `SaveDataLookup.NewInstanceId()`

| 항목 | 내용 |
|---|---|
| 가시성 | `public static` |
| 반환 형식 | `Guid.NewGuid().ToString("N")` — 하이픈 없는 32자 문자열 |
| 용도 | wagon·animal·mercenary 등 **플레이어 보유 개체** 식별 |
| caravan ID와의 관계 | `NewCaravanId()`와 동일한 `NewPersistentGuid()` 헬퍼 공유. 형식만 같고 용도·전역 uniqueness 집합은 분리 |

---

## 5. NormalizeData 자산 ID 정규화

### 5.1 시그니처 변경

```csharp
// before
public static void NormalizeData(SaveData data)

// after
public static bool NormalizeData(SaveData data)
```

- `true`: 자산 컨테이너(wagon/animals/mercenaries null 보정) 또는 자산 `instanceId`가 변경됨
- `false`: 두 번째 이후 호출 등, 자산 ID 관련 변경 없음

기존 `NormalizeData(data)` 호출부는 반환값을 무시해도 컴파일·동작에 문제 없다.

### 5.2 처리 순서 (caravan 루프 내부)

각 `data.caravans[i]`에 대해:

1. `CaravanSaveDataMapper.Normalize(caravan)` — 기존 DTO null/리스트 보정
2. **Wagon** — `wagonName`이 비어 있지 않을 때만 `EnsureUniqueInstanceId`
3. **Animals** — null이 아닌 모든 항목에 `EnsureUniqueInstanceId`
4. **Mercenaries** — null이 아닌 모든 항목에 `EnsureUniqueInstanceId`
5. 기존 caravanId 중복 처리 등 나머지 정규화 계속

### 5.3 전역 uniqueness

- `usedInstanceIds: HashSet<string>`는 **모든 caravan을 순회하며 공유**
- wagon·animal·mercenary 타입 간에도 동일 ID 사용 불가
- caravanId uniqueness(`caravanIds`)와는 별도 집합

### 5.4 `EnsureUniqueInstanceId` 동작

| 입력 상태 | 동작 | `assetDataChanged` |
|---|---|---|
| ID가 비어 있음 | 새 GUID 발급 (`NewInstanceId`) | `true` |
| ID가 있고 전역 집합에 미등록 | 그대로 등록 | `false` |
| ID가 있으나 이미 사용됨 (중복) | 새 GUID 발급 + Warning 로그 | `true` |

중복 교체 Warning 예:

```text
Duplicate asset instance ID was replaced. Type: Animal, Label: Persistence Horse, InstanceId: {duplicateId}
```

### 5.5 wagon만 이름 조건이 있는 이유

- `wagonName`이 비어 있으면 "보유 중인 마차"로 간주하지 않고 ID backfill을 건너뛴다.
- animal·mercenary는 리스트에 존재하면 이름 유무와 관계없이 ID를 부여한다.
- 빈 runtime wagon → `CopyToSave`가 `instanceId`를 클리어하는 기존 mapper 계약과 정합을 맞추기 위함이다.

---

## 6. Load 시 자동 저장

`JsonSaveService.Load()` 흐름:

```text
JSON 역직렬화
  → version 5면 MigrateVersion5 + Save (기존)
  → version != 6이면 새 게임 데이터 (기존)
  → normalized = NormalizeData(data)
  → normalized == true 이면 Save(data) 1회
  → data 반환
```

| 항목 | 내용 |
|---|---|
| 트리거 | legacy empty ID backfill, 중복 repair, null 컨테이너 보정 |
| 저장 실패 | `FrameworkLog.Error` — 메모리상 정규화는 유지, 디스크 미반영 |
| version 변경 | 없음 (v6 유지) |
| 마이그레이션과의 관계 | v5→v6 마이그레이션 Save와 별개로, v6 로드 후 정규화 Save가 추가로 실행될 수 있음 |

---

## 7. Debug / E2E 샘플 데이터

### 7.1 `TradeStartDebugHarness.FillSampleCaravan()`

샘플 caravan 생성 시:

- wagon, animal 2마리, mercenary 1명에 각각 `SaveDataLookup.NewInstanceId()` 부여
- mercenary 항목 자체가 신규 추가됨 (기존에는 animals만 존재)

### 7.2 `FrameworkM1LoopE2EEditorTests.CreateSampleCaravan()`

Editor E2E용 샘플 caravan에도 동일하게 ID 부여 + mercenary 1명 추가.

---

## 8. E2E 검증 (`RunAssetInstanceIdPersistenceChecks`)

메뉴: `ND/Framework/Run M1 Loop + Economy E2E Checks` — 전체 E2E 시작 시 **첫 번째**로 실행.

### 8.1 검증 시나리오

| 단계 | 검증 내용 |
|---|---|
| ID 생성 | `NewInstanceId()` 2회 → 32자, Guid 파싱 가능, 서로 다름 |
| 1차 Normalize | wagon ID 보존, animal 중복 ID repair, mercenary empty ID backfill → `true` |
| 2차 Normalize | ID 변경 없음 → `false` (안정성) |
| Mapper 왕복 | `ToRuntime` → `CopyToSave` 후 wagon·animal·mercenary ID 보존 |

### 8.2 테스트 데이터 구성

```text
wagon.instanceId     = firstGeneratedId  (유지되어야 함)
animals[0].instanceId = firstGeneratedId  (wagon과 중복 → repair)
mercenaries[0]       = instanceId 없음   (backfill)
```

### 8.3 검증하지 못한 항목

- Unity Editor에서 실제 메뉴 실행 및 Console Error 0 확인 (코드·로직 기준 문서화)
- `JsonSaveService.Load()` → 디스크 `save_data.json` 왕복 E2E
- multi-caravan 간 cross-caravan duplicate repair
- `CaravanAssetLock` 등 runtime 소비측 end-to-end

---

## 9. 선행 작업과의 관계

| 주제 | 선행 PR (`asset-instance-id-persistence`) | 이번 브랜치 |
|---|---|---|
| Save DTO `instanceId` 필드 | 추가 | 변경 없음 |
| Mapper Save ↔ Runtime 복사 | 구현 | 변경 없음 |
| ID **생성** | 범위 밖 ("mapper는 보존만") | `NewInstanceId()` + Normalize backfill |
| legacy backfill | 범위 밖 | `NormalizeData`에서 수행 |
| 중복 repair | 없음 | `EnsureUniqueInstanceId` |
| 로드 후 디스크 반영 | 없음 | `Load()` 자동 Save |

`CaravanSaveDataMapper.CopyToSave` 주석:

> 빈 runtime ID로 저장 ID를 지우면 NormalizeData가 새 ID를 발급해 child 연동이 끊긴다.

이번 정규화가 추가되면서, **의도적으로 runtime ID를 비우면 save 측 ID가 새로 발급**될 수 있다. debug/sample 경로에서는 harness가 ID를 선부여하여 이 분기를 피한다.

---

## 10. 호환성·리스크

### 10.1 호환성

- schema version 6 유지 → 기존 세이브 로드 가능
- 첫 로드 시 empty `instanceId`가 backfill되고 디스크에 저장됨 → **legacy 데이터가 한 번 로드되면 ID가 생김**
- wagonName이 비어 있는 wagon slot은 ID backfill 대상 아님

### 10.2 리스크

| 항목 | 내용 |
|---|---|
| Load 시 자동 Save | 사용자 세이브 파일이 로드만으로 수정됨. backup 정책·QA 시 주의 |
| 전역 ID 공유 | wagon·animal·mercenary가 하나의 집합을 공유. 타입별 분리가 필요하면 후속 정책 검토 |
| 중복 repair | child 시스템이 구 ID를 캐시하고 있으면 연동 끊김 가능. 현재 child는 caravanId 중심 |
| gameplay 발급 미연결 | 구매·고용 등 gameplay 경로에서 ID를 안 넣으면 Normalize가 backfill. 의도된 fallback이지만 발급 시점 정책은 후속 |
| Normalize bool 반환 | 기존 호출부는 무시. `Load()`만 변경 감지에 사용 |

---

## 11. 후속 후보

1. gameplay-owned 자산 생성(구매·고용·배치) 경로에서 `NewInstanceId()` 명시적 호출
2. `JsonSaveService.Load()` 디스크 왕복 E2E (temp save path)
3. multi-caravan·cross-save duplicate repair Editor test 확장
4. `CaravanAssetLock` / 멀티 상단 UI가 정규화된 ID를 소비하는 handoff
5. wagonName empty slot 정책 문서화 (slot vs owned asset 구분)

---

## 12. 관련 문서

- Asset Instance ID Persistence: `Docs/Personal_Documents/CSU/0722_asset_instance_id_persistence.md`
- CaravanId CopyToSave 동기화: `Docs/Personal_Documents/CSU/0722_caravan_id_copytosave_sync.md`
- Multi-Caravan Save Cutover: `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
- Multi-Caravan 아키텍처: `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`
