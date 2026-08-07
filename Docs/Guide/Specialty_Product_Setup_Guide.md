# 퀘스트 보상으로 마을 특산품 추가하기

## 목적

새로운 무역 물품을 특정 마을의 특산품으로 등록하고, 퀘스트 완료 후 해당 마을 시장에서 구매할 수 있도록 설정하는 절차를 설명한다.

특산품은 일반 시장 상품과 다르게 다음 세 데이터가 정확히 연결되어야 한다.

```text
TradeItemData.itemId
    → MarketData.localSpecialtyItems
    → QuestData의 Trade Item 보상 rewardId
```

세 위치의 상품이 서로 다르거나 Market에 등록되지 않으면 퀘스트 보상 검증 또는 시장 노출이 실패한다.

## 데이터 위치

운영 데이터는 다음 경로 아래에 생성한다.

```text
Assets/_Project/02.Data/01_ScriptableObjects/TradeItem
Assets/_Project/02.Data/01_ScriptableObjects/Markets
Assets/_Project/02.Data/01_ScriptableObjects/Quest
```

`Assets/99.Sandbox`는 기존 호환 데이터가 남아 있을 수 있는 레거시 경로다. 신규 운영용 특산품과 퀘스트는 `_Project/02.Data` 아래에 생성한다.

## 1. TradeItemData 생성 및 설정

기존 `TradeItem_*.asset`을 참고하여 새 `TradeItemData` 에셋을 만든다.

Inspector에서 다음 항목을 설정한다.

| 항목 | 설정 | 설명 |
| --- | --- | --- |
| `Item Id` | 프로젝트 전체에서 고유한 ID | Quest 보상의 `Reward Id`와 정확히 일치해야 한다. |
| `Display Name` | 화면에 표시할 이름 | 시장과 퀘스트 UI에 사용된다. |
| `Icon` | 상품 아이콘 | 시장 목록에 표시된다. |
| `Base Buy Price` | 기본 구매 가격 | 시장 가격 계산의 기준이다. |
| `Base Sell Price` | 기본 판매 가격 | 판매 가격 계산의 기준이다. |
| `Max Count` | 슬롯당 최대 수량 | 인벤토리 스택 제한이다. |
| `Weight` | 상품 무게 | 캐러밴 적재량 계산에 사용된다. |
| `Local Specialty` | 활성화 | 반드시 체크해야 특산품 보상으로 인정된다. |

예시 `TradeItem_Fish.asset`:

```text
Item Id: Fish
Display Name: 생선
Local Specialty: On
```

`Item Id`는 에셋 파일명이나 표시 이름이 아니다. 대소문자를 포함한 실제 `Item Id` 문자열을 이후 설정에서 그대로 사용한다.

## 2. 마을 Market에 특산품 등록

특산품을 판매할 마을의 `Market_*.asset`을 연다.

Inspector의 `Local Specialty Items` 배열에 앞에서 만든 `TradeItemData` 에셋을 추가한다.

```text
Market_RiverTown
└─ Local Specialty Items
   └─ TradeItem_Fish
```

주의사항:

- 특산품은 `Trade Items`가 아니라 `Local Specialty Items`에 등록한다.
- 동일 상품을 두 배열에 중복 등록하지 않는다.
- Quest의 `Submission Town Id`에 해당하는 마을 Market에 등록해야 한다.
- 해금 전에는 해당 마을의 구매 목록에 나타나지 않는 것이 정상이다.
- 캐러밴이 이미 보유한 상품은 해금 여부와 관계없이 판매할 수 있다.

## 3. 해금 Quest 생성 및 설정

`Assets/_Project/02.Data/01_ScriptableObjects/Quest` 아래에 `QuestData` 에셋을 만든다.

기본 정보와 완료 비용을 설정한 뒤 `Rewards` 배열에 다음 보상을 추가한다.

| 항목 | 설정 |
| --- | --- |
| `Reward Type` | `Trade Item` |
| `Reward Id` | 특산품의 `TradeItemData.itemId` |
| `Value` | 현재 특산품 해금에서는 사용하지 않으므로 기본값 사용 가능 |

예시 `Quest_SP_RiverTown.asset`:

```text
Quest Id: rivertownproduct
Submission Town Id: RiverTown
Required Trading Currency: 100

Rewards:
- Reward Type: Trade Item
  Reward Id: Fish
```

Quest 저장 시 다음 조건을 만족해야 한다.

1. `Reward Id`와 일치하는 TradeItem이 Shared Game Data Catalog에 존재한다.
2. 해당 TradeItem의 `Local Specialty`가 활성화되어 있다.
3. 상품이 Quest의 `Submission Town Id`에 해당하는 Market의 `Local Specialty Items`에 등록되어 있다.

