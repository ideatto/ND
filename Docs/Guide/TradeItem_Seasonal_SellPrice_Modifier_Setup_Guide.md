# TradeItem 계절 판매가(Modifier) 설정 가이드

## Purpose

- `TradeItemData` SO에 Season Modifier를 작성해, **시장 판매 commit** 시 현재 게임 계절에 맞는 판매가가 적용되도록 한다.
- Inspector에서 어떤 필드를 어떤 값으로 채워야 하는지, 자주 하는 실수를 같은 기준으로 맞춘다.

관련 문서:

- 마켓·일반 Modifier 설정: [`Market_And_Route_Event_Data_Setup_Guide.md`](./Market_And_Route_Event_Data_Setup_Guide.md)
- 계절 ID·달력 API: [`Framework_Game_Calendar_and_Seasons_API_Guide.md`](./Framework_Game_Calendar_and_Seasons_API_Guide.md)
- 도착 판매·Claim 정책: [`../Contract/Arrival_Sale_Settlement_Claim_Policy.md`](../Contract/Arrival_Sale_Settlement_Claim_Policy.md)

## 한 줄 요약

```text
Affect Modify 켜기
→ Modifier Type = Season
→ Source Id = spring | summer | autumn | winter  (소문자 기술 ID만)
→ Bundle Target = SellPrice
→ Operation = Add 또는 Percent
→ Value = 금액 또는 소수 비율
```

판매가 변동은 **판매 확정(commit) 시점**의 `world.currentSeasonId`와 `Source Id`가 **정확히 일치**할 때만 적용된다.  
출발 계절·Claim 시점 계절은 판매가에 쓰이지 않는다.

## 적용 범위 (현재 제품)

| 항목 | 현재 |
|------|------|
| Seasonal **SellPrice** | 지원 — 시장 판매 commit에 적용 |
| Seasonal **BuyPrice** | 미지원 — SO에 넣어도 이번 계절 판매 경로에서 판매가를 바꾸지 않음 |
| 적용 시점 | durable market sale transaction (도착 판매 확정 포함) |
| Claim | 이미 commit된 판매 수익을 재사용. Claim에서 재판매가하지 않음 |
| SaveData | 별도 필드·버전 변경 없음. 기존 `currentSeasonId` 사용 |

판매 패널 UI 표시 단가가 commit 단가와 항상 같다고 가정하지 않는다.  
최종 지급 기준은 **판매 확정 시 계산된 단가·매출**이다.

## SO 위치

```text
Assets/_Project/02.Data/01_ScriptableObjects/TradeItem
```

- 운영 데이터만 수정한다. Sandbox / Demo / Test SO와 혼동하지 않는다.

## Inspector 작성 순서

상품 SO를 연 뒤 `TradeItem_Modify_Info`를 설정한다.

### 1. Affect Modify

- 반드시 **켜기** (`true`).
- 꺼져 있으면 `Modifiers`가 있어도 기본 구매가·판매가만 사용한다.

### 2. Modifiers 배열에 Season 항목 추가

한 계절·한 판매 규칙 = Modifier 항목 하나(권장).  
사계절을 모두 다르게 주려면 항목을 네 개 만든다.

#### Modifier Type

- `Season` 만 계절 매칭 대상이다.
- `Disaster`, `AffectToTown` 등은 계절 필터를 타지 않고 기존 규칙대로 통과한다.

#### Source Id (필수·가장 중요)

현재 게임 계절과 **Ordinal·대소문자 구분**으로 비교한다.  
허용되는 값만 사용한다.

| 계절 | Source Id (정확히 이 문자열) |
|------|------------------------------|
| 봄 | `spring` |
| 여름 | `summer` |
| 가을 | `autumn` |
| 겨울 | `winter` |

다음에 해당하면 **매칭 실패**이며, 그 Season SellPrice 항목은 판매 commit에서 제외된다 (예외 없이 무시).

| 잘못된 예 | 이유 |
|-----------|------|
| `Summer`, `SUMMER` | 대문자 |
| `fall` | `autumn`이 정식 ID |
| `season_summer` | 접두사 불가 |
| `여름` | 표시용 한글 불가 |
| 빈 문자열 / 공백 | 비정규 |
| Display Name만 채움 | Display Name은 식별자가 아님 |

#### Display Name

- UI·디버그용 설명이다.
- 가격 매칭에 **사용되지 않는다**.
- `Source Id` 대신 Display Name에 `summer`를 넣어도 계절 판매가는 적용되지 않는다.

#### Modifier Bundles

각 Season 항목 아래 Bundle을 하나 이상 둔다. 계절 판매가용 권장 값:

| 필드 | 값 | 설명 |
|------|-----|------|
| Modifier Target | `SellPrice` | 판매가에만 적용 |
| Modifier Operation | `Add` 또는 `Percent` | 현재 Adapter가 가격에 연결하는 연산 |
| Value | 아래 예시 참고 | Add=금액, Percent=소수 비율 |

지원하지 않거나 기대와 다른 설정:

