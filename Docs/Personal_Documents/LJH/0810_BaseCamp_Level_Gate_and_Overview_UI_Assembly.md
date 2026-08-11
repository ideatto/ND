# BaseCamp 건물 레벨 제한 및 현황 UI 조립

## 1. 목적

BaseCamp 레벨을 다른 거점 건물의 건설·증축 상한으로 사용하고, BaseCamp 건물 블록을 눌렀을 때 현재 건물 레벨을 확인하는 UI를 연다.

```text
BaseCamp Lv.0 → 다른 건물 건설 불가
BaseCamp Lv.N → 다른 건물 Lv.N까지 건설·증축 가능
```

UI 패널과 행은 런타임에 생성하지 않는다. `BaseCampOverviewPopup.prefab` 안에 Backdrop, Card, Header, 열 제목, BaseCamp 포함 7개 행, 하단 안내, 닫기 버튼이 모두 정적 오브젝트로 존재한다.

## 1.1 브랜치 전달 및 재조립 전제

현재 기능 개발 브랜치에서는 다음 두 파일의 조립 변경을 충돌 방지를 위해 discard하고 코드·독립 Prefab·문서만 전달한다.

- `Assets/_Project/08.Prefabs/MainUICanvas.prefab`
- `Assets/_Project/07.Scenes/04_InGame/InGame.unity`

이 discard 방침은 **현재 기능 개발 브랜치에만 적용**된다. 기능을 조립할 대상 브랜치에서는 최신 버전의 위 두 파일을 열어 이 문서대로 수정하고, 완성된 조립 결과를 저장·커밋해야 한다. 대상 브랜치에서도 두 파일을 discard하라는 뜻이 아니다.

현재 기능 개발 브랜치에서 다음 재료는 discard하지 않고 보존한다.

- BaseCamp 관련 C# 및 `.meta`
- `BaseCampOverviewPopup.prefab` 및 `.meta`
- `BuildingMaterialTestButton.prefab` 및 `.meta`
- `TradeItem_Logs.asset` 및 `.meta`
- BaseCamp 관련 EditMode 테스트 및 `.meta`

대상 브랜치의 시작 상태는 다음과 같아야 한다.

- 최신 `dev2`를 반영한 뒤의 `MainUICanvas.prefab`, `InGame.unity`를 사용한다.
- 현재 기능 개발 브랜치의 두 파일을 checkout, cherry-pick, 파일 복사로 덮어쓰지 않는다.
- 대상 브랜치에서 두 파일에 이미 존재하는 다른 기능의 UI, Scene 연결, Prefab override를 보존한다.
- 조립 전 두 파일의 기존 변경 여부를 확인하고 BaseCamp와 무관한 변경을 Revert하거나 덮어쓰지 않는다.

재조립 순서는 반드시 다음과 같다.

```text
코드·독립 Prefab 존재 및 컴파일 확인
→ MainUICanvas.prefab에 Popup/Entry 조립 후 저장
→ Prefab Mode를 닫았다 다시 열어 참조 유지 확인
→ InGame.unity를 열어 Prefab 상속 확인
→ InGame에 통나무 지급 버튼만 Scene 전용으로 배치
→ Scene 저장
→ EditMode 계약 테스트
→ Play Mode 수동 검증
→ 대상 브랜치에서 MainUICanvas.prefab과 InGame.unity 조립 결과 커밋
```

## 2. 권위 데이터와 처리 흐름

BaseCamp도 다른 건물과 동일하게 다음 SaveData에 저장한다.

```text
SaveData.player.villageBuildings
└─ VillageBuildingSaveData
   ├─ displayName = "베이스 캠프"
   └─ level = 현재 레벨
```

건설 확정 흐름:

```text
건설/증축 확인
→ buildId, BuildData, SaveData 유효성 확인
→ targetLevel = currentLevel + 1
→ BaseCamp 레벨 사전 검증(사용자 안내용)
→ HomeInventory와 재료 입력 검증
→ BuildingUpgradeCommand
→ Snapshot
→ TryStage에서 현재 SaveData 기준 BaseCamp 레벨 재검증
→ 재료 재검증
→ 재료 차감 + 건물 레벨 stage
→ SaveResult 성공 확인
→ Runtime 건물 반영
→ BuildingListPanel 갱신
```

