# Seasonal SellPrice · 구현 로직 정리

**작성일:** 2026-08-04  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/economy/seasonal-sell-price`  
**베이스:** `dev2` (`b6069e8`)  
**문서화 시점 상태:** 워킹 트리 미커밋 변경 기준 (커밋/PR 전)  
**Feature root:** `Assets/_Project/03.Economy/01_Market/`  
**판매 commit 경계:** `Assets/Scripts/UI/MarketInventoryIntegration.cs`

관련 계약·선행:

- [`Docs/Contract/Arrival_Sale_Settlement_Claim_Policy.md`](../../Contract/Arrival_Sale_Settlement_Claim_Policy.md) — 도착 판매·Claim 정책 (Seasonal SellPrice 절 포함)
- [`0731_game_calendar_and_seasons_logic.md`](./0731_game_calendar_and_seasons_logic.md) — 달력·`currentSeasonId` 권위
- [`JJH/0803_Market_Minimum_Stock_And_Price_Modifier_Work_Log.md`](../JJH/0803_Market_Minimum_Stock_And_Price_Modifier_Work_Log.md) — `PriceCalculator` / Modifier 연결 선행

---

## 1. 목적

시장 판매 **내구성 commit** 시점에, `TradeItemData`의 Season 계열 SellPrice modifier를  
현재 게임 계절(`saveData.world.currentSeasonId`)과 매칭해 단가·매출에 반영한다.

이번 작업이 하는 것:

1. **정책 명문화** — 도착 판매 계약에 Seasonal SellPrice 규칙·유예 범위를 고정
2. **선택기 분리** — `SeasonalSellPriceModifierSelector`가 Season SellPrice 자격만 판정
3. **산술은 기존 유지** — `PriceCalculator`가 단가 계산의 유일한 권위
4. **transaction 단일 캡처** — 한 번의 판매 commit에서 Season ID를 한 번만 읽고 모든 라인에 재사용
5. **Edit Mode 검증** — 선택기 규칙 + 실제 `MarketTransactionCommand` 경계(수익·rollback) 테스트

이번 작업이 하지 않는 것:

- Seasonal **BuyPrice** 적용
- 판매 패널 UI가 commit 단가와 동일하게 보이도록 맞추는 표시 정렬
- 거리 배율, 번개 잭팟, 흑자 한정 정산 보너스
- Claim 시점 재판매가 재산정
- SaveData 버전 변경·마이그레이션
- 출발 시점 계절을 판매가에 반영

---

## 2. 변경 파일 요약

| 영역 | 파일 | 역할 |
|------|------|------|
| Selector (신규) | `03.Economy/01_Market/SeasonalSellPriceModifierSelector.cs` | Season SellPrice 자격 필터 |
| Tests (신규) | `03.Economy/01_Market/Editor/SeasonalSellPriceTests.cs` | 선택기 + market commit 경계 |
| Sale commit | `Assets/Scripts/UI/MarketInventoryIntegration.cs` | commit 시 Season 캡처·선택기 적용 |
| Contract | `Docs/Contract/Arrival_Sale_Settlement_Claim_Policy.md` | Seasonal SellPrice / deferred 정책 |

`PriceCalculator`, `MarketTransactionCalculator`, `GameCalendarService`는 **동작 변경 없이** 기존 권위를 재사용한다.

---

## 3. 제품 위치 (도착 판매 흐름 안에서의 자리)

```text
Prepare → Traveling → Settle → SettlementPending
  → arrival cargo sale UI (draft, 비내구)
  → ConfirmSale / MarketTransactionCommand.Execute   ← 여기서 계절 SellPrice 확정
  → settlement presentation
  → Claim (여행 정산만, 아이템 판매 수익 재판매가 금지)
```

- 판매 수량 draft는 런타임 전용이다. 편집·나가면 버려질 수 있다.
- **확정 단가·매출**은 durable market sale transaction 안에서만 결정된다.
- Claim은 이미 commit된 아이템 판매 결과를 재사용하며, Claim 시점 계절로 다시 가격을 매기지 않는다.

---

## 4. 계절 식별자 계약

권위 상수는 `GameCalendarDate`에 있다.

| Season | 기술 ID (`SourceId` / `currentSeasonId`) |
|--------|------------------------------------------|
| Spring | `spring` |
| Summer | `summer` |
| Autumn | `autumn` |
| Winter | `winter` |

매칭 규칙:

- `StringComparison.Ordinal` (대소문자·문화권 무시 없음)
- `DisplayName` / `DisplayNameKey`는 **절대** 계절 식별자가 아님
- 비어 있거나, 공백, `Summer`, `SUMMER`, `fall`, `여름` 등 비정규 값은 **불일치**로 제외 (예외 throw 없음)

런타임 Season 소스:

```text
saveData.world.currentSeasonId
```

출발 계절·Claim 시점 계절은 판매가에 사용하지 않는다.

---

## 5. Modifier 선택 로직 (`SeasonalSellPriceModifierSelector`)

### 5.1 책임 경계

| 컴포넌트 | 책임 |
|----------|------|
| `SeasonalSellPriceModifierSelector` | “이 Season modifier를 이번 commit에 넣을지”만 결정 |
| `PriceCalculator` | 전달된 목록으로 Add/Percent/Multiply 산술·정렬·반올림 |
| `MarketInventoryMutationSession` | commit Season 캡처, 선택기 호출 여부, transaction 조립 |
| Game Calendar | `currentSeasonId` 유지 (판매 경로에서 달력을 직접 조회하지 않음) |

`PriceCalculator`는 전역 달력 상태를 읽지 않는다. 계절 컨텍스트는 호출측이 인자로 넣는다.

### 5.2 `SelectForSellPrice` 의사코드

입력: 변환된 `PriceModifierInput` 목록 + commit Season ID  
출력: 필터된 **새 리스트** (원본 목록을 파괴하지 않음)

각 modifier에 대해:

```text
null → skip

