# Contextual SellPrice Modifier · 구현 로직 정리

**작성일:** 2026-08-05 (최종 갱신 2026-08-06)  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/economy/local-specialty-sell-price-exception`  
**베이스:** `dev2`  
**문서화 시점 상태:** 커밋 `e185e0b` 기준  
**Feature root:** `Assets/_Project/03.Economy/01_Market/`  
**판매 commit·preview 경계:** `Assets/Scripts/UI/MarketInventoryIntegration.cs`  
**Scene 진입점:** `Assets/Scripts/UI/Market/MarketTradePanelController.cs`

관련 선행·후속:

- [`0804_seasonal_sell_price_logic.md`](./0804_seasonal_sell_price_logic.md) — TradeItem Season SellPrice 선택기·commit Season 캡처
- [`Docs/Guide/TradeItem_Seasonal_SellPrice_Modifier_Setup_Guide.md`](../../Guide/TradeItem_Seasonal_SellPrice_Modifier_Setup_Guide.md) — TradeItem SO 계절 Modifier 작성
- [`Docs/Guide/Contextual_Sell_Price_Modifier_Policy_Setup_Guide.md`](../../Guide/Contextual_Sell_Price_Modifier_Policy_Setup_Guide.md) — 정책 SO 사용·InGame 등록 가이드

---

## 1. 목적

시장 **판매 미리보기**와 **판매 commit**에 동일한 contextual SellPrice 계산을 적용한다.

기존 TradeItem SO의 Season modifier에 더해, **정책 ScriptableObject**(`SellPriceModifierPolicy`)로 아래 세 가지를 추가한다.

1. **카테고리·계절 규칙** — `TradeItemCategory` + canonical Season ID 조합
2. **낙뢰 행운(Lucky Money) 규칙** — 활성 trade의 `WeatherLuckyStore` 잔여 여부
3. **거리 구간 규칙** — `CaravanSaveData.currentDistanceKm` 구간별 Percent/Add

**도착 시장 지역 특산품 예외** — 판매 대상 `ItemId`가 해당 시장 `LocalSpecialtyItemIds`에 포함되면 item Season SellPrice·policy 계절·policy 거리를 제외하고, Lucky Money와 비계절 item modifier만 남긴다.

이번 작업이 하는 것:

1. **Resolver·Calculator 분리** — modifier 선택과 산술 책임 분리
2. **정책 SO 데이터 모델** — Inspector에서 규칙 작성·검증
3. **preview = commit 정렬** — `CaptureSellPriceContext()`를 preview·commit 공통 사용
4. **Lucky 소비 시점 고정** — `ClaimSettlement` Save 성공 직후 `WeatherLuckyStore.Consume`
5. **지역 특산품 스냅샷** — `TryOpen` 시 `LocalSpecialtyItemIds`를 세션에 고정, preview·commit 공유
6. **Edit Mode 검증** — 선택기·preview/commit 일치·거리 경계·특산품 예외·중복 제거 테스트

이번 작업이 하지 않는 것:

- Seasonal **BuyPrice** 적용
- Claim 시점 판매가 재산정
- SaveData 스키마·버전 변경
- 출발 시점 계절·Claim 시점 계절을 판매가에 반영
- Prefab/Scene에 정책 SO 자동 배선 (InGame에서 수동 등록 필요)

---

## 2. 변경 파일 요약

| 영역 | 파일 | 역할 |
|------|------|------|
| Context (갱신) | `SellPriceCalculationContext.cs` | Season·거리·Lucky + `DestinationLocalSpecialtyItemIds` struct |
| Policy SO (신규) | `SellPriceModifierPolicy.cs` | 카테고리·Lucky·거리 규칙 정의 |
| Resolver (갱신) | `ContextualSellPriceModifierResolver.cs` | item modifier + policy modifier 선택 + **지역 특산품 예외** |
| Calculator (신규) | `ContextualSellPriceCalculator.cs` | Resolver → `PriceCalculator` 위임 |
| Lucky bridge (신규) | `WeatherLuckyMoneyStateReader.cs` | tradeId → Lucky 활성 여부 (읽기 전용) |
| Default asset (신규) | `Data/SellPriceModifierPolicy_Default.asset` | 운영 기본 정책 |
| Editor (신규) | `Editor/ContextualSellPriceTests.cs` | 선택·조합·preview/commit 경계 |
| Editor (신규) | `Editor/SellPriceModifierPolicyAssetCreator.cs` | 기본 asset 생성 유틸 |
| Sale session | `Assets/Scripts/UI/MarketInventoryIntegration.cs` | context 캡처·policy·**특산품 ID 스냅샷** 주입·commit |
| UI entry | `Assets/Scripts/UI/Market/MarketTradePanelController.cs` | policy SO + **`LocalSpecialtyItemIds` → TryOpen** 전달 |
| Claim | `11.CoreServices/.../TradeProgressCoordinator.cs` | Claim Save 성공 후 Lucky Consume |
| E2E | `11.CoreServices/Editor/FrameworkM1LoopE2EEditorTests.cs` | Claim Lucky 소비 계약 회귀 |

`PriceCalculator`, `SeasonalSellPriceModifierSelector`, `MarketTransactionCalculator`는 **산술·transaction 경계 권위를 유지**하며, contextual layer가 modifier 목록만 확장한다.

---

## 3. 제품 위치 (판매 흐름 안에서의 자리)

```text
Prepare → Traveling → Settle → SettlementPending
  → arrival cargo sale UI (draft, 비내구)
  → MarketTradePanelController.Open* → MarketInventoryMutationSession
       SharedGameData.TryGetMarket(marketId).LocalSpecialtyItemIds 스냅샷
  → preview: ResolvePreviewUnitPrices(context, policy)
  → ConfirmSale / MarketTransactionCommand.Execute
       CaptureSellPriceContext()  ← transaction당 1회 (Season·거리·Lucky·특산품 ID)
       ResolveUnitPrices(item, context, policy)  ← 라인별, 특산품이면 Season·policy 계절·거리 제외
  → Save 성공 → 화폐·Cargo·재고 반영
  → settlement presentation
  → ClaimSettlement Save 성공 → WeatherLuckyStore.Consume(tradeId)
