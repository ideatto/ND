# Settings PlayerPrefs Persistence · InGameSettingPannel 로직 정리

**작성일:** 2026-08-11  
**브랜치:** `feature/framework/setting-playerprefs-save-ingame-setting-pennel-prefabs`  
**Base:** `dev2`  
**Feature root:** `Assets/_Project/05.UI/02_Title/`  
**목적:** Title Settings의 PlayerPrefs 영속화, InGame용 Settings Prefab 분리, Title ↔ InGame 설정 공유 계약을 개인 작업 로그로 정리한다.

**연결 가이드:** [`Docs/Guide/InGame_Settings_Panel_Wiring_Guide.md`](../../Guide/InGame_Settings_Panel_Wiring_Guide.md)

---

## 0. API 쉬운 안내 (팀원용)

설정 UI는 **매니저를 직접 건드리지 않고** `SettingsUIManager`만 연결한다. 실제 값과 저장은 `SoundManager` / `DisplayManager`가 담당한다.

### 한 줄로

```text
UI UnityEvent → SettingsUIManager → SoundManager / DisplayManager → PlayerPrefs
OpenOption() → SyncUiFromManagers() → 패널 표시
CloseOption() → PlayerPrefs.Save()
ResetAllSettings() → 매니저 ResetToDefaults() → UI 재동기화
```

| 하고 싶은 일 | 연결 대상 | 메서드 |
|---|---|---|
| 설정 패널 열기 | `SettingsUIManager` | `OpenOption()` |
| 설정 패널 닫기 | `SettingsUIManager` | `CloseOption()` |
| BGM 볼륨 | `SettingsUIManager` | `SetBgmVolume(float)` |
| SFX 볼륨 | `SettingsUIManager` | `SetSfxVolume(float)` |
| UI SFX 볼륨 | `SettingsUIManager` | `SetUiSfxVolume(float)` |
| BGM on/off | `SettingsUIManager` | `SetBgmEnabled(bool)` |
| SFX on/off | `SettingsUIManager` | `SetSfxEnabled(bool)` |
| UI SFX on/off | `SettingsUIManager` | `SetUiSfxEnabled(bool)` |
| 창 모드 | `SettingsUIManager` | `SetWindowMode(int)` |
| 해상도 | `SettingsUIManager` | `SetResolution(int)` |
| 설정 초기화 | `SettingsUIManager` | `ResetAllSettings()` |

**매니저 진입점**

| 매니저 | Namespace | 생성 시점 | 수명 |
|---|---|---|---|
| `SoundManager.Instance` | `ND.UI.Title` | `BeforeSceneLoad` | DontDestroyOnLoad |
| `DisplayManager.Instance` | `ND.UI.Title` | `BeforeSceneLoad` | DontDestroyOnLoad |

Title과 InGame은 **같은 DDOL 매니저 인스턴스**를 공유한다. 씬을 바꿔도 Audio/Display 설정은 유지된다.

---

## 1. 배경과 해결하려던 문제

기존 Title Settings는 UI 조작 시 런타임 상태만 바뀌고, 재실행 후 설정이 초기화될 수 있었다. 또 InGame에서는 `PanelOpener` + `MenuPopup` 그레이박스만 있어 실제 Settings UI가 없었다.

이번 브랜치에서 해결한 범위:

1. **Audio / Display 사용자 설정 PlayerPrefs 영속화**
2. **Settings Reset과 Game Save Reset 분리**
3. **Title Settings UI를 기반으로 한 InGame 전용 Prefab (`InGameSettingPannel`) 생성**
4. **Title ↔ InGame 동일 Manager 상태 공유** (코드·Prefab wiring 완료, MainUICanvas 연결은 후속)

아직 하지 않은 범위:

- `MainUICanvas` / `InGame.unity`에 Prefab 실제 배치 및 `SettingsBtn` 재연결 (Scene/UI Owner 후속)

---

## 2. 변경 파일 요약

| 구분 | 경로 | 역할 |
|---|---|---|
| 신규 | `Scripts/SettingsPlayerPrefsKeys.cs` | Settings 전용 PlayerPrefs 키 상수 |
| 수정 | `Scripts/SoundManager.cs` | Audio 설정 load/save/reset |
| 수정 | `Scripts/DisplayManager.cs` | Display 설정 load/save/reset, invalid enum fallback |
| 수정 | `Scripts/SettingsUIManager.cs` | UI ↔ Manager 위임, `CloseOption()`에서 flush |
| 신규 | `Prefabs/InGameSettingPannel.prefab` | InGame용 Settings 패널 (Canvas 없음) |
| 신규 | `Editor/InGameSettingsPrefabBuilder.cs` | TitleSettingsCanvas 복제·정리 Editor 도구 |

**보호 파일 (이번 브랜치에서 미수정)**

