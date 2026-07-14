# Save 구조 비교 — 현재 하이브리드 vs ID-only

**작성일:** 2026-07-14  
**담당:** Framework & Integration  
**Feature root:** `Assets/_Project/11.CoreServices/`  
**관련 코드:** `Scripts/Save/SaveData.cs`, `CaravanSaveDataMapper.cs`, `JsonSaveService.cs`  
**관련 가이드:** [`Docs/Guide/Framework_Shared_Game_Data_Guide.md`](../../Guide/Framework_Shared_Game_Data_Guide.md)  
**목적:** 현재 Save 스키마(하이브리드)의 장점·리스크와, ID-only 전환 시 필요한 추가 구현·장점·리스크를 정리한다.

---

## 1. 배경

프로젝트 Save는 Unity `JsonUtility`로 `SaveData` DTO 그래프를 `save_data.json`에 직렬화한다.  
ScriptableObject(SO) 참조 자체는 저장하지 않는다.

공용 데이터 가이드의 권장 패턴은 다음과 같다.

```text
SaveData: ID만 저장
    ↓
Shared Game Data (ISharedGameDataProvider): ID로 정의 조회
    ↓
UI / Core / Progression: 표시·계산
```

가이드 FAQ도 wagon 스펙 전체를 Save에 넣지 말라고 명시한다.  
다만 **현재 caravan 저장 경로**는 아직 그 패턴으로 완전 전환되지 않았다.

### 현재 실제 구조: 하이브리드

| 영역 | 저장 방식 | 예시 |
|------|-----------|------|
| 월드·무역 식별 | ID만 | `currentTownId`, `activeRouteId`, `currentSeasonId` |
| cargo / wagon / animal / mercenary | ID(또는 이름) + 정의값 스냅샷 | `TradeItemSaveData.weight`, `basePrice`, `WagonSaveData.maxLoad` |
| 여정·정산·시간 | 동적/확정 스냅샷 | `progress01`, `pendingSettlement`, `inGameTimeMultiplierAtStart` |

cargo DTO 예 (`SaveData.cs`):

- `itemId`, `itemName`, `weight`, `basePrice`, `maxCount` + `quantity`

즉 **ID도 있고, SO에서 왔을 법한 정적 필드도 Save에 복제**되어 있다.

이 구조가 생긴 주된 이유는 다음과 같다.

1. Core 런타임(`CaravanData`, `imsiTradeItemData` 등)이 SO가 아닌 plain 값 객체다.
2. `CaravanSaveDataMapper`가 runtime ↔ Save를 **필드 1:1 복사**한다.
3. SO/`UnityEngine.Object`를 save JSON에 넣지 않는다는 팀 규칙이 있다.
4. Shared Game Data catalog는 이후 추가된 ID lookup 층이라, caravan 저장 경로가 아직 resolve로 바뀌지 않았다.
5. M0~M2에서 마이그레이션 없이 빠르게 무역 루프를 연결하는 쪽이 우선이었다.

---

## 2. 현재 버전(하이브리드) — 장점

### 2.1 복원 단순성

- caravan 표시·적재 계산에 필요한 weight/maxLoad 등이 JSON에 이미 있다.
- 로드 직후 Shared Game Data resolve 없이도 mapper만으로 runtime을 채울 수 있다.
- Core `imsi*` 모델과 Save DTO가 대응되어 변환 로직이 짧다.

### 2.2 진행 중 세션 안정성

- 출발 후 보유 cargo의 weight/price가 Save에 고정되면, 밸런스 SO를 핫픽스해도 **이미 진행 중인 무역 계산이 갑자기 바뀌지 않는다**.
- 의도적으로 스냅샷하는 필드와 성격이 맞다.
  - `inGameTimeMultiplierAtStart`
  - `PendingSettlementSaveData` (확정 정산 결과)

### 2.3 디버깅·자기완결 JSON

- `SaveDataDebugPrinter` / raw JSON만으로 이름·무게·가격을 바로 확인할 수 있다.
- catalog 누락 ID만으로는 “무엇을 들고 있었는지” 추적이 어렵다. 현재는 스냅샷 필드가 보조 정보가 된다.

