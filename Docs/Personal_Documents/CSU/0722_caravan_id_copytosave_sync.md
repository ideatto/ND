# CaravanId CopyToSave 및 Runtime 동기화 구현 로직

- 작성일: 2026-07-22
- 담당: Framework & Integration (CSU)
- 브랜치: `fix/framework/runtime-data-caravanid-to-savedata-mapping`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현·Unity 컴파일 확인 완료(커밋 전)
- 선행 작업:
  - `feature/framework/asset-instance-id-persistence` — `instanceId` Save ↔ Runtime 매핑
  - `feature/framework/multi-caravan-save-cutover` — SaveData v6 `caravans[]` / `selectedCaravanId`
- 관련 문서:
  - `Docs/Personal_Documents/CSU/0722_asset_instance_id_persistence.md`
  - `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
  - `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`

---

## 1. 목적

SaveData v6 multi-caravan 스키마에서 **caravan 식별자(`caravanId`)**가 Save ↔ Runtime 왕복 시 끊기지 않도록 Framework 매퍼·출발 경로·debug harness를 정합시킨다.

### 1.1 관찰된 증상

| 구간 | 증상 |
|---|---|
| 저장 후 DTO | `caravanId=` (빈 값) |
| 로드 후 runtime | `caravanId=` (빈 값) |
| 자산 잠금 | `WagonInUse(by ___)` — 점유 상단 ID가 비어 있음 |

`ToRuntime`은 이미 `saveData.caravanId`를 runtime에 복사하고 있었으나, **`CopyToSave`가 runtime → save 방향으로 `caravanId`를 쓰지 않아** 출발·진행·정산·claim 이후 저장 DTO의 ID가 소실되었다.

### 1.2 왜 문제인가

1. **자산 잠금(`CaravanAssetLock`)** — 다른 상단이 같은 `instanceId` 마차·동물·용병을 쓰는지 판별할 때 `other.caravanId`가 필요하다. 비어 있으면 "자기 자신 제외"가 깨져 false positive 충돌 또는 `by` 뒤가 빈 로그가 발생한다.
2. **multi-caravan child 연동** — `tradeProgressEntries[].caravanId`, `pendingSettlements[].caravanId`는 caravan DTO의 ID와 일치해야 한다. save DTO ID가 지워지면 `JsonSaveService.NormalizeData`가 **새 ID를 발급**할 수 있어 child 레코드와 parent caravan이 엇갈린다.
3. **Core `CaravanRuntimeList` 임시 보정** — Core 측에서 `ToRuntime` 후 `caravanId`를 다시 채우는 방어 코드가 있었으나, **저장 경로(CopyToSave) 누락**은 근본 해결이 아니다.

---

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `CaravanSaveDataMapper.cs` | `CopyToSave`에 `caravanId` 복사 추가 + 빈 runtime ID 방어 |
| `TradeStartService.cs` | 출발 직전 runtime `caravanId` ← 선택 caravan save ID 동기화 |
| `TradeStartDebugHarness.cs` | 샘플 caravan / 출발 직전 save ID 동기화 헬퍼 |

---

## 3. 책임 분리

```text
SaveData v6
  └─ caravans[].caravanId          (영구 식별자, JsonSaveService가 빈 값 보정)
  └─ selectedCaravanId             (선택 포인터)
  └─ tradeProgressEntries[].caravanId / pendingSettlements[].caravanId

CaravanData (Core runtime)
  └─ caravanId                     (Framework가 부여·보존, Core는 생성하지 않음)

CaravanSaveDataMapper
  └─ ToRuntime:   save.caravanId → runtime.caravanId        (기존)
  └─ CopyToSave:  runtime.caravanId → save.caravanId        (신규, 빈 값이면 skip)

TradeStartService.TryStartTrade
  └─ 출발 기록 전 SyncRuntimeCaravanIdFromSave
  └─ 이후 JourneyRunner.TryDepart → CopyToSave → Save

TradeStartDebugHarness
  └─ FillSampleCaravan / StartTradeAndRecordTime 직전 SyncCaravanIdFromSelectedSave

