# Atomic Claim · Town Routing — Economy Handoff

- 작성일: 2026-07-21
- 작성: Framework & Integration (CSU)
- 대상: Economy / Progression
- 브랜치: `feature/framework/atomic-claim-town-routing`
- 기준 브랜치: `dev2`
- 구현 로직: `Docs/Personal_Documents/CSU/0721_atomic_claim_town_routing.md`

---

## 1. 이 handoff의 목적

Framework가 claim을 **원자 저장 + 마을 도착**으로 바꿨다.  
Economy가 이어서 작업할 때 필요한 **공개 API 계약**, **깨지면 안 되는 호출 순서**, **Town/`currentTownId` 전제**를 정리한다.

Economy가 당장 구현해야 하는 새 계산기가 필수는 아니다.  
다만 **마을 단위 상점·가격·성장·대출 UI**를 claim 이후 흐름에 붙일 때 아래 계약을 전제로 해야 한다.

---

## 2. 쉬운 요약

### 바뀐 것

1. Claim 성공 → `player.currentTownId` = 목적지 town
2. Claim 성공 → 화면 상태 `Town` (`Preparation` 아님)
3. Claim 중 Economy 화폐 반영 실패 → **전체 claim rollback**
4. Save 실패 → **전체 claim rollback** (Settlement 유지, 재시도 가능)

### Economy가 기억할 것

- Settle 단계에서는 여전히 **화폐를 바꾸지 않는다** (preview만).
- Claim 단계에서만 `TryApplyPendingEconomy`로 화폐·stat을 반영한다.
- Apply가 false면 Framework가 SaveData/caravan을 되돌리므로, **부분 적용을 남기면 안 된다**.
- Town 화면 진입 후 상점·성장 API는 **`SaveData.player.currentTownId`** 를 현재 위치로 사용한다.

---

## 3. Framework 공개 API

### 3.1 Claim 진입점

| API | 위치 | 설명 |
|---|---|---|
| `FrameworkRoot.ClaimSettlementAndReset()` | Bootstrap | UI/외부용 facade. Bridge 중복 클릭 가드 포함 |
| `SettlementUiBridge.ClaimSettlementAndReset()` | UI Bridge | pending cache 검증 후 Coordinator 위임 |
| `TradeProgressCoordinator.ClaimSettlementAndReset()` | Coordinator | 실제 원자 claim 구현 |
| `SettlementUiDataAdapter.OnClickClaimSettlement()` | UI Adapter | Claim 버튼 entry |

**반환값**

- `true`: destination 검증, Economy apply, 상태 기록, commit 정리, Save, Town 전환까지 모두 성공
- `false`: 검증 실패, apply 실패, save 실패, 중복 claim 등. **호출 전 상태로 복구되었거나 변경되지 않음**

### 3.2 화면·위치 조회

| API / 필드 | 설명 |
|---|---|
| `SaveData.player.currentTownId` | claim 성공 후 도착 town ID. 재실행에도 유지 |
| `InGameScreenState.Town` | 도착 후 인게임 화면 enum |
| `InGameScreenStateRouter.MapFromSaveData(saveData)` | 로드/재실행 시 Town 복원 규칙 |
| `InGameScreenStateRouter.CurrentScreenState` | 런타임 현재 화면 |
| `FrameworkEvents.InGameScreenChanged` | 화면 변경 이벤트. 인자: `InGameScreenState` |

Town 판정 조건 (`MapFromSaveData`):

```text
tradeProgress.state ∈ { Completed, Failed }
AND pendingSettlement 정리됨
AND tradePreparationCommit 정리됨
AND currentTownId 비어 있지 않음
→ Town
```

### 3.3 Trade Prepare Commit (claim 전제)

Claim은 아래 commit이 **같은 tradeId로 stage**되어 있어야 한다.

| Interface | 메서드 | 역할 |
|---|---|---|
| `ITradePrepareCommitSink` | `TryStage(TradePrepareCommitData)` | 출발 확정 시 기록 |
| `ITradePrepareCommitSource` | `TryGet(tradeId, out commit)` | claim destination 조회 |
| `ITradePrepareCommitCompletion` | `TryComplete(tradeId, out commit)` | claim 성공 시 commit 제거 |

구현체: `FrameworkTradePrepareCommitStore` (`FrameworkRoot.TradePrepareCommitStore`)

Economy/정산 입력에 필요한 commit 필드 (최소):

```csharp
TradePrepareCommitData
{
    string tradeId;
    string currentTownId;                 // 출발 마을
    string selectedDestinationTownId;     // 도착 마을 (claim이 이 값을 currentTownId에 씀)
    string routeId;                       // SharedGameData route와 일치해야 함
    // 그 외 구매/식량/용병 비용 등은 Economy settle 입력에 사용
}
```

