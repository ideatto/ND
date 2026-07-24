# Multi-Caravan Save Cutover 구현 로직

- 작성일: 2026-07-21
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/multi-caravan-save-cutover`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현 완료(커밋 전). Editor smoke(`RunMultiCaravanSaveDataChecks`) 포함
- 상위 계약: `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`
- 필드 계약(목표): `Docs/Personal_Documents/CSU/SaveDataPolicy/SaveData_V2_Field_Contract.md`

## 1. 목적

단일 필드(`caravan` / `tradeProgress` / `pendingSettlement`)로 묶여 있던 SaveData를 **ID 기반 컬렉션**으로 전환한다.

이번 컷오버가 담당하는 범위:

- schema `CurrentVersion`을 **5 → 6**으로 올린다
- `caravans[]`, `tradeProgressEntries[]`, `pendingSettlements[]`, `selectedCaravanId`를 직렬화 진실로 둔다
- 기존 runtime 호출부용 비직렬화 호환 접근자(`SaveData.caravan` 등)를 유지한다
- version 5 세이브를 version 6으로 **명시적 마이그레이션**한다
- 조회·쓰기·ID 발급을 `SaveDataLookup`으로 중앙화한다

이번 범위에 **포함하지 않는 것**:

- Coordinator/UI를 caravanId 인자 API로 전면 전환하는 작업
- 동시에 여러 caravan이 Traveling/SettlementPending인 완전한 multi-active runtime
- claim/settle 이벤트를 `(caravanId, tradeId)` 복합키로 바꾸는 작업

기존 단일 선택 caravan 흐름은 `selectedCaravanId` 호환 접근자로 유지한다.

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `SaveData.cs` | version 6, 컬렉션 필드, `selectedCaravanId`, 호환 property |
| `SaveDataLookup.cs` *(신규)* | caravan/progress/pending ID 조회·선택 쓰기·ID 발급 |
| `PendingSettlementSaveData.cs` | `caravanId` 소유 키 추가 |
| `JsonSaveService.cs` | `NormalizeData` public화, child 검증, v5→v6 마이그레이션 |
| `TradeStartService.cs` | progress 부재(null) 출발 snapshot/rollback 대응 |
| `SaveDataDebugPrinter.cs` | multi-caravan 요약·연결 ID 로그 |
| `FrameworkM1LoopE2EEditorTests.cs` | `RunMultiCaravanSaveDataChecks` 추가 |

## 3. 책임 분리

```text
SaveData (직렬화 진실)
  └─ caravans[] / tradeProgressEntries[] / pendingSettlements[]
  └─ selectedCaravanId

SaveDataLookup
  └─ ID 조회, 중복 거부, 선택 caravan 쓰기, NewCaravanId()

JsonSaveService
  └─ Load 시 v5 마이그레이션 → NormalizeData → Save
  └─ orphan/duplicate child는 삭제하지 않고 Error 로그만 남김

기존 runtime (TradeStartService, Coordinator 등)
  └─ SaveData.caravan / tradeProgress / pendingSettlement 호환 API 계속 사용
  └─ 내부적으로 selectedCaravanId 기준으로 컬렉션을 읽·쓴다