```

- **확정 단가·매출**은 durable market sale transaction 안에서만 결정된다.
- **패널 표시 단가**도 동일 context·policy·특산품 스냅샷을 사용하므로, 정책 SO가 연결된 경우 preview ≈ commit이다.
- policy가 `null`이면 item Season modifier + 기존 seasonal 선택만 적용된다 (contextual policy 효과 없음). **지역 특산품 예외는 policy 유무와 관계없이** Resolver에서 적용된다.

---

## 4. 계산 파이프라인

### 4.1 책임 경계

| 컴포넌트 | 책임 |
|----------|------|
| `SellPriceCalculationContext` | Season ID, route distance km, Lucky active, **destination local specialty ItemId snapshot** (immutable) |
| `ContextualSellPriceModifierResolver` | item modifier + policy 규칙 → `PriceModifierInput` 목록. **특산품이면 Season·policy 계절·policy 거리 제외** |
| `SeasonalSellPriceModifierSelector` | item Season SellPrice 자격 필터 (기존) |
| `PriceCalculator` | Add/Percent/Multiply 산술·정렬·반올림 (유일한 산술 권위) |
| `ContextualSellPriceCalculator` | Resolver → PriceCalculator thin wrapper |
| `MarketInventoryMutationSession` | context 캡처, preview/commit 호출, transaction 조립 |

### 4.2 `CaptureSellPriceContext()` (transaction당 1회)

```text
seasonId      = saveData.world.currentSeasonId  (없으면 "")
distanceKm    = max(0, targetCaravan.currentDistanceKm)
isLuckyActive = false
specialtyIds  = session.destinationLocalSpecialtyItemIds  (TryOpen 시 스냅샷, 불변)

if caravan + TradeProgress.activeTradeId 존재:
  isLuckyActive = WeatherLuckyStore.GetCount(activeTradeId) > 0
```

- 거리는 **출발 시 SaveData에 복사된 route distance snapshot** (`currentDistanceKm`)을 사용한다.
- Lucky는 **읽기만** 한다. 소비는 판매 commit이 아니라 **Claim Save 성공** 시점이다.
- 특산품 ID 목록은 **세션 생성 시** `MarketTradePanelController`가 `SharedMarketDefinition.LocalSpecialtyItemIds`에서 읽어 `TryOpen`에 전달한 배열을 재사용한다. `CaptureSellPriceContext()`는 매 preview/commit마다 동일 스냅샷을 context에 넣는다.

### 4.2.1 지역 특산품 ID 공급 (`MarketTradePanelController.OpenResolved`)

```text
destinationLocalSpecialtyItemIds = Array.Empty<string>()

if root.SharedGameData.TryGetMarket(marketData.MarketId, out destinationMarket)
   && destinationMarket.LocalSpecialtyItemIds != null:
  destinationLocalSpecialtyItemIds = destinationMarket.LocalSpecialtyItemIds

