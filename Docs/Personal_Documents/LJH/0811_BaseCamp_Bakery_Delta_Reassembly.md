# BaseCamp·빵집 브랜치 증분 조립

## 1. 목적과 대상

최신 `dev2`의 기존 InGame UI를 보존하고, 이 브랜치에서 추가·수정한 기능만 조립한다.

- 실제 UI Prefab: `Assets/_Project/08.Prefabs/MainUICanvas.prefab`
- InGame Scene: `Assets/_Project/07.Scenes/04_InGame/InGame.unity`
- UI 배치 확인용 Scene: `Assets/_Project/07.Scenes/04_InGame/Build UI.unity`
- InGame에는 BaseCamp·오두막·빵집 Popup을 중복 배치하지 않는다. MainUICanvas Prefab 인스턴스를 통해 상속한다.
- Caravan Set, Rename, Treadmill, Journey 아이콘, 목장, 무역 실패 Popup, Warehouse, 운송 수단 테스트 지급 버튼은 기존 dev2 조립을 보존한다.

### 현재 브랜치에서 discard 가능한 조립 결과물

다음 파일은 이 문서로 재조립 검증을 마친 뒤 현재 브랜치에서 discard할 수 있다.

- `Assets/_Project/08.Prefabs/MainUICanvas.prefab`
- `Assets/_Project/07.Scenes/04_InGame/InGame.unity` — 실제 변경이 있을 때만
- `Assets/_Project/07.Scenes/04_InGame/Build UI.unity` — 배치 확인용 변경을 보존하지 않을 때

다음은 재조립 재료이므로 함께 discard하면 안 된다.

- BaseCamp·빵집·오두막·엔딩 UI Prefab 및 Installer
- `Build_EndingItem.asset`
- `SandboxSharedGameDataCatalog.asset`, `SharedGameDataWatchInventory.asset`
- 생산·엔딩·판매·Cargo 관련 C# 및 EditMode 테스트

다른 브랜치에서는 최신 `dev2`의 MainUICanvas와 InGame을 기준으로 아래 Installer와 수동 연결을 실행한다. 이 문서는 조립 결과물을 커밋하라는 의미가 아니라, 필요 브랜치에서 동일 상태를 재현하기 위한 계약이다.

### 최신 dev2 rebase 원칙

rebase 충돌이 발생하면 파일 종류별로 다음 기준을 적용한다.

- `MainUICanvas.prefab`, `InGame.unity`: 최신 `dev2` 쪽을 기준으로 충돌을 해소한 뒤 이 문서로 다시 조립한다. 이전 브랜치의 YAML 전체를 덮어쓰지 않는다.
- C#, 기능 Prefab, SO, Installer, 테스트: 이 브랜치의 기능 변경을 유지하되, 최신 `dev2`의 API 변경과 함께 의미 단위로 병합한다.
- `SandboxSharedGameDataCatalog.asset`, `SharedGameDataWatchInventory.asset`: 한쪽 배열 전체를 선택하지 않는다. 최신 `dev2` 항목을 모두 보존하고 `Build_EndingItem.asset`을 정확히 한 번 추가한다.
- `Village_Home.unity`: 최신 `dev2`의 Registry 항목을 보존하고 EndingItem 항목만 정확히 한 번 추가한다.
- `Build UI.unity`: 런타임 권위가 아니다. 레이아웃 확인에는 사용할 수 있지만 MainUI/InGame 조립 결과를 대신하지 않는다.

rebase 직후에는 Play Mode로 들어가기 전에 다음을 먼저 확인한다.

1. 충돌 마커(`<<<<<<<`, `=======`, `>>>>>>>`)가 0개인지 검사한다.
2. Unity가 컴파일과 Domain Reload를 끝낼 때까지 기다린다.
3. Console Error가 0개인지 확인한다.
4. `MainUICanvas.prefab`과 `InGame.unity`에 이 브랜치의 임시 조립 override가 남지 않았는지 확인한다.
5. 아래 Installer를 순서대로 실행한 뒤 수동 참조를 연결한다.

