# Central Sound Catalog — 구현 로직 정리

> 브랜치: `feature/audio/central-sound-catalog`  
> 작성일: 2026-08-09

---

## 1. 작업 목적

기존 `SoundManager`는 `AudioClip`을 직접 넘겨 BGM/SFX를 재생했다.  
씬마다 BGM을 수동으로 호출하거나, UI 버튼마다 클립 참조를 중복 배치하면 다음 문제가 생긴다.

1. 사운드 ID·클립·카테고리가 코드와 Prefab에 분산된다.
2. BGM/SFX/UI SFX 채널 구분이 없어 UI 클릭음과 게임 SFX 볼륨을 따로 조절하기 어렵다.
3. 씬 전환 시 BGM 전환과 loop SFX 정리를 각 feature가 개별 처리해야 한다.

이번 작업의 목표는 다음과 같다.

1. `SoundCatalog` ScriptableObject에 사운드 정의와 Scene BGM 매핑을 중앙 집중한다.
2. `SoundManager`가 ID 기반 재생, Scene 자동 BGM, loop SFX, UI SFX 채널을 담당한다.
3. 기존 `AudioClip` 직접 재생 API와 Title Settings Reset 흐름은 유지한다.
4. UI 버튼 클릭음은 `UIButtonSound` 컴포넌트로 Prefab에서 ID만 지정해 연결한다.

---

## 2. 전체 구조

```text
Resources/SoundCatalog.asset          ← 사운드 정의 + Scene BGM 매핑
Resources/SoundSettingsConfig.asset   ← BGM/SFX/UI SFX 기본 볼륨·토글

RuntimeInitializeOnLoad (BeforeSceneLoad)
  ↓
SoundManager (DontDestroyOnLoad singleton)
  ├─ bgmSource      (loop, fade 지원)
  ├─ sfxSource      (legacy one-shot, AudioClip 직접 재생용)
  ├─ uiSfxSource    (legacy 채널, 현재 ID one-shot은 별도 AudioSource 사용)
  └─ loopSources    (handle → 독립 AudioSource)

FrameworkEvents.SceneChanged
  ↓
SoundManager.HandleSceneChanged
  ├─ StopAllLoopSfx()
  └─ SoundCatalog.TryGetSceneBgm → PlayBgm(id, fade) / StopBgm(fade)
```

### 책임 분리

| 구성요소 | 위치 | 역할 |
|---|---|---|
| `SoundCategory` | `10.Audio/Scripts` | `Bgm`, `Sfx`, `UiSfx` enum |
| `SoundDefinition` | `10.Audio/Scripts` | ID, category, clips[], volume, pitch range |
| `SceneBgmMapping` | `10.Audio/Scripts/SoundCatalog.cs` | Scene 이름 → BGM ID |
| `SoundCatalog` | `10.Audio/Resources` | 정의 조회, Scene BGM 조회, Inspector 검증 |
| `SoundManager` | `05.UI/02_Title/Scripts` | 재생, 볼륨, fade, loop handle, Scene 구독 |
| `SoundSettingsConfig` | `05.UI/02_Title/Resources` | Reset 기본값 |
| `SettingsUIManager` | `05.UI/02_Title/Scripts` | UI SFX Slider/Toggle 위임 |
| `UIButtonSound` | `10.Audio/Scripts` | Button.onClick → `PlayUiSfx(id)` |

---

## 3. 변경 파일

| 파일 | 역할 |
|---|---|
| `Assets/_Project/10.Audio/Scripts/SoundCategory.cs` | 사운드 카테고리 enum |
| `Assets/_Project/10.Audio/Scripts/SoundDefinition.cs` | 단일 사운드 정의 데이터 |
| `Assets/_Project/10.Audio/Scripts/SoundCatalog.cs` | 중앙 카탈로그 + lookup + 검증 |
| `Assets/_Project/10.Audio/Scripts/UIButtonSound.cs` | UI 버튼 클릭음 컴포넌트 |
| `Assets/_Project/10.Audio/Resources/SoundCatalog.asset` | 런타임 카탈로그 asset |
| `Assets/_Project/05.UI/02_Title/Scripts/SoundManager.cs` | ID 재생, Scene BGM, loop SFX, UI SFX 채널 |
| `Assets/_Project/05.UI/02_Title/Scripts/SoundSettingsConfig.cs` | UI SFX 기본값 필드 추가 |
| `Assets/_Project/05.UI/02_Title/Scripts/SettingsUIManager.cs` | UI SFX Slider/Toggle 위임·동기화 |
| `Assets/_Project/05.UI/02_Title/Resources/SoundSettingsConfig.asset` | UI SFX 기본값 데이터 |
| `Assets/_Project/05.UI/02_Title/Prefabs/TitleSettingsCanvas.prefab` | UI SFX 컨트롤 연결 |
| `Assets/_Project/07.Scenes/02_Title/Title.unity` | Title Settings 연결 반영 |

