# Contextual SellPrice Modifier Policy 설정 가이드

## Purpose

- `SellPriceModifierPolicy` SO로 **카테고리·계절**, **낙뢰 행운(Lucky Money)**, **무역 거리** 기반 판매가 보정을 Inspector에서 설정한다.
- 시장 **판매 미리보기**와 **판매 commit**에 동일하게 적용되도록 InGame Scene에 정책 SO를 등록하는 방법을 정리한다.

관련 문서:

- TradeItem 계절 Modifier: [`TradeItem_Seasonal_SellPrice_Modifier_Setup_Guide.md`](./TradeItem_Seasonal_SellPrice_Modifier_Setup_Guide.md)
- 마켓·Modifier 일반: [`Market_And_Route_Event_Data_Setup_Guide.md`](./Market_And_Route_Event_Data_Setup_Guide.md)
- 계절 ID·달력 API: [`Framework_Game_Calendar_and_Seasons_API_Guide.md`](./Framework_Game_Calendar_and_Seasons_API_Guide.md)
- 구현 로직 (개인): [`../Personal_Documents/CSU/0805_contextual_sell_price_modifier_logic.md`](../Personal_Documents/CSU/0805_contextual_sell_price_modifier_logic.md)

## 한 줄 요약

```text
SellPriceModifierPolicy SO 작성
→ InGame의 MarketTradePanelController에 policy 필드 연결
→ 판매 preview / commit 모두 동일 context(계절·거리·Lucky) + policy 규칙 적용
```

정책 SO를 연결하지 않으면 TradeItem SO의 Season SellPrice modifier만 적용된다 (contextual 효과 없음).

## 적용 범위 (현재 제품)

| 항목 | 현재 |
|------|------|
| Contextual **SellPrice** | 지원 — 시장 판매 preview·commit |
| Contextual **BuyPrice** | 미지원 |
| 적용 시점 | `MarketInventoryMutationSession` preview 및 durable sale transaction |
| 계절 권위 | `saveData.world.currentSeasonId` (commit/preview 공통 1회 캡처) |
| 거리 권위 | `CaravanSaveData.currentDistanceKm` (출발 시 snapshot) |
| Lucky 권위 | `WeatherLuckyStore.GetCount(activeTradeId) > 0` (판매 시 읽기, Claim Save 성공 후 소비) |
| Claim | 판매 commit 결과 재사용. Claim에서 재판매가하지 않음 |

## SO 위치

### 기본 정책 asset (버전 관리됨)

```text
Assets/_Project/03.Economy/01_Market/Data/SellPriceModifierPolicy_Default.asset
```

### 새 정책 asset 만들기

Unity 메뉴:

```text
Create → Economy → Market → Sell Price Modifier Policy
```

또는 Project 창에서 우클릭 → 동일 메뉴.

운영용은 `01_Market/Data/` 아래에 두고, Sandbox/Test asset과 구분한다.

## Policy SO Inspector 필드

### Category Seasonal Rules

카테고리 + 계절 조합으로 SellPrice modifier를 추가한다.

| 필드 | 설명 |
|------|------|
| Rule Id | 규칙 식별자. 중복 시 Editor 경고 |
| Enabled | false면 무시 |
| Category | `TradeItemCategory` (Food, Material, LuxuryGoods 등) |
| Season Id | `spring` / `summer` / `autumn` / `winter` (소문자 canonical ID만) |
| Operation | `Add` / `Percent` / `Multiply` |
| Value | Add=금액, Percent=소수 비율 (`0.2` = +20%) |
| Modifier Type | 기본 `Season`. SourceId는 `category-season:{season}:{category}:{ruleId}` |

현재 계절(`world.currentSeasonId`)과 Category가 모두 일치하는 rule만 적용된다.

### Lucky Money Rule

낙뢰 행운이 활성인 trade에서 SellPrice modifier 1개를 추가한다.

