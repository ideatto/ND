# World Map Seasonal Background · 구현 로직 정리

**작성일:** 2026-08-11  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/ui/worldmapv4-season-sprite-change`  
**HEAD (문서화 시점):** `593b4a5` — `feat(worldmap): add seasonal background sprite switching`  
**베이스:** `dev2`  
**Feature root:** `Assets/_Project/05.UI/04_WorldMap/`  
**대상 Prefab:** `Assets/_Project/08.Prefabs/UI/Maps/WorldMapRenderRootV4.prefab`

관련 선행·연동:

- [`0731_game_calendar_and_seasons_logic.md`](./0731_game_calendar_and_seasons_logic.md) — 달력·`currentSeasonId` 권위·`SeasonChanged` 발행
- [`0711_world-force-debug-commands.md`](./0711_world-force-debug-commands.md) — `ForceSeason` 레거시 캐시 계약
- [`Docs/Guide/Framework_World_Map_API_Guide.md`](../../Guide/Framework_World_Map_API_Guide.md) — 월드맵 표시 계층 분리
- [`Docs/Guide/World_Map_Seasonal_Background_Usage_Guide.md`](../../Guide/World_Map_Seasonal_Background_Usage_Guide.md) — Prefab·스프라이트 배선 사용법

---

## 1. 목적

프로덕션 게임 계절(`saveData.world.currentSeasonId`)에 따라 **월드맵 V4 배경 SpriteRenderer**만 교체한다.

이번 작업이 하는 것:

1. **`WorldMapSeasonBackgroundBinder`** — 계절 ID → 배경 스프라이트 매핑
2. **`OnEnable` 즉시 동기화** — 저장 복원·맵 재활성 시 현재 계절 배경을 바로 표시
3. **`FrameworkEvents.SeasonChanged` 구독** — 달력 전환 완료 후 배경만 즉시 갱신
4. **V4 Prefab 배선** — `WorldMapBackground` SpriteRenderer + 사계절 스프라이트 4종
5. **안전 실패** — 미할당·알 수 없는 계절 ID는 기존 스프라이트 유지, SaveData 미변경

이번 작업이 하지 않는 것:

- 마을·루트·캐러밴·카메라·날씨·맵 상호작용 변경
- `WorldMapPresenter` / `RouteVisual` / Shared catalog 수정
- `ForceSeason` 동작 변경 (`SeasonChanged` 미발행 유지)
- Scene 직접 수정 (`InGame.unity` 변경 없음)
- 계절 ID를 binder가 직접 쓰거나 달력을 조회하지 않음 (읽기 전용)

---

## 2. 변경 파일 요약

| 영역 | 파일 | 역할 |
|------|------|------|
| Runtime (신규) | `05.UI/04_WorldMap/Scripts/WorldMapSeasonBackgroundBinder.cs` | 계절 → 배경 스프라이트 반영 |
| Prefab (수정) | `08.Prefabs/UI/Maps/WorldMapRenderRootV4.prefab` | binder 컴포넌트 + 4계절 스프라이트 할당 |

**통계:** 3 files, +145 lines

커밋:

```text
593b4a5  feat(worldmap): add seasonal background sprite switching
```

---

## 3. Prefab 계층·배선

```text
WorldMapRenderRootV4
└── WorldMapRoot
    └── Background
        └── WorldMapBackground
            ├── SpriteRenderer          (sortingOrder = -20)
            └── WorldMapSeasonBackgroundBinder