BaseCamp 자체 건설·증축은 자기 레벨 제한에서 제외한다. 제한 실패 또는 재료·저장 실패 시 Scene 건물과 목록을 성공 상태로 갱신하지 않는다.

## 3. 필요 코드와 에셋

| 용도 | 경로 |
| --- | --- |
| 레벨 제한 정책 | `Assets/_Project/11.CoreServices/Scripts/Building/BaseCampBuildingLevelPolicy.cs` |
| 실제 건설 연결 | `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/BuildingConstructionRuntimeHandler.cs` |
| Popup 표시 | `Assets/_Project/05.UI/04_InGame/YHY/Scripts/BaseCamp/BaseCampOverviewPopupController.cs` |
| 건물 클릭 연결 | `Assets/_Project/05.UI/04_InGame/YHY/Scripts/BaseCamp/BaseCampMainUiEntry.cs` |
| 정적 Popup Prefab | `Assets/_Project/08.Prefabs/UI/Building/BaseCampOverviewPopup.prefab` |
| 통나무 지급 스크립트 | `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/Building/BuildingMaterialTestButton.cs` |
| 통나무 지급 Prefab | `Assets/99.Sandbox/_LJH/Prefab/BuildingMaterialTestButton.prefab` |
| 통나무 데이터 | `Assets/_Project/02.Data/01_ScriptableObjects/TradeItem/Material/TradeItem_Logs.asset` |
| Editor 재생성 도구 | `Assets/_Project/05.UI/04_InGame/YHY/Editor/BaseCampOverviewUiPrefabBuilder.cs` |
| 레벨 정책 테스트 | `Assets/_Project/11.CoreServices/Editor/BaseCampBuildingLevelPolicyTests.cs` |
| Prefab/Scene 계약 테스트 | `Assets/_Project/11.CoreServices/Editor/BaseCampOverviewUiContractTests.cs` |

## 4. MainUICanvas Prefab 조립

`MainUICanvas.prefab`에 다음을 한 번만 배치한다.

1. `BaseCampOverviewPopup.prefab` 인스턴스를 `MainUICanvas`의 비활성 자식으로 둔다.
2. 기존 `BuildingListPanel` GameObject에 `BaseCampMainUiEntry`를 한 개 추가한다.
3. 다음 참조를 연결한다.

이 단계에서 `BuildingLogsDebugButton`은 배치하지 않는다. 통나무 지급 Prefab은 과거 테스트 재료로만 보존하며 기본 InGame 조립에는 사용하지 않는다.

| BaseCampMainUiEntry 필드 | 대상 |
| --- | --- |
| `buildingListPanel` | 같은 MainUICanvas의 기존 `BuildingListPanel` |
| `popup` | 정적 `BaseCampOverviewPopup`의 Controller |
| `noticeUI` | 같은 MainUICanvas의 기존 `NoticeUI` |

### 권장 자동 조립

1. 최신 dev2의 `MainUICanvas.prefab`이 로컬에 반영됐는지 확인한다.
2. Unity 컴파일 오류가 없는 상태에서 메뉴 `ND > UI > Install BaseCamp Overview Into Main UI`를 실행한다.
3. 설치 도구는 문자열 경로가 아니라 `BuildingListPanel`, `NoticeUI`, `BaseCampOverviewPopupController` 컴포넌트로 대상을 찾는다.
4. 기존 Popup/Entry가 있으면 재사용하고 없을 때만 추가하므로 반복 실행해도 중복 생성하지 않는다.
5. 저장 후 Prefab Mode에서 아래 수동 조립 결과와 동일한지 확인한다.

### 수동 조립 위치와 연결

```text
MainUICanvas (Prefab root)
├─ ...최신 dev2 기존 자식 유지
├─ BuildingPanel 또는 BuildingListPanel 컴포넌트를 가진 기존 오브젝트
│  └─ BaseCampMainUiEntry 컴포넌트 추가
└─ BaseCampOverviewPopup (BaseCampOverviewPopup.prefab instance, 기본 비활성)
```