| 필드 | 설명 |
|------|------|
| Effect Id | Lucky modifier SourceId에 사용 (`lucky-money:{effectId}`) |
| Enabled | false면 Lucky가 있어도 미적용 |
| Operation / Value | 기본 Percent `0.5` (+50%) |
| Modifier Type | 기본 `RouteEvent` |

Lucky 활성 조건: 진행 중 trade의 `activeTradeId`에 대해 `WeatherLuckyStore` count > 0.  
Lucky는 **판매 commit 시 소비되지 않으며**, `ClaimSettlement` Save 성공 후 소비된다.

### Distance Rules

`currentDistanceKm` 구간별 SellPrice modifier를 추가한다. **매칭 rule 중 Rule Id Ordinal 최소 1개만** 적용된다.

| 필드 | 설명 |
|------|------|
| Rule Id | 규칙 식별자. 겹치는 구간에서 tie-break 기준 |
| Enabled | false면 무시 |
| Minimum Distance Km | 구간 하한 (**포함**) |
| Has Maximum Distance | true면 상한 사용 |
| Maximum Distance Km | 구간 상한 (**미포함**) |
| Operation / Value | Value가 0이면 modifier 미추가 |
| Modifier Type | 기본 `RouteEvent` |

구간 예시 (기본 policy):

```text
[0,   100)  → +0%
[100, 300)  → +5%
[300, 600)  → +10%
[600, ∞)    → +15%
```

겹치는 구간이 있으면 Editor에서 경고가 출력된다. 런타임은 Rule Id 문자열 Ordinal 정렬 후 첫 rule만 선택한다.

## 기본 policy 값 (`SellPriceModifierPolicy_Default`)

| CategorySeasonal Rule Id | Category | Season | Value |
|--------------------------|----------|--------|-------|
| spring-food | Food | spring | +5% |
| summer-food | Food | summer | -10% |
| autumn-material | Material | autumn | +10% |
| autumn-luxury | LuxuryGoods | autumn | +5% |
| winter-food | Food | winter | +20% |
| winter-material | Material | winter | +5% |

| Lucky Money | Value |
|-------------|-------|
| lucky_money_default | +50% |

거리 구간은 위 Distance Rules 예시와 동일하다.

## InGame Scene · Prefab에 policy 등록 (필수)

코드만 merge되어 있고 **Scene/Prefab 배선은 수동 작업**이 필요하다.  
policy SO를 연결하지 않으면 contextual 규칙(카테고리·Lucky·거리)이 적용되지 않는다.

### 등록 대상 Component

`ND.UI.Market.MarketTradePanelController`

Inspector 필드:

```text
Sell Price Modifier Policy  (sellPriceModifierPolicy)
```

### 등록해야 하는 위치

InGame에서 market UI를 여는 **모든** `MarketTradePanelController` 인스턴스에 동일 policy를 연결한다.

| 위치 | 용도 |
|------|------|
| `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab` | Town market, Trade Prepare 구매 등 |
| `Assets/_Project/08.Prefabs/UI/Market/ArrivalSaleFlow.prefab` | 도착 판매(Sell Only) |

`InGame.unity` 등 운영 Scene은 `MainUICanvas` Prefab Instance를 사용하므로, **Prefab에서 등록하면 Scene에도 반영**된다.  
Scene에 Prefab override가 있으면 override 항목도 확인한다.

### 등록 절차

1. Unity에서 `MainUICanvas` Prefab을 연다 (또는 InGame Scene에서 Prefab 편집 모드 진입).
2. Hierarchy에서 `MarketTradePanelController`가 붙은 GameObject를 찾는다.
3. Inspector → **Sell Price Modifier Policy**에 `SellPriceModifierPolicy_Default` asset을 드래그한다.
4. `ArrivalSaleFlow` Prefab을 열어 동일 Controller에 같은 policy asset을 연결한다.
5. Prefab 저장 후 InGame Scene에서 market·도착 판매 flow를 한 번씩 열어 Inspector 참조가 Missing이 아닌지 확인한다.

