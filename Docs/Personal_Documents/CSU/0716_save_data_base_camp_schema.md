# 거점(Base Camp) SaveData 스키마 · Core 연동 작업 요약

**작성일:** 2026-07-16  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/framework/save-data-update-base-camp-data`  
**Base:** `dev2`

---

## 1. 한 줄 요약

거점 창고(`homeInventory`)와 마을 건물 진행(`villageBuildings`)을 `SaveData.player`에 넣고, `JsonSaveService.NormalizeData`로 구 세이브 null을 보정했다.  
스키마 version은 **5 유지**. Core의 `PlayerMainManager` / `VillageBuildingRegistry`가 `CurrentSaveData`를 진실로 읽기·쓰도록 연동했고, InGame 진입 시 UI 이벤트를 한 번 발행한다.

---

## 2. 왜 이 작업을 했나

`PlayerMainManager` 머지 이후:

- 소지금·마차 화물은 이미 `CurrentSaveData`를 참조
- **거점 인벤토리**는 매니저 로컬 `[SerializeField]`만 소유 → 세이브에 안 남음
- **마을 건물**은 `VillageBuildingRegistry` 씬 데이터만 → 재실행 시 진행 유실

방치형 거점 성장·창고를 유지하려면 Framework Save 스키마에 편입하고 Core가 같은 객체를 참조해야 한다.

Core TODO 참고: [`../YHY/0715_player_main_manager.md`](../YHY/0715_player_main_manager.md) — “거점 인벤토리 저장: SaveData에 필드 추가”가 이번으로 Framework 측에 반영됨. YHY 문서는 Core 소유라 직접 수정하지 않았다.

---

## 3. 이번 브랜치에서 한 일

### 3-1. Framework

| 파일 | 변경 |
|------|------|
| [`Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs`](../../../Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs) | `PlayerSaveData`에 `homeInventory`, `villageBuildings` 추가. `VillageBuildingSaveData` DTO 추가. 헤더·주석 갱신. **`CurrentVersion = 5` 유지** |
| [`Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`](../../../Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs) | `NormalizeData`에서 home/village null → 빈 리스트, village 항목 `displayName`/`level` 보정 |

### 3-2. Core

| 파일 | 변경 |
|------|------|
| [`Assets/_Project/01.Core/08_Player/YHY/PlayerMainManager.cs`](../../../Assets/_Project/01.Core/08_Player/YHY/PlayerMainManager.cs) | `homeInventory`를 `Save.player.homeInventory` 참조로 전환. `Start`·`SceneChanged(InGame)`에서 `OnGoldChanged` / `OnHomeInventoryChanged` / `OnCaravanCargoChanged` 1회 발행 |
| [`Assets/_Project/01.Core/07_Village/YHY/VillageBuildingRegistry.cs`](../../../Assets/_Project/01.Core/07_Village/YHY/VillageBuildingRegistry.cs) | `Start`에서 SaveData 복원. `AddOrUpgrade` 시 `displayName`+`level` upsert. FrameworkRoot 없으면 씬 로컬만 |

### 3-3. 문서

| 파일 | 내용 |
|------|------|
| 본 문서 | 작업 요약 |
| [`Docs/Guide/Framework_CoreServices_Team_Usage_Guide.md`](../../Guide/Framework_CoreServices_Team_Usage_Guide.md) | §4에 거점 필드·Normalize·본 문서 링크 |
| [`Docs/Guide/Framework_InGame_Time_Multiplier_API_Guide.md`](../../Guide/Framework_InGame_Time_Multiplier_API_Guide.md) | version 5 부가 필드 문단에 base camp 한 줄 |

**의도적으로 하지 않은 것**

- `SaveData.CurrentVersion` 증가 (구 version 5 wipe 방지)
- Scene / Prefab / ProjectSettings 수정
- 커밋·푸시·PR (사용자가 확인 후 직접)
- 마을 건물 `buildingId` 도입 (기획상 종류 한정 → `displayName` 키 유지)

---

## 4. 스키마

### 필드

| 경로 | 타입 | 의미 |
|------|------|------|
| `player.homeInventory` | `List<CargoEntrySaveData>` | 거점 창고. `caravan.cargo`와 동일 형식 |
| `player.villageBuildings` | `List<VillageBuildingSaveData>` | 거점 건물 진행 |
| `VillageBuildingSaveData.displayName` | `string` | 카탈로그와 동일 키 |
| `VillageBuildingSaveData.level` | `int` | 0 이하 = 미건축, 1+ = 보유 레벨 |

### version 결정

**결정:** version **5 유지** + `NormalizeData` 보강.

| 세이브 | 동작 |
|--------|------|
| 기존 version 5 (새 필드 없음) | Load 시 null → Normalize가 빈 리스트 → **로드 가능** |
| 새 세이브 | 필드 초기값 빈 리스트 → **정상** |

### displayName 키 전제

건물 종류가 한정되고 표시명이 고정된다는 기획 전제. 표시명을 바꾸면 옛 세이브와 매칭이 깨질 수 있다.

---

## 5. Core 연동 요점

```text
JsonSaveService.Load/Save
  → FrameworkRoot.CurrentSaveData
      → PlayerMainManager (gold / homeInventory / caravan.cargo)
      → VillageBuildingRegistry (villageBuildings 복원·기록)
```

- `PlayerMainManager`는 SaveData를 캐시하지 않고 호출마다 `CurrentSaveData`를 조회한다.
- UI는 데이터 참조만으로는 갱신되지 않으므로 InGame `SceneChanged`와 `Start`에서 이벤트를 한 번 쏜다.
- `LoadCompleted`는 InGame **전**에 발생할 수 있어, InGame 씬 로드 완료 알림을 사용한다.
- FrameworkRoot가 없는 테스트 씬에서는 기존처럼 폴백 `SaveData` / 씬 로컬 건물만 동작한다.

영속화는 기존과 같이 `SaveService.Save(CurrentSaveData)`가 필요할 때 호출한다(무역 루프 등 기존 저장 지점 유지).

---

## 6. 검증

- 소스에 `homeInventory` / `villageBuildings` / `VillageBuildingSaveData` / Normalize 보정 존재 확인
- IDE 진단: 변경 스크립트 기준 확인
- Unity Play Mode (사용자): New Game → 거점 아이템/건물 변경 → Save → 재실행 Continue → 복원
- Unity Play Mode (사용자): InGame 진입 시 재화/인벤 UI 이벤트 1회

커밋·푸시는 사용자가 수행한다.

---

## 7. 관련 문서

- Core 매니저 배경: [`../YHY/0715_player_main_manager.md`](../YHY/0715_player_main_manager.md)
- 팀 사용 가이드 §4: [`../../Guide/Framework_CoreServices_Team_Usage_Guide.md`](../../Guide/Framework_CoreServices_Team_Usage_Guide.md)
- 상점 스키마(동일 version 5 + Normalize 패턴): [`0714_framework_market_inventory_save_schema_work_summary.md`](0714_framework_market_inventory_save_schema_work_summary.md)
