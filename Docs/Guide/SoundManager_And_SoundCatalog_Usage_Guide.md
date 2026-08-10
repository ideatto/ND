# SoundManager · SoundCatalog 사용 설명서

## 목적

이 문서는 프로젝트 중앙 오디오 시스템을 팀원이 빠르게 설정하고 호출하는 방법을 설명한다.

- 사운드 정의와 Scene BGM 매핑: `SoundCatalog`
- 런타임 재생·볼륨·채널 제어: `SoundManager`
- UI 버튼 클릭음: `UIButtonSound`

상세 구현 로직: [`Docs/Personal_Documents/CSU/0809_central_sound_catalog_logic.md`](../Personal_Documents/CSU/0809_central_sound_catalog_logic.md)

**네임스페이스**

| 타입 | Namespace |
|---|---|
| `SoundManager`, `SoundSettingsConfig` | `ND.UI.Title` |
| `SoundCatalog`, `SoundDefinition`, `SoundCategory`, `UIButtonSound` | `ND.Audio` |

**진입점:** `SoundManager.Instance`  
**자동 생성:** `RuntimeInitializeOnLoad(BeforeSceneLoad)` — 씬에 미리 배치하지 않아도 된다.

---

## 1. 한 줄로 이해하기

```text
SoundCatalog에 ID를 등록 → SoundManager.Play*(id)로 재생 → SceneChanged로 BGM 자동 전환
```

| 질문 | 답 |
|---|---|
| 사운드 데이터는 어디에 두나? | `Assets/_Project/10.Audio/Resources/SoundCatalog.asset` |
| 런타임 재생 API는? | `SoundManager.Instance` |
| UI 클릭음은? | `UIButtonSound` + `PlayUiSfx(id)` |
| Scene BGM은? | `SoundCatalog`의 Scene mapping |
| Reset 기본값은? | `Assets/_Project/05.UI/02_Title/Resources/SoundSettingsConfig.asset` |

---

## 2. 빠른 시작

### 2.1 SoundCatalog에 사운드 등록

1. Project 창에서 `Assets/_Project/10.Audio/Resources/SoundCatalog.asset`을 연다.
2. `Definitions` 배열에 항목을 추가한다.
3. 각 항목에 아래 값을 입력한다.

| 필드 | 설명 |
|---|---|
| `Id` | 코드/Prefab에서 사용할 문자열 ID |
| `Category` | `Bgm`, `Sfx`, `UiSfx` |
| `Clips` | 재생 후보 `AudioClip` 배열 |
| `Volume` | 채널 볼륨과 곱해지는 개별 gain (0~1) |
| `Min Pitch` / `Max Pitch` | one-shot/loop pitch random 범위 (0.1~3) |

4. Scene BGM이 필요하면 `Scene Bgm Mappings`에 Scene 이름과 BGM ID를 추가한다.

### 2.2 코드에서 재생

```csharp
using ND.UI.Title;

SoundManager.Instance.PlaySfx("trade_complete");
SoundManager.Instance.PlayUiSfx("ui_click");
SoundManager.Instance.PlayBgm("title_bgm");
```

### 2.3 UI 버튼 클릭음 연결

1. Button이 있는 GameObject에 `UIButtonSound`를 추가한다.
2. Inspector `Sound Id`에 `UiSfx` category ID를 입력한다.
3. Play Mode에서 클릭 시 UI SFX 채널로 재생되는지 확인한다.

---

## 3. SoundCatalog 설정

### 3.1 asset 위치

| asset | 경로 | Resources 이름 |
|---|---|---|
| Sound Catalog | `Assets/_Project/10.Audio/Resources/SoundCatalog.asset` | `SoundCatalog` |
| Sound Settings Config | `Assets/_Project/05.UI/02_Title/Resources/SoundSettingsConfig.asset` | `SoundSettingsConfig` |

`SoundCatalog` asset 이름과 Resources 경로는 변경하지 않는다. `SoundManager`가 고정 이름으로 로드한다.

### 3.2 SoundCategory

| 값 | 용도 | 재생 API |
|---|---|---|
| `Bgm` | 배경음 | `PlayBgm(id)` |
| `Sfx` | 게임 효과음 | `PlaySfx(id)`, `PlayLoopSfx(id)` |
| `UiSfx` | UI 클릭·확인음 | `PlayUiSfx(id)` |

category가 API와 다르면 재생되지 않고 Warning이 출력된다.

예:

- `PlaySfx("ui_click")`에서 `ui_click`이 `UiSfx`이면 재생 실패
- `PlayUiSfx("ui_click")`을 사용해야 한다.

### 3.3 Clip 선택 규칙

| 재생 종류 | clip 선택 |
|---|---|
| BGM | 첫 usable clip |
| SFX / UI SFX one-shot | usable clip 중 random |
| Loop SFX | usable clip 중 random |

