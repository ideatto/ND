# 마켓 및 무역로 이벤트 데이터 설정 가이드

## Purpose

- 마켓에 등록되는 상품, 재고 수량, 갱신 주기, 구매가와 판매가를 Unity Inspector에서 조절하는 위치를 정리한다.
- 마켓 재고·가격과 무역로 이벤트 데이터를 팀원이 같은 기준으로 설정하고 검증할 수 있게 한다.

## 운영용 SO 위치

### 마켓 데이터

```text
Assets/_Project/02.Data/01_ScriptableObjects/Markets
```

- `Market_BaseCamp.asset`
- `Market_Mount.asset`
- `Market_RiverTown.asset`
- `Market_Windy.asset`

### 무역 상품 데이터

```text
Assets/_Project/02.Data/01_ScriptableObjects/TradeItem
```

- 각 `TradeItem_*.asset`이 상품 하나의 설정을 보유한다.
- Sandbox, Demo, Test 폴더의 SO는 운영용 데이터와 구분한다.

## MarketData에서 수정하는 값

마켓 SO를 선택한 뒤 Inspector에서 다음 값을 수정한다.

### Item Max Quantity

- 마켓 갱신 시 상품 하나에 생성할 수 있는 최대 재고 수량이다.
- 상품별 실제 생성 범위는 아래와 같다.

```text
MarketData.Item Minimum Quantity
~
MarketData.Item Max Quantity
```

- 상품의 최소 재고가 마켓 최대 재고보다 크면 최대 재고 값으로 제한된다.

### Item Minimum Quantity

- 마켓 갱신 시 각 등록 상품에 생성할 최소 재고 수량이다.
- `Item Max Quantity`와 같은 `MarketData` SO에서 설정한다.
- 수량 값이므로 타입은 `int`이며 최소 허용값은 `1`이다.
- 기존 마켓 SO에서 값이 비어 있거나 0이면 런타임에서 `1`로 처리한다.

예시:

```text
Item Minimum Quantity = 3
Item Max Quantity = 10

각 상품의 실제 생성 범위 = 3 ~ 10
```

### Item Renewal Cycle

- 마켓 재고 갱신 주기다.
- 현재 런타임에서 사용하는 단위는 초다.
- 예시:
  - `60`: 1분
  - `3600`: 1시간
  - `86400`: 1일
- 값이 `1`이면 1초마다 갱신 구간이 바뀌므로 운영 데이터에서는 의도한 값인지 반드시 확인한다.

### Trade Items

- 해당 마켓에 등록할 일반 상품 후보 목록이다.
- 배열에 `TradeItemData` SO를 추가하거나 제거해 상품 구성을 변경한다.
- 현재는 별도의 등록 슬롯 수가 없으므로 배열에 포함된 상품이 모두 등록 대상이 된다.

### Local Specialty Items

- 해당 마켓의 특산품 목록이다.
- 일반 상품 목록과 합쳐져 마켓 카탈로그를 구성한다.
- 동일 상품을 일반 상품과 특산품 양쪽에 중복 등록하지 않는다.

## TradeItemData에서 수정하는 값

상품 SO를 선택한 뒤 Inspector에서 다음 값을 수정한다.

### TradeItem_Stack_Info

#### Max Count

- 화물 슬롯 하나에 쌓을 수 있는 최대 스택 수량이다.
- 마켓 재고 생성 수량의 최대값이 아니다.
- 마켓 최대 재고는 `MarketData.Item Max Quantity`에서 설정한다.

### Trade_Info

#### Base Buy Price

- 해당 상품의 기준 구매 단가다.
- 구매 가격 Modifier는 이 값을 시작점으로 적용된다.
- 마켓 갱신 시 계산된 구매가는 현재 갱신 구간의 마켓 재고 가격으로 저장된다.

#### Base Sell Price

- 해당 상품의 기준 판매 단가다.
- 판매 가격 Modifier는 이 값을 시작점으로 적용된다.
- 마켓 UI 표시가와 실제 판매 정산이 같은 계산 결과를 사용한다.

### TradeItem_Modify_Info