**필수 불변식**

```text
commit.selectedDestinationTownId == SharedGameData.GetRoute(activeRouteId).ToTownId
```

불일치 시 claim은 시작되지 않는다.

### 3.4 Economy Bridge (Framework 내부, 계약만 공유)

| 메서드 | 시점 | SaveData 화폐 변경 |
|---|---|---|
| `EconomyM1SettlementBridge.TryCalculateAndFill(...)` | settle / restore | **없음** (JourneyResult 금액 채움 + pending cache) |
| `EconomyM1SettlementBridge.TryApplyPendingEconomy(...)` | claim | **있음** (Currency/stat 반영) |

입력 조립: `FrameworkEconomyM1InputBuilder.TryBuild(...)`  
계산기: `EconomyM1LoopCalculator.Execute(input)`  
반영: `RuntimeStatsSaveDataMapper.ApplyEconomyResult(...)`

**중요 변경:** `TryApplyPendingEconomy`가 false이면 claim 전체가 rollback된다.  
이전처럼 “Economy 실패해도 claim은 진행”하지 않는다.

---

## 4. Claim 시퀀스 (Economy 관점)

```text
[Settle]
  Core JourneyResult 생성
  → TryCalculateAndFill  (currency 변경 금지, UI preview만)
  → pendingSettlement 저장
  → Screen = Settlement

[Claim 버튼]
  → destination 검증 (commit ↔ route)
  → snapshot
  → Core ClaimSettlement
  → TryApplyPendingEconomy   ← 여기서만 화폐 확정
  → Completed/Failed 기록
  → caravan ResetToPrepare
  → player.currentTownId = destination
  → pending/commit clear
  → Save (실패 시 전부 rollback)
  → Screen = Town
```

Economy가 claim 경로에서 추가 작업을 넣을 경우:

1. **Save 성공 전**에 stage할 것 (Framework와 같은 원자 단위)
2. 실패 시 Framework rollback과 충돌하지 않도록 **부작용을 SaveData/runtime에만** 둘 것
3. 외부 서비스·파일 I/O를 claim 중간에 넣지 말 것

---

## 5. Economy 후속 작업에 필요한 내용

### 5.1 Town 기준 데이터 접근

Claim 이후 플레이어 위치는 `SaveData.player.currentTownId`이다.

권장:

```csharp
var townId = saveData.player.currentTownId;
if (string.IsNullOrWhiteSpace(townId))
{
    // Town 화면이 아니거나 claim 전. 상점/성장 진입 금지 또는 Preparation 폴백
}

if (!sharedGameData.TryGetTown(townId, out var town)) { /* catalog 오류 */ }
```

마을별 상점·시세·기부/투자/대출 UI는 **이 townId**를 키로 사용한다.  
출발 전 commit의 `currentTownId`(출발지)와 claim 후 `player.currentTownId`(도착지)를 혼동하지 말 것.

### 5.2 Town 화면 진입 타이밍

| 구독 | 용도 |
|---|---|
| `FrameworkEvents.InGameScreenChanged` | `Town`일 때 마을 UI/Economy panel 활성화 |
| `FrameworkRoot` 로드 후 `RefreshFromSaveData` | 재실행 시 Town 복원 |

Town UI가 아직 없으면:

- Framework는 상태만 `Town`으로 올린다
- Economy/UI는 이벤트를 구독해 panel을 붙이면 된다
- Claim 성공을 “Preparation 복귀”로 가정하는 코드는 **수정 필요**

### 5.3 화폐·정산 계약 (유지 + 강화)

유지:

- Settle: currency 불변
- Claim: `FinalCurrencyState` / runtime stat 반영
- tradeId로 pending economy cache 일치 검사

강화:

- Apply 실패 = claim 실패 (rollback)
- 따라서 Economy apply는 **idempotent에 가깝게**, 또는 실패 시 변경을 남기지 않게 구현해야 한다
- `RuntimeStatsSaveDataMapper.ApplyEconomyResult`가 중간에 실패하면 false만 반환하고 부분 기록을 남기지 않는지 재확인 권장

### 5.4 구조 대출·성장과의 연결

- Claim 후 위치는 도착 town이다. 마을 한정 콘텐츠(기부, 투자, 성장 구매)는 `currentTownId`로 필터한다.
- 구조 대출 제한 모드(`rescueLoan.isRestrictedPreparation`)는 **다음 출발 준비**와 관련되며, Town 체류 UX와 별개다.
- Claim 원자 단위에 대출 상환을 넣으려면 Framework와 별도 합의가 필요하다. **현재 claim은 대출 상환을 포함하지 않는다.**

