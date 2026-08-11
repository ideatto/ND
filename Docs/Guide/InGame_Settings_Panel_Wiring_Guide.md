# InGame Settings Panel 연결 가이드

이 문서는 `InGameSettingPannel.prefab`을 InGame UI(`MainUICanvas`)에 연결하고, 기존 그레이박스 `MenuPopup` 대신 실제 Settings UI를 여는 방법을 설명한다.

**로직 상세:** [`Docs/Personal_Documents/CSU/0811_settings_playerprefs_ingame_setting_panel_logic.md`](../Personal_Documents/CSU/0811_settings_playerprefs_ingame_setting_panel_logic.md)

---

## 목적

- InGame 하단 메뉴 **설정** 버튼이 placeholder `MenuPopup`이 아닌 **실제 Settings 패널**을 연다.
- Title에서 바꾼 BGM/SFX/UI SFX 설정이 InGame에서도 동일하게 보인다 (`SoundManager` / `DisplayManager` DDOL 공유).
- Settings 변경값은 PlayerPrefs에 저장되며, `CloseOption()` 시 flush된다.

---

## 사전 확인

| 항목 | 경로 / 내용 |
|---|---|
| InGame Settings Prefab | `Assets/_Project/05.UI/02_Title/Prefabs/InGameSettingPannel.prefab` |
| Main UI Prefab | `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab` |
| InGame Scene | `Assets/_Project/07.Scenes/04_InGame/InGame.unity` (MainUICanvas Prefab Instance 포함) |
| Settings API | `SettingsUIManager.OpenOption()` / `CloseOption()` |
| Audio/Display backend | `SoundManager.Instance`, `DisplayManager.Instance` (`BeforeSceneLoad` 자동 생성) |

**Prefab 특성**

- Root에 `Canvas` / `CanvasScaler` / `GraphicRaycaster` **없음**
- Root: `InGameSettingPannel` + `SettingsUIManager`
- `SettingsBackGround` / `optionPanel`: **inactive** (OpenOption 시 활성화)
- Display Dropdown UI (`DisplayMode_UI_Item`, `AspectRatio_UI_Item`): **inactive** (의도적 — Audio만 InGame에서 노출)

---

## 주의사항

- **Scene Owner와 작업 시간을 맞출 것.** `MainUICanvas.prefab`은 여러 기능이 공유한다.
- Prefab YAML을 직접 편집하지 말고 **Unity Editor Inspector**에서 연결한다.
- `InGame.unity`를 직접 수정하기보다 **MainUICanvas Prefab**을 수정하면 InGame Scene Instance에 반영된다.
- `PanelOpener` + `MenuPopup`은 다른 메뉴(거점·무역·인벤토리)에서 계속 사용할 수 있다. **SettingsBtn만** 교체 대상이다.
- Display UI를 InGame에서 활성화하려면 별도 UI/기획 합의 후 Prefab에서 `DisplayMode_UI_Item` / `AspectRatio_UI_Item`을 켠다.

---

## 1. 현재 연결 상태 (교체 전)

`MainUICanvas.prefab` → `SettingsBtn`:

```text
SettingsBtn (Button.onClick)
  → PanelOpener.Open()
       panel: MenuPopup
       title: "설정"
```

`MenuPopup`은 제목만 바뀌는 **그레이박스 공용 팝업**이다. 실제 볼륨/토글 UI는 없다.

---

## 2. 목표 연결 상태 (교체 후)

```text
MainUICanvas
├─ … (기존 UI)
├─ MenuPopup              ← 다른 PanelOpener 버튼용 (유지 가능)
└─ InGameSettingPannel    ← 신규 Prefab Instance
     └─ SettingsUIManager

SettingsBtn (Button.onClick)
  → SettingsUIManager.OpenOption()   (InGameSettingPannel 인스턴스)
```

패널 닫기는 Prefab 내부 `Settings_Close_Button` → `CloseOption()` (이미 wiring됨).

---

## 3. MainUICanvas에 Prefab 배치

### 3.1 Prefab Mode 열기

1. Project 창에서 `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`을 연다.
2. Hierarchy 최상위 `MainUICanvas` 아래 **적절한 Overlay 계층**에 Prefab Instance를 추가한다.  
   권장: 다른 full-screen popup과 같은 부모 (예: Canvas root 직계 또는 Popup 레이어).

### 3.2 InGameSettingPannel 추가

1. `Assets/_Project/05.UI/02_Title/Prefabs/InGameSettingPannel.prefab`을 Hierarchy로 드래그한다.
2. Transform 설정:
   - Anchor: stretch (0,0) ~ (1,1)
   - Offset: 0 — 화면 전체를 덮는 Settings overlay
3. **초기 active:** Root `InGameSettingPannel`은 **active**, 내부 `SettingsBackGround`는 Prefab 기본(inactive) 유지.
4. Sorting: 다른 popup보다 위에 보이도록 Sibling Order를 조정한다 (`OpenOption` 시 최상위 필요하면 추후 스크립트 보완).

### 3.3 참조 확인

`InGameSettingPannel` Root의 `SettingsUIManager` Inspector:

| 필드 | 기대 |
|---|---|
| `optionPanel` | `SettingsBackGround` |
| `bgmSlider` / `sfxSlider` / `uiSfxSlider` | 각 UI_Item_Slider |
| `bgmToggle` / `sfxToggle` / `uiSfxToggle` | 각 UI_Item_Toggle |
| `windowModeDropdown` / `resolutionDropdown` | 할당됨 (Display UI inactive여도 참조 유지) |