- `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`
- `Assets/_Project/07.Scenes/04_InGame/InGame.unity`

---

## 3. PlayerPrefs 키와 기본값

키 정의: `SettingsPlayerPrefsKeys` (`internal`, `ND.UI.Title`)

| 키 상수 | PlayerPrefs key | 타입 | 담당 |
|---|---|---|---|
| `BgmVolume` | `ND.Settings.Audio.BgmVolume` | float | SoundManager |
| `BgmEnabled` | `ND.Settings.Audio.BgmEnabled` | int (0/1) | SoundManager |
| `SfxVolume` | `ND.Settings.Audio.SfxVolume` | float | SoundManager |
| `SfxEnabled` | `ND.Settings.Audio.SfxEnabled` | int (0/1) | SoundManager |
| `UiSfxVolume` | `ND.Settings.Audio.UiSfxVolume` | float | SoundManager |
| `UiSfxEnabled` | `ND.Settings.Audio.UiSfxEnabled` | int (0/1) | SoundManager |
| `WindowMode` | `ND.Settings.Display.WindowMode` | int (enum) | DisplayManager |
| `Resolution` | `ND.Settings.Display.Resolution` | int (enum) | DisplayManager |

**Config 기본값 SO**

| SO | Resources 경로 | 용도 |
|---|---|---|
| `SoundSettingsConfig` | `Resources/SoundSettingsConfig` | Audio 기본 볼륨·토글 |
| `DisplaySettingsConfig` | `Resources/DisplaySettingsConfig` | 창 모드·해상도 기본값 |

---

## 4. 저장·복구·Reset 계약

### 4.1 초기화 (앱/Play Mode 시작)

```text
BeforeSceneLoad
  ├─ SoundManager.EnsureExists()
  └─ DisplayManager.EnsureExists()
        ↓
Awake
  ├─ LoadResources / LoadSettingsConfig
  └─ LoadInitialSettings()
        ├─ ApplyDefaults()          ← SO 기본값
        └─ PlayerPrefs overlay      ← 저장값이 있으면 덮어씀
              └─ ApplyCurrent / Apply*AudioState
```

**Display invalid 값 처리**

- `LoadInitialSettings()`에서 `Enum.IsDefined`로 검증
- 잘못된 int는 무시하고 Config default 유지

### 4.2 사용자 변경 (Setter)

```text
Slider/Toggle/Dropdown UnityEvent
  → SettingsUIManager.Set*
    → SoundManager.Set* / DisplayManager.Set*
      → runtime state 변경 + 즉시 적용
      → PlayerPrefs.SetFloat / SetInt   (Save()는 호출하지 않음)
```

### 4.3 명시적 flush 경계

| 시점 | 호출 | 동작 |
|---|---|---|
| `SettingsUIManager.CloseOption()` | `PlayerPrefs.Save()` | 마지막 변경까지 디스크 반영 |
| `SoundManager.ResetToDefaults()` | `DeleteSavedSettings()` + `PlayerPrefs.Save()` | Audio 키 삭제 후 flush |
| `DisplayManager.ResetToDefaults()` | `DeleteKey` + `PlayerPrefs.Save()` | Display 키 삭제 후 flush |

**주의:** Setter만 호출하고 `CloseOption()` 없이 Play Mode를 종료하면, Editor 환경에서는 값이 유지되는 경우가 있으나, **`PlayerPrefs.Save()` 경계는 `CloseOption()` / Reset**에 있다. 독립 Build·강제 종료 경로는 별도 QA 대상.

### 4.4 Settings Reset

```text
ResetAllSettings()
  ├─ SoundManager.ResetToDefaults()
  │     ├─ ApplyDefaultSettings()
  │     ├─ DeleteSavedSettings()   ← Audio PlayerPrefs 키 삭제
  │     └─ PlayerPrefs.Save()
  ├─ DisplayManager.ResetToDefaults()
  │     ├─ Config default 적용
  │     ├─ Display PlayerPrefs 키 삭제
  │     └─ PlayerPrefs.Save()
  └─ SyncUiFromManagers()
```

Reset 후 재실행해도 **이전 사용자 설정이 다시 나타나지 않아야** 한다 (키 삭제 + SO default).

### 4.5 Game Save Reset과의 독립성

```text
TitleSceneController.ResetSaveData()
  → FrameworkRoot.ResetSaveData()
    → JsonSaveService.ResetSaveData()   ← 게임 저장 파일만 삭제
```

- Settings PlayerPrefs 키는 **건드리지 않음**
- `ResetSaveData` ≠ `ResetAllSettings`

---

## 5. SettingsUIManager 동작

### 5.1 Open / Close

```text
OpenOption()
  1. SyncUiFromManagers()     ← WithoutNotify로 Slider/Toggle/Dropdown 반영
  2. optionPanel.SetActive(true)

CloseOption()
  1. optionPanel.SetActive(false)
  2. PlayerPrefs.Save()
```

