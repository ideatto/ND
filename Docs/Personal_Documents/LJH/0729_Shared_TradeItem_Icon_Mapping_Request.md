# Shared TradeItem 아이콘 매핑 작업 요청서

## 1. 작업 목적

건축·증축 UI는 요구 재료마다 다음 정보를 표시해야 한다.

- 아이템 표시 이름
- 아이템 아이콘
- 플레이어 보유 수량
- 건축 요구 수량

보유 수량은 `PlayerMainManager.GetItemCount(itemId)`에서 조회한다.
표시 이름과 아이콘은 전역 Shared Game Data의 기존 조회 API인
`ISharedGameDataProvider.TryGetTradeItem()`을 통해 얻을 수 있어야 한다.

현재 `SharedTradeItemDefinition`에는 `DisplayName`은 있지만 `Sprite` 아이콘이
없다. 이 작업에서는 원본 `TradeItemData.Icon`을
`SharedTradeItemDefinition.Icon`으로 매핑하여, UI가 원본
`TradeItemData`를 별도로 조회하지 않아도 필요한 표시 정보를 얻을 수 있게 한다.

---

## 2. 수정 대상

### 필수 수정 파일

1. `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataView.cs`
2. `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataService.cs`

### 원본 데이터 확인 파일

- `Assets/99.Sandbox/_LJH/01.Script/Data/TradeItemData.cs`

원본 `TradeItemData`에는 아래 프로퍼티가 이미 존재한다.

```csharp
public Sprite Icon => icon;
```

따라서 새 아이콘 데이터나 별도의 아이콘 Catalog를 만들지 않고 이 값을
그대로 Shared Definition에 전달한다.

---

## 3. 요구 변경 사항

### 3.1 `SharedTradeItemDefinition`에 아이콘 필드 추가

`SharedGameDataView.cs`의 `SharedTradeItemDefinition`에 `Sprite Icon`을
추가한다.

이 파일에서 `Sprite` 타입을 사용할 수 있도록 `using UnityEngine;`도
추가한다.

```csharp
using System.Collections.Generic;
using ND.Economy;
using UnityEngine;
```

```csharp
public sealed class SharedTradeItemDefinition
{
    public string Id;
    public string DisplayName;
    public Sprite Icon;
    public string Rarity;
    public string Category;
    public long BaseBuyPrice;
    public long BaseSellPrice;
    public bool CanStack;
    public int MaxCount;
    public float Weight;
    public bool IsConsumable;
    public bool LocalSpecialty;
    public List<PriceModifierInput> PriceModifiers =
        new List<PriceModifierInput>();
}
```

기존 필드의 이름, 타입 및 순서는 특별한 이유가 없다면 변경하지 않는다.

### 3.2 `SharedGameDataService`에서 원본 아이콘 매핑

`SharedGameDataService.AddTradeItems()`에서 `TradeItemData`를
`SharedTradeItemDefinition`으로 변환할 때 `Icon`을 함께 매핑한다.

```csharp
target.Add(id, new SharedTradeItemDefinition
{
    Id = id,
    DisplayName = item.DisplayName,
    Icon = item.Icon,
    Rarity = item.Rarity.ToString(),
    Category = item.Category.ToString(),
    BaseBuyPrice = item.BaseBuyPrice,
    BaseSellPrice = item.BaseSellPrice,
    CanStack = item.CanStack,
    MaxCount = item.MaxCount,
    Weight = item.Weight,
    IsConsumable = item.IsConsumable,
    LocalSpecialty = item.LocalSpecialty,
    PriceModifiers = ToPriceModifierInputs(item.Modifiers)
});
```

매핑 방향은 반드시 다음과 같다.

```text
TradeItemData.Icon
        ↓
SharedTradeItemDefinition.Icon
```

`Sprite`를 복제하거나 런타임에 새로 생성하지 않고 원본 에셋 참조를 그대로
전달한다.

---

## 4. 변경하지 않을 사항

다음 사항은 이번 작업 범위에 포함하지 않는다.

- `ISharedGameDataProvider.TryGetTradeItem()` 시그니처 변경
- `TryGetTradeItemAsset()` 같은 원본 SO 반환 API 추가
- `SandboxSharedGameDataCatalog`의 필드 또는 공개 API 변경
- `TradeItemData` 구조 변경
- `SaveData` 구조 및 버전 변경
- `PlayerMainManager` 변경
- 건축 재료 보유량 조회 또는 차감 로직 구현
- 건축 ViewData 및 Presenter 구현
- 아이콘 전용 Catalog 또는 `IconKey` 시스템 추가
- 기존 가격·카테고리·희귀도·무게 매핑 규칙 변경

