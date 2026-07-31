# Warehouse UI 새 대화 인수인계

## 1. 작업 환경

- Unity 프로젝트: `C:\Users\ADMIN\ND`
- 대상: InGame/Village의 Warehouse Inventory UI
- 구현 전 Unity MCP 연결 상태부터 확인한다.
- 첫 단계에서 다음 도구가 실제로 노출되는지 확인한다.
  - Scene 관리/조회 도구 (`manage_scene` 포함)
  - GameObject 검색·조회 도구
  - Component 검색·조회 도구
  - Prefab/Asset 조회 및 수정 도구
  - Play Mode 제어, Console 로그 조회, 화면 캡처 도구
- 도구 이름을 추측하지 말고 현재 세션에 노출된 Unity MCP 도구 목록과 스키마를 먼저 확인한다.
- MCP가 노출되지 않으면 코드나 Scene을 임의로 수정하지 말고 연결이 필요하다고 보고한다.

## 2. 우선 읽을 문서

1. `C:\Users\ADMIN\ND\Docs\Warehouse_Inventory_UI_Implementation_Spec.md`
2. `C:\Users\ADMIN\ND\output\pdf\WareHouse_UI_Approved_Revisions.pdf`

PDF 1페이지의 원본 목업 형태를 UI 기준으로 삼고, 2~3페이지의 승인 규칙을 구현 기준으로 사용한다.

## 3. 확정된 핵심 결정

### Warehouse 슬롯 용량

- 공용 `BuildData.DataPerLevel`에 `inventorySlotCount`를 추가하지 않는다.
- `BuildData`를 상속하지 않는 별도 `WarehouseCapacityData` SO와 Asset을 만든다.
- 레벨별 데이터는 `WarehouseCapacityPerLevel(level, inventorySlotCount)`로 둔다.
- 초기값:
  - Lv.0: 0칸
  - Lv.1: 10칸
  - Lv.2: 20칸
  - Lv.3: 30칸
- 현재 레벨은 저장된 마을 건물 데이터 또는 `VillageBuildingRegistry`의 Warehouse `currentLevel`에서 얻는다.
- 배열 인덱스로 접근하지 않고 `level == currentLevel`인 항목을 찾는다.
- SO/레벨 데이터 누락은 0칸, Notice, 진단 로그로 안전하게 실패시킨다.

### Cargo 슬롯

- Cargo UI 슬롯 Prefab 12개를 초기 풀로 미리 만들고 재사용한다.
- 실제 Cargo 슬롯 제한의 단일 권위값은 `wagon.inventorySlotCount`다.
- UI는 이 값과 동일한 수만큼 슬롯을 활성화한다.
- 활성화된 UI GameObject 수를 용량 계산의 원본으로 사용하지 않는다.
- `wagon.inventorySlotCount > 12`이면 부족한 수만 추가 생성하여 풀을 확장한다.

```text
wagon.inventorySlotCount
        ↓
실제 Cargo 스택/슬롯 수용량 계산
        ↓
동일한 수만큼 UI 슬롯 활성화
```

### 아이템 이동

- 아이템 클릭 시 화살표로 즉시 이동하지 않고 수량 선택 UI를 연다.
- 수량창에는 이동 방향, 최소, `-`, 선택 수량, `+`, 최대, 취소, 이동 확인만 표시한다.
- 원본 목업의 별도 저장 버튼은 제거한다.
- 이동 확인 시 재검증, 양쪽 데이터 변경, 저장을 하나의 원자적 작업으로 처리한다.
- 저장 실패 시 양쪽 인벤토리를 모두 롤백한다.

### 최대 이동 수량

창고에서 Cargo:

```text
min(
    창고 소스 수량,
    Cargo 스택/슬롯 수용량,
    Cargo 남은 적재 무게 수용량
)
```

Cargo에서 창고:

```text
min(
    Cargo 소스 수량,
    Warehouse 스택/슬롯 수용량
)
```

