# Save Result API Implementation Decision

구현 상태: 선행 적용  
팀 최종 승인: 대기

## 목적

이 문서는 기존 저장 정책이 제안한 장기 API 방향과 `feature/framework/save-result-api` 브랜치에서 선행 적용한 API 사이의 차이를 팀 회의 전까지 추적한다. 이 기록은 최종 정책 승인이나 기존 정책 폐기를 의미하지 않는다.

## 기존 정책 제안

`Immediate_Save_and_Dirty_Policy.md`는 기존 호출부의 단계적 이전을 위해 다음 방향을 제안한다.

```csharp
void Save(SaveData data);
SaveResult TrySave(SaveData data);
```

기존 `Save()`는 임시 호환 wrapper로 유지하고, 저장 결과가 필요한 호출부를 `TrySave()`로 전환하는 장기 제안이다.

## 이번 브랜치의 선행 결정

이번 브랜치의 확정 작업 범위는 다음 계약을 적용하는 것이다.

```csharp
SaveResult Save(SaveData data);
```

작업 요청이 저장 성공과 실패를 호출자가 확인할 수 있는 최소 기반을 `Save()` 자체에 추가하도록 명시했기 때문에 이 반환형을 선행 적용한다. C# 호출부는 반환값을 받지 않아도 기존 `saveService.Save(data);` 호출 형태를 유지할 수 있으므로, 현재 호출 순서나 행동을 변경하지 않고 최소한의 결과 계약을 도입할 수 있다.

이 적용 이유는 이번 브랜치의 허용 범위를 설명할 뿐이며, `SaveResult Save(...)`를 팀의 최종 단일 계약으로 승인하는 근거가 아니다.

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

이번 선행 구현은 기존 wrapper 및 `TrySave()` 제안을 폐기하거나 대체하는 최종 결정이 아니다. 기존 정책 문서는 변경하지 않으며, 코드와 정책 제안의 차이는 팀이 최종 API 방향을 결정할 때까지 이 문서로 추적한다.

## 팀 논의 필요 항목

- `SaveResult Save(SaveData data)`를 최종 단일 계약으로 확정할지
- 기존 `void Save()` 호환 wrapper가 필요한지
- 별도의 `TrySave()`가 필요한지
- 현재 또는 다른 작업 브랜치에 `ISaveService` 구현체가 추가되어 있는지
- 테스트용 Mock, Fake 또는 Memory Save 구현체에 미치는 영향
- 중요 저장 호출부가 언제부터 `SaveResult`를 검사해야 하는지
- 저장 실패 후 게임 행동, 이벤트 발행 및 런타임 상태를 어떻게 처리할지
- 결정 이후 기존 정책 문서에서 wrapper 및 `TrySave()` 설명을 유지, 수정 또는 폐기할지

## 최종 정렬 필요 사항

팀 회의에서 최종 계약을 승인한 뒤 `ISaveService`, 모든 구현체와 테스트 대역, 호출부 이전 계획, 그리고 기존 저장 정책 문서를 같은 방향으로 정렬해야 한다. 최종 결정 전에는 이 선행 구현이나 기존 정책 제안 중 어느 쪽도 승인된 최종 계약으로 간주하지 않는다.
