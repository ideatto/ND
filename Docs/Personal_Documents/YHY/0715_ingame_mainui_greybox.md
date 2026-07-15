# InGame 메인 UI 그레이박스 + 거점 마을 시스템 — 인수인계

- **작성**: 윤호영 (Core Gameplay) / 2026-07-15
- **브랜치**: `feature/UI/InGame-MainUI-YHY` (dev2 분기)
- **목적**: 인게임 메인 화면(거점 마을 + 정보창 + 월드맵) 그레이박스 + 마을 건물 시스템

---

## 1. 화면 구성 (그레이박스)

```
┌────────────────────────────────[지도]│ ← 월드맵 책갈피(오른쪽 앵커, 슬라이드)
│      거점 마을 화면 (3D)              │
│      (별도 씬 → RenderTexture)        │
├──────────────────────────────────────┤
│ [소지금]        [거점][무역][인벤][설정]│ ← 재화 라인(버튼→공용 팝업)
│ 건물(스크롤/추가) │ 상단(캐러밴) │ 상인   │ ← 정보창 3분할
└──────────────────────────────────────┘
```

## 2. 씬 구조 (방법 B — 씬 분리 + RenderTexture)

- **`07.Scenes/04_InGame/InGameMainUITest.unity`** — 메인 UI 씬 (테스트용)
  - `MainUICanvas`: BackgroundWall / VillageView(RawImage) / InfoPanel / WorldMapPanel / MenuPopup / BuildingAddPopup
  - `SceneLoader`(AdditiveSceneLoader): Play 시 마을 씬을 additive 로드
- **`07.Scenes/04_InGame/Village_Home.unity`** — 거점 마을 씬 (3D)
  - Ground + 건물(상점/창고/목장) + VillageCamera(→ RT_Village) + BuildingRegistry
- **`05.UI/04_InGame/YHY/RenderTextures/RT_Village.renderTexture`** — 마을 카메라 출력

> **마을 담당은 Village_Home 씬만 채우면 됨.** UI는 RenderTexture로 자동 반영. UI/마을 협업 분리.

## 3. 스크립트 (UI ↔ 빌딩 시스템 분리)

**빌딩 시스템 (코어)** `01.Core/07_Village/YHY/`
- `VillageBuildingRegistry` — 건물 데이터·레벨·하이라이트·카탈로그. static Instance(싱글톤)로 UI가 접근. **UI와 독립**

**UI** `05.UI/04_InGame/YHY/Scripts/`
- `SlidePanel` — 화면 밖 슬라이드 패널(월드맵). 앵커 기준 openPos/closedPos
- `AdditiveSceneLoader` — 지정 씬 additive 로드
- `PanelOpener` — 버튼 → 공용 팝업 열기/제목 세팅
- `BuildingListPanel` — 건물 스크롤 리스트(클릭 하이라이트 + [+] 추가 팝업)
- `BuildingAddPopup` — 건물 추가 팝업(카탈로그 선택)

## 4. 마을 건물 시스템

- 건물은 **종류별 하나씩**. 기본 = 상점/창고/목장 (Lv.1)
- **카탈로그**(BuildingRegistry에 등록): 상점/창고/목장/대장간/시장/여관 — 프리팹은 공용 `08.Prefabs/Village/Building_GrayBox.prefab`
- [+] → 추가 팝업 → 종류 선택:
  - **이미 있으면 레벨업** (새로 안 지음)
  - **없으면 신축** (Lv.1)
- 리스트·팝업에 레벨 표시(있으면 Lv.n, 없으면 Lv.0)
- 리스트 항목 클릭 → 마을 씬 해당 건물 3D 하이라이트

## 5. 반응형(중요)

- 월드맵·마을창·정보창·배경벽 모두 **앵커 기준 배치**(고정 좌표 아님) → 화면 크기 바뀌어도 안정
- 월드맵은 **오른쪽 가장자리 앵커 + 세로 stretch**, 세로 크기는 VillageView와 동일
- ⚠️ 단, 월드맵 **가로 폭은 고정(980px)** — 가로형 게임 전제. 세로(모바일 세로)까지 지원하면 폭도 비율 기반 작업 필요 (타겟 방향 팀 확인 필요)

## 6. 다음 담당자에게 (인수인계)

- **Build Settings**에 `InGameMainUITest`, `Village_Home` 등록됨 (additive 로드 위해). 씬 이름 바뀌면 로더/빌드 확인
- 재화 라인 버튼(거점/무역/인벤토리/설정)은 **공용 MenuPopup**에 제목만 바뀜 → 실제 기능 확정 시 각 패널로 분리. **무역 버튼**은 우리 무역준비 플로우(`UI_TradePrepare`)와 연결 예정
- 재화 = **소지금(골드)만** (자원 생산 시스템 미정). 이종현님 CurrencyHudPresenter 재활용 여부 조율
- 건물 프리팹은 지금 **공용 그레이박스 1종**. 건물 종류별 프리팹 만들어 카탈로그에 연결하면 확장
- 마을 확장·업그레이드로 **기능 확장** 예정 — 레벨 시스템은 토대만(레벨업 로직만, 실제 효과 미구현)
- 정보창 **상단(캐러밴)/상인** 영역은 아직 빈 그레이박스
- 미완: 무역 버튼 연동, 재화 실연결, 각 팝업 실기능, 상단/상인 패널 내용

## 7. 주의 (커밋/환경)

- 개인 도구(unity-mcp)·`Assets/Screenshots`는 커밋에 안 들어감 (dev2에서 이미 처리됨)
- 그레이박스라 색·크기·위치는 임시. 아트 확정 시 교체
- 테스트: `InGameMainUITest.unity` 열고 ▶ Play → 마을 3D 표시, 지도 슬라이드, 건물 추가/레벨업/하이라이트 확인