#### Affect Modify

- 가격 Modifier를 적용할지 결정한다.
- 꺼져 있으면 `Modifiers`가 있어도 기본 구매가와 기본 판매가를 사용한다.
- 켜져 있지만 `Modifiers`가 비어 있으면 기본 가격을 유지한다.

#### Modifiers

- 상품 가격에 적용할 변동 규칙 목록이다.
- 각 항목은 원인 정보와 하나 이상의 Modifier Bundle을 가진다.

##### Modifier Type

- 가격 변동 원인을 구분한다.
- 예: `Season`, `Disaster`, `ActiveEvent`, `PlayerGrowth`, `OverSupply`, `AffectToTown`.
- 여러 Modifier가 있으면 Economy 가격 계산기의 Modifier Type 순서로 적용된다.

##### Source Id

- 가격 변동을 발생시킨 데이터나 규칙의 식별자다.
- 디버그와 가격 변동 내역 식별에 사용할 수 있도록 중복되지 않는 값을 권장한다.

##### Display Name

- 가격 변동 원인을 UI 또는 디버그에서 식별하기 위한 이름이다.

##### Modifier Target

- `BuyPrice`: 구매가에만 적용한다.
- `SellPrice`: 판매가에만 적용한다.
- `None`: 가격에 적용하지 않는다.
- `BaseMoveSpeed`는 마켓 가격 대상이 아니므로 가격 계산에서 사용하지 않는다.

##### Modifier Operation

- `Add`: 기준 가격에 Value를 금액으로 더한다.
- `Percent`: 기준 가격에 비율을 적용한다.
- 현재 마켓 가격 연결에서 안전하게 지원하는 연산은 `Add`, `Percent`다.
- `Subtract`, `None`은 현재 가격 Adapter에서 적용되지 않는다.

##### Value 입력 예시

```text
Operation = Add
Value = 20
결과 = 기존 가격 + 20
```

```text
Operation = Percent
Value = 0.1
결과 = 기존 가격 × 1.1
```

```text
Operation = Percent
Value = -0.2
결과 = 기존 가격 × 0.8
```

- `10`은 10%가 아니라 1000% 증가이므로 퍼센트 입력 시 소수 비율을 사용한다.
- 최종 가격은 반올림되며 최소 1로 제한된다.

## 가격 적용 흐름

### 구매 가격

```text
TradeItemData.Base Buy Price
→ Affect Modify 확인
→ BuyPrice 대상 Modifier 적용
→ 최종 단가 계산
→ 마켓 갱신 재고의 unitPrice로 저장
→ UI 구매 표시와 실제 구매 결제에 사용
```

- 구매 Modifier를 Inspector에서 바꿔도 이미 생성된 현재 마켓 재고 가격은 즉시 바뀌지 않을 수 있다.
- 다음 재고 갱신 또는 신규 재고 생성 후 변경된 구매가가 반영된다.

### 판매 가격

```text
TradeItemData.Base Sell Price
→ Affect Modify 확인
→ SellPrice 대상 Modifier 적용
→ 최종 단가 계산
→ UI 판매 표시와 실제 판매 정산에 사용
```

- 판매가는 상품 SO에서 결정적으로 다시 계산한다.
- 플레이 중 Inspector 값을 바꾸면 판매 표시와 정산에 바로 반영될 수 있다.
- 운영 빌드에서는 SO가 런타임에 변경되지 않는 것을 전제로 한다.

## 설정 예시

기본 구매가 100, 기본 판매가 150인 상품에 구매가 10% 증가와 판매가 20 추가를 적용하는 예시다.

```text
Affect Modify = true

Modifier 1
- Modifier Type = AffectToTown
- Modifier Target = BuyPrice
- Modifier Operation = Percent
- Value = 0.1

Modifier 2
- Modifier Type = Season
- Modifier Target = SellPrice
- Modifier Operation = Add
- Value = 20
```

예상 결과:

```text
구매가 = 110
판매가 = 170
```

## Check

설정 변경 후 다음 항목을 확인한다.

