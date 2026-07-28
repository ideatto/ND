# 결정론 날씨 + 현실적 구름 모델 (0728)

> 브랜치 `feature/Core-grid-weather-event-YHY` (feature/Core-minimap-grid-YHY의 `2bbe013`에서 분기).
> 목적: 그리드 지역 이벤트를 얹기 위한 **재현 가능한(offline-safe) 날씨**로 구름 SIM을 바꾸고, 동시에 **더 현실적인** 구름 거동으로 튜닝.

## 1. 왜 결정론인가 (그리드 이벤트 전제)

- 기존 **루트 이벤트**(산적/행운 = 거리 기반 랜덤, `TradeRouteEventProcessor` 결정론 엔진)는 **그대로 유지**.
- 그 위에 **"루트가 지나는 셀 × 시간(날씨)" 그리드 이벤트를 추가**할 계획.
- 캐러밴은 **오프라인**으로 여행 → 복귀 시 "그때 그 셀에 비가 있었나?"를 계산해 이벤트를 줘야 함.
- 그런데 구름이 매번 랜덤이면 껐다 켤 때마다 딴 모습 → 재현 불가.
- **결정론적 카오스**로 해결: 씨앗만 같으면 똑같이 재생(스타크래프트 리플레이 원리). 카오스(뭉침·비)는 100% 유지.

### 접근 3안 중 ②채택
1. 시드 노이즈 필드 — 싸지만 지금의 창발(뭉침→비) 거동 사라짐.
2. **고정스텝 결정론 리플레이** ← 채택. 지금 에이전트 SIM을 그대로 두되 랜덤·시간만 결정론화. 카오스 손실 0.
3. 레이어 분리(연출≠판정) — 눈요기와 판정 어긋날 수 있음.

## 2. Phase1 결정론화 (완료)

| 바꾼 것 | 전 → 후 |
|---|---|
| 난수 | `UnityEngine.Random` → **`DetRng`**(xorshift32 시드 난수 "번호표"). `DetRng.Seed(worldSeed, step, salt)` |
| 시간 | `Time.deltaTime`(가변) → **고정스텝 `fixedDt=0.1`**("똑딱시계"). `acc`에 누적→fixedDt 단위로 잘라 전진 |
| 바람·구름 박자 | 각자 `Update` → **구름이 매 스텝 `wind.StepSim(dt)`→`StepClouds(dt)` 순서로 몰기** |

- `DetRng.cs` 신설(순수 도구). 스폰마다 `new DetRng(Seed(worldSeed, simStep, spawnCounter))`로 구름별 고유·재현 씨앗.
- `MinimapWind`: 시간전진을 `public StepSim(float dt)`로 분리. `Update`는 `externallyDriven`이면 정지(구름이 몲), 아니면 자체 실시간(디버그 화살표용). `SetSeed`/`SetExternallyDriven`/`Season` 추가. ambient·이벤트지터도 DetRng.
- `MinimapClouds`: `Update`=똑딱시계 구동부(guard 500 폭주방지), 실제 처리는 `StepClouds`. `simStep`/`spawnCounter`/`acc` 추가. 켤 때 `wind.SetSeed(worldSeed)`+`SetExternallyDriven(true)`, 끌 때 false.
- **검증**: 같은 씨앗(12345) 두 번 → 구름 위치·크기·수명 완전 동일. 다른 씨앗 → 다른 구름. (execute_code 미러링)
- `worldSeed=12345` 기본(구름·바람 공유).

## 3. 현실적 날씨 모델 (0728)

### 문제
- 구름이 **강 위에서 생기고 강 위에서 즉사**(반복). 원인: 운반 조건이 "시간"이라 제자리에서도 채워짐.
- **모든 구름이 먹구름**이 됨. 원인: 물생성 과다 + 한번 먹구름이면 안 마름 + 뭉침 자동먹구름 + 강=호수 동급.

### 해결 — 운반 = 실제 이동거리
- `Cloud.carryDist`: 먹구름 상태로 **실제 이동한 거리**만 누적(물 만나면 0 리셋). 제자리 구름은 안 쌓임 → 강 위 즉사·제자리 반복 방지.
- 비 발동: `moisture≥darkT && (carryDist≥carryDistance(2.5u) || age≥life)`. **수명 끝엔 반드시 방출**(불멸 방지).

### 해결 — 현실적 다양성 A~D
- **A** 물 위 생성 `waterSpawnChance` 0.55→0.2 (대부분 가장자리 흰구름).
- **B** 먹구름도 **완만 증발** `darkDryRate=0.02`(흰구름 dryRate=0.1). 비 못 뿌리고 오래 떠돌면 **흰구름으로 소산** → "다 비로 끝" 방지.
- **C** 뭉침 `crowdGain` 0.05→0.02 (북쪽 더미 통째 먹구름화 완화).
- **D** **강<호수**: `WetWeight` 호수(Water)=1.0, 강(River)/다리(Bridge)=0.4. 얇은 강 스친다고 다 먹구름 안 됨.

### 계절 온도 프로파일 (계절=열)
- `ApplySeasonProfile()`가 `wind.Season` 읽어 배율 갱신. 계절과 열을 **온도** 하나로 통합(여름 고온다습 / 겨울 저온건조).

| 계절 | 증발 seEvap | 건조 seDry | 문턱 seThreshAdd | 개수 seSpawnMul | 색 |
|---|---|---|---|---|---|
| 여름 | ×1.5 | ×0.6 | −0.10 | ×1.3 | 비구름(진청회) — 비 자주 |
| 봄/가을 | ×1.0 | ×1.0 | 0 | ×1.0 | 진청회 — 가끔 |
| 겨울 | ×0.5 | ×1.6 | +0.20 | ×0.6 | **눈구름 SnowCol(옅은 회색)** — 비 드묾 |

- 적용: 물충전 `waterGain*seEvap`, 흰구름 마름 `dryRate*seDry`, 먹구름 마름 `darkDryRate*seDry`, 문턱 `darkT=clamp(darkThreshold+seThreshAdd)`, 생성목표 `maxClouds*seSpawnMul`, 색 `seDarkCol`.

## 4. 열대류(오후 소나기) — 보류

- "뜨거운 낮 소나기" 같은 **하루 내 열 반응**은 **낮/밤(time-of-day)** 필요 → 지금 게임엔 없음(`GameTimeService`=시간 배율뿐). **프레임워크 도메인**.
- 지금은 계절 온도로 근사(여름 전체가 비 잘옴). 낮/밤 생기면 얹기.

## 5. 함정 / 제약

- **계절 자동회전 없음**(항상 여름 고정, 디버그 여름/겨울 버튼으로만). 실제 회전은 프레임워크(성욱님/정헌님). 우리는 "온도→날씨" 훅만 만들어 둠 → 회전 붙으면 자동 반영.
- play 중 코드수정=핫리로드로 구름 초기화 → **구름 끄기/보기 토글**로 재생성 후 확인.
- **Weather 효과 API 없음(stub)** → 실제 게임플레이 효과는 Phase3 + 프레임워크 협의.

## 6. 다음(미완)

- **Phase2 캐치업**: `SimulateUpTo(step)` — 미니맵 늦게 열어도 그동안 흘렀을 상태로 점프(렌더 없이 고정스텝 빠르게 감기).
- **Phase3 이벤트 판정**: 캐러밴 셀 도착 시각 → sim 스텝 → 그 셀 비 여부 → `hash(tradeId, checkIndex, cellId)`로 결정론 이벤트. 저장/되감기 상한은 프레임워크 협의.

> 관련: [[0727_wind_system_proto]], [[0727_event_system_audit]]
