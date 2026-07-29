# dev2 대비 feature/soldierdata/marketdata/jjh 병합 기록

작성일: 2026-07-28  
저장소: `ideatto/ND`  
Pull Request: `#263`  
PR URL: <https://github.com/ideatto/ND/pull/263>

## Purpose

- 기존 ND 통합 브랜치인 `dev2`와 현재 작업 브랜치
  `feature/soldierdata/marketdata/jjh` 사이의 차이를 병합 기준으로 기록한다.
- 다중 Caravan 무역, 시장 Cargo, 도착 판매, 정산 연결 과정에서 변경된
  데이터 소유권과 ID 연결 방식을 다음 병합 및 리뷰에서 추적할 수 있게 한다.
- Scene과 Prefab을 포함하는 변경이므로 코드 컴파일 성공과 실제 UI 연결 검증을
  구분해 남긴다.

## Comparison Baseline

| 구분 | 값 |
|---|---|
| 이전 ND 기준 브랜치 | `dev2` |
| 이전 ND 기준 커밋 | `37ff6e7666342bcb983e8d3ea6ac7daf1bf2bc92` |
| 현재 작업 브랜치 | `feature/soldierdata/marketdata/jjh` |
| 현재 작업 커밋 | `efdf50870ac4d46749ef24eb9a53435ad27503b2` |
| Merge base | `36e5dd35a930ad866fd137b7b5aee30e820f07f3` |
| PR 상태 | Open |
| Merge 가능 여부 | GitHub 기준 mergeable |
| 변경 커밋 | 3개 |
| 변경 파일 | 34개 |
| 추가/삭제 | `+4,976 / -3,529` |

GitHub compare 기준으로 작업 브랜치는 `dev2`보다 3커밋 앞서고 8커밋 뒤처진
`diverged` 상태이다. PR의 base는 2026-07-28에 실수로 지정된 `main`에서
`dev2`로 변경했다.

## Commit History

### `d8712ac` - refactoring multiple caravans bug fix

- 작성자: `junghen001-oss`
- 작성 시각: 2026-07-27 12:59:59 +09:00
- 다중 Caravan 시장 Cargo draft를 `marketId + caravanId` 기준으로 분리했다.
- 출발 준비 snapshot, 도착 판매, pending settlement, 정산 영수증 연결을
  Caravan과 trade ID 기준으로 보완했다.
- 저장 실패 시 시장 재고, Cargo, 화폐 및 추가 정산 데이터를 함께 롤백하도록
  transaction 경계를 정리했다.
- `CaravanCargoDraftStore.cs`와 `.meta`를 추가했다.
- InGame Scene과 Main UI Prefab의 실제 연결을 갱신했다.

### `b4036a3` - Merge remote-tracking branch `origin/dev2`

- 작성자: `junghen001-oss`
- 작성 시각: 2026-07-28 11:10:16 +09:00
- `dev2`의 다중 진행, route event, 정산 identity, Caravan Overview 변경을
  작업 브랜치로 병합했다.
- 충돌 해결 과정에서 `dev2`의 명시적 `caravanId + tradeId` 검증과 작업 브랜치의
  시장/Cargo 연결을 함께 유지했다.
- Route 및 Town 데이터, SharedGameData catalog, Framework E2E 테스트와 관련
  계약 문서를 포함했다.

### `efdf508` - Consolidated Summary

- 작성자: `junghen001-oss`
- 작성 시각: 2026-07-28 11:11:31 +09:00
- 공용 무역 버튼이 전역 Traveling 화면 상태 때문에 차단되지 않고 Caravan 선택
  패널을 열도록 수정했다.
- 선택 패널에서는 `JourneyState.Prepare` 상태의 Caravan만 선택 가능하게 유지했다.
- 도착 판매 완료 후 현재 선택 Caravan을 다시 매핑하지 않고 정산 화면을 직접
  표시하도록 변경했다.
- 가격 배율 계산에서 `179.999999...` 형태의 부동소수점 오차를 보정한 뒤
  floor 정책을 적용하도록 수정했다.
- 모든 활성 Caravan의 지도 진행 snapshot을 조회할 수 있게 하고 snapshot에
  `CaravanId`를 포함했다.

## Scope

### 주요 변경 영역

- Trade Prepare UI 및 Runtime binding
- Caravan별 Cargo draft와 시장 재고 예약
- 시장 transaction과 저장 실패 rollback
- 도착 판매와 pending settlement 연결
- Economy M1 정산 입력 및 영수증 표시
- 다중 Caravan 진행 및 월드맵 snapshot
- Town 무역 버튼과 Caravan 선택 패널
- InGame Scene 및 Main UI Prefab 연결

### 대표 변경 파일

