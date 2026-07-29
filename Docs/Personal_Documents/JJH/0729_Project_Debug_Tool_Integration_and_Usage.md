# Project Debug Tool 통합 및 사용 가이드

작성일: 2026-07-29  
대상 프로젝트: `C:\unity\ND`  
대상 브랜치: `featuer/debug/integration/jjh`

## Purpose

- 기존 F12 `ProjectDebugPanel`의 Caravan 상태, 강제 도착, 재화 조절 기능을 유지한다.
- `TradeItemData` 기반 상품을 선택해 거점 인벤토리에 추가하는 개발 기능을 통합한다.
- `InGameScreenStateRouter`의 현재 상태와 화면 전환 이력을 F12 패널에서 확인한다.
- 길어진 디버그 화면을 탭으로 분리하고 각 탭에서 세로 스크롤을 사용할 수 있게 한다.

## 변경 파일

### `Assets/_Project/98.DebugTools/Scripts/ProjectDebugPanel.cs`

- F12 공용 디버그 창의 진입점이다.
- 다음 탭을 추가했다.
  - `Status`
  - `Home Inventory`
  - `Screen Router`
- 각 탭은 독립적인 세로 스크롤 위치를 유지한다.
- 기존 Status 화면의 다음 기능은 유지된다.
  - Framework 및 SaveData 상태 조회
  - 선택 Caravan과 전체 Caravan 상태 조회
  - 정산 대기 상태 조회
  - 선택된 Traveling 무역의 강제 도착
  - Trading/Development Currency 추가
- 패널 표시 여부와 관계없이 Screen Router 상태 전환을 감지한다.

### `Assets/_Project/98.DebugTools/Scripts/HomeInventoryItemDebugSection.cs`

- `Home Inventory` 탭의 화면과 동작을 담당한다.
- `FrameworkRoot.SharedGameData`에 로드된 `TradeItemData` 기반 상품을 조회한다.
- 선택한 상품 정의를 `TradeItemSaveData`로 변환한다.
- `PlayerMainManager.AddItem(...)`을 호출해 거점 인벤토리에 수량을 추가한다.
- `ND.DebugTools.Runtime`에서 predefined assembly를 직접 참조할 수 없으므로 리플렉션으로 연결한다.

### `Assets/_Project/98.DebugTools/Scripts/InGameScreenRouterDebugSection.cs`

- `Screen Router` 탭의 화면과 상태 감시를 담당한다.
- 다음 상태를 표시한다.
  - `InGameScreenStateRouter` 준비 여부
  - 현재 `InGameScreenState`
  - 현재 `TradeProgressState`
  - 활성 Trade ID
  - 활성 Route ID
- 화면 상태가 변경되면 최근 전환 이력을 최대 20개까지 기록한다.
- 기존 `InGameSceneRouterDebug`와 동일한 형식의 Console 로그를 출력한다.

### `Assets/_Project/11.CoreServices/Scripts/Debug/InGameSceneRouterDebug.cs`

- 기존 Scene과 Prefab의 컴포넌트 참조를 깨지 않기 위해 삭제하지 않았다.
- 새 F12 `Screen Router` 탭 사용을 안내하는 `Obsolete` 표시를 추가했다.
- 기존 Scene에 이 컴포넌트가 남아 있으면 화면 전환 로그가 중복 출력될 수 있다.

### 수정하지 않은 운영 코드

`Assets/_Project/11.CoreServices/Scripts/SceneFlow/InGameScreenStateRouter.cs`

- 실제 게임 화면 상태를 결정하고 이벤트를 발행하는 운영 코드다.
- 디버그 통합 과정에서는 수정하지 않았다.
- F12 패널은 이 Router의 공개 상태를 읽기만 한다.

## 사전 조건

### ProjectDebugCanvas 배치

테스트 Scene에 다음 Prefab이 배치되어 있어야 한다.

```text
Assets/_Project/98.DebugTools/Prefabs/ProjectDebugCanvas.prefab
```

실행 중 Hierarchy에 다음과 같은 오브젝트가 존재하면 정상이다.

```text
ProjectDebugCanvas
```

Scene 전환 과정에서 인스턴스 이름에는 `(Clone)`이 붙을 수 있다.

