# 미니맵 바람 시스템 프로토타입 (0727)

## 개요
미니맵 24×16 격자 위에 **기압 기반 바람 필드**를 프로토타입으로 구현. 표시/연출용(게임플레이 영향은 나중, 프레임워크 독립).

## 원리
- **바람 = -∇P(기압 기울기) + 소용돌이 회전(swirlAngleDeg)**. 고기압 → 저기압으로 흐름.
- 기압원(PressureSource) 3종을 가우시안으로 합산:
  1. **계절(Seasonal)**: 여름 = 아래쪽 고기압 / 겨울 = 반전(몬순 반전). 봄·가을 ×0.4 약화.
  2. **떠도는 배경(Ambient)**: 여러 개가 맵을 천천히 드리프트 → 바람이 유동적으로 변함.
  3. **이벤트(Event)**: 큰불/전쟁 = 저기압(빨아들임), 메테오 = 고기압(밀어냄). 수명 감쇠.
- **지형(산) 기압**: 산 셀 = 고기압으로 지형 기압장(`terrainP`)을 만들고 박스 블러 → 경계가 부드러운 그라디언트 → **바람이 산을 우회**.

## 산 우회 검증(0727)
- 산 37셀(상단중앙·우하단 두 덩어리). `mountainPressure`를 올릴수록 산 셀 평균기압: **−0.50(구멍) → +0.26(1.3) → +1.02(2.6) → +1.83(4.0)**.
- **`mountainPressure=2.6` 채택**: 산이 계절풍과 맞먹는 고기압(+1.0대)이 되어, 캡처에서 산 위 화살표만 강풍(분홍/빨강)으로 바뀌며 바깥으로 갈라져 우회. 주변 평지는 잔잔(청록) 유지.

## 파일
| 파일 | 역할 |
|---|---|
| `MinimapWind.cs` | 기압장·바람 계산(계절/배경/이벤트/지형). `WindAt`/`WindAtCell`, `DropEventAtCenter`, `BuildTerrainPressure` |
| `MinimapWindDebug.cs` | 화살표 시각화(LineRenderer/셀) + 디버그 UI(바람 보기·여름/겨울) |
| `MinimapEventPlacer.cs` | 하단 버튼으로 큰불/메테오 무장 → 미니맵 클릭한 좌표에 이벤트 배치. RawImage 부착(클릭→월드 변환은 페인터와 동일) |

## 이벤트 배치(위치 지정)
- 기존 `DropEventAtCenter`는 맵 중앙+랜덤 지터라 위치 지정 불가 → `MinimapWind.DropEventAt(world,...)` 추가.
- UX: 하단 `🔥 큰불 놓기`/`☄ 메테오 놓기` 버튼 무장 → 미니맵 클릭 → 그 지점 발생(한 번 놓으면 해제). 무장 중 마을 라우터 잠시 off.
- `MinimapEventPlacer`는 `MainUICanvas/WorldMapPanel/RawImage`에 부착. 그 루트가 **공유 MainUICanvas.prefab**이라 프리팹 Apply 금지 → **InGame_Test 씬 오버라이드로만** 저장.

- 부착: `WorldMapRenderRootV2.prefab` (미니맵 렌더 루트). `MinimapGrid` 필요.
- 계절 읽기: `FrameworkRoot.Instance.CurrentSaveData.world.currentSeasonId` (없으면 "summer" 기본).

## 튜닝값(프리팹 반영)
swirlAngleDeg=50, ambientCount=3, ambientStrength=0.55, ambientDriftSpeed=0.35, seasonalStrength=1.0, **mountainPressure=2.6**, terrainBlur=2.

## 함정 메모
- **stale 어셈블리 주의**: play 중 소스 수정 → 재컴파일 전엔 구버전 어셈블리로 실행됨(새 필드/메서드가 런타임 타입에 없어 리플렉션 NRE). 반드시 play 정지 → refresh → 재진입.
- `BuildTerrainPressure`는 `grid.BuildCells` 이후에 호출해야 함(셀 미빌드 시 NRE).