Installer는 기존 오브젝트를 찾아 갱신하는 방식으로 사용한다. 같은 Entry 또는 Popup을 수동 복제한 뒤 Installer를 다시 실행하지 않는다. 실행 후 각 컴포넌트가 정확히 1개인지 검사한다.

## 2. 권위 데이터

- 건물 레벨: `SaveData.player.villageBuildings`
- 캐러밴 슬롯 해금: `BaseCampProgressionPolicy.GetUnlockedCaravanSlotCount()`
- 일반 건물 레벨 제한: `BaseCampBuildingLevelPolicy`
- `SaveData.world.unlockedCaravanSlotIndices`는 이전 저장 호환용이며 화면·생성 권위로 사용하지 않는다.
- BaseCamp Lv.0~4의 슬롯 해금 수는 각각 0~4개다.
- BaseCamp Lv.5에서는 슬롯이 아니라 EndingItem을 해금한다.

## 3. 조립 순서

1. Unity가 Edit Mode인지, 컴파일 Error가 없는지 확인한다.
2. 최신 `dev2`의 `MainUICanvas.prefab`을 기준으로 `ND > UI > Install BaseCamp Overview Into Main UI`를 실행한다.
3. 기존 Caravan Slot 4개의 참조와 최신 외형을 보존했는지 확인한다.
4. `ND > UI > Install Bakery Production Into Main UI`를 실행한다.
5. 오두막 알림 아이콘이 구형 아이콘이면 `Tools > LJH > Install Cottage Production Popup To Main UI`를 실행한다.
6. `Village_Home.unity`의 EndingItem Registry를 확인하고 누락된 경우 한 항목만 추가한다.
7. `ND > UI > Install Ending Completion Into Main UI`를 실행한다.
8. ArrivalSaleFlow와 CargoSellPopup 연결 및 S8 Presenter 연결을 확인한다.
9. MainUICanvas와 InGame의 Missing Script 및 중복 Entry/Popup을 검사한다.
10. Prefab과 필요한 Scene을 저장한 뒤 Unity를 재로드하여 직렬화 참조가 유지되는지 다시 확인한다.

설치 메뉴는 Editor에서 Prefab을 정적으로 수정한다. Player 런타임에 UI를 생성하지 않는다.

## 4. BaseCamp 조립 계약

MainUICanvas에는 다음 항목이 정확히 1개씩 있어야 한다.

- `BaseCampMainUiEntry`
- `BaseCampOverviewPopupController`

필수 참조:

| 필드 | 대상 |
| --- | --- |
| `BaseCampMainUiEntry.buildingListPanel` | 기존 `BuildingListPanel` |
| `BaseCampMainUiEntry.popup` | `BaseCampOverviewPopupController` |
| `BaseCampMainUiEntry.noticeUI` | 기존 `NoticeUI` |

Popup은 기본 비활성화 상태이며 X와 Backdrop으로 닫혀야 한다. 표시 내용은 건물별 현재 레벨, 현재 캐러밴 슬롯 수 `N / 4`, 다음 해금 안내다.

## 5. Caravan Slot 확인 계약

기존 최신 슬롯 UI를 교체하지 않는다. `CaravanSlotView`는 정확히 4개여야 하며 각 슬롯에서 다음 참조가 비어 있지 않아야 한다.

- `createButton`
- `createButtonLabel`
- `lockOverlay`
- `lockOverlayButton`
- `displayNameText`
- `renameButton`
- `settingButton`
- `cargoButton`
- Journey 상태 표시 관련 참조

`CaravanSlotView` 자체에는 `noticeUI` 직렬화 필드가 없다. 잠긴 슬롯 클릭 안내는 슬롯이 기존 공용 Notice 경로를 통해 한국어로 표시하는 현재 코드 계약을 따른다.

상태 계약:

- Locked: 잠금 표시, 클릭하면 BaseCamp 레벨 부족 안내
- Empty: `캐러밴 생성` 버튼
- Occupied: 저장된 Caravan 이름·상태·기능 버튼 표시
- 상태 아이콘은 슬롯 가장 왼쪽의 투명 배경 영역에 표시
- Prepare 아이콘은 `HorseCycle_0`
- Rename과 Treadmill 호출은 기존 기능을 유지

## 6. EndingItem 계약

필요 자산:

- `Assets/_Project/02.Data/01_ScriptableObjects/Build/Build_EndingItem.asset`
- 외형: `Assets/_Project/09.Art/06_3DModels/Bed/Cama/Cama.fbx`

`Build_EndingItem.asset`의 현재 프레젠테이션 계약:

| 필드 | 값 |
| --- | --- |
| `buildPrefab` | `Cama.fbx` |
| `visualScale` | `(0.32, 0.32, 0.32)` — 1.25m 한 칸 안에 외형·그림자 여유 확보 |
| `visualEulerAngles` | `(0, 0, 0)` — 이불과 베개가 위를 향하는 수평 배치 |
| `visualOffset` | `(0.05, -0.15, -0.35)` — FBX 피벗을 1x1 셀 중심·지면에 맞춤 |
| `footprintCellsX/Z` | `1 / 1` |

이미 설치된 런타임 인스턴스는 SO 변경을 즉시 반영하지 않을 수 있으므로 재배치 또는 Play Mode 재진입으로 검증한다.

공용 데이터 등록도 조립 재료에 포함한다.

- `SandboxSharedGameDataCatalog.builds`에 `Build_EndingItem.asset`이 정확히 1회 등록돼야 한다.
- 카탈로그 갱신 후 `SharedGameDataWatchInventory.asset`도 Refresh한다.
- Play Mode 시작 시 `Shared game data asset is not registered in catalog ... EndingItem` 경고가 없어야 한다.

`Assets/_Project/07.Scenes/04_InGame/Village_Home.unity`의 `VillageBuildingRegistry.catalog`에 다음 항목이 정확히 1개 있어야 한다.

| 필드 | 값 |
| --- | --- |
| `displayName` | `최고급 침대` (UI·건물 Block은 이 값을 직접 사용) |
| `prefab` | EndingItem Prefab |
| `buildData` | `Build_EndingItem.asset` |
| `isEnvironment` | `false` |
| `envCost` | `0` |

BaseCamp Lv.0~4에서는 비활성, Lv.5에서 활성화한다. 1회 건설 후 행에는 별도 `건설 완료` 접미사를 붙이지 않고 다른 건물과 같은 배경색을 유지한다. 완료된 행을 다시 누르면 상세 증축 UI 대신 `더 이상 레벨업할 수 없습니다.` Notice를 표시한다.

엔딩 Popup 조립 메뉴:

`ND > UI > Install Ending Completion Into Main UI`

필수 Prefab:

`Assets/_Project/08.Prefabs/UI/Building/EndingCompletionPopup.prefab`

- MainUICanvas에 `EndingCompletionMainUiEntry`와 Popup 인스턴스가 정확히 1개씩 있어야 한다.
- Popup은 기본 비활성화 상태이며 런타임에 생성하지 않는다.
- 엔딩 건물 건설 저장과 런타임 반영이 성공한 뒤 `VillageBuildingCommitted` 이벤트로 최초 표시한다.
- 저장 복원 시에는 자동 표시하지 않는다.
- 좌하단 엔딩 건물 Block 클릭 시 같은 Popup을 다시 표시한다.
- `계속하기`는 Popup을 닫고, `끝내기`는 기존 저장 경로를 거쳐 타이틀로 복귀한다.
- 달성 여부는 별도 SaveData 필드가 아니라 `player.villageBuildings`와 `Build_EndingItem.displayName`을 사용한다. 과거 저장의 `황금 침대`는 호환 별칭으로만 인정한다.
- `EndingCompletionPopup/Backdrop`에는 프리팹에 저장된 `Button`이 있어야 하며 `EndingCompletionPopupView.backdropButton`에 연결한다.
- 건설 목록에서 BaseCamp Lv.5 이전 행은 parchment UI에 맞는 연갈색 비활성 표시를 사용한다.
- 건설 완료 후 행은 클릭 가능 상태를 유지하되 `더 이상 레벨업할 수 없습니다.` Notice만 표시한다.

