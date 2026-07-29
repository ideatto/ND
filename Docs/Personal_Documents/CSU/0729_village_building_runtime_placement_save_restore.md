# Village Building Runtime Placement Save · Restore 구현 로직

**작성일:** 2026-07-29  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/framework/save-data-base-camp-building-cells-direction-field-addition`  
**HEAD (문서화 시점):** `de7173d2b6568344be06689682b24f098b6ff12a`  
**베이스:** `dev2`  
**범위:** Work B — 마을 건물 런타임 드래그·회전 배치의 Save 커밋, Save 실패 롤백, 로드 시 배치 복원

관련 선행:

- [`0716_save_data_base_camp_schema.md`](./0716_save_data_base_camp_schema.md) — `villageBuildings` / `displayName`+`level` 스키마 도입
- 배치 필드(`hasPlacement`, `gridCellX`, `gridCellZ`, `yawStep`)는 SaveData version **6** 계약에 포함되어 있다. 이번 Work B는 **스키마 버전을 올리지 않고**, 이미 있는 필드를 런타임으로 읽고·쓰도록 연결한다.

---

## 1. 목적

거점 마을에서 이미 지어진 건물을 드래그 이동·90° 회전한 뒤, 그 격자 배치를 영속 저장하고 재진입 시 복원한다.

달성 계약:

```text
confirmed drag placement
confirmed 90-degree rotation
one durable Save per commit
SaveData rollback on save failure
Transform + occupancy rollback on save failure
load-time restoration from saved cell and yaw
saved-placement-first registration
per-building invalid-placement fallback
```

의도적으로 하지 않은 것:

- `SaveData.CurrentVersion` 변경 (6 유지)
- Scene / Prefab / ScriptableObject / `.meta` 수정
- stable `buildingId` 도입 (`displayName` 키 유지)
- 제품 `MainUICanvas`에 `BuildingPlacementController` 배선

---

## 2. 변경 파일

| 영역 | 파일 | 역할 |
|------|------|------|
| Framework command | `Assets/_Project/11.CoreServices/Scripts/Building/CaravanBuildingConstructionCommand.cs` | `BuildingPlacementCommand` / `BuildingPlacementResult` / `BuildingPlacementFailureReason` |
| Framework bootstrap | `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs` | `BuildingPlacement` 속성 생성·노출 |
| Core input | `Assets/_Project/01.Core/07_Village/YHY/BuildingPlacementController.cs` | 드래그 preview · 커밋 · 회전 · 로드 복원 · 런타임 롤백 |
| Core registry | `Assets/_Project/01.Core/07_Village/YHY/VillageBuildingRegistry.cs` | Start 이후 복원 호출, `TryGetDisplayName` |
| Editor checks | `Assets/_Project/11.CoreServices/Editor/FrameworkM1LoopE2EEditorTests.cs` | `ND/Framework/Run Building Placement Command Checks` |

---

## 3. 전체 아키텍처

```text
BuildingPlacementController (VillageView RawImage)
  │  OnDrag / OnScroll / RotateLeft / RotateRight
  │  preview only → snapshot
  │  OnPointerUp 또는 독립 회전 확정
  │
  ├─ VillageBuildingRegistry.TryGetDisplayName(placeable)
  │       displayName 키 해석
  │
  └─ FrameworkRoot.Instance.BuildingPlacement.Execute(
         displayName, gridCellX, gridCellZ, yawStep)
           │
           ├─ villageBuildings에서 displayName 정확히 1건 조회
           ├─ hasPlacement / gridCellX / gridCellZ / yawStep 만 변경
           ├─ level 불변
           ├─ SaveService.Save 1회
           └─ Save 실패·예외 시 네 배치 필드 롤백
```

로드 경로:

```text
VillageBuildingRegistry.Start
  → RestoreFromSaveData()                 // level / 누락 건물 생성 (기존)
  → BuildingPlacementController
       .RestoreAndRegisterExistingBuildings()
         1) hasPlacement==true 를 SaveData 목록 순서로 먼저 점유
         2) 나머지 pending 은 authored/nearest-free
         3) SaveData 변경·Save 호출 없음
  → 이후 0.25s RegisterExistingBuildings 폴링은 registered 셋으로 스킵
