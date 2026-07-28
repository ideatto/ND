# 마을/미니맵 기능 프리팹화 & 실제 씬 병합 가이드 (0727)

## 개요
InGame_Test(내 샌드박스)와 Village_Home에서 만든 마을/미니맵 기능들을 **프리팹으로 패키징**했다.
목적: 나중에 실제 InGame 씬에 **얹기(추가)만** 하면 되고, 남의 씬/공유 프리팹을 깨지 않기 위함.

> ⚠️ **공유 프리팹 주의**: `MainUICanvas.prefab`은 실제 `InGame.unity` + `InGameMainUITest.unity`에서도 쓰는 **공유 프리팹**이다.
> 여기에 오버라이드를 Apply하면 실제 InGame이 바뀌므로 **절대 Apply 금지**. (그래서 UI 글루는 프리팹으로 못 뽑고 아래 수동 배선으로 남김.)
> 반면 `WorldMapRenderRootV2.prefab`은 InGame_Test 전용이라 Apply 안전.

## 만든 프리팹 목록

| 프리팹 | 경로 | 내용 |
|---|---|---|
| **WorldMapRenderRootV2** | `08.Prefabs/UI/Maps/WorldMapRenderRootV2.prefab` | 미니맵 렌더 루트(카메라→RT, 마을/경로, 배경) + 내 스크립트(`MinimapCaravanIndicator`(비활성), `MinimapMultiCaravanMarkers`) |
| **WorldMapPanelV2** ⭐ | `08.Prefabs/UI/Maps/WorldMapPanelV2.prefab` | 미니맵 UI 패널 통째(RawImage=V2 RT + `MinimapCameraController`+`MinimapTownClickRouter` 내장 + 지도버튼→SlidePanel). **이거 하나로 패널 교체**. Router는 renderRoot 자동 탐색 |
| **VillageWorldTowns** | `08.Prefabs/Village/VillageWorldTowns.prefab` | 한 맵 안 거점/무역마을 마커(`TradeTown_*`) + `TradeTownCameraMover`(카메라 이동기). 마을을 좌표로 떨어뜨려 배치 |
| **MinimapCaravanTestPanel** | `08.Prefabs/UI/Maps/MinimapCaravanTestPanel.prefab` | 개발용 테스트 버튼(캐러밴 출발/즉시도착/리셋). 실제 빌드에는 넣지 않음 |
| **HomeButton** | `08.Prefabs/UI/Maps/HomeButton.prefab` | "거점" 버튼(`HomeViewButton`) — 카메라를 거점으로 이동 |
| **TownNameLabel** | `08.Prefabs/UI/Maps/TownNameLabel.prefab` | 상단 이름표(`CurrentTownNameLabel`) — 현재 보는 마을 이름 |

## 관련 스크립트 (01.Core/07_Village/YHY)
- `MinimapCameraController` — 미니맵(2D XY) 드래그 팬 + 스크롤 줌 + 경계 클램프
- `MinimapTownClickRouter` — 미니맵 마을 클릭 → 정박 캐러밴 있으면 카메라 이동(거점 항상 활성)
- `MinimapCaravanIndicator` — (구) 단일 정박 캐러밴 표시. 지금은 MultiMarkers가 대체(비활성)
- `MinimapMultiCaravanMarkers` — 여러 캐러밴을 슬롯 색으로 미니맵에 동시 표시(이동중=경로, 정박=마을)
- `TradeTownCameraMover` — RequestedTownId 마을 좌표로 VillageCamera 이동(카메라 없으면 이름으로 자동 탐색)
- `HomeViewButton` / `CurrentTownNameLabel` — 위 프리팹의 컴포넌트
- `MinimapCaravanTestPanel` — 테스트 패널
- `BuildingPlacementController.RecenterPanHome()` — 마을 이동 후 팬 중심 재설정(드래그 시 옛 거점으로 안 끌려오게)

## 실제 InGame 씬에 얹는 순서 (병합 시) — **간단 버전(프리팹 교체 + 드롭만)**
> 배선(Add Component/인스펙터 연결) 불필요. `WorldMapPanelV2`는 자기완결(RawImage=V2 RT,
> Router/CameraController 내장, 지도버튼→SlidePanel.Toggle 내부 참조). Router는 `renderRoot`가
> 비면 **자기 RawImage가 그리는 RT의 카메라 루트**를 런타임에 자동 탐색한다.

1. **Village_Home**: 이미 `VillageWorldTowns`가 들어있음(공유 씬). 별도 작업 없음.
   (신규 씬에 얹을 때만 `VillageWorldTowns.prefab` 드롭 — 카메라는 `VillageCamera` 자동 탐색.)
2. **미니맵 렌더 소스**: `WorldMapRenderRootV2.prefab`을 Hierarchy에 드롭(위치 무관, 자기 RT로 렌더).
3. **미니맵 패널 교체**: `MainUICanvas`의 기존 `WorldMapPanel`을 지우고,
   그 자리에 `WorldMapPanelV2.prefab`을 `MainUICanvas` 자식으로 드롭. → **배선 끝**.
   - ⚠️ 다른 스크립트가 기존 WorldMapPanel을 참조하면 V2로 다시 연결할 것.
4. (선택) `HomeButton.prefab` / `TownNameLabel.prefab`을 `MainUICanvas` 하위에 드롭(HUD, 위치 자유).
5. (선택) 테스트할 때만 `MinimapCaravanTestPanel.prefab` 드롭 → 확인 후 제거.

**요약: ②③만 하면 미니맵 완성.** (프리팹 2개 드롭, 하나는 기존 패널과 교체)

## 남은 열린 질문 (팀 논의)
- 정식 미니맵(`WorldMapPresenter`)은 **선택 캐러밴 1대만** 그린다. 여러 캐러밴을 미니맵에 동시 표시하려면
  프레임워크(정헌님/성욱님) 쪽 다중 마커 지원 논의 필요. `MinimapMultiCaravanMarkers`는 임시 표시용.
- 마을 한글 표시명은 `SharedGameData.DisplayName`을 채우면 이름표에 자동 반영됨(현재는 townId 그대로).