MarketInventoryMutationSession.TryOpen(..., sellPriceModifierPolicy, destinationLocalSpecialtyItemIds)
```

- `SharedGameData` 미로드·시장 미등록·목록 null → 빈 배열 → **예외 없음**, 기존 contextual·seasonal 규칙 전부 적용.
- Cargo 전용 상품(`transactionCatalog`에만 있고 재고 슬롯에는 없음)도 `ItemId`가 목록에 있으면 동일 예외 적용.

### 4.3 `ContextualSellPriceModifierResolver.Resolve` 의사코드

```text
1. item.AffectModify → LjhEconomyM1InputAdapter 변환
2. isLocalSpecialtyAtDestination =
     item.ItemId ∈ context.DestinationLocalSpecialtyItemIds  (Ordinal)
3. if isLocalSpecialtyAtDestination:
     SelectWithoutSeasonalSellPriceModifiers(itemModifiers)
       // Season + SellPrice/Both 만 제거, Disaster/AffectToTown 등은 유지
   else:
     SeasonalSellPriceModifierSelector.SelectForSellPrice(itemModifiers, context.SeasonId)
4. if policy != null:
     if !isLocalSpecialtyAtDestination:
       AddCategorySeasonalModifier(category, seasonId, policy.CategorySeasonalRules)
     AddLuckyMoneyModifier(context.IsLuckyMoneyActive, policy.LuckyMoneyRule)
     if !isLocalSpecialtyAtDestination:
       AddDistanceModifier(context.RouteDistanceKm, policy.DistanceRules)
5. Deduplicate(modifiers)  // Type+SourceId+Target+Operation identity
6. return resolved list
```

### 4.3.1 지역 특산품 예외 요약

| Modifier 출처 | 특산품 at destination |
|---------------|------------------------|
| TradeItem `Season` + `SellPrice`/`Both` | **제외** |
| TradeItem 비계절 (`Disaster`, `AffectToTown`, …) | **유지** |
| Policy `CategorySeasonalSellPriceRule` | **제외** |
| Policy `DistanceSellPriceRule` | **제외** |
| Policy `LuckyMoneySellPriceRule` | **유지** (Lucky active 시) |

매칭 실패(빈 목록·다른 ItemId·SharedGameData 미조회) 시 **기존 full composition**과 동일하게 동작한다.

### 4.4 Policy 규칙별 선택 규칙

#### CategorySeasonalSellPriceRule

- `Enabled` && `Category` 일치 && `SeasonId`가 canonical && `SeasonId == context.SeasonId` (Ordinal)
- 매칭 rule 전부 추가 (복수 rule 허용)
- `RuleId` 오름차순 정렬 후 순서대로 modifier 생성
- `SourceId` = `category-season:{seasonId}:{category}:{ruleId}`

#### LuckyMoneySellPriceRule

- `context.IsLuckyMoneyActive && rule.Enabled`일 때 1개만 추가
- `SourceId` = `lucky-money:{effectId}`
- `ModifierType` 기본값 = `RouteEvent`

#### DistanceSellPriceRule

- 구간: `[MinimumDistanceKm, MaximumDistanceKm)` — **하한 포함, 상한 미포함**
- `HasMaximumDistance == false`이면 상한 없음 (∞)
- `distanceKm` NaN/Infinity → 규칙 미적용
- 매칭 rule을 `RuleId` Ordinal 정렬 후 **첫 번째만** 선택 (겹침 시 tie-break)
- `Value == 0`이면 modifier 추가 생략
- `SourceId` = `distance:{ruleId}`

#### Deduplicate

- identity = `{ModifierType}\u001f{SourceId}\u001f{Target}\u001f{Operation}`
- 동일 identity는 한 번만 `PriceCalculator`에 전달

### 4.5 산술 예시 (테스트 기준, Base Sell 200)

| 조건 | UnitSellPrice |
|------|----------------|
| item Summer Season +20% | 240 |
| policy winter-food +20% (겨울·Food) | 240 |
| Lucky +50% (active) | 300 |
| distance 350km → [300,600) +10% | 220 |
| item Winter +10%, policy winter-food +20%, Lucky +50%, 600km +15% | 455 |
| **도착 시장 지역 특산품** + item Winter +10%, AffectToTown +10%, policy 전부, Lucky +50% | **330** (Season·policy 계절·거리 제외; 200×1.1×1.5) |
| **도착 시장 지역 특산품**, Lucky off | **200** (Base Sell만) |

Percent는 `price × (1 + value)`. 최종 단가는 AwayFromZero 반올림, 최소 1.

---

## 5. 시장 세션 연동 (`MarketInventoryMutationSession`)

### 5.1 Policy·특산품 ID 주입

```text
MarketInventoryMutationSession.TryOpen(..., SellPriceModifierPolicy policy,
    IReadOnlyList<string> destinationLocalSpecialtyItemIds, ...)
  → session 필드에 policy·특산품 ID 배열 보관 (null/빈 목록 허용)
