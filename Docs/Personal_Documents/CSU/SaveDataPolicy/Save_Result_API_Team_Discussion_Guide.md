# SaveData 반환 타입 팀 회의 논의 가이드

브랜치: `feature/framework/save-result-api`  
용도: 팀 회의에서 `Save()` / `SaveData` 반환 타입 방향을 정할 때 사용  
작성자 개인 메모: 천성욱 (CSU)

---

## 1. 회의에서 정해야 할 한 줄 질문

> **저장 API는 앞으로 `SaveResult Save(...)` 하나로 갈지,  
> `void Save(...)` + `TrySave(...)` 두 개로 갈지,  
> 아니면 다른 형태로 갈지?**

이번 브랜치는 답을 확정하지 않고, **논의용 선행 구현**만 넣었다.

---

## 2. 왜 이 변경이 필요한가

### 지금 문제

많은 코드가 아래처럼 저장 후 바로 성공 처리를 한다.

```csharp
saveData.player.tradeMoney -= cost;
saveService.Save(saveData);
return Ok(); // 저장 실패여도 성공처럼 보일 수 있음
```

`Save()`가 `void`이면:

- 디스크 쓰기 실패를 호출자가 모른다
- UI는 이미 성공 메시지를 보여줄 수 있다
- 이벤트는 저장 전/후 타이밍이 애매하다
- "즉시 저장이 필요한 중요 행동"과 "나중에 저장해도 되는 변경"을 구분하기 어렵다

### 이번 브랜치가 해결하는 것

- 저장 성공/실패를 **코드로 표현할 수 있는 기반** 추가
- 실패 원인을 **단계별로 분류**
- 기존 호출문은 당장 깨지지 않게 유지

### 이번 브랜치가 아직 해결하지 않는 것

- 저장 실패 시 UI
- 재시도
- 롤백
- Dirty autosave
- "저장 성공 후에만 이벤트 발행"

즉, **도로를 먼저 깔았고, 신호등은 아직 없다**고 보면 된다.

---

## 3. 이번 구현의 의도 (선행 적용 이유)

### 선택한 방향

```csharp
SaveResult Save(SaveData data);
```

### 왜 `TrySave()`를 따로 만들지 않았나

기존 정책 문서(`Immediate_Save_and_Dirty_Policy.md`)는 장기적으로 아래를 제안했다.

```csharp
void Save(SaveData data);              // 임시 호환 wrapper
SaveResult TrySave(SaveData data);     // 새 계약
```

이번 브랜치는:

- API 이름을 하나만 유지하고
- 반환형만 `SaveResult`로 바꿔
- **가장 작은 diff**로 결과 계약을 도입했다

C# 특성상 기존 호출부는 반환값을 무시해도 컴파일되므로,  
"결과 타입 추가"와 "모든 호출부 즉시 수정"을 분리할 수 있다.

### 이 선택의 trade-off

| 장점 | 단점 |
|---|---|
| API 이름이 하나라 혼란이 적음 | `ISaveService` 구현체는 전부 시그니처 수정 필요 |
| 호출부를 단계적으로 이전 가능 | `void Save()` 호환 wrapper 없음 |
| 실패 분류가 바로 보임 | 다른 브랜치의 Mock/Fake와 충돌 가능 |

---

## 4. SaveData 반환 타입 논의 포인트

회의에서 아래 4가지를 나눠서 결정하면 된다.

### A. Save() 반환 타입

| 옵션 | 설명 |
|---|---|
| **A-1. `SaveResult Save(...)` 유지** | 이번 브랜치 방향. 단일 계약 |
| **A-2. `void Save(...)` + `TrySave(...)`** | 기존 정책 문서 방향. 점진 이전 |
| **A-3. 다른 이름/형태** | 예: `bool TrySave`, Result struct, async 등 |

### B. Load()도 결과 타입이 필요한가

현재:

```csharp
SaveData Load(); // 실패 시 새 게임 데이터 반환
```

논의 질문:

- Load 실패를 호출자가 구분해야 하는가?
- "복구된 새 게임"과 "정상 로드"를 UI에서 다르게 보여줄 것인가?

이번 브랜치는 **Save만** 건드렸다.

### C. 언제부터 SaveResult를 검사할 것인가

