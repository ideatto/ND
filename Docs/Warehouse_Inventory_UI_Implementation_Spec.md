# Warehouse Inventory UI 구현 명세

## 1. 목적과 확정 UI

Warehouse를 선택했을 때 Player 거점 인벤토리와 BaseCamp에 정박한 특정 Caravan Cargo 사이에서 아이템을 이동한다.

- 왼쪽: `PlayerMainManager.HomeInventory`
- 오른쪽 최초 상태: 선택 가능한 Caravan 슬롯
- Caravan 선택 후: 선택된 `caravanId`의 Cargo
- 아이템 클릭: 수량 선택 UI
- 수량 확인: 재검증, 양쪽 데이터 변경, 저장을 하나의 원자적 작업으로 수행
- 원본 목업의 별도 `저장` 버튼은 제거
- Warehouse에서 고른 Caravan은 UI 로컬 상태이며 `SaveData.selectedCaravanId`를 변경하지 않음

수량 선택 UI에는 이동 방향, 최소, `-`, 현재 수량, `+`, 최대, 취소, 이동 확인만 표시한다.

```text
창고 → Caravan N
Caravan N → 창고
```

선택 수량은 항상 `1 <= selected <= maxTransfer`를 만족해야 한다.

## 2. Warehouse 전용 슬롯 용량 데이터

### 2.1 결정

`inventorySlotCount`는 Warehouse만 사용하는 도메인 값이므로 공용 `BuildData.DataPerLevel`에는 추가하지 않는다.

별도의 `WarehouseCapacityData` SO와 Asset을 만든다. `BuildData`를 상속하지 않는다.

상속을 사용하지 않는 이유:

- 이름, 비용, Prefab, 레벨별 건설 데이터 등 `BuildData`가 이미 소유한 공통 정보를 다시 가지게 된다.
- Build Asset과 Warehouse Capacity Asset 사이에 중복 데이터가 생길 수 있다.
- 두 Asset의 레벨 배열이 서로 다르게 수정되면 불일치가 발생한다.
- 현재 필요한 것은 Warehouse의 레벨별 슬롯 수뿐이므로 독립된 작은 데이터가 책임이 명확하다.

권장 형태:

```csharp
[CreateAssetMenu(
    fileName = "WarehouseCapacityData",
    menuName = "ND/Building/Warehouse Capacity Data")]
public sealed class WarehouseCapacityData : ScriptableObject
{
    [SerializeField]
    private WarehouseCapacityPerLevel[] dataPerLevels;

    public bool TryGetInventorySlotCount(int currentLevel, out int slotCount)
    {
        foreach (WarehouseCapacityPerLevel data in dataPerLevels)
        {
            if (data.level != currentLevel)
                continue;

            slotCount = Mathf.Max(0, data.inventorySlotCount);
            return true;
        }

        slotCount = 0;
        return false;
    }
}

[Serializable]
public struct WarehouseCapacityPerLevel
{
    [Min(0)] public int level;
    [Min(0)] public int inventorySlotCount;
}
```

초기 Asset 값:

| Warehouse 레벨 | Player Inventory 슬롯 |
|---:|---:|
| 0 | 0 |
| 1 | 10 |
| 2 | 20 |
| 3 | 30 |

### 2.2 조회 흐름

1. 저장된 마을 건물 데이터 또는 `VillageBuildingRegistry`에서 Warehouse `currentLevel`을 얻는다.
2. `WarehouseCapacityData`에서 `level == currentLevel`인 항목을 찾는다.
3. 배열 인덱스를 레벨로 간주하지 않는다.
4. SO 참조 누락, 레벨 항목 누락, 음수 슬롯 값은 임의로 보정해 슬롯을 열지 않는다.
5. 실패 시 0칸으로 처리하고 Notice와 진단 로그를 남긴다.

초기에는 Warehouse Presenter에 SO 직렬화 참조를 두는 방식이 최소 수정이다. 공용 카탈로그 담당 범위가 준비되면 `ISharedGameDataProvider`를 통한 조회로 옮길 수 있다.

## 3. Cargo 슬롯과 아이템 스택

### 3.1 Cargo 슬롯 UI 풀

- Cargo 슬롯 Prefab 12개를 미리 생성하고 비활성화한다.
- 선택한 Caravan의 `wagon.inventorySlotCount`만큼 활성화한다.
- 12개는 UI 풀의 초기 크기이지 실제 Cargo 제한이 아니다.
- 필요한 슬롯이 12개를 넘으면 부족한 수만 추가 생성해 풀을 확장한다.
- Caravan 변경 시 기존 슬롯을 재사용하고 Cargo ScrollRect만 맨 위로 초기화한다.
- 유효한 Wagon이 없거나 슬롯 수가 0이면 Cargo 활성 슬롯은 0칸이다.

