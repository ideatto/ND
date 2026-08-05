# Quest 마을·루트 해금 사용 가이드

## 목적

Quest 완료 보상으로 마을과 무역 Route를 해금하고, 저장 성공 직후 월드맵과 무역 준비 화면에 결과를 반영하는 방법을 설명한다.

## Quest 보상 설정

QuestData의 `Rewards`에 필요한 보상을 추가한다.

| Reward Type | Value | Reward Id | 설명 |
| --- | ---: | --- | --- |
| `Trading Currency` | 지급할 금액 | 비움 | Quest 완료 보상 화폐 |
| `Unlock Town` | 0 | Town ID | 해당 마을을 영구 해금 |
| `Unlock Route` | 0 | Route ID | 해당 방향 Route를 영구 해금 |

`Reward Id`에는 에셋 이름이 아니라 데이터의 `townId` 또는 `routeId`를 입력한다.

예시:

```text
Trading Currency / Value: 200
Unlock Town / Reward Id: MountTown
Unlock Route / Reward Id: RiverToMount
Unlock Route / Reward Id: MountToRiver
```

## 방향성 Route 주의사항

현재 Route는 방향별로 서로 다른 ID를 사용한다.

```text
RiverToMount: RiverTown → MountTown
MountToRiver: MountTown → RiverTown
```

왕복 통행이 필요하면 정방향과 역방향 `Unlock Route`를 모두 Quest 보상에 등록해야 한다. 한쪽만 등록하면 목적지 도착 후 돌아오는 Route가 잠길 수 있다.

병렬 Route(`RiverToMount2`, `MountToRiver2`)는 별도 Route이므로 의도한 경로만 추가한다.

> BaseCamp 복귀 Route 자동 개방과 양방향 자동 해금 정책은 아직 구현되지 않았다. 관련 정책이 확정되기 전까지는 Quest 데이터에서 왕복 Route를 명시한다.

## 런타임 동작

Quest 완료 성공 시 다음 순서로 처리된다.

1. 비용과 보상을 계산한다.
2. 화폐 또는 Caravan 아이템을 차감한다.
3. `unlockedTownIds`, `unlockedRouteIds`에 신규 ID를 추가한다.
4. SaveData 저장을 완료한다.
5. 화폐가 바뀌었다면 `TradingCurrencyChanged`를 발행한다.
6. `QuestRewardsCommitted`를 발행한다.
7. 월드맵과 무역 준비 화면이 현재 SaveData를 다시 읽는다.

저장 실패 시 변경 내용을 rollback하며 위 갱신 이벤트를 발행하지 않는다.

## QuestRewardsCommitted 구독

추가 UI가 Quest 보상 확정을 반영해야 한다면 활성화 수명 주기에서 이벤트를 구독·해제한다.

```csharp
private void OnEnable()
{
    FrameworkEvents.QuestRewardsCommitted += HandleQuestRewardsCommitted;
}

private void OnDisable()
{
    FrameworkEvents.QuestRewardsCommitted -= HandleQuestRewardsCommitted;
}

private void HandleQuestRewardsCommitted(QuestRewardsCommittedEvent committed)
{
    // payload를 권위 상태로 사용하지 말고 저장된 현재 데이터를 다시 읽는다.
    RefreshFromSaveData();
}
```

Payload에는 이번 저장에서 새로 추가된 다음 ID만 포함된다.

- `QuestId`
- `UnlockedTownIds`
- `UnlockedRouteIds`
- `UnlockedSpecialtyItemIds`

## 출발 검증

`TradeStartService`는 UI 선택 가능 여부와 별개로 다음 조건을 최종 검증한다.

- 요청 Route가 Shared catalog에 존재한다.
- Caravan의 현재 마을이 Route의 `FromTownId`와 일치한다.
- Route가 기본 해금 또는 SaveData 해금 상태다.
- 출발·도착 마을이 기본 해금 또는 SaveData 해금 상태다.

거부 원인은 `RouteLocked`, `DepartureTownMismatch`, `TownLocked`로 구분된다.

## 검증 시나리오

1. 테스트 전 대상 Town과 양방향 Route의 `unlockedByDefault`가 `false`인지 확인한다.
2. Quest 완료 전 월드맵과 무역 준비 화면에서 대상이 잠겨 있는지 확인한다.
3. Quest를 수락하고 요구 비용을 지불해 완료한다.
4. 화폐 HUD가 즉시 갱신되는지 확인한다.
5. 신규 Town과 정·역방향 Route가 즉시 표시되는지 확인한다.
6. 목적지로 출발하고 정산 후 역방향 Route로 복귀한다.
7. 게임을 다시 실행해 해금 상태가 유지되는지 확인한다.
8. 저장 실패 환경에서는 비용·보상·이벤트가 rollback되는지 확인한다.

## 현재 테스트 데이터

`Assets/99.Sandbox/_LJH/02.SO/QuestSO/Quest_RiverTownSupply.asset`

- 제출 마을: `RiverTown`
- 해금 마을: `MountTown`
- 정방향 Route: `RiverToMount`
- 역방향 Route: `MountToRiver`