| 단계 | 범위 |
|---|---|
| 1단계 (지금) | API/구현체만 추가, 호출부는 무시 가능 |
| 2단계 | 중요 저장 호출부만 검사 |
| 3단계 | 이벤트 타이밍을 저장 성공 후로 이동 |
| 4단계 | Dirty autosave / retry / rollback |

### D. 저장 실패 후 게임 행동

| 실패 종류 | 예시 대응 (논의용) |
|---|---|
| `InvalidData` | 저장 시도 중단, 개발자 로그 |
| `SerializationFailed` | 롤백 또는 재시도 불가 처리 |
| `WriteFailed` | 재시도 가능 여부 검토 |
| `Unknown` | 보수적으로 실패 처리 |

---

## 5. 현재 Save() 호출 위치 (영향 범위)

이번 브랜치 기준, `Save()`를 직접 호출하는 대표 위치:

| 영역 | 파일 | 호출 맥락 |
|---|---|---|
| Framework | `FrameworkRoot.cs` | 새 게임, 종료, 타이틀 복귀 |
| Framework | `TradeProgressCoordinator.cs` | 진행 저장, 오프라인 진행, 정산 |
| Framework | `TradeStartService.cs` | 무역 출발 즉시 저장 |
| Framework | `FrameworkDebugCommands.cs` | 디버그 저장 |
| Framework | `TradeStartDebugHarness.cs` | 디버그 저장 |
| UI/Progression | `MarketInventoryIntegration.cs` | 구매 draft/commit 등 |
| Editor Test | `MarketInventoryIntegrationProbe.cs` | MemorySaveService |

`ISaveService` 구현체:

- `JsonSaveService` (본 구현)
- `MemorySaveService` (Editor probe)

---

## 6. 역할별 — 앞으로 각자 어떻게 수정하면 되는가

아래는 **팀이 `SaveResult Save(...)`를 승인했을 때**의 실무 가이드다.  
최종 API가 바뀌면 이 표도 같이 수정해야 한다.

### Framework (천성욱 / CoreServices)

**지금 할 일 (이번 PR)**

- `ISaveService`, `JsonSaveService`, 테스트용 Memory 구현체 반영
- 컴파일 유지

**다음 단계**

1. 중요 저장 진입점부터 `SaveResult` 검사
   - `FrameworkRoot.StartNewGame`
   - `TradeStartService.TryStartTrade`
   - settlement/claim 확정 지점
2. 저장 실패 시 scene 전환/이벤트 발행을 막는 guard 추가
3. (정책 확정 후) retry / rollback / queue 도입

**수정 예시**

```csharp
var result = SaveService.Save(CurrentSaveData);
if (!result.Succeeded)
{
    FrameworkLog.Error($"Save failed: {result.FailureReason} {result.Message}");
    // scene 전환 또는 success event 중단
    return;
}
```

---

### Core / Trade Loop (YHY 등)

**영향 받는 흐름**

- 무역 출발
- 진행률 저장
- 오프라인 완료/정산

**수정 방향**

1. runtime 상태를 먼저 바꾸는 코드는, 저장 성공 전에 success를 확정하지 않기
2. 특히 아래는 "저장 성공 = 행동 성공"으로 묶는 후보
   - 출발 확정
   - 정산 확정
   - claim 완료

**당장 급하지 않은 것**

- 단순 진행률 중간 저장 (`CheckProgressAndCompletion` 경로)  
  → 정책상 Dirty 후보일 수 있음. 즉시 저장 필수 목록과 같이 논의

---

### UI / Market / Cargo (JJH, LJH 등)

**영향 받는 파일**

- `MarketInventoryIntegration.cs`
- `CargoLoadingPanelController.cs` (SaveService 사용 여부 확인 필요)

**수정 방향**

1. `PersistDraft`, `Commit`처럼 **돈/재고가 바뀐 뒤 Save()** 하는 메서드부터 결과 검사
2. 실패 시:
   - 성공 UI/토스트 표시 금지
   - 화면 상태를 저장 전으로 되돌리거나 재시도 안내
3. Unity Button 직렬화 콜백은 시그니처 유지 가능. 내부 서비스 호출부만 수정

**주의**

UI는 플레이어에게 `result.Message`를 그대로 보여주지 않는다.  
별도 사용자용 문구가 필요하다.

---

### Progression / Economy (JJH 등)

**영향**

- 성장 구매, 수리, 기부, 투자, 대출 등 "즉시 저장 필수" 후보 기능

**수정 방향**