1. 마켓을 열었을 때 상품 재고가 최소값 이상인지 확인한다.
2. 재고가 `MarketData.Item Max Quantity`를 넘지 않는지 확인한다.
3. UI 구매 표시 가격이 예상 Modifier 계산값과 같은지 확인한다.
4. 구매 확정 후 차감된 통화가 표시 가격과 같은지 확인한다.
5. UI 판매 표시 가격이 예상 Modifier 계산값과 같은지 확인한다.
6. 판매 확정 후 증가한 통화가 표시 가격과 같은지 확인한다.
7. `Affect Modify`를 끄면 기본 가격으로 돌아오는지 확인한다.

## Risk 및 주의사항

- Scene, Prefab, Package 설정으로 조절하는 값이 아니다.
- 운영용 SO와 Sandbox/Demo/Test SO를 혼동하지 않는다.
- `Max Count`는 화물 스택 수이고 `Item Max Quantity`는 마켓 재고 최대 수량이다.
- 마켓 최소 재고와 최대 재고가 동일하면 각 등록 상품은 항상 그 수량으로 생성된다.
- `Percent` 값은 백분율 숫자가 아니라 소수 비율이다.
- 가격 Modifier 변경 전후에는 UI 표시 가격과 실제 통화 변화를 함께 확인한다.

# RouteData와 RouteEventData 산적 설정

## 데이터와 코드 위치

### RouteData 정의

```text
Assets/99.Sandbox/_LJH/01.Script/Data/RouteData.cs
```

운영용 Route SO:

```text
Assets/_Project/02.Data/01_ScriptableObjects/Routes
```

### RouteEventData 정의

```text
Assets/99.Sandbox/_LJH/01.Script/Runtime/Data/Calculation/RouteEventData.cs
```

`RouteEventData`는 독립 SO가 아니라 `RouteData.routeEvents` 배열 안에 직렬화되는 이벤트 항목이다.

### 실제 런타임 판정

```text
Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs
Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeRouteEventProcessor.cs
Assets/_Project/01.Core/04_TradeLoop/YHY/JourneyRunner.cs
```

- `TradeProgressCoordinator`가 이동 거리 기준의 체크 간격을 계산한다.
- `TradeRouteEventProcessor`가 이벤트 발생과 이벤트 종류를 결정한다.
- 선택 결과가 `Combat`이면 `JourneyRunner.ResolveBanditRaid()`가 산적 전투와 피해를 처리한다.

## 산적 이벤트가 활성화되는 기본 조건

다음 조건을 모두 만족해야 자동 Route Event 판정이 실행된다.

1. Caravan의 무역 진행 상태가 Traveling이어야 한다.
2. `RouteData.Distance`가 0보다 커야 한다.
3. `RouteData.Max Event Count`가 0보다 커야 한다.
4. `RouteData.Route Events` 배열에 유효한 항목이 하나 이상 있어야 한다.
5. 산적 이벤트 항목의 `Event Type`이 `Combat`이어야 한다.
6. `Route Event Id`가 비어 있지 않아야 한다.
7. Caravan이 이미 치명적인 실패 상태가 아니어야 한다.

조건 하나라도 충족하지 않으면 해당 무역에서 자동 이벤트를 처리하지 않거나 유효성 검사에 실패한다.

## RouteData Inspector 설정

### Distance

- 무역로의 전체 거리다.
- 이벤트 체크 간격 계산에 사용한다.

### Base Risk Level

- 각 거리 체크에서 Route Event가 발생할 확률이다.
- 범위는 `0 ~ 1`이다.
- `0.2`는 체크 한 번당 20%를 의미한다.
- 이 값은 산적만의 확률이 아니라 `Route Events` 배열에 있는 모든 이벤트의 1차 발생 확률이다.

### Max Event Count

- 한 번의 전체 이동을 몇 개의 동일한 거리 구간으로 나눠 이벤트를 확인할지 결정한다.
- 실제 체크 간격은 다음과 같다.

```text
체크 간격 = RouteData.Distance / RouteData.Max Event Count
```

예시:

```text
Distance = 1000
Max Event Count = 5

200 거리 이동을 완료할 때마다 새 체크 1회
```

