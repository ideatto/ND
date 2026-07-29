# Caravan Activity Log Framework 연동 구현 로직

## 브랜치 정보

- 브랜치: `feature/framework/active-log-framework-integration`
- 베이스: `dev2`
- HEAD (커밋): `573c74d` — `feat(framework): add caravan activity log save contract, record caravan trade activity lifecycle, add caravan activity log normalization and identity tests`
- 작업 트리 추가분: UI 스크립트·Editor Prefab Builder 활성화, Prefab 생성물

## 목적

무역 루프 중 Caravan의 **출발 / 전투 조우 / 도착** 이력을 SaveData에 남겨, QoL UI(`CaravanActivityLogPanel`)가 선택 캐러밴과 무관하게 전역 순서로 표시할 수 있게 한다.

이번 구현의 목표는 다음과 같다.

1. `SaveData.caravanActivityLogs` 저장 계약과 `CaravanActivityLog` 헬퍼를 활성화한다.
2. 출발·루트 이벤트(전투)·도착 시점에 엔트리를 기록한다.
3. 저장 실패 시 활동 로그 스냅샷을 롤백한다.
4. 로드/정규화 시 목록 null 방지 및 최대 100건 trim을 보장한다.
5. UI는 SaveData를 폴링해 표시하며, Prefab은 Editor 메뉴로 생성한다 (Scene/MainUICanvas 배치 제외).

---

## 변경 파일

### Framework (커밋 `573c74d`)

| 영역 | 파일 | 역할 |
|------|------|------|
| Save 계약 | `Scripts/Save/SaveData.cs` | `caravanActivityLogs`, `CaravanActivityLogEntrySaveData`, `CaravanActivityLogType` |
| 헬퍼 | `Scripts/TradeProgress/CaravanActivityLog.cs` | Add / Remove / TrimToLimit (주석 해제) |
| 정규화 | `Scripts/Save/JsonSaveService.cs` | NormalizeData에서 null 생성 + TrimToLimit |
| 출발 기록 | `Scripts/TradeProgress/TradeStartService.cs` | Departure 기록 + 저장 실패 롤백 |
| 진행/도착 | `Scripts/TradeProgress/TradeProgressCoordinator.cs` | CombatEncounter / Arrival 기록 |
| 테스트 | `Editor/CaravanActivityLogTests.cs` | 5개 EditMode 테스트 활성화 |

### UI / Prefab (작업 트리)

| 영역 | 파일 | 역할 |
|------|------|------|
| Item View | `05.UI/09_QoL/CaravanActivityLog/CaravanActivityLogItemView.cs` | 메시지·슬롯 색 Bind (활성화) |
| Panel | `05.UI/09_QoL/CaravanActivityLog/CaravanActivityLogPanel.cs` | 폴링·스크롤·메시지 포맷 (활성화) |
| Builder | `11.CoreServices/Editor/CaravanActivityLogPrefabBuilder.cs` | Prefab 생성 메뉴 (활성화) |
| Prefab | `.../Prefabs/CaravanActivityLogItem.prefab` | 아이템 프리팹 |
| Prefab | `.../Prefabs/CaravanActivityLogPanel.prefab` | 패널 프리팹 |

의도적으로 건드리지 않은 범위:

- Scene / MainUICanvas 배치
- 선택 캐러밴 필터링
- CombatVictory / CombatDefeat 생산 기록 (enum·UI 표시만 지원)
- 아이템 풀링

---

## 책임 분리

```text
[저장 계약]
SaveData.caravanActivityLogs
  └─ CaravanActivityLogEntrySaveData { sequence, caravanId, tradeId, ... eventType }

[기록 API]
CaravanActivityLog.Add / TrimToLimit
  └─ 호출부: TradeStartService, TradeProgressCoordinator
  └─ 정규화: JsonSaveService.NormalizeData

[표시]
CaravanActivityLogPanel
  └─ FrameworkRoot.CurrentSaveData.caravanActivityLogs 폴링
  └─ CaravanActivityLogItemView.Bind(message, slotColor)
```

핵심 규칙:

- 로그는 **전역 목록**이다. `selectedCaravanId`로 필터하지 않는다.
- 색상·표시 이름은 엔트리의 `caravanId` → `SaveDataLookup` 슬롯으로 결정한다.
- 진행 상태(`tradeProgress`)와 분리된 **UI용 부가 이력**이다.