1. `Immediate_Save_and_Dirty_Policy.md`의 즉시 저장 목록과 맞춰 우선순위 정하기
2. 각 커맨드에서:
   - 변경 전 snapshot (정책 확정 후)
   - `SaveResult` 검사
   - 실패 시 rollback
   - 성공 후 event/UI refresh

**지금 당장**

- 아직 progression 쪽에서 `Save()` 직접 호출은 적거나 없을 수 있음
- Framework command/service 경유 설계 시 **SaveResult를 위로 전달**할지 contract부터 정하기

---

### Village / Player / Content Tools (YHY, 기타)

**영향**

- 건물/플레이어 상태를 SaveData에 쓰는 기능
- Debug harness, Editor E2E test

**수정 방향**

1. 자체 `ISaveService` Mock/Fake가 있으면 `SaveResult Save(...)`로 맞추기
2. Editor 테스트는 실패 케이스 preset 추가 검토 (`Save_Recovery_Test_Matrix.md`)

---

## 7. 구현체 작성자 체크리스트

`ISaveService`를 직접 구현하는 사람은 아래만 맞추면 된다.

```csharp
public SaveResult Save(SaveData data)
{
    if (data == null)
        return SaveResult.Failure(SaveFailureReason.InvalidData, "...", nameof(SaveData));

    // ... 저장 로직 ...

    return SaveResult.Success();
}
```

- null → `InvalidData`
- 직렬화 실패 → `SerializationFailed`
- IO 실패 → `WriteFailed`
- 그 외 → `Unknown`
- 예외를 밖으로 던지지 않을지, 팀 정책에 따름 (현재 JsonSaveService는 던지지 않음)

---

## 8. 호출부 작성자 체크리스트

### 지금 (1단계)

- [ ] 컴파일만 되면 OK
- [ ] `saveService.Save(data);` 그대로 둬도 됨

### 중요 기능 이전 시 (2단계)

- [ ] `var result = saveService.Save(data);`
- [ ] `result.Succeeded` 확인
- [ ] 실패 시 success UI / scene transition / event 발행 중단
- [ ] 필요하면 rollback (정책 확정 후)

### 피해야 할 패턴

```csharp
// 나쁜 예: SaveResult 추가 후에도 여전히 위험
MutateSaveData();
saveService.Save(saveData);
ShowSuccessPopup();
RaiseSuccessEvent();
```

```csharp
// 좋은 예: 2단계 이후 목표
MutateSaveData();
var result = saveService.Save(saveData);
if (!result.Succeeded)
{
    HandleSaveFailure(result);
    return;
}
ShowSuccessPopup();
RaiseSuccessEvent();
```

---

## 9. 회의 안건 제안 (30~45분)

1. **5분** — 현재 문제 공유 (`void Save` 한계)
2. **10분** — 이번 브랜치 선행 구현 설명 (`SaveResult Save`)
3. **10분** — 최종 API 선택 (`SaveResult Save` vs `TrySave` vs wrapper)
4. **10분** — 2단계 우선순위 (어떤 호출부부터 검사할지)
5. **5분** — Load 결과 타입, retry/rollback은 후속 PR로 미룰지 결정

---

## 10. 결정 후 follow-up 작업

팀이 방향을 정하면 아래를 같은 스프린트/다음 PR로 나누면 된다.

| 순서 | 작업 |
|---|---|
| 1 | `ISaveService` 최종 계약 확정 및 문서 정렬 |
| 2 | 모든 Mock/Fake/다른 브랜치 구현체 merge |
| 3 | 중요 저장 호출부 `SaveResult` 검사 |
| 4 | 실패 UI / 로그 정책 |
| 5 | snapshot / rollback |
| 6 | retry / Dirty autosave |
| 7 | 이벤트 타이밍을 저장 성공 후로 이동 |

---

## 11. 참고 문서

- `Save_Result_API_Implementation_Logic.md` — 이번 구현 상세 흐름
- `Save_Result_API_Implementation_Decision.md` — 정책 제안과의 diff
- `Immediate_Save_and_Dirty_Policy.md` — 장기 저장 정책
- `Framework_API_Event_Inventory.md` — 호출부/이벤트 이전표

---

## 12. 회의 메모란 (현장 작성용)

- 최종 API 결정:
- Load 결과 타입 논의 결과:
- 2단계 우선 호출부:
- wrapper / TrySave 필요 여부:
- 담당자 / 목표 PR:
