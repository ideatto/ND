# 마을 편집모드 및 드롭다운 UI 연결 가이드

## 목적

`마을 꾸미기` 팝업에 추가된 편집모드 버튼, 향후 사용할 편집 저장 버튼, 건물/환경 드롭다운과 Inspector 확장 지점을 후속 작업자가 찾고 연결할 수 있도록 정리한다.

## 주요 파일

- 팝업 UI 프리팹: `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`
- 팝업 구성 코드: `Assets/_Project/05.UI/04_InGame/YHY/Scripts/BuildingAddPopup.cs`
- 편집 상태와 건물 입력: `Assets/_Project/01.Core/07_Village/YHY/BuildingPlacementController.cs`
- 편집모드 버튼 프리팹: `Assets/_Project/08.Prefabs/UI/Building/VillageEditModeButton.prefab`
- 편집 저장 버튼 프리팹: `Assets/_Project/08.Prefabs/UI/Building/VillageEditSaveButton.prefab`

Scene에 직접 UI 오브젝트를 추가하지 않고 `MainUICanvas.prefab`을 기준으로 구성한다.

## 편집모드 버튼

현재 `BuildingAddPopup`의 `editModeButtonPrefab`에 `VillageEditModeButton.prefab`이 연결되어 있다. 팝업이 열릴 때 제목 영역 좌측 상단에 런타임으로 생성되며, 드롭다운 Content의 자식이 아니므로 스크롤되지 않는다.

현재 버튼은 우측 `닫기` 버튼과 대응하도록 `120 x 56` 크기로 생성한다. 라벨은 줄바꿈 없이 가로·세로 중앙 정렬하며, 이 규격은 `BuildingAddPopup.CreateHeaderButton()`에서 적용한다.

클릭 흐름은 다음과 같다.

1. `BuildingPlacementController.EnterEditMode()` 호출
2. `editMode` 상태를 `true`로 전환
3. `BuildingAddPopup.Close()` 호출
4. 팝업이 마을 편집 입력을 가리지 않도록 즉시 닫힘

`BuildingPlacementController`는 `editMode == true`일 때만 기존 건물을 선택하고 이동·회전할 수 있게 한다.

## 편집 저장 버튼

`VillageEditSaveButton.prefab`은 후속 배치를 위한 독립 프리팹만 존재한다. 현재 `MainUICanvas.prefab`이나 `BuildingAddPopup`에는 생성·표시·클릭 이벤트가 연결되어 있지 않다.

향후 저장 또는 편집 종료 UI를 만들 때 최소한 다음 상태 종료 호출이 필요하다.

```csharp
buildingPlacementController.ExitEditMode();
```

`ExitEditMode()`는 현재 선택을 해제하고 `editMode`를 `false`로 되돌린다. 이후 `마을 꾸미기` 팝업을 다시 열면 편집모드 버튼이 활성화된다. 실제 배치 데이터 저장 정책이 별도로 마련되면 저장 성공 후 `ExitEditMode()`를 호출하는 순서가 권장된다.

## 건물/환경 드롭다운

`MainUICanvas.prefab`에서 다음 경로의 컴포넌트를 찾는다.

```text
MainUICanvas
└─ BuildingAddPopup
   └─ BuildingAddPopup (Component)
      └─ Sections
```

`Sections`의 각 원소가 드롭다운 하나다.

- `title`: 드롭다운 제목
- `initiallyExpanded`: 처음 열릴지 여부. 현재 건물과 환경 모두 `false`
- `contentSource`
  - `RegistryBuildings`: `VillageBuildingRegistry`의 건물 목록을 자동 구성
  - `InspectorEntries`: Inspector에 입력한 `entries`를 구성
- `entries`: 해당 드롭다운 아래에 표시할 수동 항목 목록

팝업이 비활성화되면 런타임 펼침 상태를 초기화한다. 따라서 팝업을 다시 열 때 모든 드롭다운은 접힌 상태로 시작한다.

## Inspector에서 항목 추가하기

Prefab Mode로 `MainUICanvas.prefab`을 열고 `BuildingAddPopup` 오브젝트의 `BuildingAddPopup` 컴포넌트를 선택한다.

카테고리 추가:

1. `Sections` 크기를 늘린다.
2. `title`, `initiallyExpanded`, `contentSource`를 설정한다.
3. 수동 항목을 넣을 경우 `contentSource`를 `InspectorEntries`로 설정한다.

하위 블록 추가:

1. 대상 Section의 `entries` 크기를 늘린다.
2. `label`에 표시 문구를 입력한다.
3. `interactable`을 설정한다.
4. `selected` UnityEvent에 실제 처리 메서드를 연결한다.

현재 `환경` 아래에는 예시 항목 `나무`가 추가되어 있다. 버튼 블록은 표시되고 클릭 가능하지만 `selected` 이벤트는 비어 있으므로 아직 환경 오브젝트를 생성하지 않는다. 나무 배치 기능이 준비되면 이 이벤트에 담당 컴포넌트의 public 메서드를 연결한다.