### 지원 빌드

디버그 어셈블리는 다음 환경에서만 포함된다.

- Unity Editor
- Development Build

일반 Release Build에는 포함되지 않는다.

### 코드 반영

Unity가 Play Mode인 동안 파일이 변경되었다면 다음 순서로 갱신한다.

1. Play Mode를 종료한다.
2. Unity 메뉴에서 `Assets > Refresh`를 실행한다.
3. Script compile이 끝날 때까지 기다린다.
4. Console에 compile error가 없는지 확인한다.
5. Play Mode에 다시 진입한다.

탭이 보이지 않고 예전 단일 Status 화면만 보인다면 이전 DLL이 실행 중인 상태일 가능성이 높다.

## 기본 사용 방법

1. Unity에서 대상 Scene을 연다.
2. Play Mode를 시작한다.
3. 키보드의 `F12`를 누른다.
4. 상단에서 필요한 탭을 선택한다.
5. 다시 `F12`를 누르면 창을 닫는다.

창 상단의 탭은 고정된다. 탭 아래 콘텐츠는 마우스 휠 또는 오른쪽 세로 스크롤바로 이동한다. 탭을 바꿨다가 돌아와도 각 탭의 스크롤 위치는 유지된다.

## Status 탭

기존 Project Debug 기능을 제공한다.

주요 조회 항목:

- 현재 Scene
- FrameworkRoot 및 SaveData 준비 상태
- Trading/Development Currency
- SharedGameData 로드 상태와 카탈로그 개수
- 선택 Caravan
- 전체 Caravan
- Trade Progress
- Pending Settlement

상태 변경 명령:

- `Force Selected Trade to Arrival`
  - 현재 선택된 Traveling 무역만 강제 도착시킨다.
  - 판매, 보상 수령, 정산 완료까지 수행하지 않는다.
- Currency Controls
  - Trading Currency 또는 Development Currency를 지정한 양만큼 추가한다.

상태 변경 버튼은 실행 직전에 Framework 상태와 대상 ID를 다시 검증한다.

## Home Inventory 탭

### 아이템 추가

1. `Home Inventory` 탭을 선택한다.
2. 상단 상태가 다음과 같은지 확인한다.

   ```text
   PlayerMainManager: Ready
   Trade item catalog: 1 이상
   ```

3. 상품 목록에서 원하는 `TradeItemData` 항목을 선택한다.
4. 수량을 설정한다.
   - `-10`
   - `-1`
   - 직접 입력
   - `+1`
   - `+10`
5. `Add to home inventory`를 누른다.
6. 선택 상품 아래의 `Owned` 또는 실제 인벤토리 UI에서 결과를 확인한다.

수량은 최소 1, 최대 999999로 보정된다.

### Refresh catalog

SharedGameData가 늦게 로드됐거나 상품 목록이 비어 있을 때 `Refresh catalog`를 누른다.

카탈로그는 Unity Editor의 `AssetDatabase`를 직접 검색하지 않는다. 실제 게임에서 검증·로드된 `FrameworkRoot.SharedGameData`를 사용하므로 Editor와 Development Build에서 같은 상품 ID를 사용한다.

### Add 버튼이 비활성화되는 조건

버튼 활성화 조건은 다음과 같다.

```text
유효한 상품 선택 && PlayerMainManager.Instance 존재
```

다음 표시가 나오면 버튼이 비활성화된다.

```text
PlayerMainManager: N/A
```

현재 프로젝트에서 `PlayerMainManager`가 Scene에 없거나 Bootstrap에서 생성되지 않았다는 의미다.

확인 당시 `PlayerMainManager`는 다음 테스트 Scene에서만 직접 참조됐다.

```text
Assets/_Project/07.Scenes/04_InGame/InGameMainManagerTest.unity
```

일반 InGame/Village Scene에서 사용하려면 다음 중 하나가 필요하다.

- 권장: 게임 Bootstrap 과정에서 `PlayerMainManager`를 생성하고 `DontDestroyOnLoad`로 유지한다.
- 임시 테스트: 현재 Scene의 관리용 GameObject에 `PlayerMainManager` 컴포넌트를 추가한다.

