# Project Debug Panel 팀 가이드

## 목적

`ProjectDebugPanel`은 SRDebugger 대체품이 아니라 현재 프로젝트 Framework 상태만 읽는 개발용 패널입니다. 상태 변경 명령은 제공하지 않습니다.

## 소유 구조

| 역할 | 경로 |
| --- | --- |
| Runtime 스크립트 | `Assets/_Project/98.DebugTools/Scripts/ProjectDebugPanel.cs` |
| Prefab creator (Editor) | `Assets/_Project/98.DebugTools/Editor/ProjectDebugPrefabCreator.cs` |
| 이 가이드 | `Assets/_Project/98.DebugTools/Documentation/ProjectDebugPanelGuide.md` |
| **런타임 캐논 프리팹** | `Assets/_Project/08.Prefabs/Debug/ProjectDebugPanel.prefab` |

코드·문서·생성기는 `98.DebugTools`가 소유하고, Scene에 배치되는 런타임 프리팹은 `08.Prefabs/Debug`가 소유합니다.

InGame Scene은 캐논 프리팹 GUID를 참조합니다. Scene Owner 조율 없이 Scene을 수정하지 마십시오.

## 사용 조건

- Unity Editor와 `Development Build`에서만 Runtime assembly가 포함됩니다.
- 일반 Release Build에서는 assembly define constraint에 의해 컴파일 대상에서 제외됩니다.
- 플레이 중 `F12`로 열고 닫습니다.
- Inspector `Visible On Start`(`visibleOnStart`)가 false이면 시작 시 숨김 상태이며, F12로 토글합니다.

## Inspector 연결

1. **캐논 프리팹** `Assets/_Project/08.Prefabs/Debug/ProjectDebugPanel.prefab`을 테스트용 Scene에 배치합니다.
2. `ProjectDebugPanel`의 `Visible On Start`를 필요에 따라 설정합니다.
3. 공유 Scene(예: InGame)에 반영할 때는 Scene 담당자와 별도로 조율합니다.

## Prefab creator 정책

메뉴: `Tools > ND Debug > Create Project Debug Panel Prefab`

| 상황 | 동작 |
| --- | --- |
| Editor 자동 초기화 + 캐논 프리팹 존재 | 아무 작업 없음 |
| Editor 자동 초기화 + 캐논 프리팹 없음 | 캐논 경로에 생성 |
| 메뉴 실행 + 캐논 프리팹 존재 | 덮어쓰기 거부, 경로 안내 |
| 메뉴 실행 + 캐논 프리팹 없음 | 캐논 경로에 생성 |

기존 캐논 프리팹을 삭제·재생성하면 GUID 또는 내부 fileID가 바뀌어 Scene 참조가 깨질 수 있습니다. 덮어쓰기는 의도적으로 지원하지 않습니다.

## 레거시 중복 프리팹

다음 에셋은 creator가 예전에 생성하던 경로이며, InGame/TestEditor Scene은 참조하지 않습니다.

```text
Assets/_Project/98.DebugTools/Prefabs/ProjectDebugCanvas.prefab
```

상태: Scene 미참조 중복(생성된 템플릿). Work C에서는 삭제하지 않고 유지합니다. 후속 cleanup에서 참조 재확인 후 제거를 검토하십시오. **캐논으로 사용하지 마십시오.**

## 표시 값

- Scene, FrameworkRoot/SaveData 존재 상태
- 선택 Caravan과 전체 Caravan, Trade 진행, Pending Settlement
- Trading/Development Currency
- SharedGameData 로드 여부와 Town, Market, TradeItem, Wagon, DraftAnimal, Route 개수

값이 아직 초기화되지 않았거나 공개 멤버를 찾지 못하면 `N/A` 또는 `No`로 표시됩니다.

## 테스트 절차

1. FrameworkRoot가 없는 빈 테스트 Scene에 캐논 프리팹을 배치하고 Play Mode에서 F12를 눌러 예외 없이 상태가 표시되는지 확인합니다.
2. Boot에서 시작해 Title, Loading, InGame으로 이동하며 Scene 이름과 Framework 상태가 갱신되는지 확인합니다.
3. 무역을 시작해 Caravan·Trade·Pending 섹션이 갱신되는지 확인합니다.
4. 공용 데이터 로드 뒤 각 데이터 개수가 표시되는지 확인합니다.
5. Development Build에서 F12 동작을 확인합니다.
6. Development Build를 끈 Player 빌드에서 `ND.DebugTools.Runtime`과 패널이 포함되지 않는지 확인합니다.
7. 긴 패널 내용에서 스크롤이 하단까지 도달하는지 확인합니다.

## 남은 위험

- CoreServices가 asmdef 없이 predefined assembly에 있으므로 Runtime assembly는 Framework 공개 멤버를 리플렉션으로 조회합니다. 공개 타입명이나 멤버명이 바뀌면 해당 값은 `N/A`가 됩니다.
- F12 입력은 Input System Package의 현재 `Keyboard` 장치를 사용합니다. 키보드 장치가 없는 환경에서는 입력을 안전하게 무시합니다.
- 프리팹 배치 없이 자동 생성되지는 않습니다. Scene 직접 수정 금지 조건에 따라 각 테스트 Scene에서 명시적으로 배치해야 합니다.
- Unity Editor 컴파일, 실제 Scene 흐름, Player 빌드 검증은 Unity 환경에서 수행해야 합니다.

## Monitoring behavior

Monitoring sections are read-only. Opening, closing, scrolling, periodic refreshing, IMGUI Layout, and Repaint do not mutate game state.

The panel is compiled only when `UNITY_EDITOR` or `DEVELOPMENT_BUILD` is defined. It is not available in a normal Release build.

## Force Arrival command

`Force Selected Trade to Arrival` is enabled only when the current selected Caravan has an exact matching progress entry whose state is `Traveling` and whose active trade ID is non-empty.

The displayed snapshot is presentation-only. On every click, the panel resolves the current `FrameworkRoot.Instance`, `CurrentSaveData`, `selectedCaravanId`, and matching `tradeProgressEntries` entry again. It validates the entry's exact `caravanId` and `activeTradeId`, then calls:

```text
TryForceCompleteTrade(caravanId, tradeId)
```

The persistent `Last Result` area reports structured command, identity, and Save failure details when those properties are available. After an invocation, the monitoring snapshot refreshes once.

## Trade lifecycle warning

The intended lifecycle is:

```text
Traveling
-> SettlementPending / Settling
-> Arrival Sale
-> Claim
```

The command does not sell cargo. It does not grant Claim rewards. It does not move the trade directly to `Completed`.

## Legacy command distinction

`ProjectDebugPanel` does not call `CompleteTradeImmediately()`. It calls only the exact-target `TryForceCompleteTrade(caravanId, tradeId)` API through Reflection.

## Currency controls

The panel displays the current player-global Trading Currency and Development Currency values. Each currency has explicit `+100`, `+1,000`, and `+10,000` grant buttons plus a separate custom input and `Add` button.

Custom amounts accept plain positive whole numbers representable by `long`. Empty input, zero, negative values, decimal text, non-numeric text, and values above `long.MaxValue` are rejected without invoking a command. The controls do not subtract, set, or reset currency.

Each explicit grant calls the matching Framework debug command through Reflection:

```text
TryAddTradingCurrency(long amount)
TryAddDevelopmentCurrency(long amount)
```

The panel does not directly edit SaveData and does not call Save itself. A valid Framework grant performs one Save transaction; a failed Save rolls back the candidate currency change. Structured `SaveResult` success or failure details remain visible, and the panel refreshes the current SaveData values after an invocation.

`TradingCurrencyChanged` is emitted only after a successful Save. Development Currency introduces no new event contract.

Currency grants occur only inside explicit preset or `Add` button-click branches. Layout, Repaint, panel open or close, F12 toggling, scrolling, periodic refresh, and text editing do not grant currency.

## Known verification limitations

- Failed-grade visual routing was not manually exercised.
- Full Play Mode exit/restart was not exercised; the canonical API restore path passed.
- Currency persistence was verified through the Save transaction and JSON Save/Load round trip, not a full application restart.