```

핵심 원칙:

```text
preview ≠ Save
commit = Transform/occupancy 확정 + BuildingPlacementCommand.Execute + Save 1회
Save 실패 = SaveData 롤백 + Transform/회전/점유 롤백
load restore = read-only (Save 금지)
```

---

## 4. SaveData 배치 필드

`VillageBuildingSaveData` (version 6, 키 = `displayName`):

| 필드 | 의미 |
|------|------|
| `displayName` | 카탈로그와 동일한 건물 종류 키 |
| `level` | 보유 레벨. 배치 명령이 변경하지 않음 |
| `hasPlacement` | `true`면 저장 격자 배치 사용, `false`면 authored/default |
| `gridCellX` / `gridCellZ` | `VillageGrid` 논리 셀 (왼쪽 아래 기준) |
| `yawStep` | Y축 90° 단계. 정규화 후 `0..3` |

`BuildingPlacementCommand.NormalizeYawStep`:

```text
yawStep % 4
음수면 +4
예: -1→3, 4→0, 5→1
```

---

## 5. Framework — `BuildingPlacementCommand`

### 5-1. 공개 API

```csharp
FrameworkRoot.Instance.BuildingPlacement.Execute(
    displayName, gridCellX, gridCellZ, yawStep);
```

반환: `BuildingPlacementResult`

- `Succeeded`
- `FailureReason`
- `SaveResult` (Save 경로에서만 의미 있음)

### 5-2. Execute 로직

```text
1. displayName 공백 → InvalidArgument (Save 없음)
2. SaveData / player / villageBuildings / SaveService 검사
3. displayName Ordinal 일치 항목 수집
   - 0건 → BuildingNotFound
   - 2건 이상 → DuplicateBuildingEntry
   - 둘 다 Save 호출 전 실패
4. 이전 스냅샷:
   hasPlacement, gridCellX, gridCellZ, yawStep
5. 필드 기록:
   hasPlacement = true
   gridCellX / gridCellZ = 인자
   yawStep = NormalizeYawStep(인자)
   level 은 건드리지 않음
6. saveService.Save(saveData) 정확히 1회
7. Save 성공 → Success
8. Save 실패 또는 null SaveResult 또는 예외
   → RestorePlacement(스냅샷)
   → Failure(SaveFailed, saveResult?)
```

### 5-3. Editor 검증 메뉴

```text
ND/Framework/Run Building Placement Command Checks
```

검증 항목:

- 성공 트랜잭션 (level 유지, 필드 반영, SaveCalls==1)
- yaw 정규화
- Save 실패 시 SaveData 롤백
- missing / duplicate 는 Save 없이 실패

성공 로그:

```text
[Framework Building Placement] All checks passed.
```

---

## 6. Core — 드래그 · 회전 커밋

### 6-1. 상태

| 필드 | 역할 |
|------|------|
| `isDraggingBuilding` | preview 중이면 true. 드래그 중 회전은 즉시 Save하지 않음 |
| `isPlacementCommitInProgress` | 커밋 중 중첩 입력 차단 |
| `placementSnapshot` | 커밋 전 Transform·셀·풋프린트 |

`BeginPlacementPreview`:

- 현재 position / rotation / logical cell / size 를 스냅샷
- `isDraggingBuilding = true`

### 6-2. 드래그 흐름

```text
OnPointerDown
  → 선택만 (스냅샷/Save 없음)
  → RegisterExistingBuildings()로 신규 건물만 흡수

OnDrag (건물 선택 시)
  → 최초 1회 BeginPlacementPreview
  → SnapToGrid: preview 이동 + 임시 Clear/Occupy
  → Save 호출 없음

OnPointerUp
  → isDraggingBuilding == false 로 전환
  → CommitCurrentPlacement() 1회
