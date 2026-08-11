# 오두막 생산 기능 및 MainUI 재조립

## 1. 목적과 기준 상태

이 문서는 최신 `dev2`의 수정되지 않은 `Assets/_Project/08.Prefabs/MainUICanvas.prefab`에 오두막 생산 UI를 조립하는 기준이다. `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`은 InGame Scene이 참조하지 않으므로 설치 대상으로 사용하지 않는다. 전달 브랜치에는 기능 코드, 설정 SO, 고정 Popup Prefab과 에디터 조립 도구만 보존하고, `MainUICanvas.prefab`의 조립 결과는 포함하지 않는 것을 전제로 한다.

`InGame.unity`에는 이 기능을 위한 개별 오브젝트나 override를 추가하지 않는다. InGame Scene의 기존 `MainUICanvas.prefab` 인스턴스가 수정된 Prefab 에셋을 상속한다.

## 2. 조립 전에 있어야 하는 재료

| 구분 | 권위 경로 |
| --- | --- |
| 생산 설정 | `Assets/_Project/11.CoreServices/Resources/CottageProductionData.asset` |
| 생산 서비스 | `Assets/_Project/11.CoreServices/Scripts/Building/CottageProductionService.cs` |
| Popup Prefab | `Assets/_Project/08.Prefabs/UI/Cottage/CottageProductionPopup.prefab` |
| Popup View/Presenter | `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/Building/` |
| MainUI 연결부 | `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/CottageProductionMainUiEntry.cs` |
| ViewData | `Assets/99.Sandbox/_LJH/01.Script/Runtime/ViewData/CottageProductionViewData.cs` |
| 에디터 조립 도구 | `Assets/99.Sandbox/_LJH/Editor/CottageProductionPopupPrefabBuilder.cs` |
| 건물 설정 | `Assets/_Project/02.Data/01_ScriptableObjects/Build/Build_Cottage.asset` |

필수 contentId는 `Wagon_M`, `Horse`다. 설정 SO 기본값은 마차 1800초, 말 600초이며 레벨별 보관 한도는 `1/2`, `2/4`, `3/6`이다. 최초 0→1 건설 때만 마차 1대와 말 2마리를 즉시 내부 보관함에 지급한다.

## 3. 권장 조립 순서

1. 최신 dev2에서 위 재료를 반영하고 Unity 컴파일 오류가 0건인지 확인한다.
2. `CottageProductionData.asset`의 contentId와 1~3레벨 설정을 확인한다.
3. `CottageProductionPopup.prefab`이 존재하면 다시 생성하지 않는다. 외형을 재생성해야 할 때만 Build UI Scene에서 `Tools > LJH > Build Cottage Production Popup`을 실행한다.
4. `Tools > LJH > Install Cottage Production Popup To Main UI`를 실행한다.
5. `MainUICanvas.prefab`을 Prefab Mode로 열어 아래 계층과 연결을 확인한 뒤 저장한다.
6. InGame Scene에서는 MainUICanvas 인스턴스가 Prefab 변경을 상속하는지만 확인한다. 이 기능 때문에 Scene override를 만들거나 Prefab에 Apply하지 않는다.

설치 메뉴는 고정 UI를 에디터에서 조립하는 도구다. Player 런타임에 Popup, 버튼 또는 Entry를 생성하지 않는다. 다시 실행하면 기존 `CottageProductionPopup`을 제거하고 하나만 설치하므로 중복 조립하지 않는다.

## 4. 설치 후 정확한 형태

```text
MainUICanvas                         [CottageProductionMainUiEntry]
├─ ...
├─ CottageProductionPopup           [inactive]
│  ├─ Backdrop                      [Button]
│  └─ Panel
│     ├─ Header/CloseButton
│     ├─ ProductionContent
│     │  ├─ WagonProductionCard
│     │  └─ DraftAnimalProductionCard
│     └─ ReceiveAllButton
├─ NoticeUI
└─ ...
```

Popup은 `NoticeUI` 바로 앞 sibling에 두고 기본 비활성화한다. Popup Prefab 내부의 닫기, 상품 표시, 개별 받기, 모두 받기 참조는 Prefab에 저장되어 있다.

외부 연결은 다음과 같아야 한다.

| 컴포넌트/필드 | 연결 대상 |
| --- | --- |
| `CottageProductionMainUiEntry.buildingListPanel` | MainUI 내부 기존 `BuildingListPanel` (`BuildingPanel`) |
| `CottageProductionMainUiEntry.popup` | `CottageProductionPopupPresenter` |
| `CottageProductionMainUiEntry.productionReadyIcon` | `Assets/_Project/09.Art/04_UI/icons/알림 UI ICON.png` |
| `CottageProductionPopupPresenter.view` | 같은 Popup의 `CottageProductionPopupView` |
| `CottageProductionPopupPresenter.noticeUI` | MainUI 내부 기존 `NoticeUI` |

`BuildingListPanel`에 오두막 전용 행을 만들지 않는다. 기존 공용 건물 행을 사용하며, `CottageProductionMainUiEntry`가 `BuildingClicked` 이벤트를 받아 오두막 이름일 때만 Popup을 연다. 양쪽 보관함이 모두 찼을 때만 `SetBuildingBadge`를 통해 기존 오두막 행에 알림 아이콘을 표시한다. 공용 Badge 위치는 우상단 Anchor 기준 `(-120, -6)`, 크기는 `28 x 28`이며 건물 이름·레벨 오른쪽에서 잘리지 않아야 한다.