### 코드 경로 (참고)

```text
MarketTradePanelController.sellPriceModifierPolicy
  → MarketInventoryMutationSession.TryOpen(..., sellPriceModifierPolicy)
  → preview: ResolvePreviewUnitPrices
  → commit: CaptureSellPriceContext() + ResolveUnitPrices(context, policy)
```

## 런타임 계산 순서

```text
1. CaptureSellPriceContext()
     seasonId      ← world.currentSeasonId
     distanceKm    ← caravan.currentDistanceKm
     isLuckyActive ← WeatherLuckyStore(activeTradeId) > 0

2. TradeItem modifier 변환 + Season SellPrice 필터 (기존)

3. Policy rules (SO가 연결된 경우)
     CategorySeasonal (매칭 전부)
     LuckyMoney (활성 시 1개)
     Distance (매칭 1개)

4. 중복 identity 제거

5. PriceCalculator → UnitSellPrice
```

TradeItem SO의 Season modifier와 Policy CategorySeasonal rule은 **함께 적용**된다 (중복 SourceId가 아니면 누적).

## 작성·운영 예시

### 예시 A — 겨울 Food 추가 +20%

Policy에 이미 `winter-food` rule이 있으므로 별도 TradeItem 수정 없이 Food 카테고리 전체에 적용된다.

```text
Category Seasonal Rules
- Rule Id = winter-food
- Category = Food
- Season Id = winter
- Operation = Percent
- Value = 0.2
```

겨울에 Food 판매 commit 시 base SellPrice × 1.2 (다른 modifier와 순서대로 합성).

### 예시 B — Lucky + 거리 보너스만 테스트

CategorySeasonal rule을 모두 `Enabled = false`로 두고 Lucky·Distance만 켠다.

- Lucky 활성 trade + 350km → 거리 [300,600) +10%, Lucky +50% 순으로 PriceCalculator 규칙 적용.

### 예시 C — policy 미등록 (의도적 fallback)

`Sell Price Modifier Policy` 필드를 비워 둔다.

- TradeItem Season SellPrice modifier만 적용.
- 카테고리·Lucky·거리 policy 효과 없음.

## Check

설정·등록 후 아래를 확인한다.

1. `MainUICanvas`·`ArrivalSaleFlow`의 `MarketTradePanelController`에 policy SO가 연결되어 있는가.
2. Policy CategorySeasonal의 `Season Id`가 canonical 소문자인가.
3. Distance rule 구간이 의도한 km 범위인가 (하한 포함·상한 미포함).
4. 겹치는 Distance rule이 있다면 Rule Id tie-break 결과가 기획과 맞는가.
5. Lucky가 있는 trade에서 preview SellUnitPrice가 Lucky rule 반영 후 값인가.
6. 동일 조건에서 판매 commit revenue = preview UnitSellPrice × 수량인가.
7. policy 필드를 비우면 contextual 효과 없이 item Season만 적용되는가.
8. Claim Save 성공 후 동일 tradeId Lucky가 소비되어 다음 판매에 Lucky bonus가 없는가.

## Risk 및 주의사항

- **Prefab 미등록 = 기능 미적용.** merge만으로는 contextual 판매가가 켜지지 않는다.
- `Season Id`·TradeItem `Source Id` 오타는 조용히 무시된다. 먼저 canonical ID를 확인한다.
- Distance rule `Value = 0`은 modifier를 추가하지 않는다 (구간 placeholder용).
- Lucky는 판매 확정이 아니라 **Claim Save 성공** 시 소비된다. Claim Save 실패 시 Lucky는 유지된다.
- Seasonal BuyPrice·Claim 시점 재판매가·출발 계절 반영은 이 가이드 범위 밖이다.
- 운영 policy 변경은 기존 SaveData 마이그레이션 없이 즉시 반영되지만, 이미 commit된 판매 결과는 변경되지 않는다.
