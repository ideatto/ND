# Caravan Overview 트레드밀 연결 요청

## 목적

Caravan Slot의 이름 영역을 짧게 클릭하면 해당 Caravan ID의 트레드밀을 열고, 1초 이상 누르면 기존 이름 변경 팝업을 연다.

## 현재 준비된 코드

- `CaravanSlotView`는 짧은 클릭과 1초 롱프레스를 구분한다.
- 짧은 클릭은 `TreadmillRequested(caravanId, displayName)`을 전달한다.
- 롱프레스는 기존 `RenameRequested(caravanId)`을 전달한다.
- `CaravanOverviewPresenter`의 직렬화된 `treadmillPanel` 필드가 런타임 검색 없이 패널을 연다.
- Presenter는 짧은 클릭 시 `TreadmillPanel.Toggle(caravanId, displayName)`을 호출한다.
- `Toggle`은 같은 Caravan이 이미 열려 있으면 패널을 닫고, 다른 Caravan이면 열린 상태에서 해당 Lane으로 전환한다.
- `TreadmillPanel.Open(caravanId, displayName)`은 전달된 이름을 표시하고 Lane을 여는 기존 명시적 진입점으로 유지한다.
- Presenter가 ViewData를 다시 생성할 때 열린 패널의 `CurrentCaravanId`와 같은 Caravan만 `RefreshDisplayName`으로 라벨을 갱신한다.
- 이름 갱신은 Lane, JourneyState, SaveData를 변경하지 않으며 다른 Caravan의 라벨에는 영향을 주지 않는다.

## MainUI 조립 요청

현재 두 컴포넌트는 서로 다른 프리팹 인스턴스에 있으므로 `MainUICanvas.prefab` 단독 편집만으로는 런타임 인스턴스를 연결할 수 없다.
InGame Scene 수정이 허용된 뒤 다음 항목을 연결한다.

1. `Assets/_Project/07.Scenes/04_InGame/InGame.unity`를 연다.
2. Unity Hierarchy 검색창에서 `CaravanScrollView`를 검색한다. 검색 결과가 여러 개라면 Scene에 배치된 `MainUICanvas` 프리팹 인스턴스 아래의 오브젝트를 선택한다.
3. Inspector에서 `CaravanOverviewPresenter` 컴포넌트를 찾고, 그 안의 비어 있는 `Treadmill Panel` 필드를 확인한다.
4. Hierarchy 검색창에서 `TreadmillPanel`을 검색하고, Scene의 `MainUICanvas` 프리팹 인스턴스 아래에 자식으로 추가된 별도 `TreadmillPanel` 프리팹 인스턴스를 찾는다. Scene 최상위 루트 오브젝트가 아니므로 `MainUICanvas` 하위를 확인한다.
5. 해당 오브젝트의 GameObject 자체를 임의의 다른 필드에 넣는 것이 아니라, 그 오브젝트에 붙은 `TreadmillPanel` 컴포넌트를 `CaravanOverviewPresenter.Treadmill Panel` 필드로 드래그한다. GameObject를 드래그해도 Unity가 올바른 컴포넌트를 자동 선택했다면 같은 결과가 된다.
6. 연결 후 `Treadmill Panel` 필드가 `None (Treadmill Panel)`이 아니라 Scene의 `TreadmillPanel` 컴포넌트를 표시하는지 확인한다.
7. Scene을 저장한다. 이 참조는 `InGame.unity` 안의 `MainUICanvas` 프리팹 인스턴스 override로 저장되어야 한다.
8. Prefab Overrides 창에서 이 변경을 `MainUICanvas.prefab`에 Apply하지 않는다. `MainUICanvas.prefab` 에셋은 특정 Scene의 별도 `TreadmillPanel` 인스턴스를 참조할 수 없다.
9. Play Mode에서 점유된 Caravan Slot을 대상으로 짧은 클릭과 1초 이상 롱프레스가 서로 중복 실행되지 않는지 확인한다. 네 슬롯이 모두 점유되지 않았다면 현재 점유된 슬롯만 검사한다.