```

`MarketTradePanelController.OpenResolved`가 Inspector의 `sellPriceModifierPolicy`와 `SharedMarketDefinition.LocalSpecialtyItemIds`를 TryOpen에 전달한다.

### 5.2 Preview API

```text
ResolvePreviewUnitPrices(item)
  → ResolveUnitPrices(item, CaptureSellPriceContext(), sellPriceModifierPolicy)
```

`MarketTradePanelModel.Refresh`가 각 item의 `SellUnitPrice`에 사용한다.

### 5.3 Commit API

`ExecuteTransaction` 라인 루프 **진입 직전**:

```csharp
SellPriceCalculationContext transactionContext = CaptureSellPriceContext();
```

각 판매 라인:

```text
SellUnitPrice = ResolveUnitPrices(item, transactionContext, sellPriceModifierPolicy).UnitSellPrice
BuyUnitPrice  = stock.unitPrice (기존, Season Buy 미적용)
```

### 5.4 Null policy fallback

- policy == null → Resolver가 policy 분기를 건너뜀
- item modifier + seasonal 선택만 적용 → **0804 seasonal 작업과 동일한 fallback**
- **지역 특산품 예외는 policy null이어도 적용** — item Season SellPrice만 제외, Lucky policy도 없으므로 비계절 item modifier + base만 남음
- context는 캡처되지만 policy-authored 효과는 없음

### 5.5 Legacy API 유지

```text
ResolveUnitPrices(item)
  → selectSeasonalSellPrice = false (재고 생성 Buy 단가 등)

ResolveUnitPrices(item, seasonId, selectSeasonalSellPrice: true)
  → policy 없이 seasonal만 (내부·테스트용)
```

---

## 6. Lucky Money 생명주기

```text
Travel 중 MinimapWeatherEventDetector
  → WeatherLuckyStore.Add(tradeId)  (낙뢰 행운 누적, best-effort persist)

판매 preview / commit
  → WeatherLuckyMoneyStateReader.IsActive(tradeId)
  → GetCount(tradeId) > 0 이면 policy Lucky rule 적용

ClaimSettlement Save 성공
  → WeatherLuckyStore.Consume(tradeId)  (이번 PR에서 추가)
  → economySettlementBridge.ClearPending(...)
```

계약:

- Claim **Save 실패** → Lucky 유지, pending·SaveData rollback
- Claim **Save 성공** → 해당 tradeId Lucky 전량 소비
- 다른 tradeId Lucky는 격리 유지
- 중복 Claim은 기존처럼 거부

---

## 7. 기본 정책 asset (`SellPriceModifierPolicy_Default`)

경로:

```text
Assets/_Project/03.Economy/01_Market/Data/SellPriceModifierPolicy_Default.asset
```

| 규칙 종류 | 내용 |
|-----------|------|
| CategorySeasonal | spring-food +5%, summer-food -10%, autumn-material +10%, autumn-luxury +5%, winter-food +20%, winter-material +5% |
| LuckyMoney | enabled, Percent +0.5 (+50%) |
| Distance | [0,100) 0%, [100,300) +5%, [300,600) +10%, [600,∞) +15% |

Editor `SellPriceModifierPolicyAssetCreator.CreateDefaultAsset()`로 동일 내용 재생성 가능.

---

## 8. 테스트 맵 (`ContextualSellPriceTests`)

| 테스트 | 검증 |
|--------|------|
| `ExistingItemSeason_MatchingAppliesAndNonMatchingIsExcluded` | item Season 필터 유지 |
| `MarketPreview_UsesSavedCurrentSeasonLikeCommit` | preview가 commit Season과 동일 |
| `MarketSession_PolicyContextAlignsPreviewAndCommit_WithNullFallback` | preview=commit revenue, null policy → base |
| `CategorySeasonalRule_AppliesForMatchingCategoryAndSeason` | 카테고리·계절 policy |
| `LuckyMoneyRule_AppliesOnlyWhenActive` | Lucky on/off |
| `DistanceRules_UseInclusiveMinimumExclusiveMaximum` | 거리 구간 경계 |
| `AllEffectsComposeWithoutChangingBuyPrice` | 복합 조합 455, Buy 불변 |
| `DestinationLocalSpecialty_ExcludesSeasonAndDistanceButKeepsLuckyMoney` | 특산품: Season·policy 계절·거리 제외, Lucky·AffectToTown 유지 → 330 |
| `DestinationLocalSpecialty_WithoutLuckyMoneyUsesBaseSellPrice` | 특산품 + Lucky off → base 200 |
| `NonMatchingOrEmptySpecialtyList_PreservesExistingComposition` | null/빈/다른 ID → full 455 |
| `Context_CopiesDestinationLocalSpecialtyItemIds` | context가 특산품 ID 스냅샷 복사 (외부 배열 변경 불변) |
| `ItemMissingFromDestinationTradeItems_RemainsSellable` | transactionCatalog만 있는 Cargo 상품도 preview=commit, 특산품 on/off |
| `DuplicateSourceIdentity_IsAppliedOnce` | 중복 rule ID dedup |
| `OverlappingDistanceRules_SelectOrdinalFirstRuleOnly` | 겹침 시 RuleId 최소 |

E2E (`FrameworkM1LoopE2EEditorTests.AssertClaimLuckyConsumptionContract`):

- Claim Save 실패 → Lucky 2 유지
- Claim Save 성공 → Lucky 0, other tradeId 격리

---

## 9. 의존 관계

```text
SharedGameDataService / SharedMarketDefinition
  └─ LocalSpecialtyItemIds[]