targetsSellPrice =
  Target == SellPrice OR Target == Both

if ModifierType != Season OR NOT targetsSellPrice:
  → 통과 (Town 등 비-Season, 또는 Season이지만 BuyPrice-only)

else (Season AND SellPrice/Both):
  if SourceId is canonical AND SourceId == currentSeasonId (Ordinal):
    → 통과
  else:
    → 제외
```

결과:

- 현재 계절과 맞는 Season SellPrice만 남김
- 다른 계절의 Season SellPrice는 제거
- BuyPrice-only Season modifier는 “SellPrice를 타깃하지 않음”으로 통과하지만, 이후 `PriceCalculator`가 Sell 단가에 적용하지 않음
- Town 등 비-Season modifier는 그대로 유지 → 기존 적용 순서·산술과 공존

### 5.3 산술 예시 (테스트 기준)

기준: Buy 100 / Sell 200, Summer commit

| 입력 | UnitSellPrice |
|------|----------------|
| Summer Season Sell +20% | 240 |
| Winter Season Sell +50% (비매칭) | 200 |
| Summer Season Buy +20% only | 200 (Sell 불변) |
| Town Sell +10 (비-Season 통과) | 210 |
| Summer +20% 후 Summer Add +10 (매칭만, 정렬 후) | 250 |

Percent: `price * (1 + value)`. 최종 단가는 AwayFromZero 반올림 후 최소 1.

---

## 6. 시장 판매 commit 연동 (`MarketInventoryMutationSession`)

### 6.1 `ResolveUnitPrices` 이중 API

```text
ResolveUnitPrices(item)
  → selectSeasonalSellPrice = false
  → preview / 비-transaction 경로
  → Season 필터 없음 (commit Season을 발명하지 않음)

ResolveUnitPrices(item, currentSeasonId, selectSeasonalSellPrice: true)
  → AffectModify면 Adapter로 modifier 변환
  → SeasonalSellPriceModifierSelector.SelectForSellPrice
  → PriceCalculator.CalculateUnitPrices
