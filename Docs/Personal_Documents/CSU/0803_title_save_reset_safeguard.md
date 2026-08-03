# Title 저장 플로우 Safeguard · Reset 확인 팝업

**작성일:** 2026-08-03  
**브랜치:** `feat/ui/title-scene-newgame-resetdata-button-safe-guard`  
**Base:** `dev2`  
**커밋:** `65a4012 feat(ui): safeguard title save flow and reset confirmation`  
**Feature root:** `Assets/_Project/05.UI/02_Title/`  
**관련 외부 수정:** `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs`, `Assets/_Project/11.CoreServices/Scripts/SceneFlow/TitleSceneController.cs`  
**목적:** Title 메뉴에서 저장 존재 여부에 따른 버튼 전환, 저장 초기화 확인 흐름, 중복 입력 차단, 런타임·디스크 저장 상태 일관성을 개인 작업 로그로 정리한다.

---

## 0. API 쉬운 안내 (팀원용)

Title 메인 메뉴 버튼은 **저장 파일 존재 여부**에 따라 표시가 바뀐다. 버튼 클릭은 `TitleMenuButtonStateController`가 받고, 실제 게임 플로우는 `TitleSceneController` → `FrameworkRoot`로 위임한다.

### 한 줄로

- **버튼 표시/클릭 처리** → `TitleMenuButtonStateController`
- **저장 초기화 확인창** → `TitleResetConfirmPopup`
- **게임 플로우 위임** → `TitleSceneController`
- **저장 삭제 + 런타임 참조 정리** → `FrameworkRoot.ResetSaveData()`

### 저장 유무에 따른 버튼 표시

| 저장 파일 | 표시 버튼 | 숨김 버튼 |
|-----------|-----------|-----------|
| 없음 | 새 게임, 종료 | 이어하기, 저장 초기화 |
| 있음 | 이어하기, 저장 초기화, 종료 | 새 게임 |

### Prefab에 연결할 메서드

| 하고 싶은 일 | 연결 대상 | 메서드 |
|--------------|-----------|--------|
| 새 게임 | `TitleMenuButtonStateController` | `OnClickNewGame` |
| 이어하기 | `TitleMenuButtonStateController` | `OnClickContinue` |
| 저장 초기화 | `TitleMenuButtonStateController` | `OnClickResetSave` |
| 종료 | `TitleMenuButtonStateController` | `OnClickExit` |
| 확인 팝업 — 확인 | `TitleResetConfirmPopup` | (내부 `Confirm`, Show 시 콜백 주입) |
| 확인 팝업 — 취소 | `TitleResetConfirmPopup` | (내부 `Cancel`) |

`TitleMenuButtonStateController`는 Inspector에서 아래 참조를 연결해야 한다.

- `newGameButton`, `continueButton`, `resetSaveButton`, `exitButton`
- `titleSceneController`
- `resetConfirmPopup`

---

## 1. 배경과 해결하려던 문제

Title 씬에서 다음 문제가 있었다.

1. **저장 유무와 버튼 표시 불일치**  
   저장 파일이 없을 때도 이어하기·초기화 버튼이 보이거나, 저장이 있는데 새 게임 버튼이 남아 있을 수 있었다.

2. **저장 초기화의 실수 클릭**  
   Reset 버튼이 확인 없이 바로 저장 파일을 삭제했다.

3. **중복 입력**  
   새 게임·이어하기·종료·초기화 버튼을 연속 클릭하면 scene 전환이나 저장 API가 중복 호출될 수 있었다.

4. **런타임·디스크 저장 상태 불일치**  
   `TitleSceneController.ResetSaveData()`가 `SaveService.ResetSaveData()`만 호출해 디스크 파일은 지워도 `FrameworkRoot.CurrentSaveData` 메모리 참조가 남을 수 있었다. 이후 `ExitGame()`이 삭제된 데이터를 다시 저장할 위험이 있었다.

---

## 2. 전체 흐름

```text
Title Scene 진입
    ↓
TitleMenuButtonStateController.OnEnable
    ├─ actionInProgress = false
    └─ RefreshButtonStates()
           └─ TitleSceneController.HasSaveData
                  └─ FrameworkRoot.SaveService.HasSaveData()
    ↓
[저장 없음]                    [저장 있음]
새 게임 / 종료                  이어하기 / 저장 초기화 / 종료
    ↓                               ↓
OnClickNewGame                  OnClickContinue
    ↓                               ↓
TitleSceneController            TitleSceneController
    ↓                               ↓
FrameworkRoot.StartNewGame()    FrameworkRoot.ContinueGame()
    ↓                               ↓
Loading Scene                   Loading Scene

[저장 초기화 경로]
OnClickResetSave
    ↓
TitleResetConfirmPopup.Show(ConfirmResetSave)
    ↓ (확인)
ConfirmResetSave
    ↓
TitleSceneController.ResetSaveData()
    ↓
FrameworkRoot.ResetSaveData()
    ├─ SaveService.ResetSaveData()  → save_data.json 삭제
    └─ CurrentSaveData = null       → 런타임 참조 비움
    ↓
RefreshButtonStates() → 새 게임 버튼 표시로 전환
```

