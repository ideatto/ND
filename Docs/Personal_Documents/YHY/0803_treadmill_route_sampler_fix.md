# 0803 트레드밀 — 정식 부팅 시 마차가 안 흐르던 버그 수정

## 증상
Boot → Title → New Game → 무역 출발 하는 **정식 플로우**에서, 무역이 실제로 진행 중인데도
트레드밀 마차(길)가 스크롤되지 않고 가만히 서 있었다.
(디버그로 InGame 씬을 바로 열었을 땐 정상이라 재현이 까다로웠음.)

## 원인
`TreadmillRouteSampler.SampleRouteTerrainByRouteId(routeId)` 가 **빈 문자열**을 반환 →
`road.RouteCellCount = 0` → `DriveRoute`가 `if (cells <= 0) return;`로 조기 종료 →
`SetExternalProgress`가 호출되지 않음 → `externalDrive=false`, `arrived=true`가 남아 스크롤 안 함.

빈 문자열이 나온 진짜 이유:
- 정식 부팅에선 같은 `routeId`("BaseToRiver")를 가진 **RouteVisual이 2개** 존재한다.
  - #1: 월드맵 렌더용 — 좌표가 미니맵 격자 **밖** → `grid.WorldToCell` 전부 실패
  - #2: 미니맵 격자용 — 좌표가 격자 **안** → 정상 매핑
- 기존 샘플러는 `foreach ... break;`로 **첫 번째** RouteVisual만 골랐고, 그게 #1(격자 밖)이면
  모든 셀 판정이 실패해 빈 문자열이 나왔다.
- InGame 씬을 바로 열었을 땐 RouteVisual이 1개뿐이거나 순서가 달라 우연히 정상이었다.

## 수정
`SampleRouteTerrainByRouteId`를 **매칭되는 RouteVisual을 모두 시도**하도록 변경.
각 RouteVisual로 지형 문자열을 만들어 보고, **지형이 실제로 나오는(격자에 얹힌) 첫 번째**를 사용.
→ 월드맵용(격자 밖) RouteVisual은 자연히 걸러지고, 미니맵 격자용이 선택된다.

파일: `Assets/_Project/01.Core/10_Treadmill/TreadmillRouteSampler.cs`

## 검증 (정식 플로우)
Boot→Title→New Game→InGame 로드 후 실제 BaseToRiver 무역 출발:
- `SampleRouteTerrainByRouteId('BaseToRiver')` : `''`(len 0) → **`'FGGGGP'`(len 6)**
- 도로 상태: `externalDrive` False→**True**, `IsArrived` True→**False**, `IsScrolling` False→**True**
- `traveled` 175→203, `externalP01` 0.184→0.214 로 **연속 증가**(마차 스크롤 확인)

---

# 0803 트레드밀 전투 연출 다듬기 (산적 충돌 시 잠깐 멈춤)

## 요청
1. 산적 만나는 타이밍을 조금 늘리기(더 멀리서 오래 다가오게)
2. 산적과 부딪힐 때 마차가 **잠깐 멈추기**
3. 먼지를 **더 많이** 나게
4. **결과가 나오면 다시 이동**
5. ★이동속도·도착결과가 틀어지지 않게

## 어떻게 (5번 불변식이 핵심)
트레드밀 바닥 스크롤은 **velocity 기반(scrollSpeed×dt)** 이라 실제 무역 진행도(save)와 **분리**돼 있고,
목적지 마을 접근은 실제 progress `p`(remainingCells=(1-p)×cells)로 구동된다. 즉 바닥을 잠깐 멈춰도
무역 완료 시각·도착 타이밍은 그대로다(순수 시각 연출). 전투도 루트 중간(p≈0.5)에서 나고,
도착 근처(maxVisualProgress=0.9 이상)는 연출을 생략하므로 멈춤이 도착과 겹치지 않는다.

- `TreadmillRoad.SetCombatHold(bool)` 추가 — `arrived`와 별개 플래그. `Update`에서
  `if (combatHold) { lastDz=0; return; }`로 시각 스크롤만 정지(도착 접근 로직은 안 건드림).
- `TreadmillCombat`:
  - 타이밍: `warnRealSeconds` 4.5→**6초**(더 멀리서 오래 접근).
  - 충돌 순간: `road.SetCombatHold(true)` → 마차 정지 + 지속형 먼지 왕창.
  - `TickClash`: 결과(활동 로그)가 뜨면 색으로 표시 → `resultHoldSeconds`(1초)만큼 보여준 뒤
    `SetCombatHold(false)` → **다시 출발**. 결과가 끝내 안 오면 `clashMaxSeconds`(4초) 안전장치로 재출발.
  - 먼지: 1회 버스트 → **큰 버스트(120) + 멈춰 있는 내내 지속 방출(rate 60)**, 크기·수명·반경 확대.
  - `Clear()`·캐러밴 전환 리셋 경로 모두 `SetCombatHold(false)`+먼지 정리 → 멈춘 채 끝나지 않게 안전.

파일: `TreadmillRoad.cs`, `TreadmillCombat.cs`

## 결과(승/패) 정합성
결과는 여전히 `caravanActivityLogs`(UI 전투 패널과 같은 소스)에서만 읽는다 — 연출이 결과를 만들지 않음.
멈춤은 그 로그가 뜰 때까지(그리고 잠깐 더) 기다리는 용도라, UI 결과와 100% 일치.