### 2.4 초기 통합 비용

- SharedGameData 로드 순서, ID 삭제 fallback, 마이그레이션 정책을 먼저 만들지 않아도 된다.
- 현재 `JsonSaveService`는 version 불일치 시 마이그레이션 없이 새 게임으로 복구한다. 임베드 스키마는 그 전제와 잘 맞았다.

---

## 3. 현재 버전(하이브리드) — 리스크

### 3.1 가이드·설계 의도와의 불일치

- Shared Game Data 가이드는 “Save에는 ID만, 스펙은 Shared Game Data”를 권장한다.
- caravan 경로는 권장과 달라, 팀원마다 “무엇을 Save에 넣어야 하는지” 해석이 갈릴 수 있다.

### 3.2 SO와 Save 정의값 드리프트

- SO에서 Apple weight를 바꿔도, 이미 저장된 cargo는 옛 weight를 유지한다.
- “밸런스 패치가 즉시 반영되어야 한다”는 요구와 충돌한다.
- 반대로 의도치 않은 드리프트인지, 출발 고정인지가 문서화되어 있지 않다.

### 3.3 스키마·중복 유지 비용

- SO에 필드가 추가될 때마다 Save DTO / mapper / runtime `imsi*`에 같은 필드를 복제할 압력이 생긴다.
- 진실의 원천이 SO와 Save 양쪽에 있어 불일치 버그 여지가 있다.

### 3.4 파일 크기·노이즈

- cargo 라인마다 name/weight/price를 반복 저장한다.
- 시장 재고 등 라인 수가 늘면 JSON이 커지고, “동적만 바뀌었는데 정적 필드도 같이 직렬화”되는 노이즈가 커진다.

### 3.5 ID와 스냅샷의 이중 진실

- `itemId`로 Shared Game Data를 조회한 결과와 Save에 박힌 `weight`/`basePrice`가 다르면, 어느 쪽을 쓸지 호출부마다 달라질 수 있다.
- 현재 mapper는 Save 스냅샷을 runtime에 그대로 넣는다. SharedGameData lookup과 혼용 시 버그 위험이 있다.

---

## 4. ID-only 목표 모델

Save에는 **식별자 + 인스턴스 동적 값**만 두고, 정의값은 인게임에서 SO(Shared Game Data)로 채운다.

```text
예시 (목표)
CargoEntrySaveData  { itemId, quantity }
WagonSaveData       { wagonId, /* currentDurability 등 동적만 */ }
AnimalSaveData      { animalId }
MercenarySaveData   { mercenaryId, contractCount }

로드 시
Save.itemId → ISharedGameDataProvider.TryGetTradeItem → runtime weight/name/price 채움
```

### 필드 성격 구분 (전환 시 필수 정책)

| 종류 | 예 | ID-only에서의 처리 |
|------|----|-------------------|
| 정의(밸런스) | weight, basePrice, maxLoad, speed | Save에 넣지 않음. SO resolve |
| 인스턴스 동적 | quantity, durability, foodAmount, contractCount | Save에 유지 |
| 확정 스냅샷 | pending settlement, 출발 시 time multiplier, (선택) 출발 고정 단가 | 의도적으로 Save 스냅샷 유지 |
| 생성형 월드 | market seed/refresh/stock quantity | ID + 동적 (시장 재고 요청안과 동일 계열) |

**주의:** “모든 것을 ID-only”가 아니다. 이미 확정된 정산·출발 계약성 값까지 SO로 되돌리면 안 된다.

---

## 5. ID-only 적용을 위해 추가 구현해야 하는 항목

### 5.1 정책·스키마

1. **정의 vs 동적 vs 확정 스냅샷** 필드 표 확정 (팀 합의)
2. `SaveData` / `TradeItemSaveData` / `WagonSaveData` / `AnimalSaveData` / `MercenarySaveData`에서 정의 필드 제거 또는 폐기
3. `SaveData.CurrentVersion` bump + **마이그레이션 정책** (현재는 version 불일치 시 새 게임)
4. wagon/animal/mercenary도 `wagonName` 중심이 아니라 **안정적인 string ID**로 통일