## 7. 빵집 조립 계약

MainUICanvas에는 다음 항목이 정확히 1개씩 있어야 한다.

- `BakeryProductionMainUiEntry`
- `BakeryProductionPopupPresenter`
- `BakeryProductionPopupView`

필수 자산:

- `Assets/_Project/11.CoreServices/Resources/BakeryProductionData.asset`
- `Assets/_Project/08.Prefabs/UI/Bakery/BakeryProductionPopup.prefab`
- 알림 아이콘: `Assets/_Project/09.Art/04_UI/icons/알림 UI ICON.png`

필수 참조:

| 필드 | 대상 |
| --- | --- |
| `BakeryProductionMainUiEntry.buildingListPanel` | 기존 `BuildingListPanel` |
| `BakeryProductionMainUiEntry.popup` | 설치된 `BakeryProductionPopupPresenter` |
| `BakeryProductionMainUiEntry.productionReadyIcon` | `알림 UI ICON.png` |
| `BakeryProductionPopupPresenter.view` | 같은 Popup의 View |
| `BakeryProductionPopupPresenter.noticeUI` | 기존 `NoticeUI` |

Popup, Slider, 버튼은 Prefab 오브젝트로 존재해야 하며 런타임 생성하지 않는다.

## 8. 오두막·빵집 알림 아이콘

- 두 Entry의 `productionReadyIcon`은 모두 `알림 UI ICON.png`다.
- `BuildingListPanel.SetBuildingBadge()`를 사용한다.
- Anchor는 우측 상단, `anchoredPosition = (-120, -6)`, 크기 `28 x 28`을 기준으로 한다.
- 오두막: 마차와 동물 보관량이 모두 Full일 때만 표시한다.
- 빵집: 빵 보관량이 현재 레벨 최대치일 때만 표시한다.
- 수령 후 Full이 아니면 즉시 숨긴다.

## 9. Warehouse와 InGame 계약

Warehouse는 추가 Inspector 조립 없이 BaseCamp 정책으로 선택 행 수를 계산한다.

- BaseCamp Lv.1~4에서 1~4행 표시
- 해금됐지만 미생성인 행은 `캐러밴 없음`
- Prepare이면서 BaseCamp 대기 중인 Caravan만 선택 가능

InGame의 기존 `TransportInventoryRewardDebugButton`은 유지한다.

- 위치 `(20, 20)`
- 지급: `Wagon_M` 1개, `Wagon_S` 1개, `Horse` 2마리
- 통나무 40개 지급 버튼은 조립하지 않는다.

## 10. 판매 가격 묶음·Cargo 조립 계약

필수 정적 Prefab:

- `Assets/_Project/08.Prefabs/UI/Trade/CargoSellPopup.prefab`
- `Assets/_Project/08.Prefabs/UI/Market/ArrivalSaleFlow.prefab`

`CargoSellPopup.prefab`에는 런타임 생성이 아닌 다음 오브젝트가 미리 존재해야 한다.

- `PriceGroupModal`
- `PriceGroupModal/Content`와 비활성 `WarehousePriceGroupRowView` 템플릿
- `PriceGroupModal/CancelButton`
- `QuantityModal/SourceText`
- `QuantityModal/DestinationText`
- Quantity Modal Backdrop Button

가격 묶음 행만 템플릿 기반 풀링으로 부족한 수만큼 복제한다. Popup, Modal, 버튼, Content 자체를 런타임 코드로 생성하면 안 된다.

