# Village Building ID Migration Plan

## Current Implementation

- root는 `SaveData.player.villageBuildings`, DTO는 `displayName + level`이다.
- `displayName`이 저장, 비용 정의, level upsert, Scene registry, UI 표시의 식별과 표시를 겸한다.
- canonical `buildingId`, ID validator, SharedGameData Building query는 없다.
- production 후보 UI는 registry 직접 경로를 사용하고 원자 save/rollback Command는 Editor tests 외 호출자가 확인되지 않았다.
- 현재 construction Command는 selected Caravan cargo와 displayName을 사용한다.

표시명 변경·localization·Scene object 이름 변경이 영속 identity를 깨뜨릴 수 있고 duplicate/unknown을 안정적으로 판별할 수 없으므로 displayName 저장은 version 7에서 종료한다.

## Approved Target Contract

기존 root는 이동하지 않고 다음만 저장한다.

```csharp
[Serializable]
public sealed class VillageBuildingSaveData
{
    public string buildingId;
    public int level;
}
```

ID 규칙:

- 프로젝트 전체 유일, Shared Building Definition에 명시, 배포 후 변경 금지
- displayName, localization, Scene object 이름, asset filename과 독립
- filename 자동 생성이나 hash 기반 생성 금지
- 권장 형태: `building_town_hall`, `building_warehouse`, `building_caravan_office`

실제 6개 Building의 승인 ID는 아직 없으므로 예시를 mapping으로 확정하지 않는다.

canonical query:

```csharp
bool TryGetBuilding(
    string buildingId,
    out SharedBuildingDefinition building);
```

최소 definition은 `BuildingId`, displayName 또는 localization key, `MaxLevel`, `Category`, cost/effect/unlock reference를 제공한다. UI는 `buildingId → Shared definition → 표시 데이터`로 조회한다.

version 7 SaveData에는 displayName을 제거하고 `buildingId + displayName` dual write를 하지 않는다. compatibility adapter가 필요하면 runtime/API 경계에 두고 영속 필드로 유지하지 않는다.

## Version 6 처리

- 현재 production numeric version은 6, 승인된 다음 version은 7이다.
- v6 테스트 저장은 v7로 자동 migration하지 않고 명시적으로 reset한다.
- legacy displayName에서 buildingId를 추론하거나 fuzzy match/default fallback하지 않는다.
- unsupported v6 파일을 조용히 overwrite하지 않고 가능한 경우 원본/backup을 보존한다.
- legacy mapping 표는 QA/수동 확인용으로만 허용하며 loader 자동 변환 근거가 아니다.

## Duplicate와 unknown

- duplicate `buildingId`: 자동 합산, max level, first/last 선택 없이 validation failure
- unknown `buildingId`: 보존, visible error, 해당 Building Command 차단
- normalization은 null collection, 안전한 child/scalar 기본값, 음수 level→0만 처리하며 identity 의미를 바꾸지 않는다.

## Command와 resource source

목표 API:

```csharp
BuildingCommandResult UpgradeBuilding(string buildingId);
```

```text
buildingId 검증
→ definition 조회
→ current/max level 검증
→ Economy 비용 Query
→ home inventory 검증
→ 재료/level snapshot
→ home 재료 차감과 level 증가 staging
→ Save
→ SaveResult 성공
→ runtime commit 및 Building Event
```

Save 실패 시 재료와 level을 rollback하고 Event를 발행하지 않는다. Building 비용은 거점 도시 home inventory에서만 소비하며 Caravan cargo를 직접 사용하는 현재 구조는 목표 계약에서 폐기한다.

## 구현 Stage

1. Stage 1: v7 DTO, reset handling, Shared definition/ID uniqueness validator
2. Stage 2: catalog/Command/result를 buildingId로 전환하고 home inventory 비용 경로 연결
3. Stage 4: Save 성공 후 committed Building Event
4. Stage 5: registry/UI definition lookup, compatibility adapter와 serialized reference 검증, 제거 준비

SaveData v7, Building ID, InvestmentQuest, coordinator, UI/legacy migration은 별도 PR로 분리한다.

## 테스트 매트릭스

- valid ID 생성/upgrade, max level, home 재료 부족
- Caravan cargo만 충분할 때 차단
- Save 실패 시 재료/level rollback과 Event 0회
- duplicate/unknown ID 보존과 Command 차단
- displayName/localization 변경 뒤 같은 save identity 유지
- v6 unsupported/backup/reset, v7 round trip

## Superseded 정책과 제외 범위

Status: Superseded by this contract (결정일 2026-07-21)

- Building displayName 영속 식별
- `buildingId + displayName` dual write
- selected Caravan cargo를 Building 비용으로 직접 사용

production C#, Scene/Prefab/ScriptableObject, 실제 ID mapping과 asset 생성은 제외한다.

미결정 항목: SharedBuildingDefinition의 실제 asset 소유 위치와 현재 6개 표시명의 canonical ID. 필요한 추가 사실은 Content/Progression 승인 ID 목록과 definition ownership이며, SharedGameData definition을 권장 기본안으로 한다. 결정권자는 해당 콘텐츠 책임 영역과 Framework이며 v7 구현 전 확정한다.

