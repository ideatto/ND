# Caravan Set UI 외형 교체 후 기능 재조립

## 1. 목적

다른 작업자가 Caravan Set UI 외형과 계층을 교체하더라도 다음 기능을 잃지 않도록 하기 위한 문서다.

- 소유 마차만 선택 후보에 표시
- 마차를 `contentId`별 그룹으로 표시
- 그룹 아래에 `instanceId`별 개체와 현재/최대 내구도 표시
- 같은 그룹 재클릭 시 접기, 다른 그룹 클릭 시 전환
- 다른 Caravan에서 사용 중인 개체 선택 차단
- 동물은 `contentId`별 수량으로 합쳐 표시
- Wagon이 허용하는 동물 종류만 선택
- 버튼과 개체 행은 부족할 때만 생성하고 이후 재사용

기능 구현 기준 커밋은 `515b3985` (`feat: add pooled wagon instance selection popup`)이다.

현재 브랜치에서는 외형 교체 작업과의 충돌을 피하기 위해 아래 네 파일의 해당 기능 변경을 의도적으로 제거했다.

- `AnimalInventoryPanel.cs`
- `TransportSelectPanel.cs`
- `WagonSelectPopup.cs`
- `Assets/_Project/08.Prefabs/UI_TradePrepare.prefab`

따라서 외형 교체가 끝난 뒤 이 문서에 따라 기능 코드를 복원하고 새 계층에 참조를 다시 연결해야 한다. 독립 기능 자산인 `WagonInstanceRowView.cs`, `WagonInstanceRow.prefab`, `WagonSelectPopup.prefab`은 재조립 재료로 현재 브랜치에 유지한다.

## 2. 파일 소유권과 충돌 처리 원칙

### 기능 코드 — 기능 구현을 우선

- `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/AnimalInventoryPanel.cs`
- `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/TransportSelectPanel.cs`
- `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/WagonSelectPopup.cs`
- `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/WagonInstanceRowView.cs`

외형 작업자는 위 스크립트를 수정하지 않는 것을 원칙으로 한다. 파일이 누락되거나 이전 버전으로 돌아갔다면 다음 명령으로 기능 버전을 복구할 수 있다.

```powershell
git restore --source=515b3985 -- `
  Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/AnimalInventoryPanel.cs `
  Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/TransportSelectPanel.cs `
  Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/WagonSelectPopup.cs `
  Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/WagonInstanceRowView.cs `
  Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/WagonInstanceRowView.cs.meta
```

스크립트도 양쪽에서 수정했다면 파일 전체를 덮지 말고 이 문서의 4~6절에 적힌 기능 계약을 기준으로 수동 병합한다.

### 외형 프리팹 — 외형 작업 결과를 우선

- `Assets/_Project/08.Prefabs/UI_TradePrepare.prefab`
- `Assets/_Project/08.Prefabs/UI/Maps/TradePrepareUI.prefab`

위 프리팹은 `515b3985` 버전으로 통째로 복구하지 않는다. 외형 작업자의 최신 프리팹을 유지한 채 필요한 컴포넌트와 직렬화 참조만 연결한다.

## 3. 재사용 프리팹

다음 프리팹은 외형 작업 프리팹과 별개인 기능 프리팹이다.

- `Assets/_Project/08.Prefabs/UI/Trade/WagonSelectPopup.prefab`
- `Assets/_Project/08.Prefabs/UI/Trade/WagonInstanceRow.prefab`

`WagonInstanceRow.prefab` 루트에는 `WagonInstanceRowView`, `Button`, `Image`, `LayoutElement`가 있고, 자식 `Label`에는 `TextMeshProUGUI`가 있어야 한다.

`WagonInstanceRowView` 참조:

| 필드 | 대상 |
| --- | --- |
| `button` | 루트 Button |
| `label` | 자식 Label의 TextMeshProUGUI |

`WagonSelectPopup` 참조:

| 필드 | 대상 |
| --- | --- |
| `listContainer` | 그룹 버튼과 개체 행을 배치할 목록 Transform |
| `buttonPrefab` | 그룹 제목 Button 템플릿 |
| `instanceRowPrefab` | `WagonInstanceRow.prefab`의 `WagonInstanceRowView` |
| `cancelButton` | 팝업 취소 버튼 |

## 4. `TransportSelectPanel.cs` 기능 계약

`TransportEntry`에는 다음 데이터가 유지되어야 한다.

- `id`: Wagon `contentId`
- `instanceId`: 소유 개체 ID
- `name`: 사용자 표시 이름
- `currentDurability`, `maxDurability`
- `eligibleAnimalTypes`: Wagon에 장착 가능한 `DraftAnimalType[]`
- `owned`: 실제 소유 여부
- `canSelect`, `disabledReason`

`WagonViewData` 변환 규칙:

- `eligibleAnimalTypes = viewData.eligibleAnimalTypes ?? Array.Empty<DraftAnimalType>()`
- `viewData.isOwned == false`이면 `owned = 0`
- 소유 개체이고 `wagonInstanceId`가 있으면 한 항목은 한 개체이므로 `owned = 1`
- `instanceId`를 표시 이름으로 사용하지 않는다.

## 5. `AnimalInventoryPanel.cs` 기능 계약

### 마차 후보

- `owned > 0`인 `TransportEntry`만 `wagonInventory`에 추가한다.
- Legacy 할당 개체가 표시용 ViewData에 있더라도 소유 인벤토리에 없으면 선택 후보가 아니다.

### 동물 집계

- `DraftAnimalViewData.draftAnimalId` 즉 `contentId`를 키로 묶는다.
- 같은 콘텐츠의 선택 가능한 개체 수를 `ownedCount`로 합산한다.
- UI에는 말 네 마리를 `말 ×1` 버튼 네 개가 아니라 `말 ×4` 한 버튼으로 표시한다.
- 저장 Draft에서는 개별 ID 배열 대신 `SetAnimalQuantity(contentId, count)`를 사용할 수 있다.
- Application Service가 다른 Caravan에 배정되지 않은 실제 개체를 앞에서부터 수량만큼 선택한다.

### Wagon 허용 동물 검사

`CanSelectAnimal`은 다음 조건을 모두 만족해야 한다.

1. 현재 선택된 동물 콘텐츠와 같은 종류이거나 아직 아무 동물도 선택하지 않음
2. 현재 Wagon의 `eligibleAnimalTypes`가 비었거나 후보의 `animalType`을 포함함
3. 후보 자체의 `canSelect == true`
4. 남은 수량이 1 이상이고 Wagon 최대 동물 수를 넘지 않음

`AnimalEntry`에는 `DraftAnimalType animalType`이 있어야 하며 `DraftAnimalViewData.animalType`에서 복사한다.

### 기존 편성 복원

- 저장된 `selectedAnimalInstanceIds` 각각을 ViewData에서 찾는다.
- 찾은 개체의 `draftAnimalId`별 수량을 증가시킨다.
- 없는 `instanceId`는 다른 동일 콘텐츠 개체로 자동 대체하지 않고 snapshot을 무효 처리한다.

## 6. `WagonSelectPopup.cs` 기능 계약

- 입력 목록을 `TransportEntry.id` 즉 `contentId`로 그룹화한다.
- 그룹 제목은 `displayName [종류] ×수량` 형식이다.
- 그룹 클릭 시 해당 그룹 바로 아래에 개체 행을 표시한다.
- 개체 행은 `번호. displayName 내구도 현재 / 최대` 형식이다.
- 같은 그룹 재클릭 시 접는다.
- 다른 그룹 클릭 시 이전 그룹을 접고 새 그룹만 연다.
- 개체 행의 클릭 콜백에는 화면에 표시하지 않은 해당 `TransportEntry.instanceId`가 포함되어야 한다.
- `canSelect == false`인 개체 행은 `Button.interactable = false`다.

풀링 규칙:

- 그룹 버튼 기본 생성 0개
- 개체 행 기본 생성 0개
- 부족한 수량만 `Instantiate`
- 갱신 시 기존 객체를 비활성화하고 재사용
- 재사용할 때 텍스트, `interactable`, 클릭 Listener를 모두 다시 Bind
- 펼침, 접힘 또는 탭 전환 때 기존 행을 `Destroy`하지 않음

## 7. 외형 프리팹 연결 순서

외형 작업이 끝난 프리팹을 기준으로 다음 순서로 연결한다.

1. `AnimalInventoryPanel`이 붙은 오브젝트를 컴포넌트 기준으로 찾는다. `S3_Animal`이라는 이름에 의존하지 않는다.
2. 외형 프리팹 안에 기존 `WagonPopup`이 직접 작성되어 있으면 제거하거나 비활성화한다.
3. `WagonSelectPopup.prefab`을 동일 Canvas 아래에 한 번 배치한다.
4. 외형에 맞는 앵커, 크기, 형제 순서와 Sorting 순서를 적용한다.
5. Popup 루트의 기본 상태를 비활성화한다.
6. `AnimalInventoryPanel.wagonPopup`에 배치한 `WagonSelectPopup`을 연결한다.
7. Popup의 `instanceRowPrefab`이 `WagonInstanceRow.prefab`을 참조하는지 확인한다.
8. Backdrop과 다른 전체 화면 Popup보다 앞에 보이며 입력을 정상 차단하는지 확인한다.

`UI_TradePrepare.prefab`을 계속 사용하는 경우 기존 내장 `WagonSelectPopup`의 `instanceRowPrefab` 필드만 연결해도 된다. 하지만 `TradePrepareUI.prefab`처럼 별도 중첩 Popup을 사용하는 구조와 동시에 두 Popup을 활성화하면 안 된다.

`settlementPanel`, `paymentPanel`, `tradeScreenPresenter` 등 Caravan Set 외 다른 기능의 빈 직렬화 필드는 이 작업의 재조립 대상이 아니다.

## 8. 정상 결과

- 미소유 마차가 그룹 수량에 포함되지 않는다.
- `Wagon_M ×2`를 누르면 서로 다른 두 개체가 바로 아래에 표시된다.
- 개체별 내구도가 서로 다르면 각 행에 올바르게 표시된다.
- 다른 Caravan에서 사용 중인 개체 행은 보이지만 비활성화된다.
- 동물 네 마리는 `말 ×4`로 집계된다.
- 두 Caravan에 말 두 마리씩 배정하면 남은 수량은 0이고 버튼이 비활성화된다.
- 그룹을 여러 번 펼치고 접어도 행 수가 계속 증가하지 않는다.
- 팝업을 닫았다 열어도 이전 Listener가 남아 다른 개체를 선택하지 않는다.

## 9. 나오면 안 되는 결과

- `instanceId`가 표시 이름이나 툴팁에 노출됨
- 미소유 또는 Legacy 표시용 마차가 활성 선택 후보가 됨
- 동일 콘텐츠라는 이유로 다른 Caravan의 개체를 선택함
- Wagon이 허용하지 않는 동물을 선택함
- 동물이 개체별 `×1` 버튼으로 반복 표시됨
- 그룹 클릭 후 개체 행이 안 나옴
- 동일 그룹 재클릭 시 접히지 않음
- 갱신할 때마다 `Instantiate/Destroy`가 반복됨
- 외형 프리팹과 기능 프리팹의 Popup 두 개가 동시에 존재함

## 10. 검증 순서

1. Wagon 두 종류를 각각 두 개 이상 소유한 데이터로 연다.
2. 그룹 수량과 실제 소유 개체 수가 같은지 확인한다.
3. 그룹을 펼쳐 개체별 내구도와 비활성 상태를 확인한다.
4. 같은 그룹 재클릭, 다른 그룹 전환을 확인한다.
5. 개체를 선택해 실제 `instanceId`가 저장되는지 확인한다.
6. 동물 네 마리가 한 버튼 `×4`로 표시되는지 확인한다.
7. 두 Caravan에 동물을 나누어 저장한 뒤 남은 수량과 버튼 상태를 확인한다.
8. Unity Console에 Missing Reference와 이벤트 이중 등록 오류가 없는지 확인한다.
9. 관련 EditMode 테스트를 실행한다.

## 11. 최소 자동 확인 항목

- `WagonSelectPopup.instanceRowPrefab != null`
- `AnimalInventoryPanel.wagonPopup != null`
- `WagonInstanceRowView.button != null`
- `WagonInstanceRowView.label != null`
- Popup 기본 비활성화
- 그룹/행 풀 기본 Count 0
- 동일 그룹 토글 시 활성 행 0개
- 다른 그룹 전환 시 한 그룹의 행만 활성
