# 상점 재고·물품 적재 저장 연동 변경 요청

작성일: 2026-07-14  
대상 브랜치: `feature/UI/Product/JJH`

## 목적

상점 재고를 실제 UTC 시간 구간과 월드 시드에 따라 결정적으로 갱신하고, 물품 적재 내역을 기존 카라반 화물 저장 구조에 보존하며, 구매 확정·취소를 실제 `CurrencyWallet`과 원자적으로 연동한다.

UI 측 구현과 통합 프로브는 완료했지만 소유권 충돌을 피하기 위해 `ND_MARKET_SAVE_SCHEMA_VNEXT` 조건부 컴파일로 비활성화했다. 아래 소유 팀 변경이 반영된 뒤 해당 심볼을 프로젝트 빌드 설정에서 활성화하면 된다.

## Core 팀 요청 — `SaveData`

`WorldSaveData`에 다음 데이터를 추가해 달라.

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

## Content 팀 요청 — `JsonSaveService`

로드 정규화 단계에서 다음을 보장해 달라.

- `world.marketInventories`가 null이면 빈 리스트 생성
- 각 `MarketInventorySaveData.stocks`가 null이면 빈 리스트 생성
- `world.marketPurchasePreparation`이 null이면 기본 객체 생성

기존 JSON 직렬화 흐름은 유지한다. 스키마 버전 마이그레이션 정책이 있다면 새 필드 기본값 생성도 해당 마이그레이션에 포함해 달라.

## Economy 팀 요청 — `CurrencyWallet`

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

## UI 측 구현 동작

- `worldSeed + marketId + refreshIndex`를 FNV 기반 시드로 만들고 로컬 `System.Random`을 사용한다.
- 같은 시간 구간과 저장 데이터에서는 항상 같은 품목·재고가 생성된다.
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

1. 세 팀 변경을 병합한다.
2. `ND_MARKET_SAVE_SCHEMA_VNEXT`를 Unity Scripting Define Symbols에 추가한다.
3. `MarketInventoryIntegrationProbe`를 한 번 실행해 결과를 재확인한다.
4. 실제 씬에서 구매 확정→재실행→적재 복원→취소 흐름을 회귀 테스트한다.