CaravanRuntimeList (Core, YHY)
  └─ ToRuntime 후 caravanId 보정 (매퍼 미매핑 전제의 임시 코드)
  └─ ToRuntime이 이미 매핑하므로 대부분 no-op. 제거는 Core 협의 후속.
```

---

## 4. 데이터 흐름

### 4.1 정상 라운드트립 (목표 상태)

```text
[저장 DTO]  caravanId = "abc123..."
      │
      │  ToRuntime
      ▼
[런타임]    caravanId = "abc123..."
      │
      │  Core 계산 (출발/진행/정산/claim)
      │  CopyToSave
      ▼
[저장 DTO]  caravanId = "abc123..."   ← 유지
```

### 4.2 버그였던 경로 (수정 전)

```text
[저장 DTO]  caravanId = "abc123..."
      │
      │  ToRuntime
      ▼
[런타임]    caravanId = "abc123..."
      │
      │  CopyToSave (caravanId 필드 미복사)
      ▼
[저장 DTO]  caravanId = ""            ← 소실
      │
      │  재로드 / CaravanRuntimeList
      ▼
[런타임]    caravanId = ""            ← 자산 잠금 by ___ 비어 있음
```

### 4.3 CopyToSave 호출 지점

`CaravanSaveDataMapper.CopyToSave`는 다음 Framework 경로에서 공통 사용된다.

| 호출부 | 시점 |
|---|---|
| `TradeStartService.TryStartTrade` | 출발 성공 직후 |
| `TradeProgressCoordinator.CheckProgressAndCompletion` | 진행률 갱신 후 |
| `TradeProgressCoordinator.ApplyOfflineProgressOnLoad` | 오프라인 진행 적용 후 |
| `TradeProgressCoordinator.SettleActiveTrade` | 정산 생성 후 |
| `TradeProgressCoordinator.ClaimSettlement` | claim 성공 후 |
| `TradeProgressCoordinator` (legacy claim 경로) | 구형 claim 후 |

이번 수정으로 **위 모든 저장 시점**에 `caravanId`가 runtime → save로 반영된다.

---

## 5. 구현 상세

### 5.1 `CaravanSaveDataMapper.CopyToSave`

**추가 동작**

```csharp
if (!string.IsNullOrEmpty(runtimeData.caravanId))
{
    saveData.caravanId = runtimeData.caravanId;
}
```

**규칙**

| runtime `caravanId` | save `caravanId` (CopyToSave 후) |
|---|---|
| 비어 있지 않음 | runtime 값으로 덮어씀 |
| null / `""` | **기존 save 값 유지** (방어) |

**방어 이유**

- debug harness / E2E `CreateSampleCaravan`은 ID 없이 runtime을 만들 수 있다.
- 방어 없이 `saveData.caravanId = runtimeData.caravanId`만 하면 빈 runtime이 **기존 save ID를 지운다**.
- 이후 `JsonSaveService.NormalizeData`가 새 ID를 발급하면 `selectedCaravanId`·child entry 연동이 깨질 수 있다.

**대칭성**

- `ToRuntime`: `caravanId = saveData.caravanId` (기존)
- `CopyToSave`: non-empty runtime ID만 save에 반영 (신규)

### 5.2 `TradeStartService.SyncRuntimeCaravanIdFromSave`

출발 검증 통과 후, snapshot 촬영 **전**에 호출한다.

```csharp
SyncRuntimeCaravanIdFromSave(caravan, saveData.caravan);
```

**조건**

| 조건 | 동작 |
|---|---|
| runtime `caravanId` 이미 있음 | 변경 없음 |
| runtime 비어 있고 save `caravanId` 있음 | runtime ← save 복사 |
| save도 비어 있음 | 변경 없음 (ID 생성하지 않음) |

**효과**

- debug harness / Editor E2E처럼 ID 없는 sample runtime도 **선택 caravan save ID**와 맞춘 뒤 출발한다.
- 이후 `CopyToSave`가 non-empty runtime ID를 save에 다시 쓰므로 round-trip이 닫힌다.
- 정상 UI 경로(`TradePrepareCaravanFactory`가 `departureCaravanId` 설정)는 이미 ID가 있으므로 no-op.

### 5.3 `TradeStartDebugHarness.SyncCaravanIdFromSelectedSave`

**호출 시점**

- `FillSampleCaravan()` 마지막
- `StartTradeAndRecordTime()` — `TryStartTrade` 직전

**ID 소스 우선순위**

1. `saveData.caravan.caravanId` (선택 caravan DTO)
2. fallback: `saveData.selectedCaravanId`

**원칙**

- 샘플 caravan은 **새 ID를 생성하지 않는다**.
- `FrameworkRoot` / `CurrentSaveData`가 없으면 no-op.

---

## 6. 자산 잠금과의 관계

`CaravanAssetLock`은 전체 caravan 목록을 순회하며 `instanceId` 충돌을 검사한다.

```text
target.caravanId == other.caravanId  → 같은 상단, 자기 자신 제외
other.caravanId가 비어 있음           → 제외 판별 실패, by ___ 로그
```

필요한 ID 체인:

```text
save.caravans[i].caravanId
  → ToRuntime → runtime.caravanId
  → CaravanRuntimeList.Build (전체 상단 목록)
  → CaravanAssetLock.IsAssetInUse(...)