---

## 4. SoundCatalog

### 4.1 Resources 로드

- asset 경로: `Assets/_Project/10.Audio/Resources/SoundCatalog.asset`
- `SoundCatalog.ResourceName = "SoundCatalog"`
- `SoundManager.LoadResources()`에서 `Resources.Load<SoundCatalog>(SoundCatalog.ResourceName)` 호출
- asset이 없으면 경고 로그 후 ID 기반 재생은 무시된다.

### 4.2 Lookup 구조

`OnEnable`, `OnValidate`, 최초 `TryGet`/`TryGetSceneBgm` 호출 시 lookup을 재구성한다.

| Dictionary | Key | Value |
|---|---|---|
| `definitionLookup` | sound ID (`Ordinal`) | `SoundDefinition` |
| `sceneBgmLookup` | Unity Scene 이름 (`Ordinal`) | BGM ID 또는 `""` |

### 4.3 Definition 검증

다음 항목은 lookup에서 제외하고 Editor/Validate 시 로그를 남긴다.

- null definition
- 빈 ID
- clip 배열이 비어 있거나 usable clip이 없음
- 중복 ID

pitch 범위는 `NormalizePitchRange()`로 NaN/Infinity 보정 후 min/max를 swap한다.

### 4.4 Scene BGM 매핑 검증

| 조건 | 처리 |
|---|---|
| Scene 이름 비어 있음 | 무시 + Error |
| 중복 Scene 이름 | 무시 + Error |
| `BgmId`가 빈 문자열 | Scene 진입 시 BGM 중지 |
| `BgmId`가 catalog에 없음 | 무시 + Error |
| `BgmId`가 BGM category가 아님 | 무시 + Error |

Scene 이름은 `FrameworkEvents.SceneChanged`가 전달하는 Unity Scene 이름과 일치해야 한다.  
`SceneFlowService`는 scene load completion 시 `FrameworkEvents.RaiseSceneChanged(sceneName)`을 호출한다.

---

## 5. SoundManager

### 5.1 생명주기

```text
BeforeSceneLoad
  EnsureExists() → GameObject("SoundManager") 생성

Awake
  singleton 확정 + DontDestroyOnLoad
  EnsureAudioSources() → bgm/sfx/uiSfx AudioSource 생성
  LoadResources()
  ResetToDefaults()
  FrameworkEvents.SceneChanged += HandleSceneChanged

OnDestroy
  SceneChanged 구독 해제
  StopAllLoopSfx()
  Instance = null
```

`FrameworkRoot`와 분리된 별도 singleton이다. 씬에 미리 배치할 필요가 없다.

### 5.2 볼륨·토글 모델

| 채널 | runtime state | 적용 대상 |
|---|---|---|
| BGM | `bgmVolume`, `bgmEnabled`, `bgmFadeFactor` | `bgmSource` |
| SFX | `sfxVolume`, `sfxEnabled` | `sfxSource`, loop AudioSource |
| UI SFX | `uiSfxVolume`, `uiSfxEnabled` | ID one-shot AudioSource |

최종 one-shot 볼륨:

```text
channelVolume * definition.Volume
```

loop SFX 볼륨:

```text
sfxVolume * definitionVolume
```

BGM 최종 볼륨:

```text
bgmVolume * bgmFadeFactor
```

`bgmFadeFactor`는 fade coroutine 전용 계수이며, fade 중에도 사용자 `bgmVolume` 변경이 반영되도록 분리했다.

### 5.3 BGM 재생

#### ID 기반

```text
PlayBgm(soundId)
  → PlayBgm(soundId, 0f)

PlayBgm(soundId, fadeDuration)
  1. TryGetDefinition(id, Bgm)
  2. GetFirstValidClip(definition)   // BGM은 첫 usable clip 사용
  3. currentBgmId + clip + isPlaying 중복이면 no-op
  4. StartBgmTransition(clip, fadeDuration)
```

fade 동작:

- `fadeDuration <= 0`: 즉시 교체
- `fadeDuration > 0`: 기존 BGM 50% fade out → clip 교체 → 50% fade in
- fade 시간은 `Time.unscaledDeltaTime` 기준

#### Scene 자동 전환

```text
HandleSceneChanged(sceneName)
  1. StopAllLoopSfx()
  2. catalog.TryGetSceneBgm(sceneName, out bgmId)
     - 매핑 없음: no-op
     - bgmId == "": StopBgm(0.75f)
     - bgmId 있음: PlayBgm(bgmId, 0.75f)
```

기본 Scene fade 시간: `DefaultSceneFadeDuration = 0.75f`

#### Legacy AudioClip API