### 3.2 스택 표시

```text
effectiveStackSize =
    TradeItem.CanStack
        ? max(1, TradeItem.MaxCount)
        : 1
```

- `CanStack`, `MaxCount`, `Weight`는 저장 데이터에 남은 복사본이 아니라 `ISharedGameDataProvider`의 현재 `TradeItem` 카탈로그 값을 사용한다.
- 저장 데이터에서 동일 아이템 수량이 합산되어 있어도 UI는 `effectiveStackSize`에 맞춰 여러 슬롯으로 분할 표시한다.
- 대상 수용량 계산은 동일 아이템의 기존 스택 빈 공간을 먼저 사용하고, 남은 수량을 빈 슬롯에 새 스택으로 배치한다.
- 소스 수량이 0이 된 항목은 저장 목록과 UI에서 제거한다.

## 4. 최대 이동 수량과 `maxTransfer == 0`

### 4.1 방향별 계산

창고에서 Cargo로:

```text
maxTransfer = min(
    warehouseSourceQuantity,
    cargoStackAndSlotCapacity,
    cargoWeightCapacity
)
```

```text
cargoWeightCapacity =
    floor(max(0, maxLoad - currentLoad) / itemUnitWeight)
```

Cargo에서 창고로:

```text
maxTransfer = min(
    cargoSourceQuantity,
    warehouseStackAndSlotCapacity
)
```

- Cargo가 도착지일 때만 적재 무게 제한을 추가한다.
- 양방향 모두 도착지의 스택/슬롯 제한을 적용한다.
- Cargo 적재량은 `CaravanSaveDataMapper.ToRuntime()` 후 `ND.Economy.CaravanCalculator.GetCurrentLoad/GetMaxLoad`로 계산한다.
- 공식 적재량에는 Cargo와 식량 무게가 모두 포함된다.

### 4.2 `maxTransfer == 0`의 의미

`maxTransfer == 0`은 현재 선택한 아이템을 대상으로 단 하나도 이동할 수 없다는 뜻이다. 수량 선택창의 유효 범위는 `1..maxTransfer`이므로 최대값이 0이면 선택 가능한 수량이 존재하지 않는다.

따라서 수량 선택창을 열어 0을 보여주지 않고, 계산 결과의 실패 원인을 Notice UI로 표시한다.

예시:

```text
창고 보유량 20
Cargo 스택/슬롯 여유 0
Cargo 무게 여유 15

maxTransfer = min(20, 0, 15) = 0
Notice: 캐러밴 적재 칸이 부족합니다.
```

```text
창고 보유량 20
Cargo 스택/슬롯 여유 10
Cargo 무게 여유 0

maxTransfer = min(20, 10, 0) = 0
Notice: 캐러밴의 최대 적재 무게를 초과합니다.
```

소스 수량이 남아 있어도 대상 슬롯이 가득 찼거나 적재 무게가 부족하면 0이 될 수 있다.

계산 결과는 수량뿐 아니라 실패 원인도 함께 반환한다.

```csharp
public readonly struct TransferCapacityResult
{
    public int MaxTransfer { get; }
    public TransferFailureCode FailureCode { get; }
}
```

여러 제한이 동시에 실패할 때 권장 Notice 우선순위:

1. 데이터 오류
2. 캐러밴 위치/이용 상태
3. 소스 수량 부족
4. 대상 슬롯 부족
5. Cargo 적재 무게 부족

수량창이 열린 뒤 상태가 바뀔 수 있으므로 이동 확인 시 `maxTransfer`를 다시 계산한다.

- 새 `maxTransfer == 0`: 수량창을 닫고 Notice 표시, 데이터 변경 및 저장 금지
- 새 `maxTransfer < selected`: 전송 거절 후 최신 최대 수량 안내
- 검증 통과: 전송과 저장 진행

## 5. 전송, 저장과 UI 갱신

UI가 `PlayerMainManager.AddItem/RemoveItem`과 `AddCargo/RemoveCargo`를 순차 호출하지 않는다. 명시적인 `caravanId`를 받는 양방향 전송 서비스가 단일 권위자가 된다.

처리 순서:

1. Framework, SaveData, Player 검증
2. Caravan ID와 BaseCamp 위치 검증
3. Journey/TradeProgress 상태 검증
4. TradeItem 카탈로그 검증
5. 소스 수량과 대상 슬롯 검증
6. Cargo 도착 시 적재 무게 검증
7. 양쪽 SaveData 스냅샷 생성
8. 양쪽 수량 변경
9. 저장
10. 실패 시 양쪽 롤백 및 Notice 표시
11. 성공 후에만 변경 이벤트 발생