```

핸드오프의 “OnPointerDown 또는 first OnDrag에서 스냅샷” 중 **실제 구현은 first OnDrag**이다. 선택만 하고 움직이지 않으면 Save하지 않는다.

### 6-3. 회전 흐름 (`ApplyYaw`)

진입점: `OnScroll`, `RotateLeft`, `RotateRight` → 모두 `ApplyYaw`.

```text
드래그 중이 아님 (commitImmediately = true)
  → BeginPlacementPreview
  → 90° 스냅 회전
  → CanPlace 실패: 회전 취소, Save 없음
  → CanPlace 성공: CommitCurrentPlacement 1회

드래그 중 (commitImmediately = false)
  → preview 회전만
  → Save 없음
  → pointer-up 때 드래그+회전이 한 번에 커밋
```

### 6-4. `CommitCurrentPlacement`

```text
1. displayName / Framework / BuildingPlacement 없으면
   RollbackRuntimePlacement + warning
2. 현재 셀 CanPlace 실패 → RollbackRuntimePlacement
3. yawStep = euler.y / 90 정규화
4. grid Clear + Occupy (커밋 위치)
5. BuildingPlacement.Execute(...) 1회
6. 실패 시 RollbackRuntimePlacement
   (SaveData는 Command가 이미 롤백)
7. finally: snapshot 클리어, commit 플래그 해제
```

### 6-5. `RollbackRuntimePlacement`

```text
grid.Clear(building)
position / rotation = snapshot
grid.Occupy(snapshot cell/size)
```

Save 실패 시 SaveData · Transform · 점유가 함께 이전 상태로 돌아간다.

---

## 7. Core — 로드 복원

### 7-1. 호출 순서

```text
VillageBuildingRegistry.Awake
VillageBuildingRegistry.Start
  RestoreFromSaveData()                      // level 적용, 누락 건물 생성
  FindAnyObjectByType<BuildingPlacementController>()
  → RestoreAndRegisterExistingBuildings()    // 배치 복원

BuildingPlacementController
  Awake: sceneLoaded 구독 (등록 호출 제거)
  Update 0.25s: RegisterExistingBuildings()  // registered면 skip
```

제품 경로에서 Controller가 InGame UI에 먼저 있고 Village_Home이 additive로 올라오면, Registry.Start 시점에 Controller를 찾아 복원할 수 있다.  
Controller가 없으면 Registry.Start의 복원 호출은 no-op이 된다.

### 7-2. `RestoreAndRegisterExistingBuildings`

```text
grid 재생성, registered / npcBuildingList 초기
pending = 모든 PlaceableBuilding

for each SaveData.villageBuildings (목록 순서):
  hasPlacement == false → skip
  displayName 중복 ≠ 1 → warning, skip (authored fallback로 남김)
  runtime 매칭 실패 → warning, skip
  yaw 적용 후 CanPlace 실패 → 회전 원복, warning, skip
  성공 → CellToWorldCenter + RegisterAt + pending 제거

for each remaining pending:
  RegisterAtAuthoredOrNearest
    authored logical cell → CanPlace
    실패 시 FindNearestFree
    그래도 실패 → warning, 미등록
