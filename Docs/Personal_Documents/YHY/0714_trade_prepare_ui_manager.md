# 무역 준비 UI 매니저 + 정헌님 4·5페이지 통합 (2026-07-14)

- **작성**: 윤호영 (Core Gameplay)
- **브랜치**: `feature/UI/TradePrepare-UIManager-YHY`
- **목적**: ① 정헌님(PR #89)의 물품 적재(④)·용병 고용(⑤) 패널을 무역 준비 플로우에 통합, ② 화면 전환을 총괄하는 **TradePrepareUIManager**(제품 코드) 신설

---

## 1. 바뀐 플로우

```
① 도시+루트 (TownRoutePanel)
② 상단 슬롯 (CaravanSlotPanel)
③ 상단 구성 (AnimalInventoryPanel)        — 웨건+동물
④ 물품 적재 (CargoLoadingPanelController)  ★정헌님, 상점 구매 방식으로 교체
⑤ 용병 고용 (MercenaryHirePanelController) ★정헌님, 신규 삽입
⑥ 요약 → Depart                            — 적재/용병 비용 집계 추가
```

- 기존 ④(ItemLoadPanel 수량 행 방식)는 플로우에서 제외 (씬의 `S4_Item` 비활성. 스크립트 파일은 유지)
- Back 분기: ④→③(구성 경로) / ④→②(저장 슬롯 직행 경로), ⑤→④, ⑥→⑤ (재오픈)
- 무역 취소(④·⑤의 Cancel): 준비 데이터 초기화 후 ①로 복귀

## 2. 신규/변경 파일

| 파일 | 내용 |
|---|---|
| `05.UI/03_TradeSetup/YHY/Panels/TradePrepareUIManager.cs` | ★신규. 화면 전환·상태·데이터 전달 총괄(제품 코드). 데이터는 `Func` 프로바이더 주입 → 데모/실데이터 교체 가능 |
| `05.UI/03_TradeSetup/YHY/Demo/TradePrepareFlowDemo.cs` | 축소. SO→DTO 어댑터 + 프로바이더 주입 + `Begin()` 호출만 (전환 로직 전부 매니저로 이동) |
| `05.UI/03_TradeSetup/YHY/Demo/TradePrepareDemoData.cs` | `gold`(2000)·`requiredFood`(3)·`itemStocks` 필드 추가 (④ 상점용) |
| `Demo/Data/TI_*.asset` (3개) | 더미 아이템에 구매/판매가·스택 값 채움 (Wheat 8/10, Silk 60/75, Ore 25/30, stack 20) |
| `07.Scenes/04_InGame/Test.unity` + `08.Prefabs/UI_TradePrepare.prefab` | S4_Cargo(정헌님 프리팹 중첩)·S5_Mercenary 추가, 매니저 부착, S5_Summary→S6_Summary 이름 정리, S4_Item 비활성 |

**다른 팀원 파일 수정 없음** — 정헌님 스크립트/프리팹은 참조만 (읽기 전용).

## 3. 정헌님 패널 연결 규약 (중요)

- **입력**: ④ 진입 시 매니저가 `Configure(골드, 최대적재량, 필요먹이, 상점아이템[], 재고[])` 호출
  - 최대 적재량 = 웨건 MaxLoad + Σ(동물 IncreaseMaxLoad × 마릿수) — 매니저가 상단 구성에서 계산
- **출력**: `BuildTradeItemBundles()` (TradeItemBundle[]) + 용병 `SelectedCombatPower`/`SelectedHireCost`
- **이벤트**: private UnityEvent라 코드 구독 불가 → **씬 퍼시스턴트 리스너로 연결돼 있음** (프리팹에 포함)
  - `onBackRequested → OnCargoBackRequested` / `onTradeCancelled → OnTradeCancelledByPanels` / `onConfirmed → OnMercenaryConfirmed`
- Cargo의 `mercenaryStepPanel` 인스펙터에 S5_Mercenary 지정 (④→⑤ 전환은 Cargo가 자체 처리)
- `ND_MARKET_SAVE_SCHEMA_VNEXT` 심볼 미정의 상태 → Framework 저장 연동부는 컴파일 제외(프로토타입 모드)

## 4. 검증 완료 (Play, 에러 0)

①→⑥→Depart 전 구간, Back 전 분기(④→③/②·⑤→④·⑥→⑤), 저장 슬롯 직행 + 적재량 재계산(95=80+15), 무역 취소→①, Depart 이벤트 데이터(town/route/transport/animals/cargo/merc) 확인.

> 참고: MCP로 백그라운드 테스트 시 에디터 프레임이 멈춰 패널 애니메이션이 안 진행됨 → `Application.runInBackground=true`로 해결(런타임 한정, 씬에 저장 안 됨).

## 4-1. 추가 작업 (같은 날 오후) — 폰트·한글화·크기

- **한글 폰트 적용**: `09.Art/05_Fonts/문화재돌봄체 Bold SDF` (한글 완성형 11,172자 포함 Static 아틀라스)
  - 프리팹 내 모든 TMP 텍스트(101개)에 직접 지정
  - **TMP Settings(공용 에셋) 변경 2건** ⚠️: ① 기본 폰트 → 문화재돌봄체 (정헌님 패널처럼 코드로 생성되는 텍스트용) ② 전역 폴백에 LiberationSans 추가 (한글 폰트에 없는 `·` `×` 등 특수문자 보충)
- **전체 한글화**: 우리 패널 라벨(스크립트+씬) + 더미 SO 이름/설명(바람마을·말·밀 등) + **정헌님 스크립트 2개의 하드코딩 라벨** ⚠️
  - `CargoLoadingPanelController.cs` / `MercenaryHirePanelController.cs` — 라벨 문자열만 수정, 로직 무변경
  - 번역 중 발견한 버그 1건 수정: 구매 팝업 정보 텍스트의 `\\n` 리터럴(줄바꿈이 문자 그대로 표시되던 것) → 실제 줄바꿈으로
- **크기 통일**: 정헌님 패널(1176×740)이 우리 화면(③ 1400×900)보다 작아서 **래퍼(S4_CargoScale·S5_MercenaryScale) 1.2배 스케일**로 확대 — 정헌님 코드가 자기 localScale을 애니메이션하므로 패널 자체가 아닌 부모 래퍼에 스케일을 줌(코드 수정 없이 크기만 조정)
- 검증: 전 플로우 한글 표시·스택 구매(밀 3개 24G)·용병 카드(정찰병~강철 용병단)·요약 전부 확인, 에러·폰트 경고 0

## 4-2. 와이어프레임 6번(무역 요약) 구현

- **TradeSummaryPanel**(신규, 순수 UI): "출발 도시 → 목적지" 타이틀 + 좌측 6줄(경유/위험도·용병/음식/코스트/이익/예상 종료 hh:mm:ss) + 우측 이미지 자리 + 무역 시작·무역 취소·뒤로
- **계산은 진짜 Core**: 시간·음식은 `CaravanCalculator.GetTravelSeconds/GetEstimatedFood` 사용 (SO→imsi 매핑은 데모 어댑터 담당 — 동물 speed는 말=6 기준 배수 정규화)
- 위험도 = 루트 내 최고 습격(Combat) 이벤트 값, 없으면 0 (와이어프레임 규칙) / 경유 없으면 '없음'
- 이익 = Σ(판매가×수량) — 도시·계절 배율은 배율 데이터 확정 후 적용(추후)
- 매니저: `SummaryQuery`(선택값) → `SummaryStatsProvider`(주입) → 패널 바인딩. ⑥ 무역 취소 버튼 추가(준비 데이터 초기화 → ①)
- 더미 루트 fromTown/toTown 교차 수정 (지름길·큰길: 강가→바람 등) — "바람마을→바람마을" 문제 해결
- ⚠️ **예상 종료 시간이 00:00:00으로 보임** — Core 임시 튜닝(`CaravanConfig` 100km=10초)이라 데모 루트가 1초 미만. Core 값 확정되면 자연 해결
- ⚠️ **씬 인스턴스 프리팹 연결 끊김 발견** — 씬의 UI_TradePrepare가 NotAPrefab 상태(옛 복사본)여서 프리팹 인스턴스로 교체함. 씬은 프리팹 인스턴스만 유지할 것

## 4-3. 와이어프레임 7번(무역 진행 중 + 취소 경고) 구현

- **TradeProgressPanel**(신규, ⑦): "출발지 → 목적지" 타이틀 + 진행 표시 영역(3D/2D 렌더 자리 = 진행 바로 데모 대체) + "무역 종료까지 남은 시간" 카운트다운 + 무역 취소 버튼
  - 데모는 패널이 `Update`로 자체 카운트다운. 실제 통합 시 `SetProgress(0~1)`로 Framework(JourneyRunner) 진행도 바인딩하고 자체 타이머는 끔
  - 진행 바: Unity 내장 `Background` 스프라이트 할당(sprite 없으면 `fillAmount`가 무시되는 특성 때문)
- **TradeCancelWarningPopup**(신규, ⑦-1): "경고"(빨강 헤더) + "무역을 취소할 경우 불이익이 가해질 수 있습니다." + [돌아가기](초록)/[무역 취소]. 풀스크린 오버레이
- 매니저: ⑥ "무역 시작" → ⑦ 진행(`GoProgress`, ShowOnly 인덱스 5 추가). ⑦ 무역취소 → 경고창 Open. 돌아가기 → 경고창만 닫기(진행 계속). 무역취소 확정/도착 → 준비 데이터 초기화 + ① 복귀(정산 ⑧은 미구현이라 데모는 로그 + `OnJourneyFinished` 이벤트)
- 데모 관찰용 진행 시간: 매니저 `progressDemoSeconds`(기본 20초, 0이면 실제 계산값). Core 튜닝값이 확정되면 0으로 바꿔 실제 시간 사용
- 검증: ⑥→⑦ 전환·카운트다운·진행바 35%/도착·무역취소→경고창→돌아가기(진행유지)/무역취소확정(①복귀) 전부 확인, 에러 0
- ⚠️ 검증 중 관찰: MCP 자동 클릭이 용병 확인(0.2초 애니) 완료 전에 무역시작을 조기 클릭하면 진행화면 출발지가 잠시 빈다 — 실제 입력은 프레임 분리라 문제없음(테스트 아티팩트)

## 5. 남은 것 / 팀 확인 필요

1. **새 와이어프레임(Section 9 v2)** 반영 범위: ② 상단 슬롯+저장 화면이 v2엔 없고 "이동수단 가로 선택"으로 대체됨 → 이종현님과 방향 확인
2. 동물 수량 팝업(v2 3-1-2) vs 현재 즉시 1마리 탑승 → 팀 확인
3. ⑥ 요약을 v2 스펙(예상 위험도·음식 소모량·예상 이익·종료 시간)으로 확장 — `BuildPrepareDisplay()` 연결 후보
4. 용병 offer 데이터가 현재 코드 기본값(Scout/Road Guards/…) → SO화 여부 정헌님과 협의
5. v2 신규 화면(7~9 무역 진행/정산/결제)은 미착수