`SharedTradeItemDefinition`은 런타임 조회용 정의이며 SaveData DTO가 아니다.
따라서 `Sprite` 필드 추가를 이유로 SaveData 마이그레이션을 수행하지 않는다.

---

## 5. null 아이콘 처리 규칙

`TradeItemData.Icon`이 `null`이어도 Shared Game Data 로드 전체를
실패시키지 않는다.

```csharp
definition.Icon == null
```

은 허용되는 결과로 취급한다. UI는 추후 기본 아이콘을 표시할 수 있다.

이번 작업에서 아이콘 누락을 Catalog 오류로 추가하거나
`SharedGameDataService.Load()` 실패 사유로 만들지 않는다.

기존의 다음 검증은 그대로 유지한다.

- 원본 배열의 null 항목 검증
- 비어 있는 ID 검증
- 중복 ID 검증
- 기존 TradeItem 필드 검증

---

## 6. 기대 사용 방식

작업 완료 후 UI Builder는 기존 Provider API를 그대로 사용한다.

```csharp
SharedTradeItemDefinition itemDefinition;

if (sharedGameData.TryGetTradeItem(itemId, out itemDefinition))
{
    itemViewData.itemId = itemDefinition.Id;
    itemViewData.displayName = itemDefinition.DisplayName;
    itemViewData.icon = itemDefinition.Icon;
}
```

보유 수량은 Shared Definition이 아니라 `PlayerMainManager`에서 조회한다.

```csharp
int ownedQuantity =
    PlayerMainManager.Instance != null
        ? PlayerMainManager.Instance.GetItemCount(itemId)
        : 0;
```

즉, 각 데이터의 책임은 다음과 같다.

| 정보 | 조회 위치 |
|---|---|
| 아이템 ID | `SharedTradeItemDefinition.Id` |
| 표시 이름 | `SharedTradeItemDefinition.DisplayName` |
| 아이콘 | `SharedTradeItemDefinition.Icon` |
| 현재 보유량 | `PlayerMainManager.GetItemCount(itemId)` |
| 건축 요구량 | `BuildRequireItem.quantity` |

---

## 7. 검증 항목

### 코드 검증

- `SharedGameDataView.cs`가 `UnityEngine.Sprite`를 정상적으로 참조한다.
- `SharedTradeItemDefinition.Icon`의 타입이 `Sprite`이다.
- `AddTradeItems()`가 `item.Icon`을 `Icon`에 매핑한다.
- 기존 `TryGetTradeItem()`의 시그니처와 동작이 유지된다.
- 기존 TradeItem 필드가 누락되거나 변경되지 않는다.
- 새로운 컴파일 오류가 발생하지 않는다.

### 런타임 검증

아이콘이 지정된 `TradeItemData` 하나를 선택하여 다음을 확인한다.

```csharp
SharedTradeItemDefinition definition;
bool found = provider.TryGetTradeItem(itemId, out definition);

Debug.Assert(found);
Debug.Assert(definition != null);
Debug.Assert(definition.Icon == sourceTradeItemData.Icon);
```

추가로 아이콘이 `null`인 항목이 존재한다면 다음도 확인한다.

- 해당 항목의 `TryGetTradeItem()`은 성공한다.
- 반환된 `definition.Icon`만 `null`이다.
- Shared Game Data 전체 로드는 실패하지 않는다.

---

## 8. 완료 조건

아래 조건을 모두 만족하면 작업 완료로 판단한다.

1. `SharedTradeItemDefinition`에 `Sprite Icon`이 추가되어 있다.
2. `SharedGameDataService.AddTradeItems()`가 `TradeItemData.Icon`을 매핑한다.
3. 기존 `TryGetTradeItem()`만으로 ID, 표시 이름, 아이콘을 모두 조회할 수 있다.
4. 기존 Shared Game Data 검증 및 조회 동작에 회귀가 없다.
5. 아이콘이 없는 아이템도 Shared Game Data 로드에 성공한다.
6. Unity 프로젝트 컴파일 오류가 없다.