---

## 3. 신규 스크립트

### 3.1 `TitleMenuButtonStateController`

**경로:** `Assets/_Project/05.UI/02_Title/Scripts/TitleMenuButtonStateController.cs`  
**역할:** Title 메뉴 버튼의 표시 상태 관리와 클릭 입력 가드.

#### 주요 상태

| 필드 | 의미 |
|------|------|
| `actionInProgress` | 새 게임·이어하기·종료처럼 scene 전환을 유발하는 동작이 진행 중인지 |
| `resetConfirmPopup.IsOpen` | 저장 초기화 확인 팝업이 열려 있는지 |

#### `RefreshButtonStates()`

- `TitleSceneController.HasSaveData`를 조회한다.
- 저장 없음: `newGameButton`만 활성, `continueButton`·`resetSaveButton` 비활성.
- 저장 있음: `continueButton`·`resetSaveButton` 활성, `newGameButton` 비활성.
- `exitButton`은 항상 표시.

#### 입력 가드 — `CanHandleMenuInput()`

다음 조건을 **모두** 만족할 때만 메뉴 입력을 허용한다.

```csharp
!actionInProgress
&& titleSceneController != null
&& (resetConfirmPopup == null || !resetConfirmPopup.IsOpen)
```

#### 클릭 핸들러별 동작

| 메서드 | 선행 검증 | 성공 시 |
|--------|-----------|---------|
| `OnClickNewGame` | 저장 없어야 함. 있으면 `RefreshButtonStates()`만 | `actionInProgress = true` → `StartNewGame()` |
| `OnClickContinue` | 저장 있어야 함. 없으면 `RefreshButtonStates()`만 | `actionInProgress = true` → `ContinueGame()` |
| `OnClickResetSave` | 저장 있어야 함 | `resetConfirmPopup.Show(ConfirmResetSave)` |
| `OnClickExit` | — | `actionInProgress = true` → `ExitGame()` |

저장 유무가 UI와 어긋난 경우(예: 백그라운드에서 저장 파일 변경) 클릭 시 `RefreshButtonStates()`로 UI를 재동기화하고 동작을 중단한다.

#### `ConfirmResetSave()`

- `actionInProgress`가 이미 true이면 무시.
- `try/finally`로 `actionInProgress`를 true로 올린 뒤 `ResetSaveData()` 호출.
- 초기화 직후 `RefreshButtonStates()`로 버튼 표시를 갱신.
- `finally`에서 `actionInProgress = false` — 초기화는 scene 전환이 없으므로 잠금을 해제한다.

---

### 3.2 `TitleResetConfirmPopup`

**경로:** `Assets/_Project/05.UI/02_Title/Scripts/TitleResetConfirmPopup.cs`  
**역할:** 저장 초기화 전 확인 UI와 팝업 내부 중복 입력 차단.

#### Inspector 참조

- `popupRoot`: 팝업 전체 GameObject
- `confirmButton`, `cancelButton`
- `titleText`, `messageText` (TMP)

#### 공개 API

| API | 설명 |
|-----|------|
| `IsOpen` | `popupRoot.activeSelf` 기준으로 팝업 열림 여부 |
| `Show(Action confirmationCallback)` | 확인 콜백 등록 후 팝업 표시. 버튼 interactable 복구 |
| `Hide()` | 콜백 제거, `isProcessing` 해제, 팝업 비활성 |

#### 확인 흐름

1. `Show()`에서 `onConfirmed`에 콜백 저장.
2. `Confirm()` 호출 시 `isProcessing = true`, 양쪽 버튼 비활성.
3. 콜백을 로컬 변수로 복사한 뒤 `onConfirmed = null`.
4. `try/finally`에서 콜백 실행 후 무조건 `Hide()`.

이 구조로 **확인 버튼 연타**, **확인 후 팝업 잔존**, **stale 콜백 재실행**을 막는다.

#### 취소 흐름

- `isProcessing`이 false일 때만 `Hide()` — 처리 중에는 취소 불가.

---

## 4. Framework 변경

### 4.1 `FrameworkRoot.ResetSaveData()` (신규)

**경로:** `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs`

```csharp
public void ResetSaveData()
{
    SaveService.ResetSaveData();
    CurrentSaveData = null;
}
```

| 단계 | 동작 |
|------|------|
| 1 | `JsonSaveService.ResetSaveData()` — `save_data.json`이 있으면 삭제 |
| 2 | `CurrentSaveData = null` — 런타임 저장 참조 비움 |

#### 왜 FrameworkRoot에 두었는가

- 저장 초기화는 **디스크 삭제 + 런타임 참조 정리**가 한 세트여야 한다.
- `ExitGame()`은 `CurrentSaveData != null`일 때만 저장한다. 초기화 후 null이면 종료 시 삭제된 데이터가 다시 쓰이지 않는다.
- Title UI뿐 아니라 debug·테스트 경로에서도 동일한 단일 진입점을 쓸 수 있다.

### 4.2 `TitleSceneController.ResetSaveData()` (수정)

**변경 전:**

```csharp
FrameworkRoot.Instance.SaveService.ResetSaveData();
```

**변경 후:**

```csharp
FrameworkRoot.Instance.ResetSaveData();
```

Title scene controller는 더 이상 SaveService를 직접 호출하지 않고 FrameworkRoot의 통합 초기화 경로를 사용한다.

### 4.3 `TitleSceneController.HasSaveData`

변경 없음. 계속 `FrameworkRoot.Instance.SaveService.HasSaveData()`를 노출한다.

- UI 버튼 표시는 **물리적 저장 파일 존재 여부** 기준.
- `CurrentSaveData` null 여부와는 별개 — Title 진입 시 FrameworkRoot는 `HasSaveData()`에 따라 Load 또는 CreateNewGameData로 초기화하므로, UI는 디스크 상태를 source of truth로 본다.

---

## 5. Safeguard 정리

| 위험 | 대응 |
|------|------|
| 저장 없을 때 이어하기/초기화 클릭 | 클릭 시 `HasSaveData` 재검증 후 `RefreshButtonStates()` |
| 저장 있을 때 새 게임 클릭 | 동일 |
| scene 전환 중 다른 버튼 클릭 | `actionInProgress` 잠금 |
| 팝업 열린 상태에서 메뉴 클릭 | `resetConfirmPopup.IsOpen` 검사 |
| 초기화 확인 연타 | 팝업 `isProcessing` + 버튼 비활성 |
| 디스크만 삭제하고 메모리 잔존 | `FrameworkRoot.ResetSaveData()`에서 `CurrentSaveData = null` |
| 초기화 후 Exit 시 재저장 | `ExitGame()`의 null 체크로 저장 생략 |

---

## 6. 에셋 변경

| 파일 | 변경 |
|------|------|
| `TitleMenuButtonStateController.cs` | 신규 |
| `TitleResetConfirmPopup.cs` | 신규 |
| `TitleResetConfirmPopup.prefab` | 신규 — 확인/취소 팝업 |
| `TitleButtonsCanvas.prefab` | 수정 — 버튼 이벤트를 StateController에 연결 |
| `TitleSceneCanvas.prefab` | 수정 — Reset 팝업 및 참조 연결 |
| `TitleBackGroundTest.unity` | 신규 — 테스트용 Title 배경 씬 |
| `FrameworkRoot.cs` | `ResetSaveData()` 추가 |
| `TitleSceneController.cs` | Reset 위임 경로 변경 |

---

## 7. 팀원 연동 시 주의

1. **버튼 OnClick은 `TitleSceneController`가 아닌 `TitleMenuButtonStateController`에 연결**한다. 가드와 상태 전환이 StateController에 있다.

2. **Reset 버튼은 반드시 `TitleResetConfirmPopup`과 함께 사용**한다. SaveService를 UI에서 직접 호출하지 않는다.

3. **저장 초기화 후 UI 갱신**은 `ConfirmResetSave()` 내부에서 `RefreshButtonStates()`를 호출한다. Title 씬을 다시 로드하지 않아도 버튼 표시가 바뀐다.

4. **FrameworkRoot.ResetSaveData()**는 online progress tick, in-game entry prepared 플래그 등을 리셋하지 않는다. Title에서 초기화만 하고 씬을 유지하는 현재 UX 기준으로는 의도된 동작이다. 초기화 직후 새 게임을 누르면 `StartNewGame()`이 `isOnlineProgressTickEnabled = false`, `isInGameEntryPrepared = false`를 설정한다.

5. **`TitleBackGroundTest.unity`**는 배경/버튼 배치 검증용 테스트 씬이다. production Title 씬 연동 시 Prefab 참조만 동일 패턴으로 맞추면 된다.

---

## 8. 검증 체크리스트

- [ ] 저장 파일 없음 → 새 게임·종료만 표시
- [ ] 저장 파일 있음 → 이어하기·저장 초기화·종료 표시, 새 게임 숨김
- [ ] 저장 초기화 → 확인 팝업 → 확인 시 파일 삭제 및 버튼 전환
- [ ] 저장 초기화 → 취소 시 파일 유지, 팝업 닫힘
- [ ] 초기화 확인 연타 시 한 번만 실행
- [ ] 새 게임/이어하기 클릭 후 다른 버튼 입력 무시
- [ ] 초기화 후 Exit → 저장 파일 재생성 없이 종료
- [ ] Unity Console 컴파일 오류 없음

---

## 9. Related

- 선행 문서: `0713_title_settings_sound_display_managers.md` (Title Settings·ExitGame)
- 선행 문서: `0707_Framework_M0_Logic.md` (TitleSceneController·FrameworkRoot 기본 흐름)
- 관련 저장 API: `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs` — `HasSaveData()`, `ResetSaveData()`
