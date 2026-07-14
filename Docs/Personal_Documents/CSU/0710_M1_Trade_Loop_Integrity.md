## M1 Trade Loop Integrity 로직 정리

이번 작업의 목적은 무역 한 사이클이 다음 흐름으로 안전하게 반복되도록 만드는 것입니다.

```text
Preparing
→ Traveling
→ SettlementPending
→ Completed / Failed
→ Preparing
```

핵심은 정산 결과가 없는데 성공 처리되거나, 이전 정산 결과가 재사용되거나, 정산 수령이 중복 실행되는 상황을 막는 것입니다.

## 1. 무역 시작 로직

담당 파일:

`Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeStartService.cs`

무역 시작은 이제 두 단계로 처리됩니다.

```text
출발 가능 여부 검증
→ Framework 진행 기록 성공 여부 확인
→ Core Journey 출발 처리
→ 저장 데이터에 Caravan 상태 복사
→ Traveling 화면 상태 요청
→ 저장
```

중요 변경점:

- 먼저 `CaravanValidator.Validate()`로 출발 가능 여부를 확인합니다.
- Framework가 `tradeId`, `routeId`, 시작 시각, 예상 종료 시각을 기록하지 못하면 Core 상태를 `Traveling`으로 바꾸지 않습니다.
- `tradeId`가 비어 있거나 저장 데이터가 없으면 출발 기록을 막습니다.
- 새 무역 기록이 성공한 뒤에만 이전 정산 캐시를 제거합니다.

이렇게 해서 “Framework 기록은 실패했는데 Core Caravan만 이동 중이 되는 상태”를 방지합니다.

## 2. 무역 진행 기록 로직

담당 파일:

`Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressRecorder.cs`

`TradeProgressRecorder`는 저장 데이터의 무역 진행 상태를 기록합니다.

기록하는 값:

```text
activeTradeId
activeRouteId
state = Traveling
tradeStartUtcTick
expectedTradeEndUtcTick
```

이번 작업에서 추가로 보장한 것:

- `tradeId`가 비어 있으면 기록하지 않습니다.
- 이미 같은 무역이 Traveling으로 기록되어 있으면 중복 기록하지 않습니다.
- 다른 무역이 이미 Traveling이면 덮어쓰지 않습니다.
- 음수 duration은 0으로 보정합니다.

## 3. 진행도 계산 로직

담당 파일:

`Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`

진행도는 Framework가 저장된 시작/종료 시각을 기준으로 계산합니다.

```text
progress = (현재 UTC - 시작 UTC) / (예상 종료 UTC - 시작 UTC)
```

계산된 progress는 Core의 `JourneyRunner.SetProgress()`로 전달됩니다.

```text
Framework: 시간 기준 계산
Core: progress01 반영 및 도착/실패 판정
```

현재 M1에서는 UTC 기반 진행 계산을 사용합니다.  
시간 배율, 오프라인 진행, 식량 소모 복구의 완전한 통합은 M2/M3 범위로 남겨둡니다.

## 4. 정산 생성 로직

담당 파일:

`Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`

정산은 다음 조건을 만족할 때만 생성됩니다.

```text
saveData.tradeProgress 존재
state == Traveling
activeTradeId가 비어 있지 않음
TradeProgressRecorder 존재
Core가 정산 결과를 반환함
```

정산 생성 흐름:

```text
JourneyRunner.Settle(caravan)
→ Framework state = SettlementPending
→ LastSettlementTradeId 저장
→ LastSettlementResult 저장
→ Caravan 상태 저장 데이터에 복사
→ Save
→ TradeSettlementReady 이벤트 발생
→ Settlement 화면 요청
```

중요한 점:

- `LastSettlementTradeId`와 `LastSettlementResult`는 항상 한 쌍으로 관리됩니다.
- 정산 결과가 없으면 `Completed`로 처리하지 않습니다.
- `SettlementPending` 상태로 전환되지 않으면 정산 이벤트를 발행하지 않습니다.

## 5. 정산 수령 로직

담당 파일:

`Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`

정산 수령은 다음 조건을 모두 통과해야 합니다.

```text
LastSettlementResult != null
saveData.tradeProgress 존재
state == SettlementPending
LastSettlementTradeId == activeTradeId
TradeProgressRecorder 존재
Core ClaimSettlement 성공
```

수령 성공 후 흐름:

```text
Core ClaimSettlement
→ 결과 등급에 따라 Framework 상태 기록
   - Failed → Failed
   - Success / PartialSuccess → Completed
→ Core ResetToPrepare
→ Caravan 상태 저장 데이터에 복사
→ Save
→ Preparation 화면 요청
→ 정산 캐시 제거
```

중복 수령을 시도하면 `LastSettlementResult`가 이미 제거되어 있으므로 차단됩니다.

정상 차단 로그 예시:

```text
[Framework] Settlement claim blocked because cached settlement result is missing.
```

## 6. 화면 전환 로직

담당 파일:

`Assets/_Project/11.CoreServices/Scripts/SceneFlow/InGameScreenStateRouter.cs`

무역 상태와 화면 상태는 다음처럼 연결됩니다.

```text
Traveling          → Traveling 화면
SettlementPending  → Settlement 화면
Completed / Failed → Preparation 화면
Preparing / None   → Preparation 화면
```

정산 수령 후에는 명시적으로 `Preparation` 화면을 요청합니다.

## 7. 정산 UI 연결

기존 작업 재사용:

`Assets/_Project/11.CoreServices/Scripts/UI/Settlement/SettlementUiDataAdapter.cs`

이번 작업은 정산 UI 어댑터를 새로 만들지 않았습니다.

기존 구조를 그대로 사용합니다.

```text
TradeSettlementReady 이벤트
→ SettlementUiBridge
→ SettlementUiDataAdapter
→ ISettlementView
```

UI는 정산 결과를 표시하고 수령 요청만 보냅니다.  
정산 검증과 상태 변경은 Framework가 처리합니다.

## 8. 디버그 스모크 테스트

담당 파일:

`Assets/_Project/11.CoreServices/Scripts/Debug/TradeStartDebugHarness.cs`

추가된 Context Menu:

```text
Framework/Run M1 Loop Integrity Smoke
```

이 테스트는 내부적으로 3회 반복합니다.

한 사이클의 테스트 흐름:

```text
샘플 Caravan 생성
→ 무역 시작
→ 강제 완료
→ 정산 결과 생성 확인
→ SettlementPending 상태에서 progress check 재호출
→ 첫 정산 수령 성공 확인
→ 두 번째 정산 수령 실패 확인
→ Preparation 복귀 확인
→ 정산 캐시 제거 확인
```

성공 로그:

```text
[Framework] M1 loop integrity smoke completed 3 consecutive trade cycles.
```

중복 수령 차단 로그가 함께 나오는 것은 정상입니다.

```text
[Framework] Settlement claim blocked because cached settlement result is missing.
```

## 9. 이번 작업에서 막는 문제

이번 작업은 다음 문제를 방지합니다.

```text
Framework 기록 실패 후 Core만 Traveling 상태가 되는 문제
빈 tradeId로 무역이 시작되는 문제
진행 중인 다른 tradeId를 덮어쓰는 문제
정산 결과 null을 성공으로 처리하는 문제
SettlementPending이 아닌 상태에서 정산 수령하는 문제
이전 무역의 정산 결과를 새 무역에서 재사용하는 문제
정산 버튼 중복 입력으로 보상/저장/상태 변경이 두 번 실행되는 문제
정산 후 Preparation으로 복귀하지 않는 문제
```

## 10. 이번 작업의 범위 밖

아래 항목은 아직 처리하지 않았습니다.

```text
PendingSettlementSaveData 저장
게임 재실행 후 정산 대기 결과 복구
오프라인 완료 정산 복구
Atomic save: main/temp/backup 저장 복구
Dirty flag 기반 자동 저장
온라인/오프라인 동일 시간 배율 기반 식량 소모 복구
최종 정산 UI 레이아웃
```