이벤트:

- `FrameworkEvents.HomeInventoryChanged`
- `FrameworkEvents.CaravanCargoChanged(string caravanId)`

Warehouse Presenter는 왼쪽과 현재 선택한 Caravan Cargo만 갱신한다.

## 6. Backdrop, 실패 코드와 검증

### 6.1 Backdrop

- 수량 선택 UI가 열려 있으면 전용 Modal Blocker가 입력을 받는다.
- 해당 입력으로 수량창만 닫고 같은 pointer sequence를 소비한다.
- 수량창이 닫힌 뒤 권장 `0.15초`의 `unscaledTime` 닫기 가드를 적용한다.
- 가드가 끝난 뒤 새로 시작한 입력만 Warehouse UI를 닫는다.
- 전송 검증 및 저장 중에는 이동 확인과 Backdrop 입력을 잠근다.

### 6.2 실패 코드와 Notice

- `INVALID_FRAMEWORK`
- `INVALID_CARAVAN`
- `NOT_AT_BASE_CAMP`
- `CARAVAN_BUSY`
- `INVALID_ITEM`
- `INVALID_ITEM_STACK`
- `INVALID_ITEM_WEIGHT`
- `INSUFFICIENT_SOURCE`
- `WAREHOUSE_CAPACITY_DATA_MISSING`
- `WAREHOUSE_FULL`
- `CARGO_FULL`
- `CARGO_OVERWEIGHT`
- `SAVE_FAILED`

내부 코드를 직접 노출하지 않고 사용자 문구로 변환한다.

빈 Caravan 슬롯의 `404`는 의도된 연출로 유지하고 `캐러밴 없음` 보조 문구를 함께 제공한다.

빈 슬롯과 잠긴 슬롯의 클릭 정책:

- 빈 슬롯: `404`, `캐러밴 없음`을 표시한다.
- 잠긴 슬롯: `404`를 숨기고 중앙 자물쇠 아이콘과 `잠긴 슬롯`을 표시한다.
- 두 상태 모두 색감은 비활성 상태처럼 표현하지만 `Button.interactable`은 `true`로 유지한다.
- 클릭해도 Caravan 선택 상태로 전환하지 않는다.
- 빈 슬롯 클릭 시 기존 Notice UI로 `이 슬롯에는 생성된 캐러밴이 없습니다.`를 표시한다.
- 잠긴 슬롯 클릭 시 기존 Notice UI로 `아직 사용할 수 없는 캐러밴 슬롯입니다.`를 표시한다.
- Overlay는 부모 Button의 클릭을 막지 않도록 `raycastTarget`을 비활성화한다.

## 7. 회귀 테스트 체크리스트

- [ ] Warehouse Lv.0/1/2/3에서 슬롯 제한이 0/10/20/30인지
- [ ] `currentLevel`과 같은 level 항목을 값으로 검색하는지
- [ ] Capacity SO/레벨 데이터 누락 시 0칸과 Notice로 안전하게 실패하는지
- [ ] Cargo 슬롯 12개가 재사용되고 13개 이상에서 풀을 확장하는지
- [ ] 합산 저장 수량이 UI에서 여러 스택으로 분할되는지
- [ ] 양방향 모두 대상 스택/슬롯 제한을 적용하는지
- [ ] Cargo 도착 시 식량을 포함한 공식 적재 무게를 적용하는지
- [ ] `maxTransfer == 0`이면 수량창 없이 정확한 Notice를 표시하는지
- [ ] 이동 확인 직전 재검증하는지
- [ ] 저장 실패 시 양쪽 인벤토리를 롤백하는지
- [ ] 저장 성공 후에만 HomeInventory/Cargo UI를 갱신하는지
- [ ] Warehouse 선택이 `selectedCaravanId`를 변경하지 않는지
- [ ] 수량창 Backdrop 입력으로 Warehouse까지 함께 닫히지 않는지
- [ ] 전송 중 중복 입력이 차단되는지
- [ ] 빈 슬롯에 `404`와 `캐러밴 없음`이 함께 표시되는지
- [ ] 잠긴 슬롯에서 `404`가 숨겨지고 중앙 자물쇠 아이콘과 `잠긴 슬롯`이 표시되는지
- [ ] 빈 슬롯과 잠긴 슬롯 클릭 시 선택으로 진행하지 않고 상태별 Notice가 표시되는지