**Open 전 동기화가 필요한 이유**

- 패널 활성화 시 Slider/Toggle의 `onValueChanged`가 Prefab 기본값(예: 0)으로 매니저를 덮을 수 있음
- `SetValueWithoutNotify` / `SetIsOnWithoutNotify`로 루프 방지

### 5.2 Title vs InGame Prefab

| Prefab | Root Canvas | optionPanel | Display UI |
|---|---|---|---|
| `TitleSettingsCanvas.prefab` | 있음 | `SettingsBackGround` | 활성 (Title용) |
| `InGameSettingPannel.prefab` | **없음** | `SettingsBackGround` (inactive) | **inactive** (`DisplayMode_UI_Item`, `AspectRatio_UI_Item`) |

InGame Prefab은 **부모 Canvas 아래에 붙는 패널 조각**이다. `GraphicRaycaster`는 부모 Canvas가 제공한다.

---

## 6. InGameSettingPannel Prefab 구조

**경로:** `Assets/_Project/05.UI/02_Title/Prefabs/InGameSettingPannel.prefab`

**생성:** `Tools/ND/Settings/Create InGame Settings Prefab` (`InGameSettingsPrefabBuilder`)

생성 절차 요약:

1. `TitleSettingsCanvas.prefab` 복사
2. Root에서 `Canvas` / `CanvasScaler` / `GraphicRaycaster` 제거
3. Nested Prefab unpack
4. `optionPanel` inactive 유지
5. Slider/Toggle/Dropdown UnityEvent를 `SettingsUIManager` Dynamic listener로 재배선

**검증된 UnityEvent (Dynamic, mode=0)**

| 컨트롤 | 메서드 |
|---|---|
| BGM Slider | `SetBgmVolume` |
| SFX Slider | `SetSfxVolume` |
| UI SFX Slider | `SetUiSfxVolume` |
| BGM Toggle | `SetBgmEnabled` |
| SFX Toggle | `SetSfxEnabled` |
| UI SFX Toggle | `SetUiSfxEnabled` |
| Window Mode Dropdown | `SetWindowMode` |
| Resolution Dropdown | `SetResolution` |
| Reset Button | `ResetAllSettings` (Static) |
| Close Button | `CloseOption` (Static) |

---

## 7. Title ↔ InGame 공유 흐름

```text
[Title] SettingsUIManager.OpenOption()
          ↓ SyncUiFromManagers()
          ↓ 사용자 변경 → SoundManager (DDOL)
[InGame] 동일 SoundManager.Instance
          ↓ InGameSettingPannel.OpenOption()
          ↓ SyncUiFromManagers() → Title에서 저장한 값 표시
```

Display도 `DisplayManager` DDOL로 동일하게 공유된다. InGame Prefab에서 Display UI는 inactive이지만 **backend 값은 Title과 같다**.

---

## 8. 런타임 검증 요약 (2026-08-11)

| 항목 | 결과 |
|---|---|
| Title Open / Slider·Toggle 즉시 반영 | PASS |
| Close → Reopen UI sync | PASS |
| Play Mode stop/start Persistence | PASS |
| Close 없이 stop (BGM 변경) | PASS (Editor Play Mode) |
| Reset 즉시 + restart | PASS |
| ResetSaveData 후 Settings 유지 | PASS |
| InGameSettingPannel Missing ref | 0 |
| Title → InGame / InGame → Title 공유 | PASS (임시 Instantiate + DDOL) |
| MainUICanvas / InGame.unity diff | 없음 |
| 독립 Build 프로세스 재실행 | 미검증 |

**판정:** CONDITIONAL PASS — 핵심 Persistence·Prefab 동작 확인, MainUICanvas 실연결·Build 재실행은 후속.

---

## 9. 후속 Integration Contract

Scene/UI Owner 작업:

```text
MainUICanvas
└─ InGameSettingPannel (Prefab Instance)

기존:
  SettingsBtn → PanelOpener.Open(MenuPopup)

변경:
  SettingsBtn → InGameSettingPannel.SettingsUIManager.OpenOption()
```

상세 절차: [`InGame_Settings_Panel_Wiring_Guide.md`](../../Guide/InGame_Settings_Panel_Wiring_Guide.md)

---

## 10. 관련 문서

- [`Docs/Guide/SoundManager_And_SoundCatalog_Usage_Guide.md`](../../Guide/SoundManager_And_SoundCatalog_Usage_Guide.md)
- [`Docs/Personal_Documents/CSU/0809_central_sound_catalog_logic.md`](0809_central_sound_catalog_logic.md)
- [`Docs/Personal_Documents/CSU/0803_title_save_reset_safeguard.md`](0803_title_save_reset_safeguard.md)