```

중요:

- 로드 중 **SaveData를 수정하지 않음**
- 로드 중 **Save를 호출하지 않음**
- 겹침/범위 밖은 **건물 단위**로만 fallback하고 다른 건물은 계속 복원
- “먼저”의 기준은 **SaveData 리스트 순서**

### 7-3. `hasPlacement == false`

저장 배치를 쓰지 않고 `RegisterAtAuthoredOrNearest`로 간다.  
월드 좌표는 authored 위치를 논리 셀로 환산한 뒤 셀 중심에 스냅한다 (비트 단위 authored Transform 고정이 아님).

### 7-4. 폴링 안정성

복원 성공 건물은 `registered`에 들어가므로 이후 0.25초 `RegisterExistingBuildings`가 위치를 다시 옮기지 않는다.

---

## 8. displayName 연결

`VillageBuildingRegistry.TryGetDisplayName(PlaceableBuilding, out string)`:

- Registry 건물의 `renderer` Transform과 placeable Transform의 부모/자식 관계로 매칭
- 반환 문자열은 Registry 내부 참조 (호출자 변경 금지)

배치 커밋·복원 모두 이 키로 SaveData와 런타임을 연결한다. stable ID는 없다.

---

## 9. 실패 · 경고 계약

경고는 가능하면 다음을 포함한다.

```text
displayName
cell=(x,z)
yawStep
reason
```

대표 reason:

| 상황 | reason 요약 |
|------|-------------|
| Save 항목 중복 | `duplicate save entries; fallback=authored/default` |
| 런타임 건물 없음 | `runtime building mismatch` |
| OOB / overlap | `out of bounds or overlap; fallback=authored/default` |
| Save 실패 | `Village placement save failed ... FailureReason ...` |
| 통합 불가 | `integration unavailable` |
| 빈 칸 없음 | `no free cell` |

드래그 중 per-frame Save/warning 스팸은 없어야 한다.

---

## 10. 런타임 배선 현황 (검증 시점)

| 경로 | `BuildingPlacementController` | 비고 |
|------|-------------------------------|------|
| `Assets/_Project/08.Prefabs/Village/VillageView.prefab` | 있음 | Greybox `InGame_Test` / `InGameMainManagerTest` 참조 |
| `MainUICanvas`의 `VillageView` | **없음** (RawImage만) | 제품 입력·복원 호출 미배선 |
| `InGame.unity` | 직접 참조 없음 | — |
| `Village_Home.unity` | Registry만 | Controller는 UI 쪽에 존재해야 Start 복원이 동작 |

Work B 코드 계약은 Framework + Controller/Registry로 완성되어 있다.  
제품 MainUICanvas 입력 배선은 **별도 통합 작업**이다.

검증 메뉴·Greybox/임시 VillageView 경로에서는:

- 드래그/회전 1 Save
- Save 실패 이중 롤백
- 리로드 후 셀·yaw·level 공존
- overlap / OOB fallback
- 폴링 후 위치 고정

이 확인되었다 (2026-07-29 runtime verification: CONDITIONAL PASS).

---

## 11. 호출 예시

### 배치 커밋 (Controller 내부)

```csharp
BuildingPlacementResult result =
    FrameworkRoot.Instance.BuildingPlacement.Execute(
        displayName, cellX, cellZ, yawStep);

if (!result.Succeeded)
{
    // Controller는 Transform/occupancy RollbackRuntimePlacement 수행
    // SaveData는 Command가 이미 롤백
}
```

### 로드 복원 (Registry.Start)

```csharp
RestoreFromSaveData();
BuildingPlacementController placementController =
    FindAnyObjectByType<BuildingPlacementController>();
if (placementController != null)
    placementController.RestoreAndRegisterExistingBuildings();
```

---

## 12. 테스트 체크리스트

Editor:

```text
ND/Framework/Run Building Placement Command Checks
→ [Framework Building Placement] All checks passed.
```

런타임 (Controller가 있는 경로):

1. 유효 셀로 드래그 릴리즈 → Save 1회, SaveData·Transform 일치, level 유지
2. 독립 90° 회전 → Save 1회
3. 드래그 중 회전 후 릴리즈 → 드래그/회전 중 Save 0, 릴리즈 시 Save 1
4. Save 실패 주입 → SaveData + Transform + occupancy 복구
5. Title/세션 재진입 또는 씬 리로드 → 저장 셀·yaw 복원, 이후 폴링에 안 밀림
6. `hasPlacement=false` → authored/default, 로드 중 Save 없음
7. 동일 셀 중복 저장 / OOB → 해당 건물만 fallback, SaveData 불변

---

## 13. 한 줄 요약

이미 있는 version 6 배치 필드를 `BuildingPlacementCommand` 한 번의 Save 트랜잭션으로 쓰고, `BuildingPlacementController`는 preview와 commit을 분리하며 Save 실패 시 Transform·점유까지 되돌리고, 로드 시에는 SaveData 목록 순서로 저장 배치를 먼저 점유한 뒤 실패한 건물만 authored/nearest-free로 떨어뜨린다.