- `Assets/Scripts/UI/CaravanCargoDraftStore.cs`
- `Assets/Scripts/UI/CargoLoadingPanelController.cs`
- `Assets/Scripts/UI/MarketInventoryIntegration.cs`
- `Assets/Scripts/UI/Market/MarketTradePanelController.cs`
- `Assets/Scripts/UI/Market/CaravanArrivalSaleController.cs`
- `Assets/Scripts/UI/TownTradePreparationEntryController.cs`
- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareUiRuntimeBinding.cs`
- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/FrameworkTradeScreenPresenter.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`
- `Assets/_Project/11.CoreServices/Scripts/UI/Settlement/SettlementUiDataAdapter.cs`
- `Assets/_Project/07.Scenes/04_InGame/InGame.unity`
- `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`

### 제외

- Package 설정 변경
- Enum 직렬화 숫자 변경
- 기존 SaveData 버전 번호 변경
- 사용자가 요청하지 않은 commit, push 또는 추가 PR 생성

## Ownership

- Framework/CoreServices 원천 이력: `csu1222`
  - 다중 trade progress, pending settlement, route event, claim identity
- Economy/Settlement 원천 이력: `junghen001-oss`
  - Economy M1 입력, 가격 계산, 정산 breakdown
- Trade Prepare/UI 원천 이력: `ljh-ccc`, `YHY`
  - TradePrepare 패널, Caravan 선택, Cargo/요약 UI
- Market/Arrival UI 현재 작업: `junghen001-oss`
  - 시장 transaction, Cargo draft, 도착 판매 및 정산 화면 연결
- 현재 유지보수자: 확인 필요

## Changes

### 1. Caravan별 Cargo draft

이전에는 열린 Cargo 패널의 로컬 상태가 다음 Caravan 선택에도 남아 Cargo와 시장
재고의 소유자가 섞일 수 있었다.

현재는 다음 키를 사용한다.

```text
marketId + caravanId
  -> CaravanCargoDraftStore
  -> Caravan별 미확정 구매 예약
```

- 다른 Caravan이 예약한 수량은 현재 Caravan의 구매 가능 재고에서 제외한다.
- 패널을 닫았다 다시 열어도 같은 Caravan draft를 복원한다.
- 실제 Cargo와 화폐 변경은 출발 transaction이 성공할 때만 확정한다.

### 2. 출발 transaction과 저장 경계

```text
Cargo 선택
  -> Market transaction 계산
  -> 시장 재고/Cargo/화폐 mutation
  -> 출발 준비 snapshot stage
  -> Save 성공
  -> 변경 이벤트 발행
```

- Save 실패 시 시장 재고, Cargo, 화폐와 추가 정산 데이터를 이전 snapshot으로
  되돌린다.
- 실패한 transaction은 Framework 변경 이벤트를 발행하지 않는다.
- 이미 Market에서 결제한 구매 비용은 Economy claim에서 다시 차감하지 않는다.
- 구매 비용과 구매 품목은 최종 영수증 복원을 위한 기록으로 유지한다.

### 3. 다중 Caravan 진행과 정산 identity

```text
caravanId
  -> TradeProgressSaveData

caravanId + tradeId
  -> TradePreparationCommitSaveData
  -> PendingSettlementSaveData
  -> Economy pending result
  -> Settlement claim
```

- 도착 판매 버튼과 정산 claim은 정확한 Caravan과 trade 조합만 처리한다.
- 하나의 pending settlement를 처리해도 다른 Caravan의 pending 데이터는 유지한다.
- 저장 실패 시 claim 완료나 정산 UI 성공 상태를 확정하지 않는다.

### 4. 무역 버튼과 Caravan 선택

- 공용 무역 버튼은 현재 선택된 Caravan이 Traveling이어도 Caravan 선택 패널을 연다.
- 버튼 진입 단계에서는 `selectedCaravanId`와 trade progress를 변경하지 않는다.
- 선택 패널은 최신 Caravan 옵션을 다시 읽는다.
- `JourneyState.Prepare`이며 유효한 현재 마을이 있는 Caravan만 선택할 수 있다.
- Traveling 또는 SettlementPending Caravan은 카드가 비활성화되며 선택 적용
  단계에서도 다시 거부된다.

### 5. 도착 판매 후 정산 화면

- 판매가 완료된 Caravan이 현재 전역 선택 Caravan과 다를 수 있다.
- 따라서 판매 완료 후 전역 화면 상태를 다시 매핑하지 않고 Framework가 검증한
  settlement 화면을 직접 연다.
- 표시와 claim은 판매를 완료한 `caravanId + tradeId`를 유지한다.

### 6. 가격 계산 보정

