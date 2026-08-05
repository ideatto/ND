# Caravan Overview 트레드밀 연결 요청

## 목적

Caravan Slot의 이름 영역을 짧게 클릭하면 해당 Caravan ID의 트레드밀을 열고, 1초 이상 누르면 기존 이름 변경 팝업을 연다.

## 현재 준비된 코드

- `CaravanSlotView`는 짧은 클릭과 1초 롱프레스를 구분한다.
- 짧은 클릭은 `TreadmillRequested(caravanId, displayName)`을 전달한다.
- 롱프레스는 기존 `RenameRequested(caravanId)`을 전달한다.
- `CaravanOverviewPresenter`의 직렬화된 `treadmillPanel` 필드가 런타임 검색 없이 패널을 연다.
- `TreadmillPanel.Open(caravanId, displayName)`은 전달된 이름을 라벨에 표시하고 해당 Caravan ID의 Lane을 연다.

## MainUI 조립 요청

현재 두 컴포넌트는 서로 다른 프리팹 인스턴스에 있으므로 `MainUICanvas.prefab` 단독 편집만으로는 런타임 인스턴스를 연결할 수 없다.
InGame Scene 수정이 허용된 뒤 다음 항목을 연결한다.

1. `Assets/_Project/07.Scenes/04_InGame/InGame.unity`를 연다.
2. Scene에 배치된 `MainUICanvas` 프리팹 인스턴스에서 `CaravanScrollView` 오브젝트를 선택한다.
3. `CaravanOverviewPresenter` 컴포넌트의 `Treadmill Panel` 필드를 찾는다.
4. Scene에 별도 프리팹 인스턴스로 배치된 루트 `TreadmillPanel` 오브젝트의 `TreadmillPanel` 컴포넌트를 해당 필드에 연결한다.
5. 이 연결은 InGame Scene의 프리팹 인스턴스 override로 저장한다. `MainUICanvas.prefab`에 다른 프리팹의 asset 컴포넌트를 연결하지 않는다.
6. 네 개의 점유된 Caravan Slot 모두에서 짧은 클릭과 롱프레스가 서로 중복 실행되지 않는지 확인한다.

## 관련 요소 위치

- Overview 프리팹: `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`
- Presenter 오브젝트: `MainUICanvas` 인스턴스 내부 `CaravanScrollView`
- Presenter 스크립트: `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/CaravanOverviewPresenter.cs`
- 슬롯 입력 스크립트: `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/CaravanSlotView.cs`
- 트레드밀 프리팹: `Assets/_Project/08.Prefabs/Treadmill/TreadmillPanel.prefab`
- 트레드밀 대상 오브젝트: InGame Scene의 루트 `TreadmillPanel` 프리팹 인스턴스
- 트레드밀 스크립트: `Assets/_Project/01.Core/10_Treadmill/TreadmillPanel.cs`
- 연결할 필드: `CaravanOverviewPresenter.treadmillPanel`

## 확인 항목

- 짧은 클릭: 해당 `caravanId`의 트레드밀이 열리고 사용자 지정 `displayName`이 표시된다.
- 1초 이상 누름: 이름 변경 팝업만 열리고 트레드밀은 열리지 않는다.
- Empty, Locked, Unknown Slot에서는 두 요청 모두 발생하지 않는다.
- MainUI 연결 전에는 Presenter의 `treadmillPanel`이 비어 있으므로 기존 이름 변경 기능 외에 트레드밀 열기 동작은 발생하지 않는다.
