# Core(YHY) → 팀 요청 사항 (코드리뷰용)

> 작성: 윤호영 / 2026-07-10
> Core M2 진행 중 발생한, **다른 담당자에게 요청해야 하는 항목** 모음.
> 코드리뷰 때 이 목록으로 요청한다.

---

## 1. 천성욱 (Framework) — revenue/cost/netProfit long 전환

- **배경:** 돈=long 팀 결정에 따라 `JourneyResultData`의 `revenue`/`cost`/`netProfit`을 **long**으로 변경함.
- **현재 임시 처리:** `SettlementUiDataAdapter.CreateViewData`에서 `SettlementViewData(int …)`에 넘길 때 **임시 `(int)` 캐스팅** 넣어 컴파일만 통과시켜 둠 (주석 있음).
  - 파일: `Assets/_Project/11.CoreServices/Scripts/UI/Settlement/SettlementUiDataAdapter.cs`
- **요청:** `SettlementViewData`의 `revenue`/`cost`/`netProfit`(및 생성자 파라미터)을 **long**으로 변경.
- **그 후 Core가 할 일:** 위 어댑터의 임시 `(int)` 캐스팅 제거.
- **이유:** 방치형이라 자금이 21억(int 최대)을 넘을 수 있음 → int로는 값 손실.

### (참고) JourneyRunner.TryDepart 상태 가드 추가
- 이제 **`Prepare` 단계에서만 출발**됨. 이동/정산 중 중복 호출 시 `canDepart=false` + `NotInPrepare` 반환 (무역 재시작 버그 방어).
- **정상 흐름(Prepare 상단에서 출발)은 영향 없음.** `TradeStartService`가 준비된 상단으로 출발하면 그대로 동작.

## 2. 정헌 (Progression) — 속도/식량 공식

Core는 "측정"까지 됨(과적 비율·적재량·시간 계산 완료). 아래 **공식(계수)**만 받으면 "적용"은 바로 함.

- 과적 비율 → 이동속도 보정 공식
- 식량 부족 → 속도 저하 공식
- 최소 이동속도
- 견인 동물 수 보정
- (참고) 단위 인게임 시간당 식량 소모 보정, 전투력 판정 공식/결과

## 3. Content / 천성욱 — 로드 이벤트

- 거리별 로드 이벤트 발생표 (어떤 이벤트가 어디서)
- 이벤트 무효화/강화 데이터 (마차 대응)
- Core는 이벤트 **효과 적용**(`ApplyCargoLoss`/`ApplyDurabilityLoss`/`ResolveRaid`)은 이미 구현 — 발생·일정 데이터만 받으면 됨.

---

## 4. 회의 안건 — 식량 부족 시 속도 저하, 필요한가?

- 식량은 이미 (a) **무게**로 속도에 영향 주고 (소모되면 가벼워짐), (b) **0되면 실패**임.
- 여기에 "굶주림 → 감속"(무게와 무관한 별개 축)을 **또** 넣을지 = 기획 결정.
- 넣으면: 실패 전 위기 경고(방치형 피드백). 빼면: 식량은 "무게+실패"로 이미 충분, 구현 단순.
- **결정 나기 전까지 Core는 이 항목 구현 보류.**

---

## 5. 천성욱 — 식량 소모를 "인게임 시간" 기준으로 (요청 예정, 2026-07-10 논의)

천성욱이 **인게임 시간 배율(TimeScale)** 기능 추가 중. 캐러반 식량 소모를 인게임 시간 기준으로 바꾸려면 `CaravanData`·`CaravanCalculator` 수정 필요. 오늘 양쪽이 파일을 건드려 **병합 충돌 우려 → 천성욱은 자기 코드만, Core 변경은 나중에 윤호영에게 요청**하기로 함.

### 현재 식량 모델 (real-time 기준)
- `GetConsumptionPerSec(caravan)` = 동물 소모율 합 (지금은 **real 초당**)
- `GetRemainingFood` = 실은식량 − 소모율 × (progress01 × **totalSeconds**) − 이벤트차감
- `totalSeconds` = 이동에 걸리는 **real** 시간

### 인게임 기준으로 바꾸는 깔끔한 훅 (이미 설계 예정 — CaravanCalculator 주석 "2단계")
- **핵심 원칙:** Core는 시간을 모르게 유지(progress01만 받음). 인게임 시간 계산은 Framework 몫.
- **제안:** `CaravanData`에 인게임 시간 기준 값 하나 추가 → 천성욱이 출발 때 설정 → Core의 식량 계산이 그 값을 사용. Core 변경은 **필드 1개 + 계산식 2줄** 수준(작음).

### 천성욱에게 확정받을 것 (인터페이스)
식량 시간 기준을 어떤 형태로 넘길지:
- (a) **인게임 총 시간**(inGameTotalSeconds) — 이 무역이 인게임으로 몇 초인지, 출발 때 1회 설정
- (b) **TimeScale 배율**만 — Core가 `totalSeconds × scale`
- (c) 매 틱 **인게임 경과 시간**을 직접 push

→ (a)가 progress01 패턴이랑 제일 일관적. 근데 **천성욱 시간 시스템 구조 보고 확정** (미리 정하지 말 것).

### (참고) 마차 거리 마모 — 오프라인 처리 조건
- Core에 **거리 마모** 추가함: 이동 거리에 비례해 내구도 감소. `JourneyRunner.SetProgress` 안에서 **진행도 delta**로 적용 → 온라인 실시간·오프라인 점프 모두 동일 처리.
- **오프라인 정확 조건:** 재접속 시 Framework가 **`SetProgress(점프된 진행도)`로 진행도를 밀어줘야** 마모가 걸림. `caravan.progress01`을 직접 세팅하면 마모 스킵됨.
- **저장 대상 추가:** `runStartDurability`(출발 시 내구도) — 무역이 오프라인을 걸쳐 끝날 때 정산 손실(=출발−도착)이 맞으려면 저장 필요. (`currentDurability`는 이미 저장 대상)

---

*최종 수정: 2026-07-10*
