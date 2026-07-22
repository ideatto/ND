# Save Result API 구현 로직 정리

> 정책 상태: 목표 API는 `SaveResult Save(SaveData data)` 단일 계약으로 확정됐다. 아래 내용은 선행 구현의 실제 범위 기록이며, snapshot·rollback·retry·queue 및 호출부 결과 처리가 이미 구현됐다는 뜻은 아니다.

브랜치: `feature/framework/save-result-api`  
작성 기준: 로컬 diff (`ISaveService`, `JsonSaveService`, `MemorySaveService`)  
팀 최종 정책: `SaveResult Save(SaveData data)` 단일 계약

---

## 1. 작업 목적

기존 `ISaveService.Save(SaveData)`는 반환값이 없어서, 호출자가 **저장이 실제로 디스크에 기록됐는지** 알 수 없었다.

이번 변경은 저장 API에 **최소한의 결과 계약**을 추가한다.

- 저장 성공 여부
- 실패했다면 어느 단계에서 실패했는지
- 개발자 로그용 진단 메시지

중요한 점: 이번 브랜치는 **결과를 돌려주는 기반만 추가**한다.  
호출부가 실패를 처리하거나, 저장 실패 UI, 재시도, 롤백은 아직 넣지 않는다.

---

## 2. 변경 전과 후

### 변경 전

```csharp
void Save(SaveData data);
```

- null 입력: 로그만 남기고 조용히 종료
- 직렬화/파일 쓰기 실패: catch 후 로그만 남김
- 호출자는 성공/실패를 구분할 방법 없음

### 변경 후

```csharp
SaveResult Save(SaveData data);
```

- 각 실패 지점마다 `SaveResult.Failure(...)` 반환
- 성공 시에만 `SaveResult.Success()` 반환
- 예외는 밖으로 던지지 않음 (기존과 동일한 fail-safe 방향)

---

## 3. 새로 추가된 타입

### SaveFailureReason

실패가 **어느 단계**에서 났는지 구분하는 enum이다.

| 값 | 의미 |
|---|---|
| `None` | 성공 |
| `InvalidData` | 입력 `SaveData`가 null |
| `SerializationFailed` | `JsonUtility.ToJson` 실패 |
| `WriteFailed` | `File.WriteAllText` 실패 |
| `Unknown` | 정규화/메타데이터 갱신 등 기타 예외 |

### SaveResult

저장 시도의 결과를 담는 객체다.

| 속성 | 설명 |
|---|---|
| `Succeeded` | 파일 쓰기까지 완료됐을 때만 `true` |
| `FailureReason` | 실패 분류. 성공이면 `None` |
| `Message` | 개발자 진단용 텍스트 (플레이어 UI 문구 아님) |
| `FailedDataCategory` | 실패와 연관된 데이터 범주. 현재는 `"SaveData"` |

팩토리 메서드:

- `SaveResult.Success()`
- `SaveResult.Failure(reason, message, failedDataCategory = null)`

---

## 4. JsonSaveService.Save 흐름

```text
Save(data) 호출
│
├─ data == null ?
│   └─ Yes → InvalidData 실패 반환 + Warning 로그
│
├─ [1단계] NormalizeData + version/lastSavedUtcTicks 갱신
│   └─ 예외 → Unknown 실패 반환 + Error 로그
│
├─ [2단계] JsonUtility.ToJson(data)
│   └─ 예외 → SerializationFailed 실패 반환 + Error 로그
│
├─ [3단계] File.WriteAllText(savePath, json)
│   └─ 예외 → WriteFailed 실패 반환 + Error 로그
│
└─ 모두 성공 → Info 로그 + Success 반환
```

### 단계별 설계 의도

1. **null 차단**  
   null을 그대로 저장하면 기존 파일을 망가뜨릴 수 있어서, 저장 자체를 하지 않고 실패로 돌려준다.

2. **정규화와 메타데이터 갱신 분리**  
   `NormalizeData`, `version`, `lastSavedUtcTicks` 갱신 중 예외는 `Unknown`으로 분류한다.

3. **직렬화와 파일 쓰기 분리**  
   JSON 변환 실패와 디스크 쓰기 실패를 나눠야, 이후 재시도/복구 정책을 다르게 설계할 수 있다.

4. **성공 로그는 파일 쓰기 후에만**  
   이전에는 try 블록 안에서 직렬화 직후 로그가 찍힐 수 있었지만, 지금은 **실제 쓰기 성공 후**에만 성공 로그와 `Success()`를 반환한다.

---

## 5. 기존 호출부와의 관계

C#에서는 반환값을 받지 않아도 컴파일된다.

```csharp
saveService.Save(saveData); // 기존처럼 그대로 가능
```

그래서 이번 브랜치는:

- `FrameworkRoot`, `TradeProgressCoordinator`, `TradeStartService`, `MarketInventoryIntegration` 등 **기존 호출 순서는 유지**
- 호출부는 **아직 `SaveResult`를 검사하지 않음**

즉, 이번 PR은 **API 계약 확장 + 구현체 반영**이 핵심이고,  
소비자 쪽 fail-safe 처리는 다음 단계 작업이다.

---

## 6. 함께 수정된 구현체

| 파일 | 변경 내용 |
|---|---|
| `ISaveService.cs` | `SaveFailureReason`, `SaveResult` 추가. `Save` 반환형 변경 |
| `JsonSaveService.cs` | 단계별 실패 분류 및 `SaveResult` 반환 |
| `MarketInventoryIntegrationProbe.cs` | 테스트용 `MemorySaveService.Save`가 `SaveResult.Success()` 반환 |

---

## 7. 이번 브랜치에서 하지 않은 것

- `TrySave()` 별도 API
- `void Save()` 호환 wrapper
- 저장 실패 시 UI 표시
- 재시도, 큐, Dirty autosave
- Snapshot / Rollback
- 중요 저장 호출부의 `SaveResult` 검사
- 저장 성공 후에만 이벤트를 내는 타이밍 전환
- Save Version 6

---

## 8. Load()와 Save()의 비대칭

현재 `Load()`는 여전히 `SaveData`를 반환한다.

| API | 반환 | 실패 시 동작 |
|---|---|---|
| `Load()` | `SaveData` | 새 게임 데이터로 복구 (fail-open) |
| `Save()` | `SaveResult` | 실패 결과 반환 (예외 없음) |

이 차이는 의도적이다.

- **Load**는 게임을 시작할 수 있게 하는 쪽이 우선
- **Save**는 "저장됐다"고 착각하지 않게 하는 쪽이 우선

팀 회의에서 `Load()`도 결과 타입이 필요한지는 별도 논의 대상이다.

---

## 9. 관련 문서

- `Save_Result_API_Implementation_Decision.md` — 기존 `TrySave()` 제안과의 차이
- `Immediate_Save_and_Dirty_Policy.md` — 장기 저장 정책 (Dirty, 재시도, 즉시 저장)
- `Framework_API_Event_Inventory.md` — 호출부/이벤트 이전 인벤토리

---

## 10. 확인 방법

1. Unity에서 컴파일 오류 없는지 확인
2. 저장 성공 시 Console에 `Save data written: ...` 로그 확인
3. null 저장 시 `Save was skipped because data is null.` Warning 확인
4. (선택) 임시 코드로 `var result = saveService.Save(data);` 후 `result.Succeeded` 로그 출력

Unity Editor 런타임 전체 회귀 테스트는 이 문서 작성 시점에 아직 수행하지 않았다.