디버그 패널이 `PlayerMainManager`를 자동 생성하게 만들면 실제 초기화 누락을 숨길 수 있으므로 현재 구현에서는 자동 생성하지 않는다.

### 카탈로그는 있지만 버튼이 비활성화된 예

```text
PlayerMainManager: N/A
Trade item catalog: 8
Selected: Bread (Bread)
```

이 경우 `TradeItemData` 조회와 선택은 정상이다. `PlayerMainManager.Instance`만 없어서 추가 명령을 실행할 수 없는 상태다.

## Screen Router 탭

다음 항목을 확인한다.

- `InGameScreenStateRouter`
- `Current Screen State`
- `Trade Progress State`
- `Active Trade ID`
- `Active Route ID`
- `Recent transitions`

예상 전환 예:

```text
Preparation -> Traveling
Traveling -> Settlement
Settlement -> Town
```

`Clear history` 버튼으로 기록된 전환 이력을 지울 수 있다.

패널이 닫힌 상태에서도 `ProjectDebugPanel` 컴포넌트가 활성화되어 있으면 전환 감지는 계속된다.

## Troubleshooting

### F12를 눌러도 창이 열리지 않음

- Scene에 `ProjectDebugCanvas.prefab`이 배치됐는지 확인한다.
- 현재 빌드가 Editor 또는 Development Build인지 확인한다.
- Input System의 Keyboard 장치가 사용 가능한지 확인한다.

### 탭이 표시되지 않음

- Play Mode를 종료한다.
- `Assets > Refresh`를 실행한다.
- compile 완료 후 다시 Play Mode에 진입한다.
- `ND.DebugTools.Runtime` compile error가 없는지 확인한다.

### 아래 내용을 볼 수 없음

- 탭 콘텐츠 위에서 마우스 휠을 사용한다.
- 창 오른쪽의 세로 스크롤바를 드래그한다.
- `Home Inventory` 상품 목록 위에서는 내부 목록 스크롤이 우선될 수 있으므로, 목록 밖에서 전체 탭을 스크롤한다.

### Screen Router 로그가 두 번 출력됨

- Scene에 기존 `InGameSceneRouterDebug` 컴포넌트가 남아 있는지 확인한다.
- F12 패널이 전환 로그를 담당하므로 기존 컴포넌트를 Scene에서 제거하거나 비활성화한다.
- 코드 파일은 기존 Scene 직렬화 호환을 위해 유지한다.

### SharedGameData는 Ready지만 상품 목록이 비어 있음

- `Refresh catalog`를 누른다.
- SharedGameData 카탈로그에 `TradeItemData`가 등록됐는지 확인한다.
- Status 탭의 `Trade Items` 개수를 확인한다.

## Check

실제 `C:\unity\ND`에서 수행한 검증:

```text
dotnet build ND.DebugTools.Runtime.csproj --no-restore
- 오류 0
- 기존 Framework reference 경고 1

dotnet build Assembly-CSharp.csproj --no-restore
- 오류 0
- 기존 경고 40

git diff --check
- 통과
```

Unity Play Mode의 실제 입력, 스크롤, 인벤토리 추가 동작은 Unity Editor에서 수동 확인해야 한다.

## Risk

- Scene 변경: No
- Prefab 변경: No
- Meta 변경: Yes
  - Unity가 신규 디버그 섹션 CS 파일의 `.meta`를 생성했다.
- Package 변경: No
- SaveData 구조 변경: No
- 직렬화 필드 변경: No
- Enum 직렬화 변경: No
- Public API 변경: No
- 이벤트 연결 변경: No
  - Screen Router 상태는 리플렉션 polling으로 감지한다.
- 다중 객체 또는 ID 연결 변경: No
- 기존 데이터 마이그레이션 필요: No
- 원천 소유자 리뷰 필요: Yes
  - 기존 Development Tools의 공용 F12 패널에 기능을 추가했다.

## Remaining

- 일반 InGame/Village Bootstrap에서 `PlayerMainManager` 생성 책임 확정
- Unity Play Mode에서 Home Inventory 실제 추가 확인
- 기존 Scene의 `InGameSceneRouterDebug` 중복 로그 여부 확인
