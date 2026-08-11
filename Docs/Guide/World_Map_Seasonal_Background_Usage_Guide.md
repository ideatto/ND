# World Map 계절 배경 사용 가이드

월드맵 V4 배경이 **게임 계절**에 맞는 스프라이트로 자동 전환되도록 Prefab을 배선하고 검증하는 방법을 설명한다.

- 구현·이벤트·책임 경계 상세: [`Docs/Personal_Documents/CSU/0811_world_map_seasonal_background_logic.md`](../Personal_Documents/CSU/0811_world_map_seasonal_background_logic.md)
- 월드맵 전체 API: [`Framework_World_Map_API_Guide.md`](./Framework_World_Map_API_Guide.md)
- 계절 ID·달력: [`Framework_Game_Calendar_and_Seasons_API_Guide.md`](./Framework_Game_Calendar_and_Seasons_API_Guide.md)

---

## 0. 한 줄로 이해하기

```text
saveData.world.currentSeasonId  →  WorldMapSeasonBackgroundBinder  →  WorldMapBackground SpriteRenderer.sprite
```

- **바뀌는 것:** 배경 그림 한 장
- **바뀌지 않는 것:** 마을·루트·캐러밴·카메라·날씨·맵 클릭·무역 데이터

---

## 1. 대상 Prefab

| 항목 | 경로 |
|------|------|
| Prefab | `Assets/_Project/08.Prefabs/UI/Maps/WorldMapRenderRootV4.prefab` |
| Binder 스크립트 | `Assets/_Project/05.UI/04_WorldMap/Scripts/WorldMapSeasonBackgroundBinder.cs` |
| Season art (예시) | `Assets/_Project/09.Art/03_Sprites/world_map_season_{spring,summer,autumn,winter}.png` |

### 1.1 계층

```text
WorldMapRenderRootV4
└── WorldMapRoot
    └── Background
        └── WorldMapBackground
            ├── SpriteRenderer
            └── WorldMapSeasonBackgroundBinder
```

---

## 2. Inspector 배선 (최초 설정·교체)

`WorldMapBackground`를 선택한 뒤 `WorldMapSeasonBackgroundBinder` 필드를 채운다.

| 필드 | 설명 |
|------|------|
| **Target Renderer** | `WorldMapBackground`의 `SpriteRenderer`. 비워 두면 같은 GameObject에서 자동 탐색 |
| **Spring Sprite** | 봄 배경 |
| **Summer Sprite** | 여름 배경 |
| **Autumn Sprite** | 가을 배경 |
| **Winter Sprite** | 겨울 배경 |

### 2.1 SpriteRenderer 확인

`WorldMapBackground`의 `SpriteRenderer`:

- **Sorting Order:** `-20` (맵 콘텐츠 뒤)
- 초기 `Sprite`는 아무 계절이나 가능 — Play 후 binder가 `currentSeasonId`에 맞게 덮어쓴다.

### 2.2 계절 ID (반드시 이 문자열)

| 계절 | Source Id |
|------|-----------|
| 봄 | `spring` |
| 여름 | `summer` |
| 가을 | `autumn` |
| 겨울 | `winter` |

`Spring`, `SUMMER`, `여름` 등은 **매칭되지 않는다**.

---

## 3. 아트 Import 체크리스트

네 seasonal map은 **같은 world coverage**를 목표로 import한다.

| 항목 | 권장 |
|------|------|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | **동일 값** (현재 100) |
| Pivot | Center (0.5, 0.5) |
| 해상도 | 동일 비율·크기 (현재 1536×1024) |

한 계절만 PPU·크기·pivot이 다르면 배경만 확대/축소되어 마을·루트와 어긋날 수 있다.  
이 경우 **아트/import 수정**으로 맞추고, binder 코드로 보정하지 않는다.

---

## 4. 런타임 동작 (기대 결과)

### 4.1 맵을 처음 열 때

```text
WorldMapBackground 활성화
  → OnEnable
  → CurrentSaveData.world.currentSeasonId 읽기
  → 해당 계절 sprite 즉시 표시
```

Continue로 winter 저장을 불러온 경우, **추가 SeasonChanged 없이** winter 배경이 보여야 한다.

### 4.2 게임 중 계절이 바뀔 때

```text
달력 전환 완료 + Save
  → FrameworkEvents.SeasonChanged
  → 배경 sprite만 즉시 교체
```