## ② 날씨 레이어 — 구름 이동(완료, 프로토)
- `MinimapClouds.cs`: 임시 흰 뭉게구름(코드로 텍스처 베이킹, 원 여러 개 smoothstep 합)을 8개 띄우고, 매 프레임 각 구름 위치의 `wind.WindAt`로 이동. 경계 벗어나면 반대편으로 랩.
- 튜닝: `cloudSpeed=6`(지도 횡단 ~40초), 정렬순서 15(마을 위·화살표 아래), alpha 0.7. 좌열 "구름 보기/끄기" 토글.
- **`cloudSwirlDeg` — 바람 추종 vs 뭉침 트레이드오프**: 구름을 국소 바람에서 이만큼 돌려 이동. 처음엔 40°(등압선 감돌기)로 저기압 sink 붕괴를 막았으나 "구름이 바람을 안 따라간다"는 피드백 → **비 내림(②-3)이 생긴 뒤엔 `0°`(바람 그대로)로 확정**. 전체 시스템 검증: swirl 0°여도 저기압에 모인 구름이 뭉침→먹구름→비로 빠져나가(rain-out) 평균 상호거리 8~11u로 잘 퍼짐. 단 0°는 구름이 직선으로 맵을 빨리 벗어나 개수가 줄어서 `spawnInterval 3→1`·`maxClouds 12→16`으로 밀도 보강(평균 활성 ~9개). ※ "정체 재활용" 방식은 강한 sink에 역효과라 폐기.
- `WorldMapRenderRootV2.prefab`에 반영. `MinimapGrid.Area`(격자 bounds getter) 추가로 경계 참조.
- 확장 여지: "구름 밑 셀 = 비/이벤트" 판정 → `ForceRouteEvent` 훅 연결(지금은 이동 연출만).

## ②-2 수분/먹구름 + 지속 생성(완료, 프로토)
- **지속 생성**: `spawnInterval`(0.7초)마다 최대 `maxClouds`(16)까지 스폰. **`waterSpawnChance`(0.55) 확률로 강/호수 위(수증기원)에서, 나머지는 4방향 가장자리에서** 생성. 수명·맵밖·비로 소멸.
- **속도**: `cloudSpeed=0.2` — 셀(0.64u) 통과 ~23초(≈400m/셀 가정 시 17m/s, 현실적). ※ 처음 4.5는 셀을 0.5초에 통과 = 700m/s로 비현실적이었음. **느린 속도↔먹구름량 상충**(느리면 구름이 물을 덜 만남) → 속도는 느리게 두고 '물 위 생성'으로 먹구름 확보(평균 ~3개/19% 물 위). 수분은 **시간 기반**(느린 구름이 물 근처 머물며 젖음, `waterGain=0.15`/초).
- **수분(moisture 0~1)**: 구름 '발자국'(중심+상하좌우 5점) 중 강/호수/다리(River·Water·Bridge)에 걸친 비율만큼 `waterGain`↑, 안 걸치면 `dryRate`↓. ※ 강이 1칸이라 중심 한 점만 보면 못 밟아서 발자국 샘플 필수.
- **뭉침 → 먹구름**: `crowdRadius` 안 이웃 수 × `crowdGain`만큼 수분↑(과포화 방지로 약하게 0.05).
- **먹구름 표현**: 수분↑ → 색 흰→진청회색(`dark01=clamp(moisture*1.5)`로 조금만 젖어도 빨리 진하게), 불투명도↑(`darkAlpha=0.95`), **정렬순서 낮춰 일반 구름보다 아래에**(먹구름15→13), **속도 `darkSpeedMul=1.9`배 빠르게**.
- **비 내림(rain-out)**: 먹구름(moisture≥darkThreshold)이 물을 벗어나면(`wf<=0`) `raining` 시작 → **색은 어두운 채로 두고 크기를 `rainDuration`(5초) 동안 줄이다 소멸**(하얘지지 않음 = 비 뿌리고 수분 소진). `Cloud.baseScale`에서 축소. 흰 구름은 기존대로 수명 페이드로 소멸.
- 튜닝: waterGain=0.6, dryRate=0.10, crowdGain=0.05, darkThreshold=0.5. 실제 비율은 turnover에 따라 자연 혼합(헤드리스 검증은 조건별 편차 큼 — 라이브 확인 권장).
- **프레임 클리핑**: 맵 영역 크기 `SpriteMask` + 구름 `maskInteraction=VisibleInsideMask` → 구름이 격자 밖(미니맵 테두리)으로 안 삐져나오고 가장자리에서 잘림. 마을·지형은 마스크 미설정이라 무관.

## 다음(미완)
③ 캐러밴 격자 이동 연출 → 먹구름 밑 셀 = 비/이벤트 판정(`ForceRouteEvent` 훅). 실제 경제/여행 효과·계절 회전은 프레임워크 팀 도메인(협의). 근거: [[0727_event_system_audit]]