```

## 4. 저장 스키마 (version 6)

### 4.1 최상위 필드

```text
SaveData
├─ version = 6
├─ selectedCaravanId
├─ caravans[]                  // CaravanSaveData.caravanId
├─ tradeProgressEntries[]      // TradeProgressSaveData.caravanId
├─ pendingSettlements[]        // PendingSettlementSaveData.caravanId + tradeId
├─ player / rescueLoan / tradePreparationCommit / world / tutorial
└─ (비직렬화) caravan / tradeProgress / pendingSettlement property
```

### 4.2 소유 키

| DTO | 키 | 의미 |
|---|---|---|
| `CaravanSaveData` | `caravanId` | 배열 위치와 무관한 고유 ID |
| `TradeProgressSaveData` | `caravanId` | progress 소유 caravan (현재 컷오버에서는 caravan당 최대 1개 가정) |
| `PendingSettlementSaveData` | `(caravanId, tradeId)` | 대기 정산 소유 복합키 |
| `SaveData.selectedCaravanId` | — | 마지막 선택 caravan. 기존 단일 runtime의 active 대상 |

### 4.3 생성자 기본값

`SaveData()`는 기본 caravan 1개를 만들고 `caravanId`를 발급한 뒤 `selectedCaravanId`에 연결한다.  
`CreateNewGameData()`도 `NormalizeData`를 호출해 동일 불변식을 맞춘다.

## 5. 호환 접근자

`JsonUtility`는 property를 직렬화하지 않으므로, 아래는 **런타임 호환 전용**이다.

| Property | Get | Set |
|---|---|---|
| `SaveData.caravan` | `TryGetSelectedCaravan` | `SetSelectedCaravan` (없으면 추가, ID 없으면 발급) |
| `SaveData.tradeProgress` | `TryGetTradeProgress(selectedCaravanId)` | `SetTradeProgress` (해당 caravan 소유 entry 교체) |
| `SaveData.pendingSettlement` | `TryGetPendingSettlement(selected, tradeId=null)` | `SetPendingSettlement` (`hasResult`일 때만 추가) |

`SetPendingSettlement`는 `hasResult == false`이면 해당 caravan 소유 pending을 제거하고 추가하지 않는다.  
`SetTradeProgress(null)`은 해당 caravan 소유 progress를 제거한다.

## 6. `SaveDataLookup` 로직

### 6.1 조회

- `TryGetCaravan` / `TryGetTradeProgress` / `TryGetPendingSettlement`
- 동일 키 중복이 있으면 **Error 로그 후 false** (last-write-wins 금지)
- pending의 `tradeId` 인자가 null/empty이면 caravanId만으로 매칭 (호환 getter용)

### 6.2 선택

- `TrySetSelectedCaravan`: 존재하는 caravan만 선택 가능
- 선택 실패 시 Normalize가 `caravans[0].caravanId`로 보정

### 6.3 ID 발급

- `NewCaravanId()` → `Guid.NewGuid().ToString("N")`

## 7. 정규화 · 검증 (`JsonSaveService.NormalizeData`)

`NormalizeData`는 public static으로 열어 Editor/테스트에서도 호출한다.

### 7.1 컬렉션 보정

1. `caravans` / `tradeProgressEntries` / `pendingSettlements` null → 빈 리스트
2. caravan 0개 → 빈 `CaravanSaveData` 1개 추가
3. 각 caravan: null 슬롯 교체 → `CaravanSaveDataMapper.Normalize`
4. `caravanId` 공백 또는 중복 → 새 ID 발급  
   - 중복 교체 시 orphan child 재연결은 **하지 않음** (경고 로그)
5. `selectedCaravanId`가 목록에 없으면 첫 caravan으로 보정
6. `ValidateChildData` 실행

### 7.2 child 검증 (`ValidateChildData`)

삭제·자동 병합 없이 **보존 + Error 로그**만 수행한다.

- progress: orphan `caravanId`, caravan당 중복 progress
- pending: orphan `caravanId`, `(caravanId, tradeId)` 중복
- progress의 `inGameTimeMultiplierAtStart <= 0` → `1f`로 보정

정책 의도: 손상 세이브의 모호한 child를 추측으로 이어 붙이지 않는다.

## 8. version 5 → 6 마이그레이션

### 8.1 Load 경로

```text
Load JSON
 → version == 5 이면 MigrateVersion5(json)
 → 성공 시 Save()로 디스크에 v6 재기록 시도
 → version != CurrentVersion 이면 새 게임 데이터로 복구
 → NormalizeData