```

`instanceId` persistence(0722 선행) + 이번 `caravanId` CopyToSave가 모두 있어야 **저장/재실행 후에도** 잠금이 유효하다.

---

## 7. `CaravanRuntimeList` 보정과의 관계

Core `CaravanRuntimeList` 주석은 "매퍼가 caravanId를 옮기지 않는다"고 기술되어 있으나, **현재 `ToRuntime`은 이미 매핑한다.**

| 레이어 | 역할 | 이번 작업 후 |
|---|---|---|
| `ToRuntime` | save → runtime | 근본 매핑 |
| `CopyToSave` | runtime → save | 근본 매핑 (신규) |
| `CaravanRuntimeList` | load 후 no-op 보정 | 중복 방어, 제거 가능( Core 후속 ) |
| `TradeStartService` sync | 출발 전 runtime 채움 | debug/E2E 방어 |
| `CopyToSave` empty guard | save ID 지우기 방지 | debug/E2E 방어 |

`CaravanRuntimeList` 보정은 이번 Framework PR 범위 밖이며, Core 측 주석·코드 정리는 별도 협의 대상이다.

---

## 8. 검증

### 8.1 수행한 검증

- Unity Editor 컴파일 오류 없음 (`read_console` errors 0)

### 8.2 권장 수동 검증

1. **Debug harness**
   - `FillSampleCaravan` → `StartTradeAndRecordTime`
   - 저장 JSON / 로그에서 `caravanId` non-empty 확인
2. **자산 잠금**
   - 두 상단에 동일 `instanceId` 마차 배치 시도
   - `WagonInUse(by <caravanId>)`에 실제 ID 표시 확인
3. **재실행**
   - 출발 → 저장 → 에디터 재시작 → 로드
   - runtime `caravanId` 및 `instanceId` 유지 확인

### 8.3 테스트하지 못한 항목

- 실제 `save_data.json` 디스크 왕복 E2E (사용자 세이브 보호)
- multi-caravan 동시 active UI 시나리오

---

## 9. 리스크 및 후속

| 항목 | 내용 |
|---|---|
| 잘못된 non-empty runtime ID | 의도적으로 overwrite. 잘못된 ID가 runtime에 들어오면 save도 같이 바뀐다. |
| ID 생성 책임 | mapper / sync / harness 모두 **ID를 새로 만들지 않는다**. 발급은 `SaveData` ctor / `JsonSaveService.NormalizeData` / caravan 생성 시스템. |
| `CaravanRuntimeList` | outdated 주석·중복 보정. Core 정리 후 제거 검토. |
| schema version | v6 유지, 필드 추가 없음 |

---

## 10. 요약

```text
문제: CopyToSave가 caravanId를 저장하지 않아 Save→Runtime→Save 왕복 시 ID 소실
해결:
  1) CopyToSave에 caravanId 복사 (non-empty만)
  2) 빈 runtime ID로 save ID를 지우지 않는 방어
  3) TryStartTrade 출발 전 runtime ← save ID 동기화
  4) Debug harness 출발 직전 동일 동기화

결과: caravanId·instanceId가 함께 persist되어 자산 잠금·multi-caravan child 연동이 저장 후에도 유지된다.
```
