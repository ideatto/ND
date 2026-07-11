# develop 3cycle E2E 결과

**일자:** 2026-07-11  
**브랜치:** `chore/integration/develop-3cycle-smoke` @ `d75aa02` (origin/dev2 동일)  
**검증 범위:** Framework M1 — loop 3회 + Economy settle preview / claim apply

---

## Preflight

| 항목 | 결과 |
|------|------|
| `dev2` / origin 동기화 | Pass — HEAD `d75aa02` (PR #64 병합) |
| Build Settings 첫 씬 | Pass — `Assets/_Project/07.Scenes/01_Boot/Boot.unity` |
| `SandboxSharedGameDataCatalog` | Pass — `dummyroute`, `dummyitem` 등록 |
| InGame `TradeTestCaravan` | Pass — harness `routeId=dummyroute`, `SaveDataDebugPrinter` 부착 |

---

## Phase A — Loop Integrity Smoke

| 방법 | 결과 | 비고 |
|------|------|------|
| Play mode ContextMenu `Framework/Run M1 Loop Integrity Smoke` | **Pass** | Boot flow → InGame 후 실행 |
| Editor 자동 `ND/Framework/Run M1 Loop + Economy E2E Checks` | **Pass** | Unity Editor 메뉴 실행 |

**자동화 추가:** [`FrameworkM1LoopE2EEditorTests.cs`](../../Assets/_Project/11.CoreServices/Editor/FrameworkM1LoopE2EEditorTests.cs) — Play mode smoke와 동일한 3 cycle integrity 검증.

**성공 로그:**
```text
[Framework M1 E2E] Loop integrity smoke completed 3 consecutive trade cycles.
```

---

## Phase B — Economy E2E (3 cycle)

| Cycle | tradingCurrency (before) | after settle | after claim | 결과 |
|-------|--------------------------|--------------|-------------|------|
| 1 | 1000 | 1000 | 변경됨 | **Pass** |
| 2 | cycle1 claim 후 | settle 불변 | 변경됨 | **Pass** |
| 3 | cycle2 claim 후 | settle 불변 | 변경됨 | **Pass** |

**Pass 기준**
- settle 후 `tradingCurrency` 불변 (preview only)
- claim 후 `tradingCurrency` 변화
- 3회 후 음수 재화 없음
- `Economy M1 settlement preview skipped` / `claim apply did not complete` warning 0회

**자동화 추가**
- Editor: `FrameworkM1LoopE2EEditorTests.RunEconomyE2E`
- Play mode: [`TradeStartDebugHarness.RunEconomyE2ESmoke`](../../Assets/_Project/11.CoreServices/Scripts/Debug/TradeStartDebugHarness.cs) — ContextMenu `Framework/Run Economy E2E Smoke`

---

## Economy warnings

- **none** — Editor 메뉴·Play mode smoke 모두 Economy skip/failure warning 없음

---

## Blockers

- 없음

---

## 실행 방법 (팀 공유)

### A. Editor 자동 (권장)

1. Unity Editor에서 프로젝트 단독 실행 (batchmode 사용 시 Editor 종료)
2. 메뉴: **ND → Framework → Run M1 Loop + Economy E2E Checks**
3. Console에서 `[Framework M1 E2E] All checks passed.` 확인

**batchmode (CI/스크립트):**
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe" `
  -batchmode -nographics -quit `
  -projectPath "D:\CS_Project\ND" `
  -executeMethod ND.Framework.Editor.FrameworkM1LoopE2EEditorTests.RunAllFromBatchMode `
  -logFile "D:\CS_Project\ND\Logs\framework-m1-e2e.log"
```

### B. Play mode (Boot flow)

1. Play 시작 씬: **Boot**
2. Title → **New Game** → InGame
3. `TradeTestCaravan` 선택
4. `Framework/Run M1 Loop Integrity Smoke`
5. `Framework/Run Economy E2E Smoke` (또는 Phase B 수동 Step 1~8 × 3)

---

## 코드 변경 요약

| 파일 | 변경 |
|------|------|
| `Assets/_Project/11.CoreServices/Editor/FrameworkM1LoopE2EEditorTests.cs` | 신규 — Editor/batchmode 3cycle + Economy E2E |
| `Assets/_Project/11.CoreServices/Scripts/Debug/TradeStartDebugHarness.cs` | `RunEconomyE2ESmoke()` ContextMenu 추가 |

**미포함 (의도적):** Scene/Prefab YAML, `03.Economy`, `01.Core`, `PurchaseGrowth` 활성화

---

## 알려진 limitation (M1)

- `PurchaseGrowth = false` — 성장 구매 E2E는 Progression/UI 담당
- Economy 입력: 첫 cargo 1품목만
- `PendingSettlementSaveData` 재실행 복구: M3 범위

---

## 다음 단계

- [x] Unity Editor·Play mode에서 Phase A/B 실행 완료
- [x] Pass 확인 후 `dev2` PR handoff
