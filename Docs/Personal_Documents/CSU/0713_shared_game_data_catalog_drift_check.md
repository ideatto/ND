# Shared Game Data Catalog Drift Check · 문제 · 해결 · 로그

**작성일:** 2026-07-13  
**브랜치:** `feature/framework/shared-data-catalog-drift-check`  
**Base:** `dev2`  
**Feature root:** `Assets/_Project/11.CoreServices/`  
**관련:** 공용 데이터 카탈로그, `02.Data` SO seed, watch inventory  
**목적:** 신규 무역 SO가 Resources 카탈로그에 없어도 플레이가 통과하던 원인, drift 검사 도입, 발생 이슈와 Console 로그를 개인 작업 로그로 남긴다.

**공식 가이드:** [`Docs/Guide/Framework_Shared_Game_Data_Guide.md`](../../Guide/Framework_Shared_Game_Data_Guide.md)

---

## 1. 배경 · 발견한 문제

PR 머지 후 신규 무역 SO가 다음 경로에 추가되었다.

- `Assets/_Project/02.Data/01_ScriptableObjects/`

이전 공용 데이터 추적 경로로 알고 있던 곳은 다음과 같다.

- `Assets/99.Sandbox/_LJH/02.SO/`

그런데 **Resources 카탈로그에 신규 SO를 등록하지 않아도** 경고/에러 없이 플레이가 진행되었다.

### 실제 원인

Framework는 SO 폴더를 자동 스캔하지 않는다. 로드 우선순위는 다음과 같다.

1. 생성자 주입 catalog (테스트용)
2. `Resources.Load("SandboxSharedGameDataCatalog")`
3. Editor 전용 Sandbox 하드코딩 경로 fallback

런타임이 읽는 에셋은 오직 다음이다.

- `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset`

당시 Resources 카탈로그는 여전히 **Sandbox 더미 SO**(`dummyitem`, `dummyroute` 등)를 가리키고 있었다.  
검증이 그 더미 세트로 통과했기 때문에, `02.Data` 신규 SO는 **아예 로드되지 않은 채** 조용히 무시되었다.

추가로 `02.Data` 아래에 동일 이름 카탈로그 복제본이 있었으나 Resources가 아니어서 런타임에 사용되지 않았다.

```text
[착각]
02.Data에 SO 추가 → Framework가 알아서 추적

[실제]
Resources 카탈로그에 등록된 SO만 SharedGameData로 로드
폴더에만 있는 SO = 공용 데이터에 없음
```

---

## 2. 해결 방향 요약

| 목표 | 처리 |
|------|------|
| Resources 카탈로그를 신규 seed로 전환 | `02.Data` SO 참조로 동기화 |
| 미등록 SO를 Play 시 발견 | watch root 스캔 + GUID 비교 |
| Editor / Player 정책 분리 | Editor = Warning만, Player = ProjectData drift 시 Error·진입 차단 |
| Player에서 AssetDatabase 불가 | `SharedGameDataWatchInventory` 스냅샷 |
| 중복 카탈로그 혼동 제거 | `02.Data` 쪽 복제 catalog 삭제 |
| 디버그/E2E 더미 ID 깨짐 방지 | `dummyroute`→`BaseRoute`, `dummyitem`→`Apple` |

### Watch roots · 심각도

| Root | 경로 | Editor 미등록 | Player 미등록 |
|------|------|---------------|---------------|
| ProjectData | `Assets/_Project/02.Data/01_ScriptableObjects` | Warning | **Error + InGame 차단** |
| SandboxLegacy | `Assets/99.Sandbox/_LJH/02.SO` | Warning | Warning만 (진입 허용) |

스캔 타입: `TownData`, `MarketData`, `TradeItemData`, `WagonData`, `DraftAnimalData`, `RouteData`  
제외: `SandboxSharedGameDataCatalog`, `InGameTimePolicyConfig`  
비교 기준: **에셋 GUID** (ID 문자열만으로 동일 취급하지 않음)

---

## 3. 구현 결과 (파일)

### 신규