## 5. 데이터 흐름 계약

```text
오두막 0→1 건설 저장 성공
→ Cottage 상태 최초 초기화 및 즉시 지급
→ CottageProductionChanged
→ Popup/건물 Badge 갱신

UTC 경과 또는 오프라인 복원
→ CottageProductionService가 저장 상태를 계산
→ 저장 성공
→ CottageProductionChanged
→ 표시 갱신

받기 요청
→ 보관 수량과 목장 존재 여부 검증
→ 운송수단 인벤토리 및 Cottage 상태를 한 저장 경계에서 갱신
→ 저장 실패 시 양쪽 복원
→ 성공 이벤트 뒤 UI 갱신
```

UI는 SaveData를 직접 변경하지 않는다. 시간 공급원의 실제 기본값은 `DateTime.UtcNow.Ticks`이며 테스트에서만 가짜 시간을 주입한다. 오두막 레벨 0 기간은 생산 시간으로 인정하지 않고, 보관함이 차면 해당 품목 생산을 중단한다.

## 6. 조립 직후 검증

- [ ] MainUI 루트에 `CottageProductionMainUiEntry`가 정확히 1개다.
- [ ] `CottageProductionPopup`이 정확히 1개이며 기본 비활성화다.
- [ ] 위 표의 외부 참조 5개가 모두 연결되어 있다.
- [ ] InGame Scene 파일에는 이 기능 전용 변경이 생기지 않았다.
- [ ] 오두막 건설 전 클릭 대상이 없고 생산 상태도 생성되지 않는다.
- [ ] 최초 Lv.1 건설 직후 Popup에 마차 `1 / 1`, 말 `2 / 2`가 표시된다.
- [ ] 배경과 X 버튼으로 닫히며 개별 받기와 모두 받기가 동작한다.
- [ ] 수령 전 목장이 없으면 Notice가 표시되고 데이터가 변하지 않는다.
- [ ] 양쪽 보관함이 모두 차면 건물 행에 느낌표가 표시되고 하나라도 받으면 사라진다.
- [ ] 시간이 지나면 각각 설정 주기로 생산되고, 종료 후 재실행하면 UTC 경과분이 복원된다.
- [ ] 레벨별 한도와 `Build_Cottage.asset`의 1~3레벨이 일치한다.
- [ ] Console의 Missing Reference, 중복 구독, 저장 오류가 0건이다.

## 7. 조립 실패 진단

- 오두막 클릭이 무반응이면 Entry의 `buildingListPanel`, `popup`과 설정 SO의 `buildingDisplayName=오두막`을 확인한다.
- Popup은 열리지만 안내가 안 나오면 Presenter의 `noticeUI`를 확인한다.
- 느낌표가 안 나오면 Entry의 `productionReadyIcon`과 양쪽 보관 한도 상태를 확인한다.
- 아이콘/이름이 비면 `Wagon_M`, `Horse`가 실제 카탈로그에 있는지 확인한다. 런타임 카탈로그 검증 실패 시 기능 진입을 막는 것이 정상이다.
- InGame에서 보이지 않으면 Scene을 직접 수정하기 전에 MainUI Prefab 인스턴스가 최신 Prefab 에셋을 상속하는지 확인한다.

## 8. 전달 브랜치와 조립 브랜치의 차이

현재 기능 전달 브랜치에서는 `MainUICanvas.prefab` 조립 결과를 discard해도 된다. 다만 실제 통합 대상 브랜치에서는 위 설치 메뉴 실행 결과인 MainUI 변경을 저장·커밋해야 한다. `CottageProductionPopup.prefab`과 에디터 설치 도구는 조립 결과가 아니라 재조립 재료이므로 보존한다.
# 2026-08-10 설치 메뉴 재검증 결과

최신 미조립 `MainUICanvas.prefab`에서 아래 메뉴를 실행해 정적 조립이 재현됨을 확인했다.

`Tools > LJH > Install Cottage Production Popup To Main UI`

실행 후 필수 결과:

- `CottageProductionMainUiEntry`: 정확히 1개
- `CottageProductionPopup`: 정확히 1개, 시작 시 inactive
- `buildingListPanel`: 기존 MainUI의 `BuildingListPanel`
- `popup`: 설치된 Popup의 `CottageProductionPopupPresenter`
- `productionReadyIcon`: 생산 완료 느낌표용 Sprite
- Presenter의 `view`, `noticeUI`: 모두 연결

메뉴는 기존 Cottage Popup을 정리하고 하나만 다시 설치하므로 수동 복제와 메뉴 실행을 함께 하지 않는다. 설치 결과는 `MainUICanvas.prefab`에 저장되며 `InGame.unity`에 Cottage 전용 override를 만들 필요가 없다.

기능 전달 브랜치에서 임시 검증용 `MainUICanvas.prefab` 변경을 discard해도 기능 코드, 설정 SO, Popup Prefab, 설치 도구는 반드시 보존한다. 통합 브랜치에서는 메뉴 실행 결과를 discard하지 않고 MainUI 조립 결과로 커밋한다.