- `BaseCampOverviewPopup.prefab`은 MainUICanvas root의 직접 자식으로 배치한다. `InfoPanel`, `BuildingPanel`, 다른 Popup 내부에 넣지 않는다.
- Popup Prefab을 Unpack하지 않고 Prefab 인스턴스 연결을 유지한다.
- `BaseCampMainUiEntry`는 Hierarchy 이름이 아니라 `BuildingListPanel` 컴포넌트가 붙은 기존 GameObject에 추가한다.
- `buildingListPanel`: Entry와 같은 GameObject의 기존 `BuildingListPanel` 컴포넌트
- `popup`: root 직접 자식으로 배치한 `BaseCampOverviewPopup`의 `BaseCampOverviewPopupController`
- `noticeUI`: 같은 MainUICanvas 안의 기존 `NoticeUI` 컴포넌트
- Popup root는 비활성화하고 Entry와 `BuildingListPanel`은 기존 활성 상태를 유지한다.
- 완료 후 `MainUICanvas.prefab`에 Apply/Save한다. 대상 브랜치에서는 이 변경을 정상 조립 결과로 커밋한다.

Popup Controller의 필수 참조:

- `baseCampLevelText`
- `buildingRows` 6개(창고, 목장, 상점, 빵집, 오두막, 풍차)
- `levelLimitText`
- `unlockGuideText`
- `backdropButton`
- `closeButton`

Prefab 기본 상태는 비활성이다. Backdrop과 X 버튼은 `Close()`에 런타임 Listener로 연결되지만 Button 오브젝트 자체는 Prefab에 미리 존재한다.

## 5. InGame Scene 반영 방식

`InGame.unity`는 `MainUICanvas.prefab` 인스턴스를 사용하므로 BaseCamp 기능에 Scene 전용 외부 참조가 없다. 따라서 정상 조립은 다음과 같다.

```text
MainUICanvas.prefab에 정적 조립
→ InGame의 기존 MainUICanvas 인스턴스가 상속
→ 불필요한 Scene override를 만들지 않음
```

InGame에서 확인할 수량:

| 컴포넌트 | 수량 |
| --- | ---: |
| `BaseCampMainUiEntry` | 1 |
| `BaseCampOverviewPopupController` | 1 |
| `BuildingConstructionRuntimeHandler` | 1 |

Popup은 Play Mode 진입 전과 최초 화면 진입 시 비활성 상태여야 한다.

MainUICanvas Prefab 조립 후 InGame에서 Entry 또는 Popup이 보이지 않으면 Scene에 새로 만들지 말고 다음을 먼저 확인한다.

1. InGame의 MainUICanvas가 수정한 `MainUICanvas.prefab`의 인스턴스인지 확인한다.
2. Prefab override에서 BaseCamp 자식이나 컴포넌트가 제거된 기록이 있는지 확인한다.
3. 필요하면 해당 BaseCamp 제거 override만 Revert한다.
4. Entry 1개, Popup 1개가 상속된 뒤에만 통나무 버튼을 별도로 배치한다.

BaseCamp 런타임 기능을 위해 `BuildingConstructionRuntimeHandler`에 새 Inspector 참조를 연결할 필요는 없다. 레벨 정책은 기존 Handler 코드 경로에서 SaveData를 직접 검증한다. InGame에서 BaseCamp 전용으로 새로 만드는 Scene 오브젝트는 아래 테스트 버튼뿐이다.

### InGame 테스트 버튼 조립

이 버튼은 BaseCamp 기능 자체의 필수 런타임 UI가 아니라 건설·증축 검증용 Editor/Development 전용 도구다. 런타임 코드로 생성하지 않고 Scene에 Prefab 인스턴스로 배치한다.

`BuildingMaterialTestButton.prefab`은 과거 수동 검증용 재료로만 보존한다. 기본 조립에서는 활성 `MainUICanvas`나 InGame Scene에 배치하지 않는다.
3. `InfoPanel` 바로 다음 sibling에 둔다. `BackgroundWall`이나 `InfoPanel`보다 앞 sibling이면 화면 뒤에 가려진다.
4. RectTransform을 좌하단 anchor/pivot `(0,0)`, anchored position `(300,20)`, size `(260,56)`으로 둔다. 기존 운송 수단 지급 버튼 `(20,20)`의 오른쪽이며 두 버튼 사이 간격은 20px다.
5. Inspector 값을 다음 표와 맞춘다.
6. 이 Scene 인스턴스 변경은 `MainUICanvas.prefab` 원본에 Apply하지 않고 대상 브랜치의 `InGame.unity`에 저장·커밋한다.