- 여러 배율의 부동소수점 곱셈 결과를 소수점 9자리에서 정규화한다.
- 정규화 이후 기존 floor 정책을 적용한다.
- 예: `100 × 1.2 × 1.5`가 내부적으로 `179.999999...`가 되어 179로 내려가는
  문제를 방지한다.

### 7. 다중 지도 진행 snapshot

- 기존 단일 `TryGetMapProgress` 호환 API는 유지한다.
- `GetMapProgressSnapshots()`로 모든 Traveling/SettlementPending 항목을 조회한다.
- 각 snapshot에 `CaravanId`를 포함해 월드맵 소비자가 진행 소유자를 식별할 수 있다.

## Check

검증 프로젝트: `C:\unity\ND`  
Unity 버전: `6000.5.2f1`

### C# 컴파일

```text
dotnet build Assembly-CSharp.csproj --no-restore
오류 0
기존 경고 35
```

### Unity EditMode

실행 시각: 2026-07-28 11:04 KST

```text
전체 200
성공 200
실패 0
Skip 0
Inconclusive 0
```

결과 파일:

`C:\Users\ADMIN\AppData\LocalLow\DefaultCompany\ND\TestResults.xml`

### Market Inventory Integration Probe

실행 시각: 2026-07-28 11:04 KST

```text
결과: Passed
검증 항목: 21
```

주요 검증:

- 시장 재고 refresh 저장 실패 rollback
- transaction 저장 실패 rollback
- 성공 transaction 이벤트 발행
- 실패 transaction 이벤트 억제
- 다른 Caravan draft의 공유 시장 재고 예약
- Cargo panel final quantity와 market delta 연결
- 출발 시 확정 Cargo 사용 및 구매 비용 중복 차감 방지
- 구매 영수증 snapshot 보존
- 목적지 시장 외부 품목 판매
- TradeFeature prefab runtime wiring

결과 파일:

`C:\unity\ND\Temp\market-integration-test-result.json`

### Diff 검사

```text
git diff --check
통과
```

## Risk

- Scene 변경: Yes
  - InGame Scene의 Trade/Market UI 연결이 크게 변경됐다.
- Prefab 변경: Yes
  - Main UI와 TradePrepare runtime context 연결이 변경됐다.
- Meta 변경: Yes
  - `CaravanCargoDraftStore.cs.meta`가 추가됐다.
- Package 변경: No
- SaveData 구조 변경: Yes
  - pending settlement에 구매 비용, 용병 비용, 판매 수익 및 품목별 영수증 정보가
    추가됐다.
- 직렬화 필드 변경: Yes
  - 기존 데이터에는 신규 영수증 필드가 기본값으로 역직렬화된다.
- Enum 직렬화 변경: No
- Public API 변경: Yes
  - Caravan/trade identity 기반 정산 API, 화면 직접 진입 API, 다중 지도 snapshot
    API가 추가됐다.
- 이벤트 연결 변경: Yes
  - 시장/Cargo/도착 판매/정산 UI 갱신 이벤트가 저장 성공 이후에만 확정된다.
- 다중 객체 또는 ID 연결 변경: Yes
  - `marketId + caravanId`, `caravanId + tradeId`가 주요 연결 기준이다.
- 기존 데이터 마이그레이션 필요: No
  - 신규 필드는 기본값으로 보정하며 기존 SaveData version을 유지한다.
- 원천 소유자 리뷰 필요: Yes
  - Framework, Economy, Trade Prepare, Market UI, Scene 및 Prefab 영역이 함께 변경됐다.

## Remaining

- 작업 브랜치는 현재 `dev2`보다 8커밋 뒤처져 있다. 최종 병합 전에 최신 `dev2`를
  다시 반영하고 200개 EditMode 테스트와 통합 Probe를 재실행해야 한다.
- 다음 실제 PlayMode 수동 시나리오를 확인해야 한다.
  1. Caravan A에 Cargo를 예약하고 패널을 닫았다 다시 열어 복원한다.
  2. Caravan B에서 A의 예약 수량이 시장 가용 재고에서 제외되는지 확인한다.
  3. Caravan A를 출발시킨다.
  4. 공용 무역 버튼으로 선택 패널을 다시 연다.
  5. Traveling인 A는 선택 불가이고 Prepare인 B는 선택 가능한지 확인한다.
  6. A와 B를 각각 도착·판매·정산하고 서로의 pending/영수증이 섞이지 않는지 확인한다.
  7. 게임을 종료하고 Continue 후 동일한 pending settlement가 복원되는지 확인한다.
- Scene과 Prefab 변경은 담당자 리뷰가 필요하다.
- 이 문서 작성 시점에는 추가 stage, commit, push를 수행하지 않았다.