`Clips` 배열에 variation을 여러 개 넣을 수 있다.

### 3.4 Scene BGM 매핑

`Scene Bgm Mappings`는 Unity Scene 이름 기준이다.

| Bgm Id | Scene 진입 시 동작 |
|---|---|
| 유효한 BGM ID | 해당 BGM을 fade(0.75s)로 재생 |
| 빈 문자열 | BGM fade out 후 중지 |
| 매핑 없음 | BGM 자동 전환 없음 |

Scene 이름은 `FrameworkEvents.SceneChanged`와 동일해야 한다.  
Framework scene load 완료 시 `SceneFlowService`가 scene name을 전달한다.

---

## 4. SoundManager API

### 4.1 Singleton

```csharp
SoundManager.Instance
```

- `DontDestroyOnLoad`
- duplicate instance는 Destroy된다.
- `Instance`가 null이면 아직 초기화 전이거나 파괴된 상태다.

### 4.2 볼륨·토글 API

| 메서드 | 설명 | 범위 |
|---|---|---|
| `SetBgmVolume(float volume)` | BGM 볼륨 | 0~1 |
| `SetSfxVolume(float volume)` | SFX 볼륨 | 0~1 |
| `SetUiSfxVolume(float volume)` | UI SFX 볼륨 | 0~1 |
| `SetBgmEnabled(bool enabled)` | BGM on/off | |
| `SetSfxEnabled(bool enabled)` | SFX on/off | |
| `SetUiSfxEnabled(bool enabled)` | UI SFX on/off | |
| `ResetToDefaults()` | `SoundSettingsConfig` 기본값 복원 | |

읽기 전용 상태:

- `BgmVolume`, `SfxVolume`, `UiSfxVolume`
- `BgmEnabled`, `SfxEnabled`, `UiSfxEnabled`

Title Settings UI는 `SettingsUIManager`가 위 API에 위임한다.

### 4.3 BGM API

| 메서드 | 설명 |
|---|---|
| `PlayBgm(string soundId)` | catalog BGM 즉시 재생 |
| `PlayBgm(string soundId, float fadeDuration)` | fade 재생 |
| `PlayBgm(AudioClip clip, bool loop = true)` | legacy clip 직접 재생 |
| `StopBgm()` | 즉시 중지 |
| `StopBgm(float fadeDuration)` | fade out 후 중지 |

동작 메모:

- 같은 BGM ID + 같은 clip + 이미 재생 중이면 `PlayBgm(id)`는 no-op이다.
- Scene 자동 BGM은 내부적으로 `PlayBgm(id, 0.75f)` / `StopBgm(0.75f)`를 사용한다.
- legacy `PlayBgm(AudioClip)` 호출 후에는 `currentBgmId` 추적이 해제된다.

### 4.4 SFX / UI SFX API

| 메서드 | 설명 |
|---|---|
| `PlaySfx(string soundId)` | SFX one-shot |
| `PlayUiSfx(string soundId)` | UI SFX one-shot |
| `PlaySfx(AudioClip clip)` | legacy SFX one-shot |

one-shot ID 재생은 임시 `AudioSource`를 만들어 random pitch를 보존한다.

### 4.5 Loop SFX API

```csharp
int handle = SoundManager.Instance.PlayLoopSfx("campfire_loop");
bool stopped = SoundManager.Instance.StopLoopSfx(handle);
```

| 반환/인자 | 의미 |
|---|---|
| `PlayLoopSfx` 반환값 | loop handle. 실패 시 `0` |
| `StopLoopSfx(handle)` | 성공 시 `true`, unknown handle은 `false` |

주의:

- Scene 전환(`FrameworkEvents.SceneChanged`) 시 활성 loop SFX는 자동으로 모두 중지된다.
- loop SFX는 SFX 채널 볼륨/enable 상태를 따른다.

---

## 5. UIButtonSound

### 5.1 사용 조건

- 같은 GameObject에 `Button` 필요
- `soundId`는 `UiSfx` category여야 한다.

### 5.2 Prefab wiring

1. Button GameObject 선택
2. Add Component → `UIButtonSound`
3. `Sound Id` 입력
4. Play Mode에서 UI SFX mute/volume 설정이 반영되는지 확인

코드 예:

```csharp
// UIButtonSound 내부 동작
SoundManager.Instance?.PlayUiSfx(soundId);
```

---

## 6. Title Settings 연동

### 6.1 기본값 asset

`Assets/_Project/05.UI/02_Title/Resources/SoundSettingsConfig.asset`

