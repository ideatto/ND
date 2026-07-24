# 2026-07-24 충돌 교정 및 Market Integration 수정 기록

## 작업 정보

- 작업 브랜치: `featuel/conflictresolution/part2/jjh`
- 기준 브랜치: `dev2`
- 기준 커밋: `be4efe9`
- 작업 목적:
  - 구버전 병합 과정에서 원복된 데이터 구조와 다중 상단 진행 연결을 최신 구조로 복구한다.
  - 시장 구매가 출발 및 정산 과정에서 중복 반영되는 Market Inventory Integration Probe 실패를 수정한다.

## 변경 파일

### 1. `RouteEventData.cs`

- 경로:
  - `Assets/99.Sandbox/_LJH/01.Script/Runtime/Data/Calculation/RouteEventData.cs`
- 원천 소유자:
  - `ljh-ccc`
- 원천 소유 근거:
  - Git 최초 작성 커밋 `26fb3b7`

#### 수정 이유

구버전 파일에는 최신 전투 이벤트에서 사용하는 산적 전투력, 일반 화물 약탈 비율, 여물 약탈 비율이 없었다. 또한 효과가 없는 경로 이벤트 결과를 표현하는 `RouteEvent.None`과 기존 Unity 직렬화 데이터를 보호하는 명시적 enum 값이 필요했다.

#### 변경 내용

- `banditCombatPower`를 추가했다.
- `cargoLootRate`와 `fodderLootRate`를 추가했다.
- 신규 산적 전투력이 설정되지 않은 기존 에셋은 `eventValue`를 사용하는 `BanditCombatPower` 호환 접근자를 추가했다.
- 약탈 비율을 `0~1` 범위로 제한하는 `CargoLootRate`, `FodderLootRate` 접근자를 추가했다.
- Inspector 구성을 위한 `Header`, `Tooltip`, `Min`, `Range` 속성을 추가했다.
- 기존 직렬화 값을 유지하도록 enum 값을 명시했다.
  - `Combat = 0`
  - `Lucky = 1`
  - `Weather = 2`
  - `None = 3`

#### 호환성

- 기존 에셋의 `Combat`, `Lucky`, `Weather` 값은 바뀌지 않는다.
- 기존 에셋에 `banditCombatPower`가 없으면 `eventValue`가 호환 값으로 사용된다.
- 신규 약탈 비율은 기존 에셋에서 기본값 0으로 시작하므로 이전 동작을 강제로 변경하지 않는다.

### 2. `TradeProgressCoordinator.cs`

- 경로:
  - `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`
- 원천 소유자:
  - `csu1222`
- 원천 소유 근거:
  - Git 최초 작성 커밋 `97b753c`

#### 수정 이유

구버전 흐름은 `SaveData.caravan`, `SaveData.tradeProgress`, `SaveData.pendingSettlement` 호환 접근자를 중심으로 선택된 상단 하나만 처리했다. 현재 SaveData version 6은 `caravans`, `tradeProgressEntries`, `pendingSettlements`를 원본 데이터로 사용하는 다중 상단 구조이므로, 구버전 흐름에서는 선택되지 않은 상단의 온라인 진행, 오프라인 복구 및 정산 연결이 누락될 수 있었다.

#### 변경 내용

- `tradeProgressEntries` snapshot을 순회해 모든 Traveling 상단을 처리하도록 변경했다.
- 온라인 및 오프라인 진행 처리를 `TryProcessTravelingEntry`로 통합했다.
- `progress.caravanId`로 runtime caravan과 저장 대상 `CaravanSaveData`를 조회하도록 변경했다.
- 상단별 정산 로직을 `SettleTrade`로 분리했다.
- 정산 결과에 `caravanId`를 기록하고 `pendingSettlements`에 추가하도록 변경했다.
- 동일한 `caravanId + tradeId`의 중복 정산 생성을 차단했다.
- 같은 갱신 주기에 여러 상단이 도착할 수 있도록 `SettlementNotification`을 모은 뒤 저장 후 이벤트를 발행하도록 변경했다.
- 잘못된 오프라인 시간 범위를 검증하고 항목별 실패가 다른 상단의 복구를 중단하지 않도록 처리했다.
- runtime caravan을 동일 ID의 저장 데이터에 복사하도록 연결했다.
- 경로 이벤트 자동 처리와 강제 처리 흐름을 `TradeRouteEventProcessor`로 복원했다.

#### 영향 범위

- 다중 상단 온라인 진행
- Continue/Load 오프라인 진행 복구
- 경로 이벤트 처리
- 도착 정산 생성
- pending settlement 저장
- runtime caravan과 save caravan 매핑
- 정산 준비 및 화면 전환 이벤트

### 3. `TradePrepareStartAdapter.cs`

- 경로:
  - `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareStartAdapter.cs`

#### 수정 이유

`MarketInventoryIntegrationProbe`의 `VerifyDepartureCommitExcludesMarketSettlement` 검사가 실패했다.

실패 메시지:

```text
Departure commit must preserve the selected Caravan ID and exclude already
committed market purchases and automatic sale revenue.
```

시장 거래는 출발 전에 이미 SaveData의 화폐, 시장 재고, caravan cargo에 확정된다. 그러나 출발 커밋이 `totalPurchaseCost`, `estimatedSellRevenue`, `selectedBuyItems`를 다시 저장해 정산 단계에서 같은 구매 또는 판매 금액이 중복 반영될 가능성이 있었다.

#### 변경 내용

- 출발 커밋의 `purchaseCost`를 `0L`로 설정했다.
- 출발 커밋의 `estimatedSellRevenue`를 `0L`로 설정했다.
- 출발 커밋의 `purchasedItems`를 빈 배열로 설정했다.
- 선택된 `caravanId`는 그대로 유지했다.
- 시장 거래와 무관한 `foodCost`, `mercenaryCost`는 그대로 유지했다.

#### 변경 후 책임 분리

- 시장 구매:
  - Market transaction이 즉시 SaveData와 화폐에 반영한다.
- 출발 커밋:
  - 선택한 상단, 경로, 마차, 동물, 용병 및 비시장 출발 비용만 보관한다.
- 도착 판매:
  - 실제 도착 시점의 확정 cargo를 기준으로 정산한다.

## 검증 결과

- `Assembly-CSharp.csproj` 빌드:
  - 오류 0개
  - 기존 경고 35개
- `git diff --check`:
  - 통과
- 충돌 마커:
  - 없음
- 변경 범위:
  - 위 C# 파일 3개와 이 변경 기록 문서

## 추가 확인 방법

Unity Editor에서 다음 메뉴를 실행해 Market Inventory Integration Probe를 다시 검증한다.

```text
Tools > ND > Validation > Run Market Inventory Integration Probe
```

성공 시 다음 결과 파일의 `success` 값이 `true`로 갱신된다.

```text
Temp/market-integration-test-result.json
```

## 남은 확인 사항

- Unity Test Runner 환경에서 관련 EditMode 테스트를 실행한다.
- 원천 소유자 또는 현재 유지보수자에게 변경 내용을 리뷰받는다.
- 동시 이동 중인 여러 상단의 온라인 진행과 오프라인 복구를 PlayMode에서 확인한다.