### 5.5 SharedGameData / Route

Economy settle 입력도 route를 쓴다 (`FrameworkEconomyM1InputBuilder`).

주의:

- `activeRouteId`는 catalog에 실제 존재하는 ID여야 한다 (예: `BaseToRiver`, `RiverToMount`)
- 테스트/디버그의 `BaseRoute` 같은 placeholder는 claim destination 검증에서 거부된다
- route의 `ToTownId`가 commit destination과 같아야 한다

### 5.6 저장 결과

Framework claim은 `SaveResult.Succeeded`를 검사한다.  
Economy 쪽 별도 저장 경로를 만들 때도 동일 패턴을 맞출 것:

```csharp
var result = saveService.Save(saveData);
if (result == null || !result.Succeeded)
{
    // stage 원복. UI 성공 처리 금지
}
```

---

## 6. Breaking / 호환성

| 항목 | 영향 |
|---|---|
| claim 후 화면 `Preparation` → `Town` | Settlement UI “닫으면 준비 화면” 가정 코드 수정 필요 |
| Economy apply 실패 시 claim 중단 | 금액 0/실패 preview여도 claim이 false일 수 있음. UI는 `CanClaim`과 실제 claim 결과를 모두 확인 |
| commit 미stage 시 claim 불가 | Trade Prepare가 `TryStage`를 빼먹으면 claim이 영원히 막힘 |
| destination ≠ route.ToTownId | claim 거부. Prepare UI가 route와 destination을 같이 확정해야 함 |

구 세이브:

- claim 이전 세이브에 `currentTownId`가 비어 있으면 Completed/Failed여도 Town이 아니라 Preparation으로 폴백한다
- 정상 신규 claim 후에는 `currentTownId`가 채워지므로 Town 복원된다

---

## 7. 검증 방법 (Economy 연동 확인용)

1. Editor: `ND/Framework/Run M1 Loop + Economy E2E Checks`
2. 로그 확인 포인트:
   - `Atomic claim: save-failure rollback restored staged values.`
   - `normal claim succeeded. currentTownId=RiverTown, Town event raised.`
   - `duplicate claim correctly rejected.`
   - `relaunch restored Town screen from save data.`
   - Economy cycle: settle 시 currency 불변, claim 후 currency 변화
3. Play Mode 수동 확인 시 debug harness의 `routeId`를 catalog 실제 ID로 맞출 것

---

## 8. Economy 체크리스트 (후속 작업용)

- [ ] Town 화면/`InGameScreenChanged(Town)` 구독으로 마을 Economy UI 진입점 연결
- [ ] 상점·시세·성장 API가 `player.currentTownId`를 현재 위치로 사용하는지 확인
- [ ] Claim 성공을 Preparation 복귀로 가정한 UI/테스트 수정
- [ ] `TryApplyPendingEconomy` / `ApplyEconomyResult`가 실패 시 부분 화폐 변경을 남기지 않는지 재확인
- [ ] Trade Prepare `TryStage`의 `selectedDestinationTownId`와 route `ToTownId` 일치 보장 (UI/어댑터)
- [ ] (선택) Town 도착 후 마을별 이벤트·세금·수수료가 필요하면 Framework event 추가를 별도 요청

---

## 9. 관련 파일

| 영역 | 경로 |
|---|---|
| 원자 claim | `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs` |
| Economy bridge | `Assets/_Project/11.CoreServices/Scripts/TradeProgress/EconomyM1SettlementBridge.cs` |
| Settle 입력 | `Assets/_Project/11.CoreServices/Scripts/TradeProgress/FrameworkEconomyM1InputBuilder.cs` |
| Commit store | `Assets/_Project/11.CoreServices/Scripts/TradeProgress/FrameworkTradePrepareCommitStore.cs` |
| Screen router | `Assets/_Project/11.CoreServices/Scripts/SceneFlow/InGameScreenStateRouter.cs` |
| Root 조립 | `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs` |
| E2E | `Assets/_Project/11.CoreServices/Editor/FrameworkM1LoopE2EEditorTests.cs` |

---

## 10. 연락 / 범위 밖

- Town panel Prefab·시각 구현: UI 담당 (Framework는 상태/이벤트만)
- 신규 Economy 계산식 변경: 이 브랜치 범위 밖. 계약 변경 시 Framework bridge 재합의
- Claim에 대출 상환·기부·투자를 묶는 원자 트랜잭션: 아직 없음. 필요 시 별도 설계