```

마이그레이션 후 디스크 저장 실패는 Error 로그를 남기되, 메모리上的 migrated 데이터는 Load 결과에 사용할 수 있다.

### 8.2 `MigrateVersion5` 매핑

내부 `Version5SaveData`로 레거시 단일 필드를 읽는다.

| version 5 | version 6 |
|---|---|
| `caravan` | `caravans[0]` + 새 `caravanId` + `selectedCaravanId` |
| `tradeProgress` (None이 아니거나 tradeId 있음) | `tradeProgressEntries`에 추가, `caravanId` 연결 |
| `pendingSettlement` (`hasResult`) | `pendingSettlements`에 추가, `caravanId` 연결 |
| player / rescueLoan / commit / world / tutorial | 그대로 복사 |
| 그 외 version | 마이그레이션 대상 아님 → 기존 복구 정책 |

빈/None progress와 `hasResult == false` pending은 컬렉션에 넣지 않는다.

## 9. `TradeStartService` 보정

호환 getter가 progress를 못 찾으면 `tradeProgress`가 null일 수 있다.

- 출발 전 snapshot: progress가 null이면 JSON snapshot도 null
- rollback: snapshot null이면 `saveData.tradeProgress = null`로 제거  
  (존재하지 않던 entry를 억지로 overwrite하지 않음)
- caravan snapshot/rollback은 기존과 동일

즉시 저장 출발의 저장 경계(구조 대출 제한 해제 포함)는 유지된다.

## 10. 디버그 · 검증

### 10.1 `SaveDataDebugPrinter`

ContextMenu 출력 시 먼저 요약한다.

- `selectedCaravanId`
- caravan / progress / pending 개수와 각 항목의 연결 ID
- 이후 선택 caravan의 상세 + full JSON

### 10.2 Editor E2E (`RunMultiCaravanSaveDataChecks`)

`ND/Framework/Run M1 Loop + Economy E2E Checks` 맨 앞에서 실행한다.

검증 항목:

1. 신규 SaveData Normalize 후 caravan 1개, 선택 ID 연결, child 컬렉션 비어 있음
2. 두 번째 caravan 추가 후 다른 caravan ID로 progress/pending 조회가 누수되지 않음
3. JSON round-trip 후 ID·선택 caravan 데이터 유지
4. 빈/`missing` selected ID Normalize 시 유효 ID로 복구

## 11. 기존 단일 runtime과의 관계

```text
[직렬화]
caravans / tradeProgressEntries / pendingSettlements / selectedCaravanId

[호환 레이어]
SaveData.caravan / tradeProgress / pendingSettlement
        │
        ▼
TradeStartService, TradeProgressCoordinator, Settlement bridge 등
(아직 caravanId 인자 API로 전환하지 않음)
```

즉 이번 컷오버는 **저장 모델의 뼈대를 먼저 옮기고**, 호출부는 선택 caravan 호환 경로로 동작을 유지하는 단계이다.

## 12. 후속 작업(이번 문서 범위 밖)

1. Depart / Finalize / Claim을 `caravanId`(+ `tradeId`) 명시 API로 전환
2. settlement event payload에 `caravanId` 포함
3. orphan/duplicate child에 대한 복구 정책(보존 vs 제거) 팀 합의
4. `SaveData_V2_Field_Contract.md`의 “아직 version 5” 문구를 version 6 cutover 반영으로 갱신
5. 다중 동시 Traveling/SettlementPending Coordinator 상태 머신

## 13. 검증 상태 · 잔여 리스크

### 확인한 것

- 코드 수준 schema/lookup/migration/호환 접근자 구현
- Editor menu에 multi-caravan smoke 체크 추가

### 확인 필요

- Unity Editor에서 `Run M1 Loop + Economy E2E Checks` 실실행
- 실제 version 5 `save_data.json` 로드 → 마이그레이션 → 재실행 루프
- 기존 M1 claim/atomic claim/rescue loan E2E가 호환 property 경로에서 회귀 없는지

### 리스크

- 중복 caravanId 교정 시 child의 `caravanId`는 재연결되지 않음 → orphan Error 가능
- 호환 `pendingSettlement` getter는 `tradeId` 없이 caravan 단위로 조회하므로, 동일 caravan에 pending이 둘 이상이면 중복 거부(false)될 수 있음
- Coordinator가 아직 전역 단일 active를 가정하면, 컬렉션에 복수 Traveling이 있어도 UI/진행은 선택 caravan만 본다
