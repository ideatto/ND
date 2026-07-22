# Save Result API Implementation Decision

구현 상태: 선행 적용 기록
팀 최종 정책: `SaveResult Save(SaveData data)` 단일 계약

## 목적

이 문서는 과거의 이중 API 제안과 선행 구현 기록을 보존하고, 이후 팀에서 확정한 단일 Save API 목표를 명시한다. 이 문서 수정은 production 코드를 변경하거나 구현 완료를 의미하지 않는다.

## 폐기된 기존 정책 제안

`Immediate_Save_and_Dirty_Policy.md`는 기존 호출부의 단계적 이전을 위해 다음 방향을 제안한다.

```csharp
void Save(SaveData data);
SaveResult TrySave(SaveData data);
```

이 안은 과거 제안이며 최종 목표 계약으로 채택되지 않았다. 별도 `TrySave()`와 `void Save()` wrapper는 목표 API에 포함하지 않는다.

## 확정된 목표 계약

이번 브랜치의 확정 작업 범위는 다음 계약을 적용하는 것이다.

```csharp
SaveResult Save(SaveData data);
```

작업 요청이 저장 성공과 실패를 호출자가 확인할 수 있는 최소 기반을 `Save()` 자체에 추가하도록 명시했기 때문에 이 반환형을 선행 적용한다. C# 호출부는 반환값을 받지 않아도 기존 `saveService.Save(data);` 호출 형태를 유지할 수 있으므로, 현재 호출 순서나 행동을 변경하지 않고 최소한의 결과 계약을 도입할 수 있다.

팀의 최종 정책은 `SaveResult Save(...)` 단일 계약이다. 다만 현재 production 코드와 모든 호출부가 결과 처리까지 완료했는지는 별도 구현 브랜치에서 검증해야 한다.

## 두 방식의 차이

기존 장기 제안은 `void Save()` 호환 계약을 유지하면서 별도의 `TrySave()`로 소비자를 단계적으로 이전한다. 이번 구현은 별도 메서드를 추가하지 않고 기존 `Save()`의 반환형을 `SaveResult`로 변경한다.

따라서 소스 수준의 기존 호출문은 반환값을 무시한 채 유지할 수 있지만, `ISaveService` 구현체와 Mock, Fake, Memory 구현체는 반환형 변경에 맞춰 수정해야 한다. 장기 제안은 두 API의 공존 및 제거 시점 정책이 필요하고, 이번 구현은 단일 이름을 유지하는 대신 다른 브랜치의 구현체와 API 충돌 가능성을 확인해야 한다.

## 이번 브랜치에서 실제 적용한 범위

- `SaveFailureReason` 결과 분류 추가
- `SaveResult` 결과 타입 추가
- `ISaveService.Save(SaveData)` 반환형을 `SaveResult`로 변경
- `JsonSaveService.Save(SaveData)`에서 null, 직렬화 실패, 파일 쓰기 실패 및 예상하지 못한 실패 결과 반환
- 기존 저장 호출부의 호출 순서와 반환값 무시 방식 유지
- 컴파일 유지에 필요한 테스트용 `ISaveService` 구현체의 반환형 수정

## 이번 브랜치에서 구현하지 않은 범위

- 별도 `TrySave()` API
- 기존 `void Save()` 호환 wrapper
- 중요 저장 호출부의 트랜잭션 처리 또는 `SaveResult` 검사
- Snapshot, Rollback, Retry, Queue 및 Dirty autosave
- 저장 성공 여부에 따른 Event timing 전환
- 저장 실패 UI 또는 게임 행동 차단
- Save Version 6 활성화

## 기존 정책과의 관계

기존 wrapper 및 `TrySave()` 제안은 역사적 대안으로만 보존한다. 목표 정책 문서는 `SaveResult Save(...)` 단일 방향으로 정렬한다. 조사 문서의 당시 production `void Save()` 기록은 사실 기록으로 유지하되 현재 목표와 구분한다.

## 후속 구현 확인 항목

- 현재 또는 다른 작업 브랜치에 `ISaveService` 구현체가 추가되어 있는지
- 테스트용 Mock, Fake 또는 Memory Save 구현체에 미치는 영향
- 중요 저장 호출부가 언제부터 `SaveResult`를 검사해야 하는지
- 중요 Command의 snapshot, rollback, retry, queue, UI 차단 및 event timing 적용 순서

## 최종 정렬 필요 사항

후속 구현에서 `ISaveService`, 모든 구현체와 테스트 대역, 호출부 이전을 단일 계약에 맞춰 검증해야 한다. 중요 Command는 결과 반환만으로 완료된 것으로 보지 않으며 PreCommandSnapshot, 최종 실패 rollback, 성공 후 event/UI 갱신까지 구현·시험해야 한다.
