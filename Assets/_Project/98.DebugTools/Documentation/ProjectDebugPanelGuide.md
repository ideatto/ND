# Project Debug Panel 팀 가이드

## 목적

`ProjectDebugCanvas.prefab`은 SRDebugger 대체품이 아니라 현재 프로젝트 Framework 상태만 읽는 개발용 패널입니다. 상태 변경 명령은 제공하지 않습니다.

## 사용 조건

- Unity Editor와 `Development Build`에서만 Runtime assembly가 포함됩니다.
- 일반 Release Build에서는 assembly define constraint에 의해 컴파일 대상에서 제외됩니다.
- 플레이 중 `F12`로 열고 닫습니다.

## Inspector 연결

1. `Assets/_Project/98.DebugTools/Prefabs/ProjectDebugCanvas.prefab`을 테스트용 Scene에 배치합니다.
2. `ProjectDebugPanel`의 `Visible On Start`를 필요에 따라 설정합니다.
3. 공유 Scene에 반영할 때는 Scene 담당자와 별도로 조율합니다. 이 작업은 기존 Scene을 수정하지 않습니다.

프리팹을 다시 생성해야 할 때는 Unity 메뉴 `Tools > ND Debug > Create Project Debug Canvas Prefab`을 실행합니다.

## 표시 값

- Scene, FrameworkRoot/SaveData 존재 상태
- TradeProgressState, InGameScreenState, 활성 Trade/Route ID
- Trade 시작/종료 UTC 시각, Caravan 진행률
- Trading/Development Currency
- SharedGameData 로드 여부와 Town, Market, TradeItem, Wagon, DraftAnimal, Route 개수

값이 아직 초기화되지 않았거나 공개 멤버를 찾지 못하면 `N/A` 또는 `No`로 표시됩니다.

## 테스트 절차

1. FrameworkRoot가 없는 빈 테스트 Scene에 프리팹을 배치하고 Play Mode에서 F12를 눌러 예외 없이 상태가 표시되는지 확인합니다.
2. Boot에서 시작해 Title, Loading, InGame으로 이동하며 Scene 이름과 Framework 상태가 갱신되는지 확인합니다.
3. 무역을 시작해 상태, ID, UTC 시각, 진행률이 갱신되는지 확인합니다.
4. 공용 데이터 로드 뒤 각 데이터 개수가 표시되는지 확인합니다.
5. Development Build에서 F12 동작을 확인합니다.
6. Development Build를 끈 Player 빌드에서 `ND.DebugTools.Runtime`과 패널이 포함되지 않는지 확인합니다.

## 남은 위험

- CoreServices가 asmdef 없이 predefined assembly에 있으므로 Runtime assembly는 Framework 공개 멤버를 리플렉션으로 조회합니다. 공개 타입명이나 멤버명이 바뀌면 해당 값은 `N/A`가 됩니다.
- F12 입력은 Input System Package의 현재 `Keyboard` 장치를 사용합니다. 키보드 장치가 없는 환경에서는 입력을 안전하게 무시합니다.
- 프리팹 배치 없이 자동 생성되지는 않습니다. Scene 직접 수정 금지 조건에 따라 각 테스트 Scene에서 명시적으로 배치해야 합니다.
- Unity Editor 컴파일, 실제 Scene 흐름, Player 빌드 검증은 Unity 환경에서 수행해야 합니다.
