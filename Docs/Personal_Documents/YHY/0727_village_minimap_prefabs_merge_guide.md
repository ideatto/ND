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

## 실제 InGame 씬에 얹는 순서 (병합 시)
1. **Village_Home**에 `VillageWorldTowns.prefab`을 드롭. `TradeTownCameraMover`의 `targetCamera`는 비어 있으면 런타임에 `VillageCamera`를 자동 탐색하지만, 명시 배선을 권장.
2. **미니맵**: `WorldMapRenderRootV2.prefab`을 씬에 두고, 미니맵 RawImage의 texture를 이 루트의 RenderTexture(V2)로 지정.
3. **UI 글루(수동 — MainUICanvas 공유라 프리팹 불가)**: 미니맵 RawImage에
   - `MinimapCameraController` 추가
   - `MinimapTownClickRouter` 추가 → `view`=그 RawImage, `renderRoot`=WorldMapRenderRootV2, `homeTownId`="BaseCamp"
4. 캔버스에 `HomeButton.prefab`, `TownNameLabel.prefab` 배치(위치/스타일은 씬에 맞게).
5. (선택) 테스트할 때만 `MinimapCaravanTestPanel.prefab` 배치.

## 남은 열린 질문 (팀 논의)
- 정식 미니맵(`WorldMapPresenter`)은 **선택 캐러밴 1대만** 그린다. 여러 캐러밴을 미니맵에 동시 표시하려면
  프레임워크(정헌님/성욱님) 쪽 다중 마커 지원 논의 필요. `MinimapMultiCaravanMarkers`는 임시 표시용.
- 마을 한글 표시명은 `SharedGameData.DisplayName`을 채우면 이름표에 자동 반영됨(현재는 townId 그대로).
