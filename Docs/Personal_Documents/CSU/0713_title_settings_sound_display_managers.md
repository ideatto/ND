# Title Settings Sound/Display Manager · ExitGame · UI Sync

**작성일:** 2026-07-13  
**브랜치:** `ui/framework/title-scene-construct`  
**Base:** `dev2`  
**Feature root:** `Assets/_Project/05.UI/02_Title/`  
**관련:** Title 씬 Settings UX, Exit 저장·종료, Sound/Display 설정 뼈대  
**목적:** Title Settings용 Sound/Display 매니저 뼈대와 SO 기본값, ExitGame 저장 후 종료, Settings UI 동기화 이슈와 해결을 개인 작업 로그로 남긴다.

---

## 0. API 쉬운 안내 (팀원용)

Title Settings에서 쓰는 API를 **버튼·슬라이더에 무엇을 연결하면 되는지** 기준으로 정리한다.

### 한 줄로

- **화면 버튼** → 대부분 `SettingsUIManager` 또는 `TitleSceneController`
- **실제 소리/해상도 처리** → `SoundManager` / `DisplayManager` (자동 생성, 직접 씬에 안 넣어도 됨)
- **기본값(Reset 기준)** → Project의 `SoundSettingsConfig` / `DisplaySettingsConfig` 에셋

### 누가 무엇을 하나

| 이름 | 쉬운 역할 |
|------|-----------|
| `SettingsUIManager` | Settings 패널 열고 닫기 + UI 조작을 매니저에 전달 + 열 때/Reset 때 화면 숫자를 매니저 값에 맞춤 |
| `SoundManager` | BGM/SFX 볼륨, 켜기/끄기, 재생 |
| `DisplayManager` | 창모드·해상도 바꾸기 |
| `SoundSettingsConfig` / `DisplaySettingsConfig` | “공장 초기값”. Reset 누르면 여기 값으로 돌아감 |
| `TitleSceneController` / `FrameworkRoot` | 새 게임·이어하기·종료(저장 후 끄기) |

### Settings 패널 — Prefab에 연결할 메서드

| 하고 싶은 일 | 연결할 대상 | 메서드 | 전달 값 |
|--------------|-------------|--------|---------|
| Settings 열기 | `SettingsUIManager` | `OpenOption` | 없음 |
| Settings 닫기 | `SettingsUIManager` | `CloseOption` | 없음 |
| Settings 열기/닫기 토글 | `SettingsUIManager` | `ToggleOption` | 없음 |
| BGM 볼륨 | `SettingsUIManager` | `SetBgmVolume` | Slider float (0~1) |
| SFX 볼륨 | `SettingsUIManager` | `SetSfxVolume` | Slider float (0~1) |
| BGM 켜기/끄기 | `SettingsUIManager` | `SetBgmEnabled` | Toggle bool |
| SFX 켜기/끄기 | `SettingsUIManager` | `SetSfxEnabled` | Toggle bool |
| 창 모드 | `SettingsUIManager` | `SetWindowMode` | Dropdown int: 0 창모드 / 1 전체 창모드 / 2 전체모드 |
| 해상도 | `SettingsUIManager` | `SetResolution` | Dropdown int: 0=1280x720 / 1=1920x1080 / 2=2560x1440 |
| 기본값으로 되돌리기 | `SettingsUIManager` | `ResetAllSettings` | 없음 |
| 게임 종료 | `TitleSceneController` | `ExitGame` | 없음 (저장 후 종료, Editor면 Play 중지) |

### 코드에서 직접 쓸 때 (Sound / Display)

UI를 거치지 않고 게임플레이 코드에서 부를 때:

| 하고 싶은 일 | 호출 |
|--------------|------|
| BGM 바꾸기 | `SoundManager.Instance.PlayBgm(clip)` |
| BGM 정지 | `SoundManager.Instance.StopBgm()` |
| SFX 한 번 재생 | `SoundManager.Instance.PlaySfx(clip)` |
| 볼륨/토글 | `SetBgmVolume` / `SetSfxVolume` / `SetBgmEnabled` / `SetSfxEnabled` |
| 소리 기본값 복구 | `SoundManager.Instance.ResetToDefaults()` |
| 창 모드 | `DisplayManager.Instance.SetWindowMode(mode 또는 int)` |
| 해상도 | `DisplayManager.Instance.SetResolution(preset 또는 int)` |
| 화면 기본값 복구 | `DisplayManager.Instance.ResetToDefaults()` |

### BGM / SFX 교체·재생 API (존재 확인)

**이미 구현되어 있다.** `SoundManager`에 아래 API가 있으므로 추가 구현은 필요 없다.

| 메서드 | 쉬운 설명 |
|--------|-----------|
| `PlayBgm(AudioClip clip, bool loop = true)` | 넘긴 클립으로 BGM을 **바꿔서** 재생한다. 기본은 루프 |
| `StopBgm()` | 지금 나오는 BGM을 멈춘다 |
| `PlaySfx(AudioClip clip)` | 효과음을 **한 번** 재생한다 (현재 SFX 볼륨·토글 반영) |

Settings 패널 버튼에는 AudioClip을 넘기기 어렵기 때문에, **Title/InGame 스크립트에서 `SoundManager.Instance`를 직접 호출**하면 된다. `SettingsUIManager`에 Play 래퍼를 두지 않은 이유이다.

짧은 사용 예:

```csharp
using ND.UI.Title;
using UnityEngine;

// Title 입장 시 BGM 교체 재생
public void PlayTitleBgm(AudioClip titleBgm)
{
    if (SoundManager.Instance == null || titleBgm == null)
    {
        return;
    }

    SoundManager.Instance.PlayBgm(titleBgm);
}

// 버튼 클릭 SFX
public void PlayClickSfx(AudioClip clickSfx)
{
    if (SoundManager.Instance == null || clickSfx == null)
    {
        return;
    }

    SoundManager.Instance.PlaySfx(clickSfx);
}

// 씬 나갈 때 BGM 정지
public void StopTitleBgm()
{
    SoundManager.Instance?.StopBgm();
}
```

주의:

- `clip`이 null이면 아무 것도 하지 않는다.
- Settings에서 BGM/SFX 토글이 꺼져 있으면 재생되지 않는다 (볼륨 설정은 유지).
- `SoundManager`는 BeforeSceneLoad에 자동 생성되므로 보통 `Instance`가 있다. 그래도 null 체크를 권장한다.

### 기본값을 바꾸고 싶을 때

1. Project에서 `Assets/_Project/05.UI/02_Title/Resources/SoundSettingsConfig` 또는 `DisplaySettingsConfig` 선택
2. Inspector에서 숫자·토글·모드 수정
3. Play 중 Reset 버튼 → 그 값으로 복귀
4. Settings를 다시 열면 화면도 그 값에 맞춰 보임 (`OpenOption`이 UI를 동기화함)

### Inspector에서 꼭 연결할 것 (`SettingsUIManager`)

동기화가 되려면 SettingsUIManager에 아래를 드래그해 넣어야 한다.

- Bgm Slider, Sfx Slider, Bgm Toggle, Sfx Toggle
- Window Mode Dropdown, Resolution Dropdown
- Option Panel (실제 열리는 패널 오브젝트)

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
| 2026-07-13 | 상단에 팀원용 API 쉬운 안내(§0) 추가 |
| 2026-07-13 | BGM/SFX 교체·재생 API(`PlayBgm`/`PlaySfx`/`StopBgm`) 존재 확인 및 사용 예시 보강 |