### 5.2 Resolve 파이프라인

1. `CaravanSaveDataMapper` (또는 전용 Resolver)에 Shared Game Data 의존 추가
2. Load 경로: Save ID → `TryGetTradeItem` / `TryGetWagon` / … → `imsi*` / `CaravanData` 채움
3. Save 경로: runtime에서 **ID + 동적만** 기록 (정의 필드 재직렬화 금지)
4. `FrameworkRoot` 로드 순서 보장: **SharedGameData IsLoaded 이후** caravan restore

### 5.3 실패·호환 처리

1. ID 없음 / catalog 미등록 시 동작 정의
   - 해당 cargo 제거 vs 플레이스홀더 vs InGame 차단
2. SO 필드 rename/타입 변경 시 runtime 매핑 유지
3. 기존 하이브리드 JSON 마이그레이션
   - 옛 weight/price는 버리고 ID만 살릴지
   - ID가 비어 있고 name만 있는 레거시 처리
4. 디버그 하네스·Editor 테스트가 넣는 dummy cargo도 ID + catalog 등록 전제으로 정리

### 5.4 진행 중 무역 정책 (별도 결정 필요)

1. Traveling 중 SO 밸런스 변경을 **즉시 반영**할지, **출발 시점 고정**할지
2. 고정을 택하면 “출발 시 정의 스냅샷”을 Save에 남기거나, 출발 시 별도 snapshot DTO를 둔다
   - 이 경우 순수 ID-only가 아니라 **하이브리드의 의도적 축소판**이 된다
3. SettlementPending의 `PendingSettlementSaveData`는 ID-only 대상에서 **제외** (이미 확정 결과)

### 5.5 호출부·문서 정리

1. UI/Core/Progression이 Save 스냅샷 필드를 직접 읽는지 점검 후 SharedGameData lookup으로 통일
2. `Framework_Shared_Game_Data_Guide`와 실제 Save 스키마 설명 일치
3. 시장 재고 등 신규 DTO는 처음부터 ID + 동적만 쓰도록 맞춤 (이중 저장 방지)

### 5.6 검증

1. Continue → cargo/wagon resolve 성공
2. catalog에서 ID 제거 시 실패 경로
3. 밸런스 SO 변경 후 재로드 시 반영/비반영이 정책과 일치하는지
4. Traveling / SettlementPending / Claim 회귀
5. version migration 또는 의도적 wipe 동작

---

## 6. ID-only — 장점

### 6.1 단일 진실 원천

- 무게·가격·적재 한도 등 정의는 SO/Shared Game Data만 본다.
- Save와 SO가 어긋나는 드리프트가 줄어든다.

### 6.2 밸런스 패치 반영

- 패치 후 재접속 시 보유 아이템 정의가 최신 SO를 따른다.
- “저장 파일에 박힌 옛 수치” 때문에 패치가 무시되는 문제를 줄인다.

### 6.3 스키마 단순화

- Save는 진행 상태 중심으로 얇아진다.
- SO에 표시용 필드가 늘어도 Save DTO를 매번 키울 필요가 없다.
- 가이드·시장 재고(`itemId` + quantity) 패턴과 정렬된다.

### 6.4 팀 계약 명확화

- “정의는 Shared Game Data, 진행은 Save”라는 경계를 feature 간에 공통으로 쓰기 쉽다.

---

## 7. ID-only — 리스크

### 7.1 로드 순서·의존성

- SharedGameData가 준비되기 전 caravan을 restore하면 빈 정의/실패가 난다.
- catalog drift·미등록 SO가 곧 세이브 복원 실패로 이어진다.

### 7.2 ID 수명 주기

- 아이템/마차 ID 삭제·개명 시 기존 세이브가 깨진다.
- fallback·마이그레이션·와이프 정책이 없으면 Continue가 불안정해진다.

### 7.3 진행 중 무역의 소급 변경

