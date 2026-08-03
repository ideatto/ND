# 트레드밀 인게임 연동 + 진행도 동기화 + 정박-비 버그 수정 (0731)

> 브랜치 `feature/Core-treadmill-YHY`. **아직 커밋 전**(머지 이후 트레드밀 작업 전부 미커밋).
> 앞선 트레드밀(마차+동물 SO, 커브드 길, 논밭/풀 지형)을 **실제 InGame 씬에 붙이고**, 트레드밀이 자기 시계가 아니라 **캐러밴 실제 여행 진행도**로 돌게 만든 뒤, 마지막으로 "정박 중인 마차가 이동 중처럼 보인다"는 버그를 잡음.
> 날씨 신호원은 [[0729_weather_reorg_and_systems]]의 `WeatherState`/`MinimapWeatherEventDetector`.

## 1. InGame 씬 연동 (additive)

- InGame에서 트레드밀을 직접 만들지 않고, `Treadmill_Preview.unity`를 **additive로 로드**(`AdditiveSceneLoader.sceneName`, Start 시 로드). 테스트3 씬 방식과 동일.
- 프리뷰 전용 오브젝트(디버그 UI·프리뷰 조명)는 **`DisableWhenAdditive`** 로 자동 비활성: `if (gameObject.scene != SceneManager.GetActiveScene()) SetActive(false)` (Awake). → InGame에 얹혀도 프리뷰 조명/디버그가 씬을 오염 안 시킴.
- Build Settings: `InGame`·`Village_Home`·`Treadmill_Preview` 등록.
- 검증: 플레이 시 3씬 로드, 프리뷰 조명 비활성, RT 카메라(`TreadmillRT` 1024x768) 정상 렌더, 에러 0.

## 2. 진행도 구동 (TreadmillProgressSync)

트레드밀을 상수 스크롤이 아니라 **캐러밴 진행도(0~1)** 로 몬다. 매 프레임(LateUpdate):

- 표시 상태 판정은 **미니맵과 동일 정책** `CaravanMapDisplayResolver`(dev2, `ND.Framework`) 사용 → `TreadmillRouteSampler.TryResolveDisplay(cid, out onRoute, out routeId, out p, out townId, out tradeState)`로 래핑.
  - **이동 중(Traveling)** → Route 모드: routeId로 경로 지형 1회 샘플 → `road.SetExternalProgress(p)` → 트레드밀 그리드 = 캐러밴 실제 셀.
  - **정박(None/Preparing/Failed)** → Town 모드: 정지 + 그 마을 실제 지형 표시. 건물은 **성공 도착(SettlementPending/Completed)일 때만** 세움(대기·실패 땐 마차가 건물에 박히던 문제 회피).
  - **실패** → 리졸버가 출발 마을(currentTown)로 되돌려줌 → 트레드밀도 되돌아감.
- 도착 접근: 남은 셀 ≤ `arriveWindowCells(1.5)`면 목적지 마을 건물이 그리드 위로 다가와 `p=1`에 정확히 도착·정지.

### 슬롯 vs 실제 caravanId 함정

`currentKey`는 슬롯 라벨("1"~"4") **또는** 실제 caravanId 둘 다 올 수 있음. `int.TryParse` 실패(=실제 id) 시 slot=0이 되어 **slotIndex 0 캐러밴이 오매칭**되던 버그 → `bool isSlot = int.TryParse(...); match = isSlot ? (slotIndex+1==slot || slotIndex==slot) : (caravanId==key)` 로 3곳(`ShowCaravan`·`CurrentCaravanId`·`IsCaravanTraveling`) 통일.
버튼바(`TreadmillCaravanButtonBar`)도 **리스트 순서가 아니라 slotIndex로** 버튼↔캐러밴 매핑(마차2가 다른 캐러밴 가리키던 문제 방지) + 캐러밴 수 변화 감지 재갱신(해금 즉시 반영).

## 3. 정박 캐러밴이 "이동 중"처럼 보이던 버그 (핵심)

**증상**: 출발도 안 한 마차2(정박)를 트레드밀에서 보면 비 오는 시골길을 달리는 것처럼 보임.

**추적**(전부 리플렉션 계측으로 확인):
- 바닥 스크롤은 정상 정지였음 — `road.IsScrolling=False`, `traveled` 프레임 간 불변, RT 픽셀 체크섬 동일. 동물도 `animator.speed=0`(정지).
- 진짜 원인은 **비**: 정박 마차 RT를 렌더해 보니 흰 빗줄기가 내리고 있었음. `rain.caravanId=""`(빈값) / `WeatherState.Max=0.71`(=이동 중인 다른 마차의 비) / `rain.current=0.712`.

**근본 원인**:
1. `TreadmillProgressSync.DriveTown`(정박 분기)이 `rain.SetCaravan(cid)`를 **안 불러서** `rain.caravanId`가 빈 채로 남음.
2. 빈 id일 때 `TreadmillRain.TargetIntensity()`가 `WeatherState.Max`(전 캐러밴 중 최댓값)로 **폴백** → 정박 마차에 **남의 비**가 내림 → "활동 중=이동 중"처럼 보임.

**수정**: `LateUpdate`에서 이동/정박 **공통으로** `if (rain != null && !debugFakeProgress) rain.SetCaravan(cid);` 를 분기 전에 1회 호출. → 비는 항상 **표시 중 캐러밴 본인 날씨**만 따름. 정박 마차는 보통 날씨 보고가 없어 `Get(cid)=0` → **비 없음** → 멈춰 있음이 분명해짐. (DriveRoute의 중복 호출은 제거해 단일화. `Max` 폴백은 순수 디버그/프리뷰용으로만 남김.)