| 경로 | 역할 |
|------|------|
| `Scripts/Data/SharedGameDataWatchRoots.cs` | watch root 상수·판별 |
| `Scripts/Data/SharedGameDataWatchInventory.cs` | Player용 스냅샷 SO 타입 |
| `Scripts/Data/SharedGameDataDriftFinding.cs` | drift 결과 DTO |
| `Editor/SharedGameDataCatalogDriftChecker.cs` | AssetDatabase 스캔 + inventory refresh |
| `Resources/SharedGameDataWatchInventory.asset` | 빌드에 포함되는 스냅샷 |

### 수정

| 경로 | 역할 |
|------|------|
| `Scripts/Data/SharedGameDataService.cs` | drift 검사 연동 |
| `Resources/SandboxSharedGameDataCatalog.asset` | `02.Data` SO로 교체 |
| `TradeStartDebugHarness.cs` | `BaseRoute` / `Apple` |
| `FrameworkM1LoopE2EEditorTests.cs` | 동일 ID 교체 |
| `Docs/Guide/Framework_Shared_Game_Data_Guide.md` | drift·inventory 문서화 |

### 삭제

- `Assets/_Project/02.Data/01_ScriptableObjects/SandboxSharedGameDataCatalog.asset` (+ meta)

### Editor 메뉴

- `ND/Framework/Refresh Shared Game Data Watch Inventory` — Player 빌드 전 inventory 갱신

---

## 4. 작업 중 발생한 문제와 해결

### 4.1 신규 SO 미등록인데도 플레이 성공 (설계 공백)

- **증상:** `02.Data` SO를 카탈로그에 안 넣어도 Error/Warning 없이 진행
- **원인:** Resources 카탈로그가 옛 Sandbox 더미만 로드
- **해결:** Resources 카탈로그를 `02.Data` seed로 동기화 + drift 검사 추가

### 4.2 Player에서 폴더 스캔 불가

- **증상:** `AssetDatabase`는 Editor 전용 → Player에서 “디스크에 새 SO가 생겼는지” 실시간 확인 불가
- **해결:** Editor에서 inventory 스냅샷을 Resources에 저장하고, Player는 스냅샷 vs catalog 등록 GUID 비교
- **주의:** inventory를 refresh하지 않고 빌드하면 신규 SO drift를 놓칠 수 있음

### 4.3 Sandbox drift를 Player Error로 두면 상시 차단

- **위험:** 카탈로그를 `02.Data`만으로 두면 Sandbox 더미는 전부 “미등록” → Player InGame 영구 실패
- **해결:** ProjectData drift만 Player blocking Error, SandboxLegacy는 Warning만

### 4.4 컴파일 에러 CS8083 (6건)

- **증상:**

```text
SharedGameDataCatalogDriftChecker.cs: error CS8083: An alias-qualified name is not an expression.
```

- **원인:** `nameof(global::TownData)` 등 — `nameof` 안에 `global::` 사용 불가
- **해결:** `nameof(TownData)` 형태로 변경 (`case global::TownData` 패턴 매칭은 유지)

### 4.5 디버그/E2E 하드코딩 더미 ID

- **위험:** 카탈로그를 신규 seed로 바꾼 뒤 `dummyroute` / `dummyitem` 의존 테스트 실패
- **해결:** Framework harness·E2E를 `BaseRoute` / `Apple`로 교체

---

## 5. 예상 · 확인용 Console 로그

### 5.1 정상 로드 (SharedDataTest 등)

```text
[Framework] Shared game data loaded. Towns: 2, Markets: 2, ...
[SharedDataTest] SharedGameDataLoaded: Towns: 2, Markets: 2, ...
[SharedDataTest] LoadCompleted
```

신규 ID가 반영되었는지 요약에서 `BaseCamp`, `Apple`, `BaseRoute` 등을 확인한다.

### 5.2 Editor — Sandbox 미등록 Warning (정상 허용)

카탈로그가 `02.Data`만 등록한 상태면 Sandbox SO마다 Warning이 날 수 있다.

```text
[Framework] Shared game data asset is not registered in catalog: Assets/99.Sandbox/_LJH/02.SO/... (TownData, id=dummytown, root=SandboxLegacy)
```

