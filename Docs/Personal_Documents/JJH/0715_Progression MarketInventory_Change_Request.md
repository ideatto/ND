# 상점 재고·물품 적재 저장 연동 변경 요청

작성일: 2026-07-15  
대상 브랜치: `feature/UI/Product/JJH`

## 목적

상점 재고를 실제 UTC 시간 구간과 월드 시드에 따라 결정적으로 갱신하고, 물품 적재 내역을 기존 카라반 화물 저장 구조에 보존하며, 구매 확정·취소를 실제 `CurrencyWallet`과 원자적으로 연동한다.

UI 측 구현과 통합 프로브는 완료했지만 소유권 충돌을 피하기 위해 `ND_MARKET_SAVE_SCHEMA_VNEXT` 조건부 컴파일로 비활성화했다. 아래 소유 팀 변경이 반영된 뒤 해당 심볼을 프로젝트 빌드 설정에서 활성화하면 된다.

## QA / 통합 팀 요청 — `SaveData`

저장 시스템을 담당하는 QA / 통합은 `WorldSaveData`에 다음 데이터를 추가한다.

- `List<MarketInventorySaveData> marketInventories`
- `MarketPurchasePreparationSaveData marketPurchasePreparation`

제안 DTO:

```csharp
[Serializable]
public sealed class MarketInventorySaveData
{
    public string marketId = string.Empty;
    public long refreshIndex;
    public long nextRefreshUtcTicks;
    public int seed;
    public List<MarketStockSaveData> stocks = new();
}

[Serializable]
public sealed class MarketStockSaveData
{
    public string itemId = string.Empty;
    public int quantity;
    public long unitPrice;
}

[Serializable]
public sealed class MarketPurchasePreparationSaveData
{
    public string marketId = string.Empty;
    public bool isCommitted;
    public long totalCost;
    public int cargoHash;
}
```

`loadedLines` 자체를 별도 저장하지 않고 기존 `SaveData.caravan.cargo : List<CargoEntrySaveData>`에 `TradeItemSaveData + quantity`로 매핑한다. 따라서 동일 데이터의 이중 저장은 필요 없다.

## QA / 통합 팀 요청 — `JsonSaveService`

QA / 통합 담당자는 JSON 저장 서비스의 로드 정규화 단계에서 다음을 보장한다.

- `world.marketInventories`가 null이면 빈 리스트 생성
- 각 `MarketInventorySaveData.stocks`가 null이면 빈 리스트 생성
- `world.marketPurchasePreparation`이 null이면 기본 객체 생성

기존 JSON 직렬화 흐름은 유지한다. 스키마 버전 마이그레이션 정책이 있다면 새 필드 기본값 생성도 해당 마이그레이션에 포함해 달라.

## Economy / Progression 팀 작업 — 재고 기반 시장 단가

`ND_MARKET_SAVE_SCHEMA_VNEXT`가 활성화되면 Economy / Progression은 도시별 재고 수량을 반영한 시장 단가 계산 로직을 제공한다.

가격 데이터의 책임과 사용 기준은 다음과 같다.

- Content의 `TradeItemData.BaseBuyPrice`는 변경하지 않고 상품 기준 가격으로 유지한다.
- Economy / Progression은 `BaseBuyPrice`, `marketId`, 시장 재고 수량과 갱신 구간을 입력으로 받아 실제 시장 구매 단가를 계산한다.
- 계산된 단가는 `MarketStockSaveData.unitPrice`에 저장한다.
- QA / 통합은 `unitPrice`가 저장·로드 후에도 같은 시장 갱신 구간에서 유지되도록 시장 세션과 JSON 저장 흐름에 연결한다.
- 통합 UI 담당 Core는 `BaseBuyPrice`를 다시 계산하거나 수정하지 않고 `MarketStockSaveData.unitPrice`를 가격 표시, 구매 가능 수량, 구매 예정 금액 및 구매 요청에 사용한다.
- `CurrencyWallet`은 `unitPrice * quantity`로 확정된 구매 금액만 차감하거나 환불한다.

시장 단가는 재고 생성·갱신 시 한 번 계산하고 다음 갱신 시점까지 고정한다. 구매로 재고가 감소하더라도 같은 갱신 구간에서는 단가를 즉시 다시 계산하지 않는다. 다음 시장 갱신 때 변경된 재고 기준으로 새 단가를 계산한다.

계절·이벤트·성장 보정을 포함한 `PriceCalculationResult.UnitBuyPrice` 연동은 아직 확정되지 않았으므로 이 변경 범위에 포함하지 않는다.

## Economy / Progression 팀 요청 — `CurrencyWallet`

다음 API 또는 동등한 거래 API를 제공해 달라.

```csharp
CurrencyApplyResult ApplyTradePurchase(CurrencyState state, long purchaseCost);
CurrencyApplyResult ApplyTradeRefund(CurrencyState state, long refundAmount);
```

요구 동작:

- 음수 금액 거부
- 구매 시 `TradeMoney` 잔액 부족 거부
- 성공 시 before/after 스냅샷 반환
- 취소 환불 시 `long` 오버플로 방지
- 에러 코드: 잘못된 구매, 잔액 부족, 잘못된 환불을 구분

## 통합 UI 담당 Core 측 구현 동작

- `worldSeed + marketId + refreshIndex`를 FNV 기반 시드로 만들고 로컬 `System.Random`을 사용한다.
- 같은 시간 구간과 저장 데이터에서는 항상 같은 품목·재고가 생성된다.
- 상점 가격 표시와 구매 계산에는 `MarketStockSaveData.unitPrice`를 사용한다.
- 필수 식량 품목을 항상 포함한다.
- 준비 중인 카라반 화물이 있으면 상점 갱신을 동결해 재실행 시 구매 대상이 바뀌지 않게 한다.
- 초안 변경은 `caravan.cargo`에 저장하되 돈을 차감하지 않는다.
- 확정 시 지갑 차감, 상점 재고 차감, 화물·확정 마커 저장을 한 흐름에서 수행한다.
- 확정 화물을 다시 편집하거나 취소하면 지갑과 재고를 복구한다.

## 검증 결과

Unity 6000.5.2f1 에디터 자동 프로브 통과:

1. 결정적 생성 및 필수 식량 포함
2. UTC 시간 구간 변경 시 재고 갱신
3. 초안 화물 JSON 저장·재로드
4. 실제 `CurrencyWallet` 구매 차감 및 시장 재고 JSON 저장
5. 확정 구매 재편집 시 환불·재입고
6. 취소 시 환불 및 카라반 화물 제거

프로브는 실제 `JsonSaveService`를 사용했으며 기존 사용자 저장 파일은 바이트 백업 후 `finally`에서 복구했다.

## 활성화 절차

QA / 통합 담당자가 세 팀의 변경을 통합한 뒤 아래 절차를 수행하고 결과를 검증한다.

1. 세 팀 변경을 병합한다.
2. `ND_MARKET_SAVE_SCHEMA_VNEXT`를 Unity Scripting Define Symbols에 추가한다.
3. `MarketInventoryIntegrationProbe`를 한 번 실행해 결과를 재확인한다.
4. 실제 씬에서 구매 확정→재실행→적재 복원→취소 흐름을 회귀 테스트한다.