- `maxTransfer == 0`이면 유효 범위 `1..max`가 없으므로 수량창을 열지 않는다.
- 실패 원인을 `CARGO_FULL`, `CARGO_OVERWEIGHT`, `WAREHOUSE_FULL` 등의 코드로 반환하고 Notice UI 문구로 변환한다.
- 수량창이 열린 뒤 상태가 바뀔 수 있으므로 이동 확인 시 최대 수량을 다시 계산한다.

### 스택과 무게

```text
effectiveStackSize =
    TradeItem.CanStack
        ? max(1, TradeItem.MaxCount)
        : 1
```

- 저장 데이터가 합산 수량을 보관하더라도 UI는 유효 스택 크기에 맞춰 여러 슬롯으로 분할한다.
- `CanStack`, `MaxCount`, `Weight`는 `ISharedGameDataProvider`의 현재 TradeItem 카탈로그 값을 사용한다.
- Cargo 적재량은 `CaravanSaveDataMapper.ToRuntime()` 후 `ND.Economy.CaravanCalculator.GetCurrentLoad/GetMaxLoad`로 계산한다.
- 식량 무게도 현재 적재량에 포함한다.

### Caravan 선택

- Warehouse UI에서 선택한 Caravan은 UI 로컬 상태다.
- `SaveData.selectedCaravanId`를 변경하지 않는다.
- 표시 정렬은 `slotIndex`, 데이터 조회와 명령은 `caravanId`를 사용한다.
- BaseCamp에 있고 이용 가능한 Caravan만 선택할 수 있다.
- 빈 슬롯의 `404`는 연출로 유지하고 `캐러밴 없음` 보조 설명을 제공한다.
- 잠긴 슬롯은 `404`를 표시하지 않고 중앙 자물쇠 아이콘과 `잠긴 슬롯` 설명으로 빈 슬롯과 구분한다.
- 빈 슬롯과 잠긴 슬롯은 시각적으로 비활성 상태를 유지하되 Button 클릭은 허용한다.
- 클릭 시 Caravan 선택으로 진행하지 않고 기존 Notice UI로 각각 `이 슬롯에는 생성된 캐러밴이 없습니다.`, `아직 사용할 수 없는 캐러밴 슬롯입니다.`를 안내한다.

### Backdrop

- 수량창이 열려 있으면 전용 Modal Blocker가 입력을 받는다.
- Backdrop 입력 한 번으로 수량창과 Warehouse가 함께 닫히면 안 된다.
- 같은 pointer sequence를 소비하고 수량창이 닫힌 뒤 약 `0.15초`의 `unscaledTime` 가드를 둔다.
- 전송 검증/저장 중에는 확인 버튼과 Backdrop 중복 입력을 잠근다.

## 4. 구현 원칙

- 외부 또는 다른 담당자의 스크립트 수정은 최소화한다.
- 가능한 한 이번 기능 전용 스크립트, Presenter, Service, SO에서 해결한다.
- 불가피하게 공용 코드를 수정하면 수정 이유·방향·영향 범위를 코드 주석으로 남긴다.
- 기존 구매 UI를 직접 복제하거나 참조를 끊는 방식으로 임의 변경하지 않는다.
- Scene/Prefab 직렬화 참조는 Unity MCP를 통해 실제 연결 상태를 확인하며 설정한다.
- 매 프레임 `Update()`로 UI를 감시하지 않는다. 버튼 이벤트와 Framework 변경 이벤트로 갱신한다.

## 5. 구현 전 프로젝트 조사

다음 항목을 코드와 Unity MCP 양쪽에서 확인하고, 기존 구조를 재사용할 수 있는지 먼저 보고한다.

- Warehouse 건물 선택/Popup 진입 경로
- `PlayerMainManager.HomeInventory` 저장 구조
- `BaseCampInventoryTransferService` 현재 구현
- Caravan Cargo SaveData와 Runtime mapper
- Wagon `inventorySlotCount`
- `ISharedGameDataProvider`와 TradeItem 조회 방식
- 기존 Notice UI 호출 방식
- 기존 수량 선택 Popup과 Inventory Slot Prefab
- `FrameworkEvents.CaravanCargoChanged`
- `FrameworkEvents.HomeInventoryChanged` 존재 여부
- Warehouse `currentLevel` 조회 경로
- 공용 Catalog에 WarehouseCapacityData를 연결할 수 있는지

