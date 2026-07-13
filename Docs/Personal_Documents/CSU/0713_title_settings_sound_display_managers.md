# Title Settings Sound/Display Manager · ExitGame · UI Sync

**작성일:** 2026-07-13  
**브랜치:** `ui/framework/title-scene-construct`  
**Base:** `dev2`  
**Feature root:** `Assets/_Project/05.UI/02_Title/`  
**관련:** Title 씬 Settings UX, Exit 저장·종료, Sound/Display 설정 뼈대  
**목적:** Title Settings용 Sound/Display 매니저 뼈대와 SO 기본값, ExitGame 저장 후 종료, Settings UI 동기화 이슈와 해결을 개인 작업 로그로 남긴다.

---

## 1. 배경

1차 빌드 축소 범위에서 Framework는 `Title.unity` 오너이다. Title Settings 패널에 사운드·디스플레이 옵션을 붙이려면 공용 매니저 뼈대가 필요하고, Exit 시에는 현재 저장 데이터를 기록한 뒤 종료해야 한다.

관련 팀 공유 문서: [`0713_first_build_progress_scope_and_roles.md`](0713_first_build_progress_scope_and_roles.md)

---

## 2. 구현 요약

### 2-1. ExitGame

| 항목 | 내용 |
|------|------|
| `FrameworkRoot.ExitGame()` | `CurrentSaveData`를 저장한 뒤 종료. Editor에서는 `EditorApplication.isPlaying = false` |
| `TitleSceneController.ExitGame()` | `FrameworkRoot.Instance.ExitGame()`에 위임 |
| 의도 | 종료 직전 런타임 변경 유실 방지 + 디버그 Play Mode에서도 Exit 동작 |

초기 실수: `Load()` 후 `Save()`는 디스크 값으로 메모리를 덮어써 미저장 변경을 버릴 수 있음 → `ReturnToTitle`과 같이 **현재 `CurrentSaveData`만 Save** 하도록 수정.

### 2-2. Sound / Display 매니저 (FrameworkRoot 밖)

| 결정 | 이유 |
|------|------|
| FrameworkRoot에 넣지 않음 | Save/Scene/Trade와 책임 분리, Framework 리뷰·동결 범위 확대 방지 |
| `BeforeSceneLoad` + `DontDestroyOnLoad` | Boot 씬 YAML 수정 없이 Title 전부터 사용 가능 |
| 기본값은 ScriptableObject | Inspector 컴포넌트 캡처 대신 `InGameTimePolicyConfig`와 동일 Resources.Load 패턴 |

경로:

```text
Assets/_Project/05.UI/02_Title/
├── Scripts/
│   ├── SoundManager.cs
│   ├── DisplayManager.cs
│   ├── SoundSettingsConfig.cs
│   ├── DisplaySettingsConfig.cs
│   └── SettingsUIManager.cs
└── Resources/
    ├── SoundSettingsConfig.asset
    └── DisplaySettingsConfig.asset
```

SO는 `11.CoreServices/Resources`가 아니라 **Title UI Resources**에 둔다. Framework 설정과 소유를 섞지 않기 위함이다. 로드 방식(`Resources.Load` + ResourceName + 폴백)은 CoreServices와 동일하다.

### 2-3. SettingsUIManager

- 패널 open/close + Sound/Display 위임 API
- Prefab UnityEvent는 `SettingsUIManager`만 바라봄
- `OpenOption` / `ResetAllSettings`에서 매니저 → UI 동기화 (`WithoutNotify`)

---

## 3. 발생한 문제와 해결

### 문제 A — Settings 열기 버튼 첫 클릭에 패널이 안 열림

**증상:** `TitleSettingsOpen_Button` 첫 입력 시 패널이 바로 열리지 않음.

**원인:**

1. `SettingsUIManager.optionPanel`이 **자기 자신(TitleSettingsCanvas 루트)** 을 가리킴
2. TitleSceneCanvas에서 TitleSettingsCanvas는 초기 `m_IsActive: 0`
3. `OpenOption`으로 활성화되면 그 프레임에 `Start()` → `CloseOption()`이 다시 비활성화

**해결:** 패널 참조를 루트가 아닌 실제 Settings 패널 자식으로 두거나, 자기 비활성 + Start Close 조합을 제거. (씬/프리팹에서 열기 동작이 정상화된 뒤 UI sync 작업으로 이어짐)

### 문제 B — SO 기본값이 Settings UI에 안 보임

**증상:** Resources SO를 수정해도 Settings 패널 Slider 등이 Prefab 값처럼 보임.

**빠른 확인 결과:**

- Console에 `SoundSettingsConfig was not found` / `DisplaySettingsConfig was not found` **없음** → SO 로드 성공
- Hierarchy `SoundManager` AudioSource volume = `0.5` → **매니저에는 SO가 이미 적용됨**

**원인:** 매니저 ↔ UI 단방향만 존재(UI → 매니저). 패널을 열 때 매니저 값을 Slider/Toggle/Dropdown에 밀어 넣는 코드가 없음. Prefab Slider `m_Value`(예: 0)가 화면에 그대로 보임. 활성화 시 `onValueChanged`가 Prefab 값으로 매니저를 덮을 위험도 있음.

**해결:** `SettingsUIManager.SyncUiFromManagers()`

1. `OpenOption`에서 **패널 `SetActive(true)` 전에** 동기화
2. `ResetAllSettings` 후 동기화
3. `SetValueWithoutNotify` / `SetIsOnWithoutNotify` 사용
4. Inspector에 Slider / Toggle / TMP_Dropdown 참조 연결 필요

---

## 4. Prefab / API 연결 요약

| UI | Event | Method |
|----|-------|--------|
| BGM/SFX Slider | float | `SetBgmVolume` / `SetSfxVolume` |
| BGM/SFX Toggle | bool | `SetBgmEnabled` / `SetSfxEnabled` |
| Window Dropdown | int | `SetWindowMode` |
| Resolution Dropdown | int | `SetResolution` |
| Reset | click | `ResetAllSettings` |
| Exit | click | `TitleSceneController.ExitGame` |
| Settings Open | click | `OpenOption` |

Dropdown 옵션 순서 = enum 순서 (`WindowDisplayMode`, `ResolutionPreset`).

---

## 5. 검증

| 항목 | 결과 |
|------|------|
| Exit — 저장 후 Editor Play 종료 | 코드 반영 (`FrameworkRoot.ExitGame`) |
| SO Resources.Load | Console 경고 없음으로 확인 |
| SoundManager volume = SO(0.5) | Hierarchy로 확인 |
| Settings 첫 클릭 오픈 | 사용자 확인 — 정상 |
| UI Sync 후 패널에 SO 값 표시 | Inspector 참조 연결 후 확인 필요 |
| PlayerPrefs 영속화 | 이번 범위 밖 (뼈대만) |

---

## 6. 남은 주의사항

- SettingsUIManager의 Sound/Display 컨트롤 SerializeField는 **Unity Inspector에서 Prefab에 연결**해야 동기화가 동작한다.
- Sound/Display를 공용화할 때는 `05.UI/01_Common` 등으로 이동하면 되고, CoreServices Resources로 옮길 필요는 없다.
- Display `Screen.SetResolution`은 Editor Game View 크기에 영향을 줄 수 있다.

---

## 변경 이력

| 날짜 | 내용 |
|------|------|
| 2026-07-13 | Title Settings 매니저·SO·ExitGame·UI Sync 작업 로그 작성 |