## 현재 Inspector 구조

현재 Inspector 데이터는 `BuildingAddPopup` 컴포넌트 안에 직접 직렬화된다.

```text
BuildingAddPopup
└─ Sections: List<MenuSection>
   ├─ title
   ├─ initiallyExpanded
   ├─ contentSource
   └─ entries: List<MenuEntry>
      ├─ label
      ├─ interactable
      └─ selected: UnityEvent
```

현재 `MainUICanvas.prefab`의 설정은 다음과 같다.

```text
Sections
├─ 건물
│  ├─ initiallyExpanded: false
│  ├─ contentSource: RegistryBuildings
│  └─ entries: 비어 있음
└─ 환경
   ├─ initiallyExpanded: false
   ├─ contentSource: InspectorEntries
   └─ entries
      └─ 나무
         ├─ label: 나무
         ├─ interactable: true
         └─ selected: 연결 없음
```

`RegistryBuildings`는 `VillageBuildingRegistry`를 읽어 버튼을 자동 생성한다. `InspectorEntries`는 Prefab Inspector에 입력한 `MenuEntry`를 읽어 텍스트 버튼을 자동 생성한다. 실제 버튼 GameObject를 Content 아래에 저장하는 방식은 아니다.

현재 `나무`는 표시용 항목이다. 고유 ID, 아이콘, 환경 데이터 SO, 배치 Prefab 참조가 없고 `selected` 이벤트도 비어 있으므로 클릭해도 실제 기능은 실행되지 않는다.

## 환경 데이터 도입 시 Inspector 변경 방향

실제 환경 배치 기능을 붙일 때도 ScrollView와 드롭다운 버튼 생성 구조는 유지한다. 다만 `MenuEntry`를 단순 문구와 개별 UnityEvent만 가진 구조로 계속 확장하지 않는 것이 좋다.

1차 확장 시 권장하는 Inspector 항목은 다음과 같다.

```text
MenuEntry
├─ entryId          예: tree
├─ label            예: 나무
├─ icon             선택 사항
├─ interactable
└─ dataAsset        초기에는 null 허용, 추후 환경 데이터 SO 연결
```

- `entryId`는 표시 이름이 바뀌어도 선택 항목을 안정적으로 식별하기 위해 먼저 추가한다.
- `icon`은 현재 텍스트 블록 UI를 유지하면서 나중에 시각 정보를 붙일 수 있는 자리다.
- `dataAsset`은 실제 환경 데이터 형식이 확정된 뒤 구체적인 타입으로 변경한다.
- 실제 타입이 정해지기 전부터 범용 `UnityEngine.Object`를 영구 구조로 사용하는 것은 피한다.

환경 전용 SO 형식이 확정된 이후의 최종 권장 구조는 다음과 같다.

```text
Environment Section
└─ entries: List<EnvironmentPlaceableData>
   ├─ environmentId
   ├─ displayName
   ├─ icon
   └─ placementPrefab
```

또는 별도의 `EnvironmentCatalog` SO 한 개를 Section에서 참조하고, 카탈로그가 `EnvironmentPlaceableData` 목록을 소유하도록 구성할 수 있다. 항목이 많아지거나 여러 UI에서 같은 환경 목록을 사용한다면 Catalog 방식이 더 적합하다.

## 향후 마이그레이션 순서

1. 현재 `MenuEntry`에 `entryId`와 `icon`을 추가한다.
2. 실제 환경 배치 정책이 정해지면 환경 전용 데이터 SO 타입을 만든다.
3. `SectionContentSource`에 환경 카탈로그용 소스를 추가한다.
4. `나무` SO 또는 카탈로그 항목을 만들고 기존 `나무` 표시 항목과 연결한다.
5. 개별 `selected` UnityEvent 대신 공통 환경 선택 처리기에 선택 데이터를 전달한다.
6. 공통 처리기가 배치 서비스에 환경 데이터를 전달하도록 연결한다.
7. 전환이 끝난 뒤 기존 표시용 `나무` Inspector Entry와 빈 UnityEvent를 제거한다.

공통 선택 처리 방식을 사용하면 환경 항목마다 UnityEvent를 반복해서 수동 연결하지 않아도 되고, 이름과 실제 배치 데이터가 어긋나는 문제도 줄일 수 있다.
## 주의사항

- 편집모드와 드롭다운 UI는 Scene 복사본이 아니라 `MainUICanvas.prefab`에서 관리한다.
- `편집 저장` 프리팹이 존재한다고 해서 저장 정책까지 구현된 것은 아니다.
- 환경 항목은 `InspectorEntries`에 추가해야 하며, 건물 Registry 데이터와 섞지 않는다.
- 팝업의 ScrollRect는 세로 스크롤만 사용한다. Content의 가로 폭을 Viewport보다 크게 만들지 않는다.