조사 후 재사용 대상과 신규 생성 대상을 구분해서 보고한 다음 구현한다.

## 6. 예상 신규/수정 단위

정확한 이름은 기존 네이밍 규칙을 확인한 뒤 결정한다.

- Warehouse Inventory Popup Presenter/View
- Warehouse Capacity SO 스크립트와 Asset
- Warehouse/Cargo Slot View 또는 기존 Slot View 재사용
- 수량 선택 Popup 연결
- 양방향 Inventory Transfer Service
- 실패 코드와 Notice 매핑
- Home Inventory 변경 이벤트
- Warehouse UI Prefab
- 필요할 경우 Warehouse 건물 클릭 연결부의 최소 수정

## 7. 완료 조건

- Warehouse 클릭으로 UI가 열린다.
- Warehouse 현재 레벨에 맞는 Player Inventory 슬롯만 사용할 수 있다.
- Caravan 선택 전에는 Caravan 슬롯 목록, 선택 후에는 해당 Cargo가 표시된다.
- Cargo UI와 실제 제한이 모두 `wagon.inventorySlotCount`를 따른다.
- 양방향 아이템 클릭으로 수량 선택 후 이동할 수 있다.
- 대상 슬롯 및 Cargo 무게를 넘길 수 없다.
- 최대 이동 수량 0이면 정확한 Notice가 표시된다.
- 즉시 저장되고 실패 시 양쪽 데이터가 롤백된다.
- Caravan 변경 시 Cargo 스크롤만 초기화된다.
- Backdrop 입력이 계층적으로 처리된다.
- Play Mode 재진입 또는 저장 데이터 재로드 후 결과가 유지된다.
- Console에 신규 Error/Exception이 없다.

## 8. 새 대화 시작용 프롬프트

아래 내용을 새 대화의 첫 메시지로 전달한다.

```text
프로젝트 경로는 C:\Users\ADMIN\ND야.

Warehouse Inventory UI 구현을 이어서 진행해줘. 먼저 아래 두 문서를 완전히 읽고 합의된 사양을 기준으로 작업해.

- C:\Users\ADMIN\ND\Docs\Warehouse_UI_New_Chat_Handoff.md
- C:\Users\ADMIN\ND\Docs\Warehouse_Inventory_UI_Implementation_Spec.md
- 참고 PDF: C:\Users\ADMIN\ND\output\pdf\WareHouse_UI_Approved_Revisions.pdf

코드나 Scene을 수정하기 전에 Unity MCP에서 manage_scene, GameObject/Component 조회, Prefab/Asset, Play Mode 및 Console 조회 도구가 실제로 노출되는지 먼저 확인하고 보고해. 도구 이름은 추측하지 말고 현재 노출된 스키마를 확인해.

그다음 프로젝트의 기존 Warehouse 선택 경로, PlayerMainManager.HomeInventory, BaseCampInventoryTransferService, Caravan Cargo, Wagon inventorySlotCount, Notice UI, 수량 선택 Popup, Inventory Slot Prefab, FrameworkEvents를 조사해. 재사용할 것과 신규 생성할 것을 구분해서 보고한 뒤 최소 수정으로 구현해.

중요한 제약:
- 공용 BuildData.DataPerLevel에 inventorySlotCount를 추가하지 않는다.
- BuildData를 상속하지 않는 별도 WarehouseCapacityData SO/Asset을 사용한다.
- Cargo 실제 슬롯 제한의 권위값은 wagon.inventorySlotCount이며 UI 활성 슬롯 수도 같은 값을 따른다.
- UI 활성 GameObject 수를 실제 용량의 원본으로 사용하지 않는다.
- 외부/다른 담당자 코드는 최소 수정하고 불가피한 수정에는 이유·방향·영향을 주석으로 남긴다.
- Update 기반 매 프레임 감시는 사용하지 않는다.

구현 후에는 Unity MCP로 Scene/Prefab 참조, Play Mode 동작, 저장/재로드, Console 로그를 검증하고 수정 파일 및 영향 범위를 보고해.
```