마을 위치·루트 geometry·캐러밴·카메라·날씨 연출은 그대로여야 한다.

### 4.3 맵을 닫았다 다시 열 때

```text
OnDisable → 구독 해제
OnEnable  → currentSeasonId 재적용
```

---

## 5. 검증 방법

### 5.1 기본 Play Flow

```text
Boot → Title → Continue (또는 New Game) → Loading → InGame
→ 월드맵 열기
```

Console에서 확인:

```csharp
FrameworkRoot.Instance.CurrentSaveData.world.currentSeasonId
```

표시된 배경 sprite 이름이 위 계절과 일치하는지 본다.

### 5.2 사계절 매트릭스

달력 디버그로 계절 경계를 넘긴다 (`FrameworkRoot.DebugCommands`):

| 목표 계절 | 편의 호출 (예) |
|-----------|----------------|
| Spring | `AdvanceToMonth(3)` |
| Summer | `AdvanceToMonth(6)` |
| Autumn | `AdvanceToMonth(9)` |
| Winter | `AdvanceToMonth(12)` |

각 전환 후 배경만 바뀌고, 마을 7개·루트·sorting `-20`이 유지되는지 확인한다.

### 5.3 Missing Sprite (에디터 일시 테스트)

1. **Runtime instance** 또는 Prefab Stage에서 `Summer Sprite`만 `None`으로 비운다.
2. Spring 상태에서 Summer로 달력 전환.
3. 기대: spring 배경 유지, sprite null 아님, 경고 1회, 예외 없음.
4. 테스트 후 **Prefab에 summer sprite를 다시 할당**하고 저장한다.

### 5.4 ForceSeason (디버그 계약)

`ForceSeason`은 `SeasonChanged`를 **발행하지 않는다**.

| 단계 | 기대 |
|------|------|
| 맵 활성 + ForceSeason | 배경 **즉시 갱신 보장 안 됨** |
| 맵/binder disable → enable | OnEnable로 `currentSeasonId` 기준 sprite 표시 |

또한 Save normalize로 ForceSeason 값이 달력 값으로 덮일 수 있다.  
실제 계절 전환 테스트는 **`AdvanceDebugDays` / `AdvanceToMonth`** 를 권장한다.

---

## 6. 다른 Prefab에 붙이기

V2/V3 등 다른 WorldMapRoot에도 동일 패턴을 쓸 수 있다.

1. 배경 `GameObject`에 `SpriteRenderer` + `WorldMapSeasonBackgroundBinder` 추가
2. `targetRenderer`와 사계절 sprite 4개 할당
3. sorting order를 기존 배경 레이어 정책(`-20`)에 맞춘다
4. §5 검증 반복

`WorldMapPresenter`나 Scene YAML을 직접 수정할 필요는 없다.

---

## 7. 하지 말 것

- binder에서 `currentSeasonId` 또는 SaveData 수정
- 계절마다 마을·루트 Transform을 코드로 이동
- `ForceSeason`에 `SeasonChanged`를 기대하거나 binder를 그에 맞게 우회 패치
- seasonal sprite PPU 불일치를 binder에서 scale 보정
- 테스트 후 missing sprite 상태로 Prefab 저장
- 승인 없이 `InGame.unity` 수정

---

## 8. 문제 해결

| 증상 | 확인 |
|------|------|
| 항상 같은 배경 | `currentSeasonId` 값, 네 sprite 할당, binder 활성 여부 |
| 계절 전환해도 배경 안 바뀜 | `SeasonChanged` 발생 여부 (ForceSeason만 썼는지), Console Error |
| 한 계절만 안 바뀜 | 해당 Inspector sprite 필드 None 여부 |
| 배경만 크기/위치가 어색 | 네 sprite의 PPU·pivot·texture size 비교 |
| 경고 spam | 같은 계절 missing sprite — 필드 할당 또는 art 추가 |

---

## 9. 관련 문서

| 문서 | 내용 |
|------|------|
| [`Framework_World_Map_Usage_Guide.md`](./Framework_World_Map_Usage_Guide.md) | 월드맵 전체 붙이기 |
| [`Framework_World_Force_Debug_API_Guide.md`](./Framework_World_Force_Debug_API_Guide.md) | ForceSeason·달력 디버그 |
| [`0811_world_map_seasonal_background_logic.md`](../Personal_Documents/CSU/0811_world_map_seasonal_background_logic.md) | 구현·이벤트·검증 로그 |