MainUICanvas 계약:

- `CargoSellPopupController`는 기본 비활성 Popup 인스턴스에 존재한다.
- `ArrivalSaleFlow/CaravanArrivalSaleController.cargoSellPopup`은 해당 Popup을 참조한다.
- `CaravanArrivalSaleController.tradeScreenPresenter`는 MainUICanvas의 `FrameworkTradeScreenPresenter`를 연결하는 것을 권장한다.
- 위 Presenter 참조가 비어 있어도 코드가 Scene Presenter를 1회 탐색·캐시하지만, 조립 누락을 조기에 잡기 위해 Inspector 연결을 우선한다.

데이터 계약:

- 저장·Draft·runtime Cargo는 `(itemId, purchaseUnitPrice)`별 행을 보존한다.
- `0G` 생산품도 유효한 구매가 묶음이며 제거하거나 다른 가격과 합치지 않는다.
- 화면 아이콘과 물리 슬롯은 `itemId`별 총수량으로 합산한다.
- 판매 클릭 순서는 `아이템 → 구매가 묶음 → 수량`이다. 묶음이 하나면 수량 단계로 바로 이동할 수 있다.
- 수량 Modal 헤더는 `Caravan displayName → 판매 대기`다.
- 먹이는 `foodAmount`로만 복원하고 Cargo에 중복 복원하지 않는다.

판매 완료 후에는 durable Pending Settlement 저장과 `PresentSettlement()` 성공 뒤 S8을 명시적으로 연다. 이미 Router가 `Settlement`라 같은 상태 이벤트가 재발행되지 않는 경우에도 정산창이 보여야 한다.

## 11. 자동 검증

권장 EditMode Fixture:

- `BaseCampBuildingLevelPolicyTests`
- `BaseCampCaravanSlotProgressionTests`
- `BaseCampOverviewUiContractTests`
- `BakeryProductionServiceTests`
- `CottageProductionServiceTests`
- `WarehouseInventoryTransferTests`
- `SceneMissingScriptContractTests`
- `CargoSellPopupPrefabTests`
- `TradePrepareCargoPreservationTests`
- `CaravanCargoSlotGroupingTests`
- `EndingCompletionUiContractTests`

완료 조건:

- 컴파일 Error 0건
- MainUICanvas와 InGame Missing Script 0개
- Entry/Popup 중복 0건
- 모든 필수 직렬화 참조 non-null

### 조립 후 변경 파일 점검

조립 테스트 브랜치에서 예상되는 UI 결과물 변경은 원칙적으로 다음 범위다.

- `Assets/_Project/08.Prefabs/MainUICanvas.prefab`
- `Assets/_Project/07.Scenes/04_InGame/Village_Home.unity` — EndingItem Registry가 최신 dev2에 아직 없을 때만
- `Assets/_Project/07.Scenes/04_InGame/InGame.unity` — Scene 전용 참조가 실제로 필요할 때만

`InGame.unity`는 MainUICanvas Prefab을 통해 상속되는 UI 때문에 변경되어서는 안 된다. 예상 밖의 Scene override가 생겼다면 저장하기 전에 원인을 확인한다. 기능 Prefab, SO, Catalog, WatchInventory, C#은 조립 결과물이 아니라 기능 재료이므로 이 단계에서 discard하지 않는다.

## 12. Play Mode 체크리스트

검증은 가능하면 두 저장 상태에서 각각 수행한다.

- 신규 저장: BaseCamp Lv.0부터 잠금·해금·최초 생산·최초 엔딩 달성을 확인한다.
- 기존 저장: 이미 존재하는 Caravan, 건물 레벨, 생산 UTC, 가격별 Cargo가 손실 없이 복원되는지 확인한다.

### BaseCamp·슬롯·EndingItem