```

| Inspector 필드 | 연결 대상 |
|----------------|-----------|
| `targetRenderer` | 같은 GameObject의 `SpriteRenderer` (`WorldMapBackground`) |
| `springSprite` | `Assets/_Project/09.Art/03_Sprites/world_map_season_spring.png` |
| `summerSprite` | `Assets/_Project/09.Art/03_Sprites/world_map_season_summer.png` |
| `autumnSprite` | `Assets/_Project/09.Art/03_Sprites/world_map_season_autumn.png` |
| `winterSprite` | `Assets/_Project/09.Art/03_Sprites/world_map_season_winter.png` |

`targetRenderer`가 비어 있으면 `GetComponent<SpriteRenderer>()`로 같은 GameObject에서 찾는다.

---

## 4. 계절 식별자 계약

권위 상수: `GameCalendarDate` (`11.CoreServices/Scripts/Time/GameCalendarDate.cs`)

| Season | `SeasonId` / `currentSeasonId` |
|--------|--------------------------------|
| Spring | `spring` |
| Summer | `summer` |
| Autumn | `autumn` |
| Winter | `winter` |

매칭 규칙:

- `ResolveSprite`는 **Ordinal 문자열 동등 비교** (`==`)만 사용
- `DisplayName`, 대소문자 변형, 비정규 값은 매칭되지 않음 → 기존 스프라이트 유지

런타임 Season 소스 (읽기 전용):

```text
FrameworkRoot.Instance.CurrentSaveData.world.currentSeasonId   // OnEnable 초기 반영
GameCalendarSnapshot.SeasonId                                  // SeasonChanged 이벤트 인자
```

---

## 5. 런타임 흐름

### 5.1 활성화 시 (`OnEnable`)

```text
FrameworkEvents.SeasonChanged += HandleSeasonChanged
ApplySeason(CurrentSaveData.world.currentSeasonId)
```

- 맵이 비활성 상태에서 계절이 바뀌었거나 Continue로 저장이 복원된 경우에도, **활성화 직후** 최신 배경으로 맞춘다.
- `SeasonChanged`를 기다리지 않는다.

### 5.2 달력 전환 (`SeasonChanged`)

```text
GameCalendarService (저장 성공 후)
  → FrameworkEvents.RaiseCalendarChanged(previous, current)
  → previous.Season != current.Season 이면 SeasonChanged 발행
  → WorldMapSeasonBackgroundBinder.HandleSeasonChanged
  → ApplySeason(current.SeasonId)
  → targetRenderer.sprite 교체 (다를 때만)
```

달력 mutation 경로 예: `AdvanceDebugDays`, 온라인 tick, 오프라인 복구.

### 5.3 비활성화 (`OnDisable`)

```text
FrameworkEvents.SeasonChanged -= HandleSeasonChanged
```

반복 Show/Hide·파괴 후 중복 구독·중복 알림을 방지한다.

---

## 6. `ApplySeason` 동작

의사코드:

```text
if targetRenderer == null:
    같은 GameObject에서 SpriteRenderer 탐색
    없으면 경고 1회 후 return

resolved = ResolveSprite(seasonId)
if resolved == null:
    if seasonId가 spring/summer/autumn/winter 중 하나이고, 해당 seasonId에 대해 아직 경고 안 했으면:
        경고 1회 ("No sprite is assigned ... preserved")
    return   // 기존 sprite 유지, null 대입 금지

if targetRenderer.sprite != resolved:
    targetRenderer.sprite = resolved