**검증**(InGame 플레이, 시뮬레이션):
- 캐러밴A에 비 0.7 보고 + 캐러밴B(정박) 표시 → `rain.caravanId=B`, `TargetIntensity=0` ✅ (수정 전엔 Max=0.7로 비 내렸음)
- 비 오는 캐러밴A 표시 → `TargetIntensity=0.7` ✅ (정상 기능 안 깨짐)
- RT 렌더: 정박 마차가 **맑은 하늘**의 정지 장면으로 나옴(빗줄기 사라짐).

## 4. 남은 확인거리 (실제 무역 출발로 검증 필요)

- `TreadmillRouteSampler.SampleRouteTerrainByRouteId('BaseToRiver', 400)` 가 빈 문자열을 반환하는 상황을 관측함(단, 그때 trade가 만료된 강제 상태였음 = RouteVisual 미로딩 가능성). 빈 경로면 `RouteCellCount=0` → `DriveRoute`가 조기 리턴 → **이동 중에도 길이 안 흐름**. → **실제로 마차를 출발시켜** 이동 중 트레드밀 그리드가 흐르는지 반드시 재확인할 것.
- `StopIdle`(표시 캐러밴 미해석) 엣지에서 비가 Max로 남을 수 있음(패널 닫힘 상태라 화면엔 안 보임). 필요 시 정리.

## 5. ★구조 전환 — 단일 인스턴스 → 마차별 독립 레인 (핵심 리팩터)

3번 비 버그를 고쳐도 사용자 체감이 "아직도 마찬가지". 근본 진단: 트레드밀이 **딱 하나**(카메라·길·스테이지 1개)라 마차 전환 시마다 그 하나를 **재구성**하는 구조 자체가 취약 — 한 군데라도 안 갈리면 마차2에 마차1 상태가 남음(비·슬롯·진행도 버그가 전부 여기서 샜음). **팀 결정: 마차마다 전용 레인을 따로 둔다**(교차 오염 구조적 차단).

- **`TreadmillLane`**(신규): 한 레인(Camera+Stage+Road+Rain/TownArrival/Sync)의 루트에 붙어 내부 부품을 캐싱하고 담당 `caravanId`를 보관. `TreadmillLane.Of(component)`=`GetComponentInParent<TreadmillLane>`.
- **레인-로컬 참조**(중요): 기존 부품들이 서로를 `FindFirstObjectByType`(전역)로 찾아서, 레인이 여러 개면 **엉뚱한 레인끼리 연결**됨. → `TreadmillProgressSync`·`TreadmillStage`·`TreadmillTownArrival`·`TreadmillDodge`의 참조 해석을 **레인 내부(형제) 우선**으로 바꿈(레인 없으면 전역 폴백=단일 인스턴스 하위호환). RouteSampler의 전역 검색(RouteVisual/Grid/Town)은 공용 시스템이라 그대로.
- **`TreadmillLaneManager`**(신규): 마차를 열면 그 마차 전용 레인을 보임.
  - 첫 마차 = 씬 원본 레인 재사용(비용 0), 이후 마차 = 원본 **복제** + `laneGap(1000m)` 옆으로 격리(카메라가 자기 레인만 찍게).
  - 레인 생성 시 `lane.caravanId` 지정 → `ProgressSync`가 이 고정 마차를 몲, `Stage.ShowCaravan`, `Rain.SetCaravan`.
  - **활성 레인 카메라만** 공유 RT(`TreadmillRT`)에 렌더, 나머지 카메라 off → **그리기 1배 유지**. (안 보이는 레인도 진행도 로직은 계속 돌아 위치 최신.)
- **`TreadmillPanel.Open`**: 매니저 있으면 `Manager.Show(caravanId)`로 위임(없으면 기존 단일 인스턴스 동작).
- **씬 재구성**: `Treadmill_Preview`에서 Main Camera·TreadmillStage·TreadmillRoad를 **`TreadmillLane`** 부모로 묶고, `TreadmillLaneManager` 오브젝트 추가(prototypeLane 배선). Directional Light·편집카메라는 공용(레인 밖).

**검증**(InGame 플레이, 마차 2대): Open(A)→레인1개(A 담당, 카메라 켜짐). Open(B)→**레인 2개**(A pos=0 카메라 off / B pos=1000 카메라 켜짐→RT), 각 `stage.currentKey`=자기 caravanId. B 레인만 비 강제 → 렌더: **B=비 오는 풀밭 / A=맑은 마을**(같은 순간 완전 독립). 전환=카메라 on/off 즉시(재구성 0).

## 파일
- `01.Core/10_Treadmill/`: **`TreadmillLane`(신규)·`TreadmillLaneManager`(신규)**·`TreadmillProgressSync`·`TreadmillStage`·`TreadmillTownArrival`·`TreadmillDodge`·`TreadmillPanel`(레인 대응)·`TreadmillRain`·`TreadmillRoad`·`TreadmillRouteSampler`·`TreadmillCaravanButtonBar`·`DisableWhenAdditive`·`WeatherState`
- 씬: `07.Scenes/04_InGame/InGame.unity`·**`Treadmill_Preview.unity`(레인 구조로 재구성)**