Hierarchy 기준:

```text
InGame.unity
└─ 기존 MainUICanvas.prefab instance
   ├─ BackgroundWall
   ├─ ...최신 dev2 기존 UI 유지
   ├─ InfoPanel
   ├─ TransportInventoryRewardDebugButton (존재할 경우, 20,20)
   └─ ...Popup / NoticeUI
```

- `BaseCampOverviewPopup`과 `BaseCampMainUiEntry`를 InGame에서 별도로 추가하지 않는다. 둘은 MainUICanvas Prefab 상속으로 들어와야 한다.
- `BuildingLogsDebugButton` Scene override를 만들지 않는다.
- 최신 dev2에 운송 수단 지급 버튼이 없더라도 통나무 버튼 위치는 `(300,20)`을 유지한다.
- Scene 저장 후 MainUICanvas Prefab override 목록에 통나무 버튼 추가 외의 BaseCamp 관련 제거·변경 override가 생기지 않았는지 확인한다.

| 항목 | 값 |
| --- | --- |
| `BuildingMaterialTestButton.items` Size | `1` |
| `items[0].item` | `TradeItem_Logs.asset` (`itemId = Logs`) |
| `items[0].purchaseUnitPrice` | `0` |
| `grantQuantity` | `40` |
| Label | `통나무 40개 지급` |

운송 수단 지급용 `TransportInventoryRewardDebugButton.prefab`과는 별개다. 운송 수단 버튼은 `(20,20)`, 통나무 버튼은 오른쪽 `(300,20)`에 배치한다. 두 버튼 모두 Popup과 `NoticeUI`보다 낮은 sibling index로 유지한다.

## 6. 표시 내용

```text
Header: 베이스 캠프
Title: 거점 건물 현황

건물                 현재 레벨
베이스 캠프             Lv.N
창고                    Lv.N
목장                    Lv.N
상점                    Lv.N
빵집                    Lv.N
오두막                  Lv.N
풍차                    Lv.N

건물 레벨 상한: Lv.N
다음 레벨을 해금하려면 베이스 캠프를 증축하세요.
```

건물명과 레벨은 별도 Text 오브젝트이며 각 열 안에서 중앙 정렬한다. Popup 조회는 SaveData를 변경하거나 Dirty 처리하지 않는다. 중복·비정상 건물 데이터가 있으면 `Open()`이 실패하고 기존 NoticeUI가 한국어 안내를 표시한다.

## 7. 수동 Play Mode 테스트

테스트 재료가 부족하더라도 기본 조립 결과에 지급 버튼을 추가하지 않는다. 필요하면 별도 테스트 절차에서 SaveData나 전용 테스트 도구를 사용한다.

### A. BaseCamp 미건설

1. BaseCamp가 없는 신규 또는 테스트 Save를 준비한다.
2. `+`에서 창고·목장 등 일반 건물의 Lv.1 건설을 확정한다.
3. BaseCamp Lv.1 필요 안내가 출력되는지 확인한다.
4. 재료 수량과 일반 건물 레벨이 변하지 않았는지 확인한다.

### B. BaseCamp Lv.1

1. BaseCamp Lv.1을 건설한다.
2. 건물 목록의 BaseCamp 블록을 누른다.
3. 현황 Popup의 BaseCamp가 Lv.1이고 상한도 Lv.1인지 확인한다.
4. 창고 또는 목장 Lv.1 건설이 성공하는지 확인한다.
5. 같은 건물을 Lv.2로 증축하려 하면 BaseCamp Lv.2 필요 안내가 출력되는지 확인한다.
6. 실패 시 재료와 건물 레벨이 변하지 않았는지 확인한다.

### C. BaseCamp Lv.2

