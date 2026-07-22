# Currency HUD Integration Guide

## 목적

`CurrencyHud`가 Framework의 현재 `SaveData.player.tradingCurrency`를 표시하는 경로와 시스템 간 책임 범위를 정리한다.

이 문서는 `_LJH` 소유 Currency HUD 구현을 기준으로 한다. Framework/CoreServices 외부 코드나 공용 계약 문서를 직접 소유하지 않는다.

이 문서에서 `MUST`, `MUST NOT`, `SHOULD`는 구현 시 지켜야 할 규칙을 뜻한다. 외부 시스템을 수정할 때에는 이 문서를 구현 참고 자료로 사용하되, 외부 영역 소유자의 확인 없이 외부 문서까지 수정하지 않는다.

## 핵심 계약

- HUD의 표시값은 항상 현재 authoritative `SaveData.player.tradingCurrency`의 전체 잔액이어야 한다.
- HUD는 예상 잔액, 변화량, 정산 preview 값을 표시하지 않는다.
- Transactional mutation은 Save 성공 후에만 `TradingCurrencyChanged`를 발행해야 한다.
- Save 실패 또는 rollback 경로에서는 이벤트를 발행하면 안 된다.
- 하나의 성공한 mutation은 최종 잔액 이벤트를 정확히 한 번만 발행해야 한다.
- 이벤트 payload는 delta가 아니라 변경 후 전체 잔액이어야 한다.
- HUD와 UI binding은 SaveData를 변경하거나 저장을 시작하면 안 된다.
- UI 알림 실패가 이미 성공한 거래나 저장 결과를 실패로 바꾸면 안 된다.

## 진실 데이터

- 표시 대상: `ND.Framework.SaveData.player.tradingCurrency`
- 표시하지 않는 값: `developmentCurrency`
- HUD는 별도의 화폐 값을 저장하지 않는다.
- `CurrencyHudPresenter`의 `lastTradingCurrency`는 중복 렌더링을 막기 위한 UI 캐시일 뿐이다.

## 구성 요소

### `CurrencyHudPresenter`

경로: `01.Script/MonoBehaviour/CurrencyHudPresenter.cs`

- `CurrencyChangedEventChannel`을 구독한다.
- 전달받은 `tradingCurrency`를 `CurrencyTextFormatter`로 포맷한다.
- 활성화 직후에는 현재 `FrameworkRoot.CurrentSaveData`를 한 번 읽는다.
- 매 프레임 SaveData를 조회하지 않는다.

### `CurrencyHudRuntimeBinding`

경로: `01.Script/Runtime/Integration/CurrencyHudRuntimeBinding.cs`

Framework 및 Player 영역의 변경 알림을 UI 소유 `CurrencyChangedEventChannel`로 변환한다.

현재 입력 경로는 다음과 같다.

- `FrameworkEvents.LoadCompleted`
- `FrameworkEvents.SceneChanged`
- `FrameworkEvents.TradingCurrencyChanged`
- `PlayerMainManager.OnGoldChanged`

`PlayerMainManager.OnGoldChanged`는 기존 Player 경로와의 호환을 위한 입력이다. 공용 이벤트 전환이 완료될 때까지 유지한다. 두 경로가 같은 값을 연속 전달할 수 있으므로 Presenter는 동일 잔액의 중복 렌더링을 무시한다.

### `CurrencyChangedEventChannel`

경로: `01.Script/EventChannel/CurrencyChangedEventChannel.cs`

Framework 이벤트를 HUD에 직접 결합하지 않기 위한 UI 내부 이벤트 채널이다. Presenter는 Framework의 정산 서비스나 Player manager를 직접 참조하지 않는다.

## 갱신 흐름

```text
SaveData.player.tradingCurrency 변경
    ↓
변경을 소유한 시스템이 확정 이벤트 발행
    ↓
CurrencyHudRuntimeBinding
    ↓
CurrencyChangedEventChannel
    ↓
CurrencyHudPresenter
    ↓
TMP_Text 갱신
```

## 무역 정산 Claim 연동