`PlayBgm(AudioClip clip, bool loop)`는 유지된다.

- `currentBgmId = null`
- fade 취소 후 즉시 clip 교체
- Scene 자동 BGM 추적과 분리된다.

### 5.4 SFX / UI SFX one-shot

ID one-shot은 공유 `sfxSource`/`uiSfxSource`를 쓰지 않고 **임시 AudioSource**를 생성한다.

이유:

- 동시 재생마다 독립 pitch가 필요함
- `PlayOneShot`은 source pitch를 공유함

흐름:

```text
PlaySfx(soundId) / PlayUiSfx(soundId)
  1. 채널 enabled 확인
  2. TryGetDefinition(id, Sfx or UiSfx)
  3. GetRandomValidClip(definition)
  4. GetRandomPitch(definition)
  5. CreateAudioSource()
  6. PlayOneShot(clip)
  7. clip.length / |pitch| + 0.1초 후 Destroy(source)
```

clip 선택:

- usable clip만 count
- `Random.Range(0, validCount)`로 균등 선택

pitch:

- `minPitch == maxPitch`이면 고정 pitch
- 아니면 `[minPitch, maxPitch]` uniform random

Legacy:

- `PlaySfx(AudioClip clip)`는 기존처럼 `sfxSource.PlayOneShot(clip)` 사용

### 5.5 Loop SFX

```text
PlayLoopSfx(soundId) → int handle
  - SFX enabled + category Sfx 확인
  - random clip + random pitch
  - loop=true AudioSource 생성
  - handle 반환 (실패 시 0)

StopLoopSfx(handle) → bool
  - source Stop/Destroy 후 dictionary 제거
```

handle 규칙:

- `0`은 invalid handle
- `nextLoopHandle`은 1부터 증가하며 dictionary 충돌 시 skip

Scene 전환 시 `StopAllLoopSfx()`로 모든 loop source를 정리한다.

### 5.6 ResetToDefaults

`SoundSettingsConfig`에서 다음 값을 읽어 runtime state에 적용한다.

- `DefaultBgmVolume`, `DefaultSfxVolume`, `DefaultUiSfxVolume`
- `DefaultBgmEnabled`, `DefaultSfxEnabled`, `DefaultUiSfxEnabled`

config가 없으면 볼륨 1, enabled true를 사용한다.

---

## 6. Title Settings 연동

### 6.1 SoundSettingsConfig 확장

기존 BGM/SFX 기본값에 UI SFX 기본값이 추가되었다.

- `defaultUiSfxVolume`
- `defaultUiSfxEnabled`

### 6.2 SettingsUIManager

추가된 UI 위임 API:

- `SetUiSfxVolume(float)`
- `SetUiSfxEnabled(bool)`

`OpenOption()`, `ResetAllSettings()` 시 `SyncUiFromManagers()`가 UI SFX Slider/Toggle도 `SetValueWithoutNotify` / `SetIsOnWithoutNotify`로 동기화한다.

---

## 7. UIButtonSound

```text
Button.onClick
  → SoundManager.Instance?.PlayUiSfx(soundId)
```

- `[RequireComponent(typeof(Button))]`
- `OnEnable`/`OnDisable`에서 listener 등록·해제
- Prefab Inspector에 `soundId`만 지정하면 된다.

---

## 8. 실패 처리 정책

| 상황 | 동작 |
|---|---|
| `SoundCatalog` 없음 | ID 재생 무시 + Warning |
| unknown ID | Warning, 재생 안 함 |
| category mismatch | Warning, 재생 안 함 |
| usable clip 없음 | Warning, 재생 안 함 |
| BGM/SFX/UI SFX disabled | 해당 채널 재생 skip |
| invalid loop handle stop | `false` 반환 |

대부분 fail-soft이며, 게임 진행을 막지 않는다.

---

## 9. 아직 하지 않은 범위

- 사운드 설정(SO 기본값 외) SaveData 영속화
- Addressables 기반 clip 로드
- BGM crossfade 외 고급 mixer routing
- `SoundCatalog.asset` 실제 clip/ID 데이터 채우기 (현재 asset은 빈 배열)

---

## 10. 검증 메모

코드 기준으로 확인한 항목:

- `SoundCatalog` lookup/validation 로직
- `SoundManager` ID 재생, fade, loop handle, SceneChanged 구독
- `SettingsUIManager` UI SFX Slider/Toggle 위임
- legacy `AudioClip` API 유지

Unity Editor에서 추가 확인이 필요한 항목:

- `TitleSettingsCanvas.prefab` UI SFX 컨트롤 wiring
- `SoundCatalog.asset`에 clip/ID/Scene mapping 등록 후 Play Mode 재생
- Scene 전환 시 BGM 자동 전환
- `UIButtonSound` Prefab 연결