- Traveling 중 weight/price가 SO 패치로 바뀌면 이동 시간·정산 전제가 흔들릴 수 있다.
- 스냅샷 정책을 따로 두지 않으면 “공정성/재현성” 이슈가 된다.

### 7.4 구현·이행 비용

- mapper, 로드 순서, 마이그레이션, 테스트, 디버그 도구를 한꺼번에 손봐야 한다.
- 현재 version 정책(불일치 시 새 게임)만으로는 유저 세이브 보호가 약하다. ID-only 전환 시 마이그레이션 또는 명시적 wipe 공지가 필요하다.

### 7.5 디버깅 가독성

- JSON만 보면 이름·수치가 없어, 문제 재현 시 Shared Game Data/catalog를 같이 봐야 한다.
- ID만 남은 orphan 엔트리는 원인 추적이 더 어렵다.

### 7.6 Core `imsi*` 모델과의 간극

- Core는 여전히 값 복사를 전제로 한다.
- Resolve를 Framework mapper에만 두면 Core 단독 테스트/샌드박스는 별도 시드가 필요하다.

---

## 8. 비교 요약

| 항목 | 현재 하이브리드 | ID-only (+ 필요 시 확정 스냅샷만 유지) |
|------|-----------------|----------------------------------------|
| 진실의 원천 | Save에 정의 복제 가능 | SO / Shared Game Data |
| 밸런스 패치 | 보유분 옛 값 유지 가능 | 재로드 시 최신 반영(기본) |
| 진행 중 무역 안정 | 유리(사실상 고정) | 정책 없으면 소급 변경 위험 |
| 로드 의존성 | caravan만으로 일부 복원 가능 | SharedGameData·catalog 필수 |
| 스키마 비용 | 정의 필드마다 Save 증가 | Save는 얇음 |
| 가이드 정합 | 부분 불일치 | 일치 |
| 전환 비용 | 이미 동작 중 | resolve·정책·마이그레이션 필요 |

---

## 9. 실무 권고 (개인 정리)

1. **신규 데이터**(시장 재고 등)는 처음부터 `ID + 동적`만 저장한다.
2. **확정 결과**(pending settlement, 출발 시 time multiplier)는 계속 스냅샷한다.
3. caravan의 weight/price/maxLoad 임베딩은  
   - 단기: “출발/보유 스냅샷”인지 “임시 중복”인지 팀 문서로 못 박고  
   - 중기: ID-only + (Traveling 중 고정이 필요하면) **출발 시점 전용 snapshot**으로 분리한다.
4. ID-only 전환은 “DTO에서 필드 삭제”만으로 끝나지 않는다.  
   **Resolve 파이프라인 + ID 수명 정책 + 진행 중 무역 고정 정책 + version/migration**이 세트다.

---

## 10. 관련 파일

| 경로 | 역할 |
|------|------|
| `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs` | 현재 Save 스키마 (v5) |
| `Assets/_Project/11.CoreServices/Scripts/Save/CaravanSaveDataMapper.cs` | runtime ↔ Save 복사 |
| `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs` | JSON 로드/저장, version 검사 |
| `Assets/_Project/11.CoreServices/Scripts/Save/PendingSettlementSaveData.cs` | 확정 정산 스냅샷 |
| `Assets/_Project/01.Core/04_TradeLoop/YHY/TradeDataDraft.cs` | `imsi*` 값 객체 |
| `Assets/_Project/01.Core/05_Caravan/YHY/CaravanData.cs` | caravan runtime |
| `Docs/Guide/Framework_Shared_Game_Data_Guide.md` | ID-only 권장 가이드 |
| `Docs/Personal_Documents/JJH/0714_Progression MarketInventory_Change_Request.md` | 시장 재고 ID+동적 저장 요청 |

---

## 11. 후속 액션 (미정)

- [ ] 정의/동적/확정 스냅샷 필드 표 팀 합의
- [ ] Traveling 중 SO 변경 반영 여부 결정
- [ ] ID-only 전환 시 version bump + migration vs wipe 결정
- [ ] caravan mapper resolve 설계 초안 (별도 문서/PR)