MarketTradePanelController.OpenResolved
  └─ TryGetMarket(marketId) → destinationLocalSpecialtyItemIds
       └─ MarketInventoryMutationSession.TryOpen(..., policy, specialtyIds)

GameCalendarService
  └─ world.currentSeasonId

Trade 출발 / JourneyRunner
  └─ caravan.currentDistanceKm snapshot

MinimapWeatherEventDetector
  └─ WeatherLuckyStore.Add(tradeId)

MarketTradePanelController (Inspector policy SO)
  └─ MarketInventoryMutationSession.TryOpen(..., policy)
       ├─ CaptureSellPriceContext()
       ├─ ContextualSellPriceCalculator
       └─ MarketTransactionCalculator → Save

TradeProgressCoordinator.ClaimSettlement
  └─ Save 성공 → WeatherLuckyStore.Consume(tradeId)
```

---

## 10. 남은 리스크·후속

1. **InGame Scene/Prefab 미배선** — `MarketTradePanelController.sellPriceModifierPolicy`가 아직 Prefab YAML에 연결되지 않음. 미등록 시 contextual policy 효과 없이 seasonal-only(+특산품 예외) fallback.
2. **MainUICanvas + ArrivalSaleFlow 이중 Controller** — Town market용·도착 판매용 `MarketTradePanelController` 각각 policy 등록 필요.
3. **외부 파일 수정** — `MarketInventoryIntegration.cs`, `MarketTradePanelController.cs`, `TradeProgressCoordinator.cs`는 feature root 밖. PR 리뷰·소유권 확인 필요.
4. **Buy 경로** — 재고 생성 `unitPrice`는 기존 non-seasonal Buy 경로 유지.
5. **거리 snapshot 갱신** — `currentDistanceKm`이 판매 시점에 유효한 route distance인지는 Trade loop 쪽 데이터 계약에 의존.
6. **특산품 ID 데이터 계약** — `LocalSpecialtyItemIds`가 SharedGameData에 누락·오타면 예외가 발동하지 않아 Season·거리 modifier가 그대로 적용된다. `ItemId`는 Ordinal 비교.
7. **특산품 판별 범위** — “도착 시장”의 `MarketId` 기준만 사용. 출발지·경유지 특산품 여부는 판매가에 반영하지 않음.

---

## 11. 검증 체크리스트 (구현자용)

- [ ] Edit Mode: `ContextualSellPriceTests` 전부 통과 (특산품 예외·Cargo-only 판매 포함)
- [ ] InGame Scene에서 `MarketTradePanelController`에 `SellPriceModifierPolicy_Default` 등록
- [ ] `ArrivalSaleFlow` Prefab의 Controller에도 동일 policy 등록
- [ ] 겨울 + Food + Lucky + 350km 판매 → preview 단가 = commit revenue / quantity
- [ ] **도착 시장 `LocalSpecialtyItemIds` 등록 상품** → Season·policy 계절·거리 제외, Lucky만 추가되는지 확인
- [ ] policy 미등록 → item Season modifier만 적용 (contextual policy 없음). 특산품이면 item Season도 제외
- [ ] Claim Save 실패 → Lucky 유지 / 성공 → Consume
- [ ] Unity Console 컴파일 오류 없음
