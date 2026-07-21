# SaveData V2 Field Contract

## Status and version

`V2`는 second-build 제품 문서 라벨이며 serialized numeric version이 아니다. 현재 production은 version 6이고 승인된 다음 numeric version은 7이다. 이 문서 작업은 production `CurrentVersion`을 변경하지 않는다.

Version 7 목표는 Unity-serializable List를 persisted source로 사용하고 Dictionary는 저장하지 않는다. runtime index/cache는 validation 뒤 재구성한다. Shared definition과 Scene 표시값을 복제하지 않고 stable ID로 참조한다.

## Target hierarchy

```text
SaveData
|- version, metadata(lastSavedUtcTicks)
|- player(currencies, growth levels, home inventory, villageBuildings[])
|- selectedCaravanId
|- caravans[]
|- tradeProgressEntries[]
|- pendingSettlements[]
|- world(unlockedTownIds[], unlockedRouteIds[], investmentQuestCompletions[])
|- rescueLoan
`- tutorial/world mutable state
```

`selectedCaravanId`는 UI 선택과 마지막 선택 복원만 저장한다. mutation 대상, runtime context, 진행/완료/정산 대상을 추론하지 않는다.

## Canonical field rules

| 영역 | 저장하는 원본/결과 상태 | 저장하지 않는 파생·정의 값 |
|---|---|---|
| Caravan | `caravanId`, definition IDs, counts, durability, cargo, food | load/speed/consumption/final stats |
| Trade | full GUID `tradeId`, route, state, UTC timestamps | 표시 progress 문자열 |
| Settlement | `(caravanId, tradeId)`와 confirmed result snapshot | 재계산 settlement result |
| Building | `buildingId`, `level` | `displayName`, localization, icon, costs, effects |
| InvestmentQuest | `investmentQuestId`, `townId`, `completedUtcTicks` | state/isCompleted/isRewardClaimed, progress/contribution, costs, unlock definition |
| World | unlocked town/route ID lists | definition values와 표시 데이터 |
| Rescue loan | 승인된 loan DTO 원본 상태 | eligibility, UI text, 계산값 |

## Building DTO

기존 root `SaveData.player.villageBuildings`를 불필요하게 이동하지 않는다.

```csharp
[Serializable]
public sealed class VillageBuildingSaveData
{
    public string buildingId;
    public int level;
}
```

version 7은 `displayName`을 저장하지 않고 dual write도 하지 않는다. UI는 `buildingId → Shared Building Definition → displayName/localization/icon/effect`로 조회한다.

## InvestmentQuest completion DTO

```csharp
[Serializable]
public sealed class InvestmentQuestCompletionSaveData
{
    public string investmentQuestId;
    public string townId;
    public long completedUtcTicks;
}
```

권장 root는 `SaveData.world.investmentQuestCompletions`이며 primary key는 `investmentQuestId`다. entry 존재가 one-time completion 상태다. contribution은 Command input이고 제출 이력이나 파생 progress는 저장하지 않는다.

## Compatibility boundary

version 6의 selected compatibility properties는 collection의 UI/legacy view일 뿐 source of truth가 아니다. v6은 v7로 자동 변환하지 않고 원본/backup을 보존한 명시적 reset 경로를 사용한다. `displayName → buildingId` 변환, duplicate/orphan 수정, 보상/unlock 재적용은 normalization에 포함하지 않는다.

세부 정책은 `Save_Version_and_Normalization_Policy.md`, `Investment_Quest_SaveData_Contract.md`, `Village_Building_ID_Migration_Plan.md`를 따른다.