### 올바른 저장 구조

```text
InGame.unity
└─ MainUICanvas 프리팹 인스턴스
   ├─ CaravanScrollView
   │  └─ CaravanOverviewPresenter
   │     └─ treadmillPanel ─────────────┐
   └─ TreadmillPanel 프리팹 인스턴스   │
      └─ TreadmillPanel 컴포넌트 ◀─────┘
```

- Inspector 조작 대상은 `MainUICanvas` 인스턴스 내부이지만, 참조의 저장 대상은 `InGame.unity`다.
- `MainUICanvas.prefab`과 `TreadmillPanel.prefab` 에셋에는 이 Scene 간 참조를 저장하지 않는다.
- 현재 조립에서 Scene에 추가되어야 하는 것은 직렬화 참조 하나뿐이다. 새 패널 복제, 새 스크립트 추가, 레이아웃 변경은 필요하지 않다.

### 연결 오류 확인

- 짧게 눌러도 아무 반응이 없다면 `CaravanOverviewPresenter.Treadmill Panel` 필드가 비어 있는지 먼저 확인한다.
- 이름 변경 팝업은 열리지만 트레드밀만 열리지 않는다면 롱프레스 연결보다 `treadmillPanel` Scene 참조를 먼저 확인한다.
- 클릭할 때 기존 패널 외에 패널이 하나 더 나타난다면 `TreadmillPanel` 프리팹을 새로 복제하거나 MainCanvas 아래에 중첩하지 않았는지 확인한다.
- 다른 Caravan을 눌러도 같은 Lane만 유지된다면 연결 문제가 아니라 전달된 `caravanId` 또는 Lane 조회 문제일 수 있으므로 Console 로그와 런타임 데이터를 확인한다.

## 관련 요소 위치

- Overview 프리팹: `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`
- Presenter 오브젝트: `MainUICanvas` 인스턴스 내부 `CaravanScrollView`
- Presenter 스크립트: `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/CaravanOverviewPresenter.cs`
- 슬롯 입력 스크립트: `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/CaravanSlotView.cs`
- 트레드밀 프리팹: `Assets/_Project/08.Prefabs/Treadmill/TreadmillPanel.prefab`
- 트레드밀 대상 오브젝트: InGame Scene의 `MainUICanvas` 인스턴스 아래에 자식으로 추가된 `TreadmillPanel` 프리팹 인스턴스
- 트레드밀 스크립트: `Assets/_Project/01.Core/10_Treadmill/TreadmillPanel.cs`
- 연결할 필드: `CaravanOverviewPresenter.treadmillPanel`

## 확인 항목

- Play Mode 진입 전 Unity Console에 새 컴파일 오류가 없는지 확인한다.
- 짧은 클릭: 해당 `caravanId`의 트레드밀이 열리고 사용자 지정 `displayName`이 표시된다.
- 같은 Caravan을 다시 짧게 클릭: 열린 트레드밀 패널이 닫힌다.
- 다른 Caravan을 짧게 클릭: 패널을 닫지 않고 대상 Caravan의 Lane과 `displayName`으로 전환된다.
- 열린 Caravan의 이름 변경 성공: Slot과 트레드밀 라벨이 함께 최신 이름으로 갱신된다.
- 다른 Caravan의 이름 변경 성공: 현재 열린 트레드밀의 ID와 라벨은 유지된다.
- 1초 이상 누름: 이름 변경 팝업만 열리고 트레드밀은 열리지 않는다.
- Empty, Locked, Unknown Slot에서는 두 요청 모두 발생하지 않는다.
- MainUI 연결 전에는 Presenter의 `treadmillPanel`이 비어 있으므로 기존 이름 변경 기능 외에 트레드밀 열기 동작은 발생하지 않는다.
- Play Mode 종료 후 `InGame.unity` diff에 의도한 `treadmillPanel` 참조 외의 레이아웃·오브젝트 변경이 섞이지 않았는지 확인한다.
