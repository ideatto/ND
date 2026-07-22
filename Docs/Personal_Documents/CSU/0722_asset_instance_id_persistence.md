# Asset Instance ID Persistence 구현 로직

- 작성일: 2026-07-22
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/asset-instance-id-persistence`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현·Editor/Play Mode 왕복 검증 완료(커밋 전)
- 선행 작업:
  - `feature/framework/multi-caravan-save-cutover` — SaveData v6 컬렉션 컷오버
  - Core `CaravanData` / `imsiWagonData` / `imsiAnimalData` / `imsiMercenaryData`에 `instanceId` 필드 존재
- 관련 문서:
  - `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
  - `Docs/Personal_Documents/CSU/0714_save_hybrid_vs_id_only.md`
  - `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`

---

## 1. 목적

SaveData v6 multi-caravan 스키마 위에서, **caravan에 배치된 마차·동물·용병의 보유 개체 ID(`instanceId`)**를 저장·복원 경로에 연결한다.

Core runtime 모델(`CaravanData`, `imsiWagonData` 등)에는 이미 `instanceId` / `caravanId`가 정의되어 있으나, Save DTO와 mapper가 이를 복사하지 않아 **저장 후 ID가 소실**되는 상태였다.

이번 브랜치가 해결하는 문제:

1. 같은 종류 마차·동물·용병을 여러 개 보유할 때, 종류 이름만으로는 개체를 구분할 수 없다.
2. 멀티 상단·자산 잠금·파괴/전손 처리 등 후속 기능은 **안정적인 보유 개체 ID**가 필요하다.
3. `CaravanSaveDataMapper.ToRuntime`이 `caravanId`를 복원하지 않아, Save → Runtime 왕복 시 caravan 식별자가 비어 있었다.

이번 브랜치 범위:

- `WagonSaveData` / `AnimalSaveData` / `MercenarySaveData`에 `instanceId` 필드 추가
- `CaravanSaveDataMapper`에서 `caravanId` 및 자산 `instanceId`의 Save ↔ Runtime 양방향 복사
- null wagon 처리 시 `instanceId` 클리어 규칙 정합
- Editor/Play Mode 왕복 smoke 검증

이번 범위에 **포함하지 않는 것**:

- `instanceId` / `caravanId` **생성·발급** 로직 (소유·저장 시스템 책임, mapper는 보존만)
- SaveData schema version 추가 변경 (v6 유지)
- 기존 세이브의 빈 `instanceId`를 backfill하는 마이그레이션
- `JsonSaveService` 디스크 `save_data.json` 실제 쓰기 E2E (사용자 세이브 보호를 위해 smoke에서 제외)
- Economy / UI / 자산 잠금 소비측 구현

---

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `SaveData.cs` | `WagonSaveData` / `AnimalSaveData` / `MercenarySaveData`에 `instanceId` 필드 추가, 헤더 주석 갱신 |
| `CaravanSaveDataMapper.cs` | `ToRuntime` / `CopyToSave` 경로에서 `caravanId`·자산 `instanceId` 보존, null wagon 클리어 |

---

## 3. 책임 분리

```text
Core runtime (CaravanData, imsiWagonData, imsiAnimalData, imsiMercenaryData)
  └─ instanceId / caravanId 필드 보유
  └─ ID 생성·교체는 하지 않고, Framework가 부여한 값을 비교·참조만 한다.

SaveData v6 DTO
  └─ CaravanSaveData.caravanId (기존)
  └─ WagonSaveData.instanceId / AnimalSaveData.instanceId / MercenarySaveData.instanceId (신규)

CaravanSaveDataMapper
  └─ Save → Runtime: caravanId + 자산 instanceId 복원
  └─ Runtime → Save: 자산 instanceId 복사 (caravanId는 CopyToSave에서 덮어쓰지 않음)
  └─ ID를 새로 만들지 않는다.

JsonSaveService
  └─ 변경 없음. JsonUtility 직렬화로 instanceId가 JSON에 포함된다.
```

---

## 4. 저장 스키마 변경 (version 6 유지)

### 4.1 추가 필드

| DTO | 필드 | 의미 |
|---|---|---|
| `WagonSaveData` | `instanceId` | 마차 **종류**와 별개로, 플레이어가 보유한 **한 대**를 식별하는 안정 ID |
| `AnimalSaveData` | `instanceId` | 동물 **종류**와 별개로, 플레이어가 보유한 **한 개체**를 식별하는 안정 ID |
| `MercenarySaveData` | `instanceId` | 용병 **종류**와 별개로, 플레이어가 보유한 **한 명**을 식별하는 안정 ID |

기본값은 `string.Empty`이다.

### 4.2 schema version

- `SaveData.CurrentVersion`은 **6 유지**
- JsonUtility는 누락 필드를 기본값(`""`)으로 역직렬화하므로, 기존 세이브는 **손상 없이 로드**되며 `instanceId`만 비어 있다.
- backfill 마이그레이션은 이번 PR 범위 밖이다. 필요 시 별도 작업에서 발급 정책과 함께 추가한다.

---

## 5. Mapper 동작

### 5.1 `ToRuntime(CaravanSaveData saveData)`

| 항목 | 동작 |
|---|---|
| `caravanId` | `saveData.caravanId` → `CaravanData.caravanId` (**신규 연결**) |
| wagon | `WagonSaveData.instanceId` → `imsiWagonData.instanceId` |
| animals | 각 `AnimalSaveData.instanceId` → `imsiAnimalData.instanceId` |
| mercenaries | 각 `MercenarySaveData.instanceId` → `imsiMercenaryData.instanceId` |

