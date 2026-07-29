# 날씨 시스템 확장 + 미니맵·날씨 폴더 분리 (0729)

> 브랜치 `feature/Core-grid-weather-event-YHY`. 커밋 `de7895e` (57 files). push 전.
> 앞선 [[0728_deterministic_weather]]의 Phase1(결정론) 위에 **Phase2(캐치업)·세기 판정·속도 감소·번개/불/기압·효과 골격**을 얹고, 파일을 `08_Minimap`/`09_Weather`로 분리.

## 1. 폴더 분리 (07_Village → 08_Minimap / 09_Weather)

날씨·미니맵 스크립트가 `01.Core/07_Village/YHY/`에 섞여 있어 어색 → 두 폴더로 분리.

- `01.Core/08_Minimap/` (13개): `MinimapGrid`·`MinimapCell`·`MinimapGridDebug`·`MinimapGridPainter`·`MinimapCameraController`·`MinimapTownClickRouter`·`MinimapMultiCaravanMarkers`·`TradeTownCameraMover`·`HomeViewButton`·`CurrentTownNameLabel`·`MinimapCaravanTestPanel`·`MinimapCaravanIndicator`·`MinimapDebugToolsInstaller`
- `01.Core/09_Weather/` (11개): `DetRng`·`MinimapWind`·`MinimapWindDebug`·`MinimapClouds`·`MinimapEventPlacer`·`WeatherEventData`·`WeatherEvents`·`MinimapWeatherEventDetector`·`LightningSystem`·`IWeatherEffect`·`WeatherEffectDispatcher`·`WeatherNoticeEffect`

- **방법**: `AssetDatabase.MoveAsset`(에디터) → `.meta`째 이동해 **GUID 유지** → 씬·프리팹 참조 안 깨짐. git도 `R`(rename)으로 감지 → 히스토리 보존.
- 전역 네임스페이스라 코드 참조도 안 깨짐(컴파일 0 에러).

### V3 프리팹 승격 (Variant) — 왜, 어떻게

문제: 튜닝값(maxClouds=40, seasonalStrength=3 등)과 **새 4종(Detector·Lightning·Dispatcher·Notice)** 이 `InGame_Test` **씬에만** 있고 공유 프리팹 `WorldMapRenderRootV2`엔 없었음 → 다른 씬에서 쓰면 날씨가 옛 값/미작동.

- V2는 **성욱님도 공동 편집** → 직접 못 건드림. 그래서 **`WorldMapRenderRootV3.prefab`을 V2의 Prefab Variant로 신설**.
- Variant라 **지도 본체(격자·마을·루트·카메라)는 V2에서 상속**(V2 개선 자동 반영), V3는 그 위에 **날씨 레이어만 override/add**.
- 방법: `PrefabUtility.SaveAsPrefabAssetAndConnect(sceneRootInstance, v3Path, ...)` — 씬의 V2 인스턴스(우리 오버라이드·새 컴포넌트 다 얹힌 상태)를 그대로 저장 → 자동으로 **Variant** 생성 + 샌드박스 씬을 **V3에 재연결**(씬 오버라이드가 V3 본체로 승격돼 씬이 깔끔).
- 검증: V3 타입=Variant, 베이스=V2, 새 4종 전부 ✅, 튜닝값 전부 ✅(cloudSpeed1.2/maxClouds40/initialClouds24/spawnInterval0.3/simSpeed0.2/crowdGain0.06/seasonalStrength3). 외부 씬 참조 0(깨질 ref 없음).
- **경계**: 실제 게임(InGame.unity)에 V3를 넣을지는 프레임워크 팀과 협의 후. 지금은 우리 샌드박스만 V3 사용.

## 2. Phase2 캐치업 (완료) — 껐다 켜도 이어지는 날씨

목적: **게임을 멈추거나 껐거나 미니맵을 늦게 열어도** 그동안 흘렀을 만큼 구름·바람이 이동해 있어야 함.

- **벽시계 기준점** `weatherEpoch`(static): 최초 1회 `NowSeconds()`로 고정.
- 매 프레임 `targetStep = (NowSeconds() - weatherEpoch) / fixedDt` 계산 → `simStep < targetStep`이면 렌더 없이 고정스텝을 **빠르게 감아** 따라잡음(`wind.StepSim`+`StepClouds`, guard로 폭주 방지).
- **핵심 함정**: `NowSeconds()`는 `Time.realtimeSinceStartup`이 아니라 **`DateTime.UtcNow`(진짜 벽시계)** 여야 함 — 일시정지 중에도 흘러야 하니까. (GameTime 있으면 우선 사용)
- **완전 재현**: 리플레이가 결정론이려면 구름뿐 아니라 **바람도 epoch로 리셋**해야 함 → `BuildClouds`에서 `wind.SetSeed`+`ResetSim`. 3회 리플레이 동일값 검증(870161.63).

## 3. 먹구름 세기 판정 (이진 → 수치)

먹구름도 크기·강수량이 다른데 "비 있음/없음"만 보던 걸 **세기(intensity)** 로 수치화.