- `Max Event Count`는 실제 발생 이벤트 수를 보장하지 않는다.
- 최대 체크 횟수에 가깝고, 각 체크는 `Base Risk Level` 판정에 실패할 수 있다.
- 이미 처리한 체크 인덱스는 Caravan별로 저장되므로 같은 거리 구간을 반복 판정하지 않는다.

### Route Events

- 발생 가능한 Route Event 후보 배열이다.
- 1차 발생 판정에 성공하면 배열 항목 중 하나를 균등하게 선택한다.
- 현재 자동 런타임에서 지원되는 종류는 `Combat`과 `Lucky`다.
- 배열 순서를 변경하면 같은 Trade ID와 체크 인덱스에서도 선택 결과가 달라질 수 있다.
- 동일한 Trade ID, 체크 인덱스와 배열 구성에서는 Stable Hash를 사용하므로 결과가 결정적으로 재현된다.

## RouteEventData 산적 항목 설정

산적 항목은 `RouteData.Route Events` 배열의 원소를 펼쳐 다음처럼 설정한다.

### Event Type

```text
Combat
```

- `Combat`만 산적 전투로 처리된다.
- `Lucky`는 산적이 아니며 현재 Core에서는 발생 기록만 남기고 별도 보상 효과를 적용하지 않는다.
- `None`은 enum에 존재하지만 현재 자동 Processor 유효성 검사에서는 지원 이벤트로 인정되지 않는다.

### Route Event Id

- 이벤트의 고유 식별자다.
- 비어 있으면 전체 이벤트 테이블 유효성 검사가 실패한다.
- 같은 Route 안에서 중복되지 않는 ID를 사용한다.

### Display Name / Description

- UI와 디버그에서 이벤트를 식별하기 위한 표시 데이터다.
- 판정 확률에는 영향을 주지 않는다.

### Bandit Combat Power

- 산적의 전투력이다.
- 값이 0보다 크면 이 값을 사용한다.
- 값이 0이고 `Event Type`이 `Combat`이면 기존 호환 필드인 `Event Value`를 전투력으로 사용한다.

```text
실제 산적 전투력 =
Bandit Combat Power > 0
    ? Bandit Combat Power
    : max(0, Event Value)
```

새 산적 데이터에서는 의미가 명확한 `Bandit Combat Power`를 직접 입력하는 것을 권장한다.

### Cargo Loot Rate

- 산적 전투에 실패했을 때 일반 무역품을 잃는 비율이다.
- 범위는 `0 ~ 1`이다.
- `0.25`는 대상 화물의 25%를 의미한다.

### Fodder Loot Rate

- 산적 전투에 실패했을 때 사료를 잃는 비율이다.
- 범위는 `0 ~ 1`이다.
- `0.5`는 대상 사료의 50%를 의미한다.

### Default Evasion Per / Loss Count

- `RouteEventData`에 남아 있는 기존 전투 호환 필드다.
- 현재 정식 산적 적용 경로는 `Bandit Combat Power`, `Cargo Loot Rate`, `Fodder Loot Rate`를 `JourneyRunner.ResolveBanditRaid()`에 전달한다.
- 현재 판정의 핵심 설정으로 오해하지 않도록 한다.

## 실제 산적 등장 판정 순서

```text
Caravan 이동 거리 증가
→ 완료된 거리 체크 수 계산
→ 아직 처리하지 않은 체크마다 Base Risk Level 판정
→ 성공하면 Route Events 배열에서 항목 하나를 균등 선택
→ 선택 항목이 Combat인지 확인
→ Quest 산적 조우 배율 판정
→ ResolveBanditRaid로 전투 적용
```

세부 순서:

1. 현재 이동 완료 거리를 계산한다.

```text
traveledDistance = progress01 × currentDistanceKm
```

2. 완료된 체크 수를 계산한다.

```text
completedCheckCount = floor(traveledDistance / eventIntervalKm)
```