```

의도:

- UI preview가 “가짜 commit Season”을 만들어 표시와 정산이 어긋나는 것을 피함
- 실제 판매 확정만이 계절 필터를 켠다

현재 정책상 판매 패널 표시 정렬은 **유예**이므로, preview가 base(또는 비필터) 단가를 보여도 commit과 다를 수 있다. 계약 문서에 명시됨.

### 6.2 `ExecuteTransaction` 단일 캡처

라인 루프 **진입 직전**:

```csharp
string transactionSeasonId = saveData.world.currentSeasonId;
```

이후 각 판매 라인:

```text
SellUnitPrice = ResolveUnitPrices(item, transactionSeasonId, true).UnitSellPrice
BuyUnitPrice  = stock.unitPrice (기존 동적 재고 단가, Season Buy 미적용)
```

의미:

- 한 transaction 안의 모든 아이템이 **동일 commit Season**을 공유
- 루프 중 `currentSeasonId`가 바뀌어도 이번 commit 가격은 흔들리지 않음
- Buy 단가 경로는 이번 정책 범위 밖

### 6.3 이후 기존 transaction 경계 (변경 없음, 재확인)

```text
1. MarketTransactionCalculator로 검증·총액 계산
2. Cargo / market stock / tradingCurrency 사전 스냅샷
3. 변이 적용
4. (옵션) stageBeforeSave
5. Save
6. Save 실패 → Cargo·stock·currency·staged 전부 rollback, 성공 이벤트 없음
7. Save 성공 → CaravanCargoChanged, TradingCurrencyChanged
```

도착 판매 진입점: `CaravanArrivalSaleController.ConfirmSaleAndOpenSettlement` → `MarketTransactionCommand.Execute`.

판매 수익은 **sale-confirm Save 성공 시** 화폐에 반영된다. Claim이 아이템 판매 수익을 다시 지급하면 안 된다.

---

## 7. 정책과의 대응

| 정책 문구 | 구현 |
|-----------|------|
| SellPrice만 Season 적용 | Selector가 Season+Sell/Both만 필터; Buy-only Season은 Sell에 영향 없음 |
| commit 시점 Season | `transactionSeasonId = world.currentSeasonId` |
| Claim 재판매가 금지 | Claim 경로 미변경; 수익은 sale commit에 확정 |
| 정규 Season ID만 | `spring`/`summer`/`autumn`/`winter` + Ordinal |
| DisplayName 비식별 | Selector가 DisplayName을 보지 않음 |
| PriceCalculator 무달력 | Selector 전처리 후 Calculator 호출만 |
| SaveData 버전 불변 | 기존 `currentSeasonId` 필드 재사용 |
| 유예: UI 표시·Buy·거리·번개 등 | 코드/계약에 미배선, deferred 절로 명시 |

---

## 8. 테스트 맵 (`SeasonalSellPriceTests`)

### 선택기 + Calculator

| 테스트 | 검증 |
|--------|------|
| `MatchingSeason_AppliesSellModifierOnly` | 매칭 Season이 Sell만 변동 |
| `NonMatchingSeason_DoesNotChangeSellPrice` | 비매칭 제외 |
| `InvalidSeasonSourceId_IsExcludedWithoutThrowing` | null/공백/비정규 ID 안전 제외 |
| `DisplayName_DoesNotParticipateInSeasonMatching` | DisplayNameKey로 매칭 불가 |
| `BuyPriceOnlySeasonModifier_DoesNotAffectSellPrice` | Buy-only Season 무영향 |
| `NonSeasonModifier_PassesThroughUnchanged` | Town 등 통과 |
| `MultipleAndMixedSeasonModifiers_PreserveCalculatorOrderingForMatches` | 매칭만 남겨 Calculator 순서 유지 |

### Market commit 경계

| 테스트 | 검증 |
|--------|------|
| `MarketCommit_NonMatchingSeason_KeepsBaseSellRevenue` | Winter 월드 + Summer-only SO → 200×3=600 |
| `MarketCommit_UsesOneSeasonalSellPriceForRevenueAndMutations` | Summer → 240×3=720, 화폐·Cargo·stock·Save 일치 |
| `MarketCommit_SaveFailure_RollsBackCurrencyCargoStockAndStaging` | Save 실패 시 전체 rollback, 이벤트 미발행 |
| `MarketCommit_TwoSellLines_ReuseCapturedSeasonIdForEveryItem` | 두 라인 동일 Summer 단가 공유 |

---

## 9. 데이터 작성 가이드 (SO)

`TradeItemData`에 Season SellPrice를 넣으려면:

1. `AffectModify = true`
2. Modifier `modifierType = Season`
3. `sourceId` = `spring` / `summer` / `autumn` / `winter` (정확히 소문자 기술 ID)
4. Bundle target = `SellPrice` (또는 Both; Buy만이면 이번 정책에서 Sell에 안 먹음)
5. Operation = `Add` / `Percent` / `Multiply` (기존 Calculator 규칙)

잘못된 `sourceId`는 조용히 무시되며 base SellPrice로 판매된다.

---

## 10. 의존 관계 (읽기 전용)

```text
GameCalendarService
  └─ 저장/진행으로 world.currentSeasonId 갱신

TradeItemData + LjhEconomyM1InputAdapter
  └─ Modifier → PriceModifierInput

MarketInventoryMutationSession.ExecuteTransaction
  ├─ capture currentSeasonId
  ├─ SeasonalSellPriceModifierSelector.SelectForSellPrice
  ├─ PriceCalculator.CalculateUnitPrices
  └─ MarketTransactionCalculator → mutate → Save → events

Claim / JourneyResult
  └─ 아이템 판매 수익 재산정 없음 (선행 계약)
```

---

## 11. 남은 리스크·후속

1. **UI 표시 불일치** — preview가 Season 필터를 쓰지 않으면 패널 표시 단가 ≠ commit 단가일 수 있음 (의도적 유예).
2. **Buy 경로** — 재고 생성 `unitPrice`는 기존 Buy modifier 경로; Seasonal Buy는 미구현.
3. **워킹 트리 상태** — 문서화 시점 기준 미커밋. PR 전 컴파일·`SeasonalSellPriceTests` 실행·Unity Console 확인 필요.
4. **외부 파일** — `MarketInventoryIntegration.cs`는 Economy feature root 밖 UI 통합 파일이다. 팀 소유권·PR 리뷰에 포함할 것.

---

## 12. 검증 체크리스트 (구현자용)

- [ ] Edit Mode: `SeasonalSellPriceTests` 전부 통과
- [ ] Summer `currentSeasonId` + Summer Season Sell +20% 아이템 판매 → 단가 240·매출 일치
- [ ] Winter에서 동일 SO → base Sell·매출 일치
- [ ] Save 강제 실패 시 화폐·Cargo·재고·이벤트 롤백
- [ ] Claim 후 아이템 판매 수익이 이중 지급되지 않음
- [ ] Unity Console 컴파일 오류 없음
