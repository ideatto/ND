# Investment Quest SaveData Contract

## 시스템 범위와 현재 상태

InvestmentQuest는 한 번에 전체 비용을 지불하고 즉시 완료·보상을 적용하는 one-time 시스템이다. 현재 production에는 Quest runtime model, definition, Command, UI, completion SaveData collection, Event가 없으므로 모두 `Contract Only`다. 현재 존재하는 `world.unlockedTownIds`와 `world.unlockedRouteIds`는 접근 권한의 canonical source다.

현재 numeric version은 6이며 승인된 다음 version 7에서 completion collection을 추가한다. v6 저장을 v7로 자동 변환하지 않으며 원본/backup을 보존한 visible reset 경계만 사용한다. reset으로 생성한 v7 저장은 `world.investmentQuestCompletions`를 빈 목록으로 시작한다. legacy donation balance, investment progress, contribution, unlock 상태를 completion entry로 추론하거나 변환하지 않는다. 이 문서 작업은 구현하지 않는다.

## 제출 자원 계약

제출 가능:

- 거래 재화
- Caravan이 직접 보유한 적격 무역품

제출 불가:

- 거점 도시 인벤토리의 무역품
- 건축 재료 전용 물품과 비적격 물품
- Traveling 또는 SettlementPending 상태 Caravan의 물품

Caravan goods 제출은 각 stack에 `caravanId`, `itemId`, `amount`를 명시한다. 여러 Caravan 제출은 허용하되 모든 대상 상태와 asset lock을 검증한다. 분할 납부와 제출 이력 저장은 없다.

각 Quest definition은 대체 가능한 두 가지 전체 비용을 제공할 수 있다: full currency cost 또는 full goods-cost set. 한 요청은 두 결제 방식 중 하나만 선택하며 currency와 goods를 혼합하지 않는다. 선택한 방식의 전체 비용을 한 번에 충족해야 한다.

## SaveData와 identity

권장 root:

```csharp
SaveData.world.investmentQuestCompletions
```

```csharp
[Serializable]
public sealed class InvestmentQuestCompletionSaveData
{
    public string investmentQuestId;
    public string townId;
    public long completedUtcTicks;
}
```

primary key는 `investmentQuestId`이고 entry 존재 자체가 완료 상태다. `state`, `isCompleted`, `isRewardClaimed`, `progressAmount`, `contributedAmount`, `completionPercent`, `sourceCaravanId` 이력, 분할 납부 상태는 저장하지 않는다. `completedUtcTicks`는 UTC ticks이며 음수는 안전 범위에서 0으로 normalize한다.

## Command 계약

```csharp
InvestmentQuestCommandResult CompleteInvestmentQuestWithCurrency(
    string investmentQuestId);

InvestmentQuestCommandResult CompleteInvestmentQuestWithGoods(
    string investmentQuestId,
    IReadOnlyList<InvestmentGoodsContribution> contributions);

public sealed class InvestmentGoodsContribution
{
    public string caravanId;
    public string itemId;
    public int amount;
}
```

한 request 타입으로 통합할 수 있으나 identity, 일시불, resource validation, transaction 의미는 유지한다.

## 완료·보상 transaction

```text
definition 조회
→ 중복 완료 검사
→ 요구 자원 및 Caravan 상태/lock 검증
→ 모든 영향 상태 snapshot
→ currency 또는 goods 차감
→ completion 추가
→ unlock 적용
→ Save
→ SaveResult 성공
→ runtime commit 및 Completed Event
```

별도 Reward Claim은 없다. Save 실패 시 currency, 모든 Caravan cargo, completion, `unlockedTownIds`, `unlockedRouteIds`를 rollback하고 Event를 발행하지 않는다. completed quest는 다시 차감하거나 보상하지 않는다.

Definition의 비용과 unlock ID는 InvestmentQuest SharedGameData가 소유한다. SaveData의 unlock lists가 접근 권한 source이고 completion collection이 중복 완료 방지 source다.

## Normalization과 invalid data

- null collection은 empty list로 만든다.
- unknown quest ID는 보존하고 오류를 보고하며 관련 Command를 차단한다.
- unknown town ID도 보존하고 오류를 보고한다.
- duplicate completion은 validation failure이며 자동 삭제/병합하지 않는다.
- completion/unlock 불일치는 자동 보상이나 unlock 재적용을 하지 않는다.
- unlock만 존재한다고 completion을 생성하지 않는다.
- v6과 legacy donation/progress/contribution 데이터는 completion으로 자동 변환하지 않는다.
- visible reset 뒤 completion collection은 empty list이며 기존 donation/progress나 unlock 존재만으로 completion을 생성하지 않는다.

## Superseded 정책

Status: Superseded by this contract (결정일 2026-07-21)

- Investment `progressAmount`/`contributedAmount`와 분할 납부
- `isRewardClaimed`와 별도 보상 Claim
- 거점 도시 inventory의 무역품 제출
- donation balance를 Investment로 전환하는 흐름

## 구현 Stage와 테스트

1. Stage 1: v7 completion DTO/validation과 Shared definition 계약
2. Stage 2: currency/goods Commands와 structured Result
3. Stage 3: snapshot/rollback, unlock 적용, Save 성공 후 Event
4. Stage 5: UI/query와 invalid-data 표시

테스트: currency 완료, 단일/복수 Caravan goods 완료, home inventory 차단, Traveling/Pending goods 차단, 분할 납부 차단, 중복 완료, unknown quest/town, completion/unlock 불일치, Save 실패 전체 rollback, 성공 Event 1회/실패 0회, v7 round trip.

## 제외 범위와 미결정

production 구현, 실제 definition asset, 콘텐츠 ID/비용/unlock 데이터, UI는 제외한다. InvestmentQuestDefinition의 실제 asset 형태와 소유 위치는 추가 사실이 필요하며 SharedGameData 소유를 권장 기본안으로 한다. 결정권자는 Progression/Content와 Framework 책임 영역이며 v7 구현 착수 전 확정한다.