---

## 저장 데이터 계약

### `CaravanActivityLogEntrySaveData`

| 필드 | 의미 |
|------|------|
| `sequence` | 증가 단조 시퀀스 (정렬·UI 변경 감지) |
| `occurredUtcTicks` | 발생 UTC ticks (미지정 시 `DateTime.UtcNow.Ticks`) |
| `caravanId` | 해당 Caravan 식별자 (필수) |
| `tradeId` / `routeId` / `townId` / `routeEventId` | 선택적 컨텍스트 |
| `eventType` | `Departure` / `CombatEncounter` / `CombatVictory` / `CombatDefeat` / `Arrival` |

### `CaravanActivityLog` 헬퍼

```text
Add(saveData, eventType, caravanId, ...)
  ├─ caravanId 비어 있으면 null 반환 (변경 없음)
  ├─ sequence = max(기존) + 1
  ├─ 목록에 append
  └─ TrimToLimit (기본 100)

TrimToLimit
  ├─ null 엔트리 제거
  ├─ sequence → occurredUtcTicks 정렬
  └─ 오래된 항목부터 제거해 최대 N건 유지
```

---

## 기록 시점과 이벤트 타입

### 1. Departure — `TradeStartService`

호출 경로:

- `DepartInternal`
- `TryStartTrade`

동작:

1. 출발 직전 `caravanSave.currentTownId`를 출발지로 확보한다.
2. 저장 직전 `caravanActivityLogs` **스냅샷**을 복사한다.
3. `CaravanActivityLog.Add(..., Departure, caravanId, tradeId, routeId, destinationTownId)` 호출.
4. `ResolveDestinationTownId(route, departureTownId)`:
   - `route.FromTownId == departureTownId` → `route.ToTownId`
   - 아니면 반대 방향 → `route.FromTownId`
5. Save 실패(또는 null 결과) 시 기존 스냅샷으로 `caravanActivityLogs` 복구.

### 2. CombatEncounter — `TradeProgressCoordinator`

호출 경로:

- `ProcessRouteEvents` (온라인/오프라인 진행)
- `TryProcessForcedRouteEvent` (강제 처리, 저장 실패 시 로그 스냅샷 롤백)

`RecordRouteEventLogs`:

```text
processResult.Occurrences 순회
  ├─ occurrence / definition 없으면 경고 후 skip
  ├─ definition.EventType != Combat 이면 skip
  └─ CaravanActivityLog.Add(..., CombatEncounter, caravanId, tradeId, routeId, routeEventId: eventId)
```

생산 코드는 **CombatEncounter만** 기록한다. Victory/Defeat는 enum·UI 텍스트만 준비되어 있다.

### 3. Arrival — `TradeProgressCoordinator.SettleTrade`

- `result.grade != JourneyResultGrade.Failed` 일 때만 Arrival 기록.
- `townId` = `ResolveArrivalDestinationTownId`:
  1. exact trade prepare commit의 `selectedDestinationTownId` 우선
  2. 없으면 SharedGameData 루트의 `ToTownId`

---

## 정규화 (`JsonSaveService.NormalizeData`)

```text
caravanActivityLogs == null
  → 빈 List 생성

CaravanActivityLog.TrimToLimit(data)
  → 개수가 줄면 assetDataChanged = true
```

로드된 구버전/손상 저장본에서도 UI·기록이 null 참조 없이 동작한다.

---

## UI 표시 로직 (`CaravanActivityLogPanel`)

### 데이터 소스

```text
FrameworkRoot.Instance.CurrentSaveData.caravanActivityLogs
```

약 0.25초(`refreshInterval`) 폴링. `count` + 최신 `sequence`가 같으면 rebuild 생략.

### Rebuild

```text
ClearItems (기존 인스턴스 Destroy)
시작 인덱스 = max(0, Count - maxVisibleEntries)
  → 최근 최대 100건만 Instantiate(itemPrefab)
Bind(FormatMessage, ResolveCaravanColor)
```

### 메시지 포맷