```

| 조건 | 동작 |
|------|------|
| `targetRenderer` 없음 | 경고 1회, return |
| 알려진 계절 + 해당 스프라이트 미할당 | 경고 1회(계절 ID당), 기존 sprite 유지 |
| 알 수 없는/빈 `seasonId` | 경고 없음, 기존 sprite 유지 |
| 정상 매칭 | `SpriteRenderer.sprite`만 교체 |

SaveData·달력·이벤트 발행은 **하지 않는다**.

---

## 7. `ForceSeason` 계약 (기존 Framework 유지)

`FrameworkDebugCommands.ForceSeason`:

```text
world.currentSeasonId = seasonId
SaveService.Save(...)
SeasonChanged 발행 없음
```

추가로 `JsonSaveService.NormalizeData`가 Save 시 `currentSeasonId`를 **달력 권위 값으로 재동기화**할 수 있다.  
따라서 ForceSeason 직후 in-memory season ID가 유지되지 않을 수 있다.

**Binder 기대 동작 (버그 아님):**

| 시점 | 배경 갱신 |
|------|-----------|
| ForceSeason 직후 (맵 활성) | **즉시 보장되지 않음** (`SeasonChanged` 없음) |
| 맵/binder disable → enable | `OnEnable` → `currentSeasonId` 읽어 반영 |

검증·디버그 시 ForceSeason 후 배경 확인이 필요하면 **disable/enable** 또는 **달력 전진(`AdvanceDebugDays`)** 을 사용한다.

---

## 8. 아트·Import 일관성 (런타임 전제)

네 seasonal map sprite는 동일 import profile을 전제로 한다 (2026-08-11 검증값):

| 항목 | 값 |
|------|-----|
| Texture | 1536 × 1024 |
| PPU | 100 |
| Pivot | center (768, 512) |
| Import mode | Single |
| World size | 15.36 × 10.24 |

PPU·크기·pivot 불일치는 **아트/import 설정 이슈**이며, binder가 보정하지 않는다.  
마을·루트 Transform은 배경 sprite 교체와 무관하게 유지되어야 한다.

---

## 9. 책임 경계

| 컴포넌트 | 책임 |
|----------|------|
| `GameCalendarService` | UTC → 게임 일 → 월·계절·재난, Save 후 `SeasonChanged` |
| `JsonSaveService` | Save normalize 시 `currentSeasonId` ↔ 달력 동기화 |
| `WorldMapPresenter` | 마을·루트·캐러밴·진행률 표시 (배경 제외) |
| `WorldMapSeasonBackgroundBinder` | **배경 SpriteRenderer.sprite만** 계절에 맞게 교체 |
| `MinimapWind` / weather | 별도로 `currentSeasonId`를 읽을 수 있음; binder와 독립 |

---

## 10. Play Mode 검증 요약 (2026-08-11)

| 항목 | 결과 |
|------|------|
| Continue → winter 즉시 표시 | PASS |
| Spring / Summer / Autumn / Winter 매트릭스 | PASS (달력 `AdvanceToMonth`) |
| `SeasonChanged` 즉시 sprite 교환 | PASS |
| missing summer sprite → preserve + 1 warning | PASS |
| unknown seasonId → preserve, no exception | PASS |
| ForceSeason → no immediate refresh; OnEnable refresh | PASS (계약대로) |
| disable/enable ×5 lifecycle | PASS |
| 7 towns / routes / markers / weather / sorting -20 | PASS |
| Console new errors | 0 |
| `InGame.unity`, `WorldMapRenderRootV2.prefab` | unchanged |

---

## 11. 확장·금지

**허용:**

- 다른 WorldMapRoot Prefab에 동일 binder 추가 (Inspector 스프라이트만 재배선)
- seasonal art 교체 (동일 world coverage 유지 권장)

**금지 (이 feature 범위 밖):**

- binder에서 SaveData·달력 직접 수정
- `SeasonChanged` 대신 polling 또는 Update에서 매 프레임 조회
- 배경 교체로 town/route Transform 보정 (아트 문제는 import에서 해결)
- `ForceSeason`에 `SeasonChanged` 추가 (Framework 변경 필요 시 별도 PR)

---

## 12. 관련 문서

| 문서 | 내용 |
|------|------|
| [`World_Map_Seasonal_Background_Usage_Guide.md`](../../Guide/World_Map_Seasonal_Background_Usage_Guide.md) | Prefab 배선·테스트·아트 체크리스트 |
| [`Framework_Game_Calendar_and_Seasons_API_Guide.md`](../../Guide/Framework_Game_Calendar_and_Seasons_API_Guide.md) | 계절 ID·달력 API |
| [`Framework_World_Force_Debug_API_Guide.md`](../../Guide/Framework_World_Force_Debug_API_Guide.md) | ForceSeason 디버그 API |