1. BaseCamp를 Lv.2로 증축한다.
3. 이전에 차단된 일반 건물 Lv.2 증축이 성공하는지 확인한다.
4. Popup을 다시 열어 BaseCamp, 대상 건물, 상한이 모두 Lv.2로 갱신되는지 확인한다.
5. Play Mode를 종료 후 다시 진입해 같은 레벨이 복원되는지 확인한다.

### D. Popup 동작

1. BaseCamp 블록 클릭으로만 Popup이 열리는지 확인한다.
2. Backdrop 클릭과 X 버튼이 각각 Popup을 닫는지 확인한다.
3. 카드 내부 클릭은 Popup을 닫지 않는지 확인한다.
4. Popup을 반복해서 열어 이벤트가 중복 실행되지 않는지 확인한다.
5. Console에 Missing Reference, Missing Script, NullReferenceException이 없는지 확인한다.

### E. 테스트 버튼

1. Play Mode에서 좌하단에 `통나무 40개 지급` 버튼이 보이지 않는지 확인한다.
2. 버튼 클릭 전후 HomeInventory의 Logs 수량 차이가 정확히 40인지 확인한다.
3. 버튼을 두 번 누르면 누적 80이 증가하는지 확인한다.
4. Popup을 연 상태에서 버튼이 Popup 위로 그려지거나 클릭을 가로채지 않는지 확인한다.
5. Hierarchy에 `BuildingLogsDebugButton`이 없는지 확인한다.

## 8. 자동 검증

EditMode에서 다음을 실행한다.

- `BaseCampBuildingLevelPolicyTests`
- `BaseCampOverviewUiContractTests`

현재 기능 개발 브랜치에서는 독립 Popup/Build UI 계약만 일반 실행한다. 다음 두 조립 계약은 `MainUICanvas.prefab`과 `InGame.unity`를 조립한 대상 브랜치에서 Explicit로 실행한다.

- `MainUiPrefab_HasOneWiredEntryAndPopup`
- `InGameScene_InheritsBaseCampUiThroughMainUiPrefab`

Unity Test Runner에서 `BaseCampOverviewUiContractTests` Fixture 자체를 명시적으로 선택하면 NUnit이 Explicit 테스트도 선택된 것으로 처리한다. 전달 브랜치에서는 전체 EditMode 실행을 사용하거나 `PopupPrefab_HasAllStaticReferences`, `BuildUiScene_DependsOnPopupPrefab`만 선택한다.

완료 조건:

- 관련 테스트 10개 통과
- MainUICanvas에 Entry/Popup 각각 정확히 1개
- 모든 직렬화 참조 연결
- Build UI Scene이 Popup Prefab을 참조
- InGame Scene에 `BuildingLogsDebugButton`이 존재하지 않음
- Unity Console Error 0건

## 9. 원복 후 재조립 검증 기록

2026-08-10에 다음 왕복 검증을 수행했다.

1. 현재 기능 개발 브랜치의 `MainUICanvas.prefab`, `InGame.unity` 변경을 HEAD로 원복했다.
2. BaseCamp Entry, Popup, `BuildingLogsDebugButton`이 모두 제거된 시작 상태를 확인했다.
3. 이 문서 순서대로 MainUI의 Entry/Popup과 InGame의 통나무 버튼만 재조립했다.
4. MainUI diff에는 `BaseCampMainUiEntry`와 `BaseCampOverviewPopupController`만 남고, InGame에는 `BuildingLogsDebugButton` 관련 diff가 없는지 확인한다.
5. 통나무 버튼의 `Logs`, `40`, `(300,20)`, `(260,56)`, `InfoPanel` 다음 sibling 설정을 확인했다.
6. BaseCamp 정책/Popup/Scene 조립 계약 테스트 10/10과 수동 Play Mode 검증을 통과했다.
7. Unity가 자동 기록한 타 기능의 빈 직렬화 필드와 `WorldMapRenderRootV2.prefab` 기본값 변경은 제거했다.

따라서 현재 기능 개발 브랜치에서는 두 조립 결과 파일을 다시 discard해도 되고, 대상 브랜치에서는 이 문서로 동일한 조립 결과를 재현한 뒤 Explicit 계약 테스트를 실행한다.