| eventType | 텍스트 요지 |
|-----------|-------------|
| Departure | `{캐러반 N}이(가) {마을}(으)로 무역을 출발` / 마을 없으면 일반 출발 문구 |
| CombatEncounter | `{캐러반 N}이(가) 산적과 전투 중` |
| CombatVictory / Defeat | 승리/패배 문구 (표시 지원, 생산 미기록) |
| Arrival | `{캐러반 N}이(가) {마을}에 도착` / 마을 없으면 목적지 도착 문구 |

이름 해석:

- Caravan: `SaveDataLookup.TryGetCaravan` → `캐러반 {slotIndex+1}`, 실패 시 `캐러반`
- Town: `SharedGameData.TryGetTown` DisplayName, 실패 시 `townId` 원문 또는 빈 문자열

색상: `caravan.slotIndex % caravanColors.Length` (선택 캐러밴과 무관).

### 스크롤 동작

```text
기본: 하단(최신) 고정 (verticalNormalizedPosition ≈ 0, BottomToTop scrollbar)

사용자 스크롤바 조작
  → NotifyScrollbarInteraction
  → 약 5초(autoReturnDelay) 위치 유지
  → ReturnToBottom (약 0.2초, unscaled)

폴링 / 복귀 애니메이션 중 scrollbar 이벤트는 suppress 하여 오인 상호작용 방지
Time.unscaledTime / unscaledDeltaTime 사용 → timeScale 0에서도 유지
```

### Prefab Builder

메뉴: `ND/UI/Create Caravan Activity Log Prefabs`

- Item: Icon + Bubble + TMP, Graphic `raycastTarget = false`
- Panel: ScrollRect + Viewport + Content + Vertical Scrollbar (track/handle raycast true, 배경/뷰포트 false)
- Scene에 배치하지 않음. 제품 Canvas 배치는 Scene Owner 후속 작업.

---

## 전체 흐름

```text
[출발]
TradeStartService.DepartInternal / TryStartTrade
  └─ CaravanActivityLog.Add(Departure)
  └─ Save 실패 시 activityLogSnapshot 복구

[이동 중 루트 이벤트]
TradeProgressCoordinator.ProcessRouteEvents / TryProcessForcedRouteEvent
  └─ RecordRouteEventLogs
       └─ Combat 정의만 CombatEncounter Add

[도착 정산]
TradeProgressCoordinator.SettleTrade
  └─ grade != Failed → Add(Arrival)

[로드]
JsonSaveService.NormalizeData
  └─ null → [] , TrimToLimit(100)

[UI]
CaravanActivityLogPanel.Update 폴링
  └─ Rebuild → ItemView.Bind
```

---

## 테스트

`CaravanActivityLogTests` (EditMode) **5/5**:

| 테스트 | 검증 |
|--------|------|
| `Add_AppendsOrderedEntryWithIdentity` | sequence 증가, town/routeEvent 필드 |
| `Add_TrimsOldestEntriesAtDefaultLimit` | 105 Add → 100 유지, 최신 윈도우 |
| `NormalizeData_CreatesMissingLogContainer` | null → 빈 리스트 |
| `NormalizeData_TrimsOversizedLogToDefaultLimit` | Normalize trim + idempotent |
| `Add_TwoCaravanIdentitiesRemainIndependent` | caravan-a/b 동시 이력 독립 |

---

## 검증 메모 (2026-07-29)

- Prefab 생성·직렬화 참조·raycast 정책: PASS
- 런타임 표시 (empty / 1 / multi-caravan / 100 / append): PASS
- `CaravanActivityLogTests` 5/5: PASS
- Scene / MainUICanvas 미변경: PASS
- 라이브 5초 hold·0.2초 return 타이밍: MCP Play Mode `playmode_transition` 동결로 벽시계 재현 불가 → 로직 경로는 직접 호출로 확인 (CONDITIONAL)

---

## 후속 작업

1. Scene Owner가 `CaravanActivityLogPanel.prefab`을 제품 Canvas에 배치
2. (선택) CombatVictory / CombatDefeat 생산 기록 정책 결정
3. (선택) 대량 갱신 시 아이템 풀링

---

## Related

- 브랜치: `feature/framework/active-log-framework-integration`
- Prefab 메뉴: `ND/UI/Create Caravan Activity Log Prefabs`
- Prefab 경로: `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/Prefabs/`