## 4. Shared Game Data Catalog 갱신

TradeItem, Market 또는 Quest 에셋을 추가하거나 이동한 다음 Unity 상단 메뉴에서 다음 순서로 실행한다.

```text
ND > Framework > Preview Shared Game Data Catalog Sync
ND > Framework > Sync Shared Game Data Catalog
```

Preview 결과에서 예상한 TradeItem과 Quest가 추가 대상으로 표시되고 오류가 없는지 먼저 확인한다.

Sync가 성공하면 다음 두 에셋이 함께 갱신된다.

```text
Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset
Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset
```

Watch Inventory만 다시 갱신해야 하는 경우에는 다음 메뉴를 사용한다.

```text
ND > Framework > Refresh Shared Game Data Watch Inventory
```

두 에셋을 Inspector에서 직접 편집하기보다 동기화 메뉴를 사용하는 것을 권장한다.

## 5. 동작 원리

Quest가 정상 완료되면 SaveData에 마을 ID와 상품 ID의 조합이 저장된다.

```text
(Submission Town Id, Reward Id)
예: (RiverTown, Fish)
```

시장 구매 카탈로그는 현재 마을에 대해 해금된 특산품만 노출한다. 같은 시장 재고 갱신 주기 안에서 특산품이 해금되더라도 기존 상품 재고는 유지하고, 새로 해금된 상품의 재고만 추가한다.

저장에 실패하면 해금 및 시장 재고 변경은 이전 상태로 복원된다.

## 6. 검증 절차

### 데이터 검증

1. TradeItem의 `Item Id`가 고유한지 확인한다.
2. `Local Specialty`가 활성화되어 있는지 확인한다.
3. 대상 Market의 `Local Specialty Items`에 같은 TradeItem 에셋이 등록되어 있는지 확인한다.
4. Quest의 `Submission Town Id`가 대상 마을과 일치하는지 확인한다.
5. Quest 보상의 `Reward Id`가 TradeItem의 `Item Id`와 정확히 일치하는지 확인한다.
6. Catalog Sync 후 Console에 오류가 없는지 확인한다.

### 플레이 검증

1. 대상 마을에 방문하여 Quest가 표시되는지 확인한다.
2. Quest를 수락하고 요구 비용을 준비한다.
3. Quest를 완료하여 비용이 차감되는지 확인한다.
4. 같은 마을의 시장 구매 화면을 다시 연다.
5. 해금한 특산품이 구매 목록에 표시되는지 확인한다.
6. 기존 시장 상품의 재고 수량이 초기화되지 않았는지 확인한다.
7. 저장 후 게임을 다시 실행해도 특산품이 계속 표시되는지 확인한다.

## 문제 해결

### Quest 완료 후 특산품이 보이지 않는다

- Quest 보상의 `Reward Id`와 TradeItem의 `Item Id`가 같은지 확인한다.
- TradeItem의 `Local Specialty`가 활성화되어 있는지 확인한다.
- 대상 Market의 `Local Specialty Items`에 상품이 등록되어 있는지 확인한다.
- Quest의 `Submission Town Id`가 Market이 속한 마을과 일치하는지 확인한다.
- Shared Game Data Catalog와 Watch Inventory를 다시 동기화한다.
- Quest 완료가 실제로 저장되었는지 확인한다.
- 시장 패널을 닫았다가 다시 열어 최신 카탈로그를 요청한다.

### 해금 전부터 상품이 보인다

- 상품이 Market의 일반 `Trade Items` 배열에도 중복 등록되어 있는지 확인한다.
- 기존 SaveData에 이미 같은 `(Town Id, Item Id)` 해금 기록이 있는지 확인한다.

### Catalog Sync가 차단된다

- 중복된 `Item Id` 또는 `Quest Id`가 있는지 확인한다.
- Quest 보상 상품이 대상 마을 Market에 등록되어 있는지 확인한다.
- 신규 에셋이 `_Project/02.Data/01_ScriptableObjects` 아래에 있는지 확인한다.
- Console의 `[CatalogSync]` 오류 메시지에 표시된 에셋 경로와 ID를 확인한다.

## RiverTown 예시 요약

```text
TradeItem_Fish.asset
├─ Item Id: Fish
└─ Local Specialty: On

Market_RiverTown.asset
└─ Local Specialty Items: TradeItem_Fish

Quest_SP_RiverTown.asset
├─ Submission Town Id: RiverTown
└─ Rewards
   └─ Trade Item / Reward Id: Fish
```