Missing Reference가 없어야 한다.

---

## 4. SettingsBtn 재연결

### 4.1 기존 PanelOpener 호출 제거

1. Hierarchy에서 `SettingsBtn` 선택.
2. `Button` → **On Click ()** 목록에서 `PanelOpener.Open` 항목을 **Remove**한다.
3. `SettingsBtn`의 `PanelOpener` 컴포넌트는 **제거해도 되고**, 다른 용도가 없으면 삭제한다.

### 4.2 OpenOption 연결

1. `On Click ()` → **+** 추가.
2. Object: 방금 추가한 **`InGameSettingPannel` 인스턴스** (Root).
3. Function: `SettingsUIManager` → **`OpenOption()`** (Dynamic, 인자 없음).

```text
SettingsBtn.onClick
  → InGameSettingPannel.SettingsUIManager.OpenOption
```

**Close는 Prefab 내부에서 처리**

- `Settings_Close_Button` → `CloseOption()` (Static, 이미 연결됨)
- `CloseOption()`은 `PlayerPrefs.Save()`를 호출한다.

---

## 5. 동작 확인 (수동 QA)

Play Mode — **Boot → Title → InGame** (또는 InGame 직접 진입):

### 5.1 Title → InGame 공유

1. Title Settings에서 BGM/SFX/UI SFX를 기본값과 다른 값으로 변경.
2. `CloseOption()` (닫기) 클릭.
3. InGame 진입 후 `SettingsBtn` 클릭.
4. **기대:** Title에서 설정한 값이 Slider/Toggle에 표시.

### 5.2 InGame에서 변경

1. InGame Settings에서 다른 값으로 변경.
2. Close 클릭.
3. Title로 복귀 후 Title Settings Open.
4. **기대:** InGame에서 바꾼 값 유지.

### 5.3 Persistence

1. Settings 값 변경 → Close.
2. Play Mode 종료 → 재실행.
3. Title 또는 InGame Settings Open.
4. **기대:** 이전 값 복구.

### 5.4 Reset / Save Reset 독립

| 동작 | 기대 |
|---|---|
| Settings Reset | Config default, PlayerPrefs Audio/Display 키 삭제 |
| Title Save Reset | 게임 저장만 삭제, Settings 유지 |

### 5.5 Console

- `NullReferenceException`, `SettingsUIManager` / `SoundManager` 관련 Error 없음.

---

## 6. InGame Scene만 따로 테스트하는 경우

`InGame.unity`는 `MainUICanvas` Prefab Instance를 포함한다. Prefab 수정 후:

1. InGame Scene을 연다.
2. Prefab Instance가 최신인지 확인 (Overrides 없음).
3. Play Mode에서 `SettingsBtn` 동작 확인.

**Override가 생기면** Scene Instance가 아닌 **MainUICanvas Prefab** 쪽을 수정해 일원화한다.

---

## 7. Prefab 재생성이 필요할 때

Title Settings UI 구조가 바뀌면 Editor 메뉴로 InGame Prefab을 다시 만든다.

```text
Tools → ND → Settings → Create InGame Settings Prefab
```

- 소스: `TitleSettingsCanvas.prefab`
- 출력: `InGameSettingPannel.prefab` (덮어쓰기 주의 — Scene Owner와 협의)
- MainUICanvas에 이미 Instance가 있으면 **Replace** 또는 참조 재확인

---

## 8. 하지 말아야 할 것

| 금지 | 이유 |
|---|---|
| `InGameSettingPannel` Root에 Canvas 추가 | Title 전용 구조와 충돌, MainUICanvas 이중 Canvas |
| SettingsBtn이 여전히 `MenuPopup`을 연다 | 그레이박스만 표시됨 |
| Display UI를 합의 없이 active | InGame Audio-only 정책과 불일치 |
| `ResetSaveData`로 Settings까지 지울 것 기대 | 별도 API — Settings는 유지 |
| 테스트용 임시 Canvas를 Scene/Prefab에 저장 | 불필요한 dirty·merge conflict |

---

## 9. 트러블슈팅

| symptom | 확인 |
|---|---|
| Open 시 Slider가 0으로 리셋됨 | `OpenOption()` 전 `SyncUiFromManagers()` 호출 여부, UnityEvent가 Static int가 아닌지 |
| 클릭 무반응 | 부모 Canvas에 `GraphicRaycaster` 있는지, `InGameSettingPannel` active인지 |
| Title/InGame 값 불일치 | `SoundManager.Instance` null 여부, DDOL duplicate destroy |
| 재실행 후 초기화 | `CloseOption()` 없이 종료했는지, PlayerPrefs 키 존재 여부 |
| Display Dropdown 오동작 | Dropdown option 순서 = enum 순서 (`WindowDisplayMode`, `ResolutionPreset`) |

---

## 10. 관련 파일

| 파일 | 역할 |
|---|---|
| `SettingsUIManager.cs` | UI ↔ Manager 위임 |
| `SettingsPlayerPrefsKeys.cs` | PlayerPrefs 키 |
| `SoundManager.cs` / `DisplayManager.cs` | runtime + persistence |
| `PanelOpener.cs` | 기존 그레이박스 (Settings 외 메뉴) |
| `InGameSettingsPrefabBuilder.cs` | Prefab 생성 Editor 도구 |