무역 정산 결과는 `PlayerMainManager.AddGold()`를 거치지 않고 Economy 결과를 `SaveData.player.tradingCurrency`에 직접 반영한다. 따라서 `PlayerMainManager.OnGoldChanged`만 구독하면 정산 결과를 감지할 수 없다.

현재 정산 경로는 다음 규칙을 사용한다.

1. Economy 결과를 SaveData에 적용한다.
2. Claim 결과 저장을 시도한다.
3. 저장 성공 후 `FrameworkEvents.TradingCurrencyChanged`를 한 번 발행한다.
4. HUD binding이 확정된 화폐 스냅샷을 UI 이벤트 채널로 전달한다.

저장 실패로 Claim이 rollback되는 경우에는 이벤트를 발행하지 않는다. 따라서 HUD에 저장되지 않은 임시 값이 표시되지 않는다.

## 이벤트 사용 규칙

`TradingCurrencyChanged` payload는 변화량이 아니라 변경 후 전체 잔액이다.

```csharp
FrameworkEvents.RaiseTradingCurrencyChanged(
    saveData.player.tradingCurrency);
```

다음 시점에는 발행하지 않는다.

- 구매 또는 정산 미리보기
- 아직 저장되지 않은 staging 상태
- 저장 실패 및 rollback 처리 중
- 값이 실제 SaveData에 반영되기 전

새로운 시스템이 `tradingCurrency`를 직접 변경한다면, 그 시스템은 성공적으로 확정된 뒤 동일한 공용 이벤트를 발행해야 한다.

### Transactional mutation

Save와 rollback을 포함하는 거래는 반드시 다음 순서를 사용한다.

```text
snapshot
  → SaveData mutation
  → Save 시도
  → 실패: snapshot 복구, 이벤트 없음
  → 성공: runtime/화면 상태 마무리, TradingCurrencyChanged 1회
```

이벤트를 Save 전에 발행하면 rollback된 값이 HUD에 노출되므로 금지한다.

### Non-transactional runtime mutation

`PlayerMainManager.AddGold()`와 `SpendGold()`처럼 현재 SaveData를 즉시 authoritative runtime state로 사용하는 기존 경로는 mutation 직후 알림을 발행할 수 있다. 단, 이 값은 아직 디스크 Save가 완료된 값이라는 의미는 아니다.

따라서 이 문서에서 `확정`은 다음처럼 구분한다.

- Transactional command: Save 성공까지 완료된 값
- 기존 non-transactional Player API: 현재 runtime SaveData에 반영된 값

새 기능은 가능하면 transactional command 규칙을 사용한다.

### 발행 책임

- 화폐 mutation을 소유한 command/service만 공용 이벤트를 발행한다.
- `CurrencyHudPresenter`와 `CurrencyHudRuntimeBinding`은 공용 이벤트를 발행하면 안 된다.
- 중간 mapper는 여러 command에서 재사용될 수 있으므로 원칙적으로 이벤트를 발행하지 않는다.
- Save 결과를 아는 최상위 command가 성공을 확인한 뒤 발행한다.
- 화면 전환이나 패널 닫기 코드는 화폐 이벤트의 대체 수단으로 사용하지 않는다.

### 예외 안전성

`TradingCurrencyChanged`는 UI 동기화 알림이므로 구독자 예외가 거래 성공 결과를 훼손하면 안 된다.

- 이벤트 발행은 mutation과 Save 성공 판정 이후에 실행한다.
- 구독자 예외는 Framework log로 남기고 다른 구독자 및 성공 결과 반환을 계속할 수 있어야 한다.
- 이미 Save가 성공한 뒤 발생한 UI 예외를 이유로 SaveData를 rollback하면 안 된다.
- 이벤트 처리 중 추가 화폐 mutation을 시작하는 재진입 코드는 작성하지 않는다.

현재 공용 event dispatcher가 구독자별 예외 격리를 제공하지 않는다면, 외부 적용 전에 Framework 담당 영역에서 안전 발행 정책을 먼저 합의해야 한다.

## 변경 경로별 적용 상태

