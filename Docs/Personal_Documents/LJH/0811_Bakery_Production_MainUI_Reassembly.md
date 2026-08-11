# 빵집 생산 기능 MainUI 재조립

## 1. 조립 대상

최신 `dev2`의 `MainUICanvas.prefab`을 기준으로 조립한다. 생산 Popup은 런타임에 생성하지 않으며 고정 Prefab 인스턴스로 배치한다.

| 용도 | 경로 |
| --- | --- |
| 생산 설정 | `Assets/_Project/11.CoreServices/Resources/BakeryProductionData.asset` |
| Popup Prefab | `Assets/_Project/08.Prefabs/UI/Bakery/BakeryProductionPopup.prefab` |
| MainUI 연결부 | `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/BakeryProductionMainUiEntry.cs` |
| Popup Presenter | `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/Building/BakeryProductionPopupPresenter.cs` |
| 생산 완료 아이콘 | `Assets/_Project/09.Art/04_UI/icons/알림 UI ICON.png` |

## 2. MainUICanvas 배치

```text
MainUICanvas                         [BakeryProductionMainUiEntry]
├─ ...
└─ BakeryProductionPopup             [inactive]
   ├─ Backdrop
   ├─ Panel
   └─ QuantityModal                   [inactive]
```

`BakeryProductionMainUiEntry`는 `BuildingPanel`이나 빵집 Popup이 아니라 `MainUICanvas` 최상위 루트 오브젝트에 정확히 1개 붙인다.

`BakeryProductionPopup.prefab`은 `MainUICanvas`의 자식으로 정확히 1개 배치하고 기본 비활성화한다. Prefab 내부의 Popup, 버튼, 수량 선택 Slider를 런타임 코드로 새로 생성하지 않는다.

## 3. Inspector 연결

| 컴포넌트 필드 | 연결 대상 |
| --- | --- |
| `BakeryProductionMainUiEntry.buildingListPanel` | MainUI 내부 기존 `BuildingPanel`의 `BuildingListPanel` |
| `BakeryProductionMainUiEntry.popup` | 배치한 `BakeryProductionPopup`의 `BakeryProductionPopupPresenter` |
| `BakeryProductionMainUiEntry.productionReadyIcon` | `Assets/_Project/09.Art/04_UI/icons/알림 UI ICON.png` |
| `BakeryProductionPopupPresenter.view` | 같은 Popup의 `BakeryProductionPopupView` |
| `BakeryProductionPopupPresenter.noticeUI` | MainUI 내부 기존 `NoticeUI` |

레드닷 Image를 별도로 만들지 않는다. `BakeryProductionMainUiEntry`가 `BuildingListPanel.SetBuildingBadge()`를 사용하여 기존 빵집 건물 블록의 Badge Image를 갱신한다. 빵 보관량이 현재 레벨의 최대치에 도달했을 때만 새 알림 아이콘을 표시하고, 일부 또는 전부 수령하여 최대치 미만이 되면 숨긴다.

## 4. 검증

- [ ] `MainUICanvas` 루트에 `BakeryProductionMainUiEntry`가 정확히 1개다.
- [ ] `BakeryProductionPopup`이 정확히 1개이며 기본 비활성화다.
- [ ] Entry의 세 필드가 모두 연결되어 있다.
- [ ] `productionReadyIcon`은 캐러밴 상태 아이콘이 아니라 `알림 UI ICON.png`다.
- [ ] 빵집 내부 보관량이 최대일 때 빵집 블록에 아이콘이 나타난다.
- [ ] 부분 또는 모두 수령 후 최대치 미만이 되면 아이콘이 사라진다.
- [ ] Console에 Missing Reference 또는 중복 구독 오류가 없다.