- [ ] Lv.0에서 슬롯 4개가 모두 잠김
- [ ] Lv.1~4에서 슬롯이 순서대로 하나씩 해금
- [ ] 잠긴 슬롯 클릭 시 한국어 Notice
- [ ] Empty 슬롯에서 생성 후 Occupied로 즉시 갱신
- [ ] 상태 아이콘이 왼쪽 투명 영역에 표시
- [ ] 이름 변경, Caravan Set, Treadmill, Prepare/Traveling 아이콘 정상
- [ ] Lv.5에서 황금 침대 활성화
- [ ] 황금 침대 건설 후 추가 건설·증축 차단
- [ ] 황금 침대가 1x1 점유 범위에 맞는 크기로, 이불·베개가 위를 향해 배치
- [ ] 황금 침대 최초 건설 직후 엔딩 Popup 표시
- [ ] 황금 침대 Block 재클릭 시 엔딩 Popup 재표시

### 생산 UI

- [ ] 오두막과 빵집 블록 클릭 시 각 Popup 열림
- [ ] X와 Backdrop으로 닫힘
- [ ] 빵집 전체 수령과 Slider 부분 수령 정상
- [ ] 창고 미건설·공간 부족 시 데이터 변경 없이 한국어 Notice
- [ ] 게임 종료 후 UTC 오프라인 생산 복원
- [ ] Full일 때만 알림 아이콘 표시, 수령 후 숨김
- [ ] 알림 아이콘이 건물명·레벨 오른쪽에 보이고 잘리지 않음

### Warehouse·회귀

- [ ] BaseCamp Lv.4에서 캐러밴 선택 행 4개 표시
- [ ] 빈 행 문구가 `캐러밴 없음`
- [ ] 운송 수단 테스트 지급 버튼의 지급 수량 정상
- [ ] 기존 목장, Rename, Caravan Set, Treadmill, 무역 실패 정산 기능 정상
- [ ] 저장 후 재실행해 슬롯·건물·생산량 상태 유지

### Cargo 판매·정산 회귀

- [ ] 동일 아이템의 `0G`/유료 묶음이 가격 선택창에 각각 표시
- [ ] 가격이 달라도 동일 itemId 총수량은 물리 슬롯을 공유
- [ ] 수량 Modal 헤더가 `캐러밴 표시 이름 → 판매 대기`
- [ ] Quantity Modal Backdrop으로 Modal만 닫힘
- [ ] 판매 완료 직후 Town 화면에 머물지 않고 S8 정산창 표시
- [ ] S8 Claim 후 다음 Pending Settlement 또는 Town 화면으로 정상 진행

## 13. 조립 실패 판별

- Block 클릭이 무반응이면 Entry의 `buildingListPanel`, Popup, `NoticeUI` 참조와 동일 컴포넌트 중복 여부를 먼저 확인한다.
- Popup은 열리지만 Backdrop으로 닫히지 않으면 Prefab의 정적 Backdrop Button과 View의 닫기 이벤트 연결을 확인한다.
- 슬롯 외형이 과거 버전으로 돌아갔다면 Installer 문제가 아니라 rebase 과정에서 `MainUICanvas.prefab` YAML을 이전 브랜치 것으로 덮어쓴 경우를 먼저 의심한다.
- EndingItem이 목록이나 마을에 나타나지 않으면 SO, 공용 Catalog, WatchInventory, `VillageBuildingRegistry.catalog` 네 지점을 순서대로 확인한다.
- 판매 수량창이 바로 열리거나 `0G` 묶음이 사라지면 `CargoSellPopup.prefab/PriceGroupModal` 조립과 `(itemId, purchaseUnitPrice)` 보존 코드를 함께 확인한다.
- 판매 완료 후 S8이 안 뜨면 `CaravanArrivalSaleController.tradeScreenPresenter` 참조와 Console의 `[ArrivalSale]` 오류를 확인한다.
- 조립 후 저장·재로드에서 참조가 사라지면 Scene 인스턴스가 아니라 원본 `MainUICanvas.prefab`을 수정했는지 확인한다.