| 필드 | 의미 |
|---|---|
| `Default Bgm Volume` | Reset 시 BGM 볼륨 |
| `Default Sfx Volume` | Reset 시 SFX 볼륨 |
| `Default Ui Sfx Volume` | Reset 시 UI SFX 볼륨 |
| `Default Bgm Enabled` | Reset 시 BGM on/off |
| `Default Sfx Enabled` | Reset 시 SFX on/off |
| `Default Ui Sfx Enabled` | Reset 시 UI SFX on/off |

### 6.2 Settings UI wiring

`TitleSettingsCanvas` → `SettingsUIManager`

| UI 컨트롤 | 이벤트 대상 |
|---|---|
| BGM Slider | `SetBgmVolume` |
| SFX Slider | `SetSfxVolume` |
| UI SFX Slider | `SetUiSfxVolume` |
| BGM Toggle | `SetBgmEnabled` |
| SFX Toggle | `SetSfxEnabled` |
| UI SFX Toggle | `SetUiSfxEnabled` |
| Reset Button | `ResetAllSettings` |

`OpenOption()`과 `ResetAllSettings()`는 매니저 현재값을 UI에 `WithoutNotify`로 다시 반영한다.

---

## 7. 자주 쓰는 패턴

### 7.1 게임 이벤트 SFX

```csharp
if (SoundManager.Instance != null)
{
    SoundManager.Instance.PlaySfx("caravan_depart");
}
```

### 7.2 수동 BGM 교체

```csharp
SoundManager.Instance.PlayBgm("boss_bgm", fadeDuration: 1.5f);
```

Scene mapping과 별도로 코드에서 직접 BGM을 바꿀 수 있다.

### 7.3 Scene별 자동 BGM만 사용

1. `SoundCatalog` Scene mapping만 등록
2. feature 코드에서는 BGM을 직접 호출하지 않음
3. Scene 전환마다 `SceneChanged` → `SoundManager`가 BGM 처리

### 7.4 loop SFX 시작/종료

```csharp
private int loopHandle;

public void StartAmbientLoop()
{
    loopHandle = SoundManager.Instance.PlayLoopSfx("rain_loop");
}

public void StopAmbientLoop()
{
    if (loopHandle != 0)
    {
        SoundManager.Instance.StopLoopSfx(loopHandle);
        loopHandle = 0;
    }
}
```

---

## 8. 트러블슈팅

| 증상 | 확인할 것 |
|---|---|
| ID 재생이 안 됨 | `SoundCatalog.asset`에 ID/category/clips 등록 여부 |
| `[SoundManager] SoundCatalog is unavailable` | Resources 경로와 asset 이름 |
| category mismatch Warning | API와 category 일치 여부 (`PlayUiSfx` vs `PlaySfx`) |
| Scene BGM이 안 바뀜 | Scene mapping의 scene name 문자열 |
| Scene BGM만 안 멈춤 | mapping `Bgm Id`를 빈 문자열로 지정했는지 |
| UI 클릭음만 안 들림 | `UiSfxEnabled`, `UiSfxVolume`, `UIButtonSound.soundId` |
| loop SFX가 Scene 이동 후 남음 | 현재 구현은 SceneChanged 시 자동 정리. 직접 stop도 가능 |
| Reset 후 UI 값이 어긋남 | `SettingsUIManager.ResetAllSettings()` 사용 여부 |

Editor에서 `SoundCatalog`를 수정하면 `OnValidate`가 duplicate ID, invalid clip, invalid Scene mapping을 Console에 출력한다.

---

## 9. 호환성 메모

- 기존 `PlayBgm(AudioClip)`, `PlaySfx(AudioClip)` API는 유지된다.
- 새 작업은 가능하면 `SoundCatalog` ID 기반 API를 사용한다.
- 현재 구현은 사운드 설정 SaveData 영속화를 포함하지 않는다. 앱 재시작 시 `SoundSettingsConfig` 기본값으로 시작한다.

---

## 10. 관련 파일

| 파일 | 설명 |
|---|---|
| `Assets/_Project/10.Audio/Scripts/SoundCatalog.cs` | catalog ScriptableObject |
| `Assets/_Project/10.Audio/Scripts/SoundDefinition.cs` | definition 데이터 |
| `Assets/_Project/10.Audio/Scripts/SoundCategory.cs` | category enum |
| `Assets/_Project/10.Audio/Scripts/UIButtonSound.cs` | UI button helper |
| `Assets/_Project/05.UI/02_Title/Scripts/SoundManager.cs` | runtime audio manager |
| `Assets/_Project/05.UI/02_Title/Scripts/SoundSettingsConfig.cs` | reset defaults |
| `Assets/_Project/05.UI/02_Title/Scripts/SettingsUIManager.cs` | Title settings UI bridge |
| `Assets/_Project/11.CoreServices/Scripts/Events/FrameworkEvents.cs` | `SceneChanged` event |
