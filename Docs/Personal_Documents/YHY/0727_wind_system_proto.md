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

## 다음(미완)
② 날씨 레이어(구름을 바람으로 이동) → ③ 캐러밴 격자 이동 연출. 실제 경제/여행 효과·계절 회전은 프레임워크 팀 도메인(협의). 근거: [[0727_event_system_audit]]