| 변경 경로 | 기대 발행 시점 | 현재 상태 |
|---|---|---|
| Load 또는 Scene 진입 | 현재 SaveData 준비 후 | 연결됨 |
| `PlayerMainManager.AddGold/SpendGold` | runtime mutation 직후 | `OnGoldChanged` 호환 경로로 연결됨 |
| 무역 정산 Claim | Claim Save 성공 후 | `TradingCurrencyChanged` 연결됨 |
| 시장 구매·판매 | 거래 Save 성공 후 | 외부 적용 필요 |
| 구조 대출 발급 | 발급 Save 성공 후 | 외부 적용 필요 |
| 구조 대출 상환 | 상환 Save 성공 후 | 외부 적용 필요 |

`외부 적용 필요` 항목이 남아 있는 동안 “모든 화폐 변경이 HUD에 반영된다”고 판단하면 안 된다. 해당 시스템을 수정할 때에는 외부 소유자 확인 후 이 계약을 적용한다.

## 외부 구현 요청 체크리스트

시장·대출 등 외부 시스템에 연동을 요청할 때에는 다음 조건을 함께 전달한다.

1. mutation 전 잔액 snapshot을 보존한다.
2. 기존 Save 및 rollback 순서를 변경하지 않는다.
3. Save 성공 분기에서 최종 `saveData.player.tradingCurrency`를 한 번 발행한다.
4. Save 실패 분기에서는 발행하지 않는다.
5. preview와 draft 계산에서는 발행하지 않는다.
6. 이벤트 발행 여부를 성공 1회, 실패 0회로 검증한다.
7. UI 구독자 예외가 command 성공 여부를 바꾸지 않도록 한다.

## Inspector 연결

`CurrencyHUD.prefab`에서 다음 참조가 동일한 채널 asset을 사용해야 한다.

- `CurrencyHudPresenter.currencyChangedChannel`
- `CurrencyHudRuntimeBinding.currencyChangedChannel`

Presenter의 `tradingCurrencyText`에는 실제 표시용 `TMP_Text`가 연결되어야 한다.

## 검증 체크리스트

- InGame 진입 시 현재 SaveData 잔액이 표시되는가?
- `PlayerMainManager.AddGold()`와 `SpendGold()` 직후 갱신되는가?
- 무역 정산 Claim 성공 직후 최종 잔액이 표시되는가?
- Claim 저장 실패 또는 rollback 시 임시 잔액이 표시되지 않는가?
- 시장 거래 Save 성공 직후 최종 잔액이 표시되는가?
- 시장 거래 Save 실패 시 HUD가 기존 잔액을 유지하는가?
- 구조 대출 발급·상환 Save 성공 직후 최종 잔액이 표시되는가?
- 구조 대출 Save 실패 시 HUD가 기존 잔액을 유지하는가?
- 각 성공 command가 이벤트를 정확히 1회, 실패 command가 0회 발행하는가?
- 이벤트 구독자가 예외를 발생시켜도 이미 성공한 command 결과와 화면 전환이 유지되는가?
- HUD를 비활성화했다가 다시 활성화하면 현재 SaveData 값으로 복구되는가?
- 이벤트 채널이 null이거나 서로 다른 asset으로 연결되지 않았는가?

## 소유 경계

- `_LJH`: HUD Presenter, RuntimeBinding, UI EventChannel, prefab 연결 및 이 문서
- Framework/CoreServices: `SaveData`, 정산 적용 및 공용 확정 이벤트 발행
- Player 영역: `PlayerMainManager`를 통한 화폐 변경과 `OnGoldChanged`

공용 Framework API 변경이 추가로 필요할 경우 `_LJH` 외부 문서를 바로 수정하지 않고, 변경 필요 사항을 먼저 공유한다.

## 완료 조건

다음 조건을 모두 만족해야 Currency HUD 화폐 연동이 완료된 것으로 본다.

- 표의 모든 변경 경로가 연결됨 상태이다.
- 모든 transactional command가 성공 1회, 실패 0회 발행 규칙을 만족한다.
- HUD가 preview나 rollback 값을 표시하지 않는다.
- Load, 비활성화 후 재활성화, Scene 전환에서 현재 SaveData 값으로 복구된다.
- UI 알림 예외가 저장 또는 command 성공 결과를 변경하지 않는다.
- Unity Play Mode에서 정산, 시장 거래, 구조 대출의 실제 UI 흐름을 확인했다.