- `MinimapClouds.RainIntensityAt(worldPos)` = 그 지점을 덮는 먹구름들 중 **max( moisture(강수량) × localScale.x(크기) )**. 없으면 0.
- `IsRainAt`은 `RainIntensityAt > 0`의 래퍼로 남김.
- **이벤트 계층화**: `WeatherEventData.minIntensity`(문턱) 추가 → 약한 비=0.3, 폭우=1.0 식으로 데이터만으로 등급 분리. 판정 시 문턱 통과한 이벤트 중 **가장 높은 minIntensity가 우선**, `DetRng.Seed(tradeHash, checkIndex, cellId)`로 확률 굴림(결정론).
- **식량 페널티 제거**: `foodPenaltyRate` 필드/사용처 삭제(설계 변경).

## 4. 속도 감소 = 이벤트가 아니라 "현상" (모디파이어)

> 통찰: "먹구름 밑 들어가면 느려짐"은 한 번 터지는 **이벤트**가 아니라 조건이 유지되는 동안 계속 적용되는 **현상/모디파이어**.

- `MinimapWeatherEventDetector.LateUpdate`: 캐러밴마다
  `intensity = clouds.RainIntensityAt(pos)` →
  `mul = intensity>0 ? clamp(1 - intensity*rainSlowdown, minSpeedMul, 1) : 1`.
- `GetWeatherSpeedMultiplier(caravanId)`로 **배율 노출**(프레임워크가 실제 이동속도에 곱하는 건 협의). 지금은 화면에 비 아이콘 + "비 진입 — 속도 x0.70" 전환 알림.
- 필드: `rainSlowdown=0.3`, `minSpeedMul=0.5`.
- **이벤트 발생은 보류**: `fireEvents=false`. 판정/발행 코드(WeatherEvents·Detector.EvaluateCheck)는 살려두되 실제 발생만 꺼둠 — 이벤트 형태는 더 논의.

## 5. 번개 → 불 → 기압 (LightningSystem, 신설)

날씨↔지형↔불↔기압이 서로 영향 주는 살아있는 루프.

- 주기 `strikeInterval=2.5s`마다 `strikeChance=0.6` 확률로, `RainIntensityAt ≥ minStrikeIntensity(0.4)`인 셀 중 하나에 **번개**(전부 `DetRng`로 결정론).
- 맞은 셀이 **가연 지형(숲/풀)** 이면 → **큰불** 스폰 + `wind.DropEventAt(pos, highPressure=false, ...)` 로 **저기압 주입**. 비가연이면 섬광만.
- 저기압 → 바람이 불로 수렴 → 구름이 끌림 = **동적 기압**(정적 지형기압 뭉침 완화).
- 필드: `fireDuration=8`, `firePressureStrength=1.6`, `fireRadiusFactor=0.12`, `worldSeed=777`. 스프라이트는 코드 베이킹 블롭.

## 6. 효과 골격 (IWeatherEffect)

이벤트 알림과 효과 적용을 분리(발행=`WeatherEvents.Raise`, 효과=구독자).

- `IWeatherEffect { Apply(WeatherEventOccurrence) }`.
- `WeatherEffectDispatcher`: `WeatherEvents.Occurred` 구독 → 자식 `IWeatherEffect`들에 분배(핫리로드 대비 재구독).
- `WeatherNoticeEffect`: 데모 구현체(화면 알림). 실제 게임플레이 효과는 프레임워크 협의 후 별도 구현체로.
- `WeatherEventOccurrence`에 `intensity` 추가(효과가 세기에 비례하도록), `foodPenaltyRate` 제거.

## 7. 계절풍 강화 (여름 vs 겨울 뚜렷하게)

- 증상: 여름/겨울 바람 차이가 미미. **원인**: 지형기압(산 `2.6` 등)이 계절기압(`~1.0`)을 압도 → 계절 몫이 전체 바람의 ~13%.
- 조치: `MinimapWind.seasonalStrength` **1 → 3** (씬 베이크). 검증: 여름 131° / 겨울 −32°, 순 크기 3배 차.

## 8. 함정 / 메모

- 씬 오버라이드 > 프리팹 > 코드기본값. 라이브·프리팹만 바꾸면 **씬 오버라이드가 되돌림** → `SerializedObject`로 **에디트 모드에서 씬에 베이크** 후 SaveScene 필수.
- 핫리로드 시 비직렬화 필드(List/Dict/2D/plain ref) 소실, bool 플래그는 생존 → "ready" 판정을 **실제 데이터 존재**로(`cells != null` 등).
- AddComponent(세션 중) 컴포넌트는 Update 수명 불안정 → 알림은 Detector가 직접 OnGUI로 그림.
- `_Recovery/`(Unity 크래시 복구 임시파일)가 워킹트리에 쌓임 → `.gitignore` 필요(별도 정리 대기).

## 9. 다음(미완)

- **이벤트 형태 재논의**(`fireEvents` 재활성 여부·등급).
- 프레임워크 협의: `GetWeatherSpeedMultiplier` 실제 소비, Weather 효과 API, `weatherEpoch` 저장(세션 간 오프라인).
- 폭우 티어 `WeatherEventData`(minIntensity 1.0) 데모 자산.

> 관련: [[0728_deterministic_weather]], [[0727_wind_system_proto]], [[0727_event_system_audit]]
