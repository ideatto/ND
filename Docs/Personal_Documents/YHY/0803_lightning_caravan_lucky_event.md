# 0803 번개 → 마차 낙뢰(행운) 이벤트

## 기획
비가 오면 번개가 친다. 번개가 **마차가 있는 그리드**를 때리면, 마차가 번개에 맞는다(행운).
행운 효과 = 정산 시 배율 **+10%**. (수치·중첩 규칙은 팀 확정 대기)

## "비 오면 무조건 맞음?" 문제와 결정
번개가 잦으면 비 오는 긴 무역에서 마차가 사실상 항상 맞게 됨 → 행운의 특별함이 사라짐.
→ **연출 번개(자주 번쩍)와 "행운으로 인정되는 명중"을 분리**하고, 명중에 **확률 게이트**를 둔다.
- **이번 구현(사용자 결정)**: 번개가 마차 셀을 때렸을 때 **50% 확률로 명중**, 50%는 **인스펙터 조절**.
- 방식은 (B) 실시간 반응식 — 지금 날씨 번개 시스템에 얹음.

## 구현 (이번 단계 = 명중 판정까지)
파일: `LightningSystem.cs`, `WeatherState.cs` (담당 윤호영, `01.Core/09_Weather`)

- `LightningSystem`:
  - 인스펙터 `caravanHitChance`(0~1, 기본 0.5) 추가.
  - `TryStrike`가 번개 셀을 정한 뒤 `TryStrikeCaravan(target, rng)` 호출.
  - `TryStrikeCaravan`: 이동 중 마차들을 순회 → 각 마차의 **현재 셀**을 미니맵 마커
    (`MinimapMultiCaravanMarkers`)와 **동일 방식**으로 계산(진행도→`RouteVisual.EvaluatePosition`
    →`grid.WorldToCell`) → 번개 셀과 같으면 `rng.Value() < caravanHitChance`로 명중 판정.
    → 어느 마차든/어느 루트든 데이터로 동작(하드코딩 없음).
  - 명중 시: 강조 섬광 FX + `WeatherState.ReportCaravanLightning(caravanId, tradeId)` + 로그.
- `WeatherState`:
  - `ReportCaravanLightning(caravanId, tradeId)` — 연출 펄스 + 이번 세션 임시 기록.
  - `LastCaravanLightningTime` / `LastStruckCaravanId` / `WasTradeStruck(tradeId)` 조회 제공.

## 다음 단계 (아직 안 함)
1. **정산 +10% 적용**: `WasTradeStruck`(또는 영속 기록)을 정산 입력 빌드 시 읽어 배율/이벤트수익 반영.
   - `SettlementCalculator`(순수 계산기, 테스트 있음)는 **경제팀(천성욱님) 소유** → 배율 필드 추가 vs
     기존 `EventProfit`(판매수익 10%) 재사용 중 택1은 **협의 필요**.
2. **영속화**: 현재 명중 기록은 in-memory(리로드/오프라인 미보존). 정산 연동 시 저장으로 승격 필요.
3. **연출**: 트레드밀에서 마차에 낙뢰+"행운!" 표시(`WeatherState.LastCaravanLightningTime` 구독).
4. **밸런스 확정**: 무역당 1회 상한 vs 중첩, 목표 빈도, 발생 조건(폭풍 세기/지형).

## 한계(방식 B 특성)
실시간 반응식이라 인게임에서 무역이 진행되는 동안에만 판정된다(앱 꺼둔 완전 오프라인 무역 제외).
결정론/재현이 필요하면 전투처럼 (A) 결정론 판정으로 전환해야 함(현재는 미채택).

---

# 0803 (갱신) 결정론 낙뢰 행운 + 정산 카운트 — 최종 설계

## 결정 (대화로 확정)
- 낙뢰 행운 판정은 **우리 날씨 시스템 안에서 결정론으로** 한다(실시간 LightningSystem이 아니라).
  → `MinimapWeatherEventDetector`의 **셀 진입 결정론 체크**(비-속도감소와 동일 훅)에 얹음.
- "폭풍 밑?" = 우리 비 세기(`RainIntensityAt` = moisture×scale)가 문턱 이상. "강한 비 vs 약한 비"는 세기로 구분됨.
- 폭풍급이면 → **인스펙터 % 확률(기본 50%)** 로 굴려 행운.
- 행운 횟수는 **우리 자체 저장소**에 무역별 누적 → 정산이 읽어 **+10%×횟수**.
- ★정헌님 save 스키마는 안 건드린다(우리 파일에 따로 저장).

## 구현 (우리 쪽 — 완료·검증)
- **`WeatherLuckyStore`** (신규, 09_Weather): tradeId→count 를 `persistentDataPath/weather_lucky.json`에
  저장. `Add`(누적) / `GetCount`(정산이 읽음) / `Consume`(정산 후 삭제). ※프레임워크 save와 완전 분리.
  - 검증: Add 1→2, GetCount=2, 파일 저장 확인, Consume=0 ✓
- **`MinimapWeatherEventDetector`**:
  - 인스펙터 `enableLightningLucky` / `luckyStormIntensity`(0.4, 번개 minStrikeIntensity와 맞춤) / `luckyChance`(0.5).
  - 셀 진입 체크 블록을 `fireEvents || enableLightningLucky`에서 돌게 리팩터.
  - `EvaluateLightningLucky`: 폭풍급이면 `DetRng(hash(tradeId+"|lucky", checkIndex, cellId))` 결정론 롤 <
    luckyChance → `WeatherLuckyStore.Add(tradeId)` + 화면 알림 + `WeatherState.ReportCaravanLightning`(연출).

## 남은 것
- **정산 +10% 적용(경제/천성욱님, 한 줄)**: 정산 입력 만들 때
  `revenue/price ×= (1 + 0.10 × WeatherLuckyStore.GetCount(tradeId))` 적용 후 `Consume(tradeId)`.
  (돈 계산이 거기라 우리 날씨 코드에서 직접 못 함. 정헌님 파일은 여전히 안 건드림.)
- **중복 정리**: 앞서 커밋한 `LightningSystem`의 실시간 50%(caravanHitChance)는 이제 게임 카운트와 무관
  (저장소에 안 씀). 게임 판정은 detector가 소유하므로, LightningSystem 쪽은 **시각 연출 전용으로 강등** 권장.

## 오프라인 참고
detector는 결정론이지만 실행이 `LateUpdate`(온라인)라, 앱 꺼둔 완전 오프라인 구간은 아직 자동 카운트 안 됨.
판정·저장 모두 결정론/영속이라, 필요 시 오프라인 복원 경로에서 이 체크를 돌리게 연결하면 됨(별도 작업).