3. 각 신규 체크에서 Stable Hash 결과가 `Base Risk Level`보다 작은 경우에만 이벤트 후보를 선택한다.
4. 이벤트 후보는 `Route Events` 배열에서 균등 선택한다.
5. 선택된 항목이 `Combat`이면 Quest의 산적 조우 감소 배율을 추가로 판정한다.
6. 모든 판정을 통과하면 산적 전투를 처리하고 해당 체크의 이벤트 발생을 기록한다.
7. Caravan이 치명적인 실패 상태가 되면 남은 체크 처리를 중단한다.

## 산적의 실효 등장 확률

Quest 배율이 1이고 Route Events 배열의 모든 항목이 유효하다고 가정하면 체크 한 번당 특정 Combat 이벤트가 선택될 확률은 다음과 같다.

```text
특정 산적 이벤트 확률
= Base Risk Level × (1 / Route Events 항목 수)
```

Combat 항목이 여러 개라면 산적 종류를 구분하지 않은 전체 산적 확률은 다음과 같다.

```text
전체 산적 확률
= Base Risk Level × (Combat 항목 수 / 전체 Route Events 항목 수)
```

Quest 산적 감소 효과가 적용 중이면:

```text
전체 산적 확률
= Base Risk Level
 × (Combat 항목 수 / 전체 Route Events 항목 수)
 × Bandit Encounter Multiplier
```

예시:

```text
Base Risk Level = 0.3
Route Events = [Combat, Lucky, Lucky]
Bandit Encounter Multiplier = 1

체크 한 번당 산적 확률 = 0.3 × 1/3 = 0.1 = 10%
```

`Max Event Count`가 5라면 체크는 최대 5번 수행되지만, “여정 전체 산적 확률 = 10% × 5”로 단순 합산하면 안 된다. 독립 체크라고 근사할 경우 한 번 이상 산적이 등장할 확률은 다음과 같다.

```text
1 - (1 - 체크당 산적 확률) ^ 체크 횟수
```

위 예시에서는:

```text
1 - (1 - 0.1)^5 ≈ 40.95%
```

## Quest 산적 조우 감소 효과

- Quest 보상 `RouteBanditEncounterReduction`은 Combat 이벤트에만 적용된다.
- 연결된 마을의 양방향 Route에 일정 시간 동안 `Encounter Multiplier`를 적용한다.
- `1`은 변화 없음, `0.5`는 산적 조우 확률 절반, `0`은 산적 Combat 조우 억제다.
- 이 배율은 Lucky 이벤트에는 적용되지 않는다.
- 여러 유효 배율이 있으면 현재 런타임은 가장 낮은 배율을 사용한다.

## Route 설정 예시

```text
RouteData
- Distance = 1000
- Base Risk Level = 0.2
- Max Event Count = 5
- Route Events = 2개
  - bandit_weak (Combat)
  - lucky_find (Lucky)

bandit_weak
- Bandit Combat Power = 100
- Cargo Loot Rate = 0.2
- Fodder Loot Rate = 0.3
```

Quest 배율이 없다면:

```text
체크 간격 = 200
체크당 Route Event 발생 확률 = 20%
발생 후 Combat 선택 확률 = 50%
체크당 산적 실효 확률 = 10%
```

## Route Event 검증 체크리스트

1. Route의 `Distance`가 0보다 큰지 확인한다.
2. `Max Event Count`가 1 이상인지 확인한다.
3. `Base Risk Level`이 의도한 `0 ~ 1` 값인지 확인한다.
4. `Route Events` 배열이 비어 있지 않은지 확인한다.
5. 모든 항목의 `Route Event Id`가 비어 있지 않고 중복되지 않는지 확인한다.
6. 산적 항목의 `Event Type`이 `Combat`인지 확인한다.
7. `Bandit Combat Power`가 의도한 전투력인지 확인한다.
8. 약탈률이 퍼센트 숫자가 아니라 `0 ~ 1` 비율인지 확인한다.
9. 배열의 Combat/Lucky 구성으로 실효 확률이 달라지는 점을 확인한다.
10. Quest 산적 감소 효과가 활성화된 세이브인지 확인한다.
11. 실제 Traveling 상태에서 거리 체크를 통과했는지 확인한다.