### 5.2 `CopyToSave(CaravanData runtimeData, CaravanSaveData saveData)`

| 항목 | 동작 |
|---|---|
| `caravanId` | **복사하지 않음** (기존 `CopyToSave` 계약 유지. DTO에 이미 있는 caravanId는 그대로) |
| wagon | `runtimeData.wagon == null`이면 `saveData.wagon.instanceId = string.Empty` 포함 필드 클리어 |
| wagon | non-null이면 `instanceId = runtimeData.instanceId ?? string.Empty` |
| animals / mercenaries | runtime → save 복사 시 `instanceId ?? string.Empty` |

### 5.3 설계 의도

- **생성 없음**: mapper는 ID를 발급하거나 derive하지 않는다. Core 주석과 동일하게 Framework/소유 시스템이 부여한 값만 왕복한다.
- **caravanId 비대칭**: `ToRuntime`은 복원하지만 `CopyToSave`는 caravan 메타를 덮어쓰지 않는다. caravan 컬렉션 쓰기는 `SaveDataLookup` / 상위 저장 흐름이 담당한다.

---

## 6. Core와의 정합

Core runtime(`TradeDataDraft.cs`, `CaravanData.cs`)에는 이미 다음 주석·필드가 존재한다.

- `imsiWagonData.instanceId` — "어떤 종류"가 아니라 "내가 가진 이 한 대"
- `imsiAnimalData.instanceId` / `imsiMercenaryData.instanceId` — 동일 규칙
- `CaravanData.caravanId` — caravan 생성·영속 시스템이 부여하는 안정 ID

이번 작업은 **Save 계층이 Core 계약을 따라가도록** 맞춘 것이다.

---

## 7. E2E 검증

검증 환경: Unity Editor `6000.5.2f1`, MCP `execute_code` + Console 모니터링

### 7.1 Editor — CaravanSaveDataMapper 왕복 (15항)

1. Save DTO → `ToRuntime` → `caravanId` / wagon·animal·mercenary `instanceId` 보존
2. Runtime → `CopyToSave` → 자산 `instanceId` 보존
3. `CopyToSave`가 대상 DTO의 `caravanId`를 덮어쓰지 않음
4. 동일 DTO 재복사 후 `caravanId`·wagon `instanceId` 유지
5. `JsonUtility.ToJson` / `FromJson<CaravanSaveData>` 후 ID 보존
6. null wagon → `instanceId` 클리어

결과: **`ASSET_INSTANCE_ID_ROUNDTRIP: ALL PASSED (15)`**

### 7.2 Editor — SaveData JSON 왕복 (7항)

1. `SaveData` 전체 JSON 직렬화·역직렬화
2. `selectedCaravanId`, `caravans[0].caravanId`, 자산 `instanceId` 보존
3. 역직렬화 후 `ToRuntime` 재매핑 ID 보존
4. JSON 본문에 wagon `instanceId` 키 포함 확인

결과: **`SAVEDATA_INSTANCE_ID_ROUNDTRIP: ALL PASSED (7)`**

### 7.3 Play Mode — Runtime 왕복

- `Boot` → `Title` Play Mode 진입
- Play Mode에서 mapper 왕복 smoke 실행

결과: **`PLAYMODE_INSTANCE_ID_ROUNDTRIP: PASS | scene=Title`**

### 7.4 Console

| 항목 | 결과 |
|---|---|
| 스크립트 컴파일 | 통과 |
| Editor Console Error | 0 |
| Play Mode Console Error | 0 |

### 7.5 검증하지 못한 항목

- `JsonSaveService.Save` / `Load`를 통한 실제 `save_data.json` 디스크 왕복
- 기존 v6 세이브 로드 후 `instanceId`가 비어 있는 legacy 데이터에 대한 backfill
- 자산 잠금·파괴·멀티 상단 UI가 `instanceId`를 소비하는 end-to-end 흐름

---

## 8. 호환성·리스크

### 8.1 호환성

- schema version 변경 없음 → 기존 v6 세이브 로드 가능
- 누락 `instanceId`는 `string.Empty` → legacy caravan/자산은 종류 이름(`wagonName` 등)으로만 식별되는 기존 동작과 동일
- `JsonUtility` property 미직렬화 규칙은 변경 없음

### 8.2 리스크

| 항목 | 내용 |
|---|---|
| legacy backfill 부재 | 기존 세이브의 자산은 `instanceId`가 비어 있을 수 있음. 잠금·파괴 기능은 empty ID 처리 정책 필요 |
| ID 발급 시점 | mapper만으로는 ID가 생기지 않음. caravan/자산 생성 경로에서 발급하지 않으면 저장해도 empty |
| `CopyToSave` caravanId | caravan 메타는 mapper가 쓰지 않으므로, caravanId 갱신은 상위 저장 API를 통해 수행해야 함 |

---

## 9. 후속 후보

1. caravan·자산 **생성/구매/배치** 시 `instanceId` 발급을 Framework 소유 경로에 연결
2. legacy v6 세이브 backfill 마이그레이션 정책 결정 (필요 시 version 7)
3. `JsonSaveService` temp path 디스크 왕복 E2E 추가
4. 자산 잠금·파괴·멀티 상단 UI가 `(caravanId, instanceId)`를 소비하도록 handoff
5. `FrameworkM1LoopE2EEditorTests`에 `instanceId` 보존 assertion 추가

---

## 10. 관련 문서

- Multi-Caravan Save Cutover: `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
- Save 하이브리드 vs ID-only: `Docs/Personal_Documents/CSU/0714_save_hybrid_vs_id_only.md`
- Multi-Caravan 아키텍처 계약: `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`