| 설정 | 결과 |
|------|------|
| Target = `BuyPrice` | 이번 계절 **판매** 정책에서 SellPrice를 바꾸지 않음 |
| Target = `None` / `BaseMoveSpeed` | 가격 Adapter가 무시하거나 에디터 검증에서 `None`으로 보정될 수 있음 |
| Operation = `Subtract` / `None` | 현재 가격 Adapter가 매핑하지 않아 적용되지 않음 |
| Operation = `Percent`, Value = `10` | 10%가 아니라 ×11. `0.1`이 +10% |

## Value 입력 규칙

기준은 항상 `Base Sell Price`다. 매칭된 Modifier만 순서대로 적용한 뒤 반올림하고, 최종 단가는 최소 1이다.

```text
Operation = Add
Value = 20
결과 = BaseSellPrice + 20
```

```text
Operation = Percent
Value = 0.2
결과 = BaseSellPrice × 1.2
```

```text
Operation = Percent
Value = -0.1
결과 = BaseSellPrice × 0.9
```

여러 Season SellPrice가 **같은 계절 Source Id**로 매칭되면, Economy `PriceCalculator`의 Modifier Type 정렬 규칙으로 순서대로 적용된다.

## 작성 예시

### 예시 A — 여름에만 판매가 +20%

기본 판매가 200인 상품.

```text
Affect Modify = true

Modifiers[0]
- Modifier Type = Season
- Source Id = summer
- Display Name = 여름 성수기 (아무 설명이나 가능)
- Modifier Bundles[0]
  - Modifier Target = SellPrice
  - Modifier Operation = Percent
  - Value = 0.2
```

| 현재 계절 | commit 판매 단가 |
|-----------|------------------|
| `summer` | 240 |
| `spring` / `autumn` / `winter` | 200 (해당 Season 항목 제외) |

### 예시 B — 계절마다 다른 판매가

기본 판매가 100.

```text
Affect Modify = true

Modifiers[0]  Season / spring / SellPrice / Percent / 0.0   → 봄 100
Modifiers[1]  Season / summer / SellPrice / Percent / 0.2   → 여름 120
Modifiers[2]  Season / autumn / SellPrice / Add / 10        → 가을 110
Modifiers[3]  Season / winter / SellPrice / Percent / -0.1  → 겨울 90
```

한 transaction 안에서는 commit 직전 `currentSeasonId`를 **한 번만** 읽어 모든 판매 라인에 같이 쓴다.

### 예시 C — 잘못된 작성 (적용 안 됨)

```text
Modifier Type = Season
Source Id = Summer          ← 대문자 → 제외
Target = SellPrice
Operation = Percent
Value = 0.2
```

```text
Modifier Type = Season
Source Id = (비움)
Display Name = summer       ← Display Name은 매칭에 미사용 → 제외
Target = SellPrice
```

```text
Affect Modify = false       ← Modifier 전체 무시
```

## 런타임에서 가격이 정해지는 위치

```text
판매 확정
→ saveData.world.currentSeasonId 캡처
→ TradeItem AffectModify / Modifiers 변환
→ Season + SellPrice 항목만 Source Id 매칭으로 남김
→ PriceCalculator로 단가 계산
→ Cargo / 시장 재고 / tradingCurrency 변이 후 Save
```

- 계절 권위: Framework 달력 → `SaveData.world.currentSeasonId`
- 자격 필터: `SeasonalSellPriceModifierSelector`
- 산술: `PriceCalculator` (달력을 직접 조회하지 않음)

현재 계절을 플레이 중 확인·전진하는 방법:  
[`Framework_Game_Calendar_and_Seasons_API_Guide.md`](./Framework_Game_Calendar_and_Seasons_API_Guide.md)

## Check

설정 후 아래를 확인한다.

1. `Affect Modify`가 켜져 있는가.
2. 계절 판매용 항목의 `Modifier Type`이 `Season`인가.
3. `Source Id`가 `spring` / `summer` / `autumn` / `winter` 중 하나인가 (소문자).
4. Bundle `Target`이 `SellPrice`인가.
5. `Operation`이 `Add` 또는 `Percent`이고, Percent면 Value가 소수 비율인가.
6. 해당 계절에서 판매 확정 시 화폐 증가액이 `단가 × 수량`과 같은가.
7. 다른 계절로 바꾼 뒤 같은 상품을 팔면 base(또는 그 계절용 항목) 단가로 바뀌는가.
8. `Affect Modify`를 끄면 기본 판매가로 돌아오는가.

## Risk 및 주의사항

- `Source Id` 오타는 크래시 없이 **조용히 무시**된다. 밸런스가 “안 먹히는” 것처럼 보이면 Source Id를 먼저 본다.
- Seasonal BuyPrice·판매 패널 표시 정렬·거리 배율·번개 잭팟은 이 가이드 범위 밖이다.
- 일반 Modifier(Town 등)와 Season SellPrice를 함께 둘 수 있다. Season SellPrice만 계절 필터를 받고, 나머지는 기존처럼 통과한다.
- 운영 SO와 Sandbox SO를 동시에 열어 값을 복사할 때 `Source Id`가 한글·Display Name으로 바뀌지 않았는지 확인한다.