Editor에서는 **진입을 막지 않는다.**

### 5.3 Editor — ProjectData 미등록 Warning (의도 테스트)

카탈로그에서 `TradeItem_Apple` 등을 잠시 제거한 뒤 Play:

```text
[Framework] Shared game data asset is not registered in catalog: Assets/_Project/02.Data/01_ScriptableObjects/TradeItem_Apple.asset (TradeItemData, id=Apple, root=ProjectData)
```

Editor: Warning만, 진입 허용.  
Player(동일 상태가 inventory에 반영된 빌드): Error + InGame 차단.

### 5.4 DraftAnimal IncreaseMaxLoad Warning (기존 SO 데이터)

Horse/Donkey SO에 `IncreaseMaxLoad > 0`이면 기존 계약상 Warning만 출력된다. drift와 별개이다.

```text
[Framework] DraftAnimalData 'Horse' IncreaseMaxLoad is ignored because draft animals must not increase physical maximum load.
```

### 5.5 Inventory refresh

메뉴 `ND/Framework/Refresh Shared Game Data Watch Inventory` 성공 시:

```text
[Framework] SharedGameDataWatchInventory refreshed.
```

### 5.6 Player — inventory 누락

```text
SharedGameDataWatchInventory resource was not found. Player catalog drift check cannot run. Refresh inventory before building.
```

→ InGame 진입 차단.

### 5.7 컴파일 실패 시 (수정 전)

```text
error CS8083: An alias-qualified name is not an expression.
```

`nameof(global::...)` 6곳 — 수정 완료 상태여야 한다.

---

## 6. 간단 테스트 절차

1. `Assets/_Project/11.CoreServices/Scenes/SharedDataTest.unity` Play  
   → 로드 성공, Error 없음, 신규 seed 요약 확인  
   → Sandbox Warning은 허용
2. Resources 카탈로그에서 ProjectData SO 하나 제거 후 Play  
   → ProjectData drift Warning, 진입은 허용  
   → 테스트 후 다시 등록
3. `ND/Framework/Refresh Shared Game Data Watch Inventory` 실행  
   → refresh 로그 + inventory 에셋 내용 확인
4. (선택) `ND/Framework/Run M1 Loop + Economy E2E Checks`  
   → `BaseRoute` / `Apple` 기준 통과 확인

---

## 7. 팀원에게 전달할 운영 규칙

1. 새 공용 SO는 `02.Data/01_ScriptableObjects`에 추가한다.
2. 반드시 `Resources/SandboxSharedGameDataCatalog`에도 등록한다.
3. Player 빌드 전 `Refresh Shared Game Data Watch Inventory`를 실행한다.
4. Sandbox `02.SO` 미등록 Warning은 공존 기간 동안 정상일 수 있다.
5. 런타임 카탈로그는 Resources 하나뿐이다. `02.Data`에 catalog 복제본을 두지 않는다.

---

## 8. 남은 리스크

| 리스크 | 설명 |
|--------|------|
| Inventory stale | refresh 없이 빌드하면 신규 ProjectData SO drift를 Player가 못 잡음 |
| Sandbox Warning 소음 | Editor/Player에서 Sandbox 미등록 Warning이 계속 날 수 있음 (의도) |
| IncreaseMaxLoad Warning | Horse/Donkey SO 값에 따른 기존 Warning |
| 타 feature의 dummy* ID | Framework 밖 코드가 옛 더미 ID를 쓰면 별도 수정 필요 |

---

## 9. 관련 경로 빠른 참조

| 항목 | 경로 |
|------|------|
| Runtime catalog | `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset` |
| Watch inventory | `Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset` |
| Seed SO | `Assets/_Project/02.Data/01_ScriptableObjects/` |
| Legacy SO | `Assets/99.Sandbox/_LJH/02.SO/` |
| Drift checker | `Assets/_Project/11.CoreServices/Editor/SharedGameDataCatalogDriftChecker.cs` |
| 로드·검증 | `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataService.cs` |
| 테스트 씬 | `Assets/_Project/11.CoreServices/Scenes/SharedDataTest.unity` |
