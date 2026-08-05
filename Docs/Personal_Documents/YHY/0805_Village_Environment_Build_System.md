# 마을 환경 아이템 건설 시스템 (데이터 모델 분리)

작성일: 2026-08-05 / 담당: 윤호영 / 브랜치: feature/Core-village-env-add-YHY

## 배경 — 왜 건물과 데이터를 나눴나

환경 아이템(오크나무·벤치·울타리 등)은 처음엔 재현님의 건물 파이프라인(BuildData + 건설 트랜잭션)을 그대로 재사용했다. 하지만 근본 성격이 다르다:

| 구분 | 건물 | 환경 아이템 |
|---|---|---|
| 개수 | 종류당 **1개** | 종류당 **여러 개** |
| 진행 | **레벨업** | 레벨 없음 |
| 비용 | 아이템(requireItems) | **거래재화(Gold=tradingCurrency)** |
| 삭제 | 없음 | **있음(환불 없음)** |
| 저장 키 | displayName | 인스턴스별 **instanceId** |

건물은 `villageBuildings`(displayName→level)에 저장돼, 같은 종류를 2개 놓으면 서로 덮어써 "레벨2 외형 없음" 에러가 났다. → 환경 전용 경량 경로로 분리.

## 구현 (파일별)

**데이터**
- `SaveData.cs`(11.CoreServices) — `VillageEnvironmentSaveData{instanceId, envId, gridCellX, gridCellZ, yawStep}` + `PlayerSaveData.villageEnvironments` 리스트 신설. 건물과 별개, 다중·회전·레벨없음.
- `VillageBuildingRegistry.cs` — `CatalogEntry.envCost`(long, 거래재화) 추가. `TryGetCatalogEnvironmentEntry`, `BuildEnvironmentInstance`(건물 목록/저장 안 건드리고 외형·footprint만 재사용) 추가.

**컴포넌트**
- `PlacedEnvironment.cs`(신규) — 설치 인스턴스 마커(instanceId, envId). 이동/삭제/저장 분기 기준.
- `VillageEnvironmentManager.cs`(신규) — 건설(Gold 차감→생성→배치→저장), 이동/회전 저장 갱신(UpdatePlacement), 삭제 저장(RemoveEnvironmentSave). 팝업 BuildConfirmed 구독(환경만 처리).

**배치/복원/삭제** (`BuildingPlacementController.cs`)
- CommitCurrentPlacement에서 PlacedEnvironment 감지 → 환경 저장 경로로 분기(CommitEnvironmentPlacement).
- RegisterNewEnvironment(빈 칸 배치), UnregisterEnvironment(해제), RestoreEnvironments(로드 복원), DeleteSelectedEnvironment(+빨간 ✕ 삭제 버튼 UI).
- 환경은 NPC 목표에서 제외.

**분기 가드** (`BuildingConstructionRuntimeHandler.cs`, 재현님 영역)
- 환경 buildId면 early-return → 환경 매니저가 처리. 건물 트랜잭션 무영향.

**씬 배선** (InGame + Village_Home)
- VillageEnvironmentManager를 `BuildingConstructionRuntime`(InGame)에 부착, popup/controller/notice 참조 연결.
- Village_Home 레지스트리 카탈로그 11개 환경 항목에 envCost 기본값 설정.

## 흐름 요약

- **건설**: 환경 탭 → 확정 → Gold≥envCost 확인·차감 → 인스턴스 생성 → 빈 칸 배치 → villageEnvironments append → 저장. 부족하면 NoticeUI 안내.
- **이동/회전**: 편집모드 드래그/회전 → PlacedEnvironment 감지 → instanceId 기준 셀·회전 저장.
- **삭제**: 편집모드에서 환경 선택 → 아래 빨간 ✕ → 저장 제거 + 격자 해제 + 파괴(환불 없음).
- **복원**: 로드 시 villageEnvironments 순회 재생성.

## 남은 것(팀/후속)

1. **팝업에 Gold 비용 표시** — 현재 재현님 팝업은 requireItems만 보여줘 환경은 "무료"처럼 보임. Gold는 확정 후 차감됨. → 팝업 뷰데이터에 재화 비용 노출 협의 필요.
2. **envCost 밸런싱** — 현재 80~250 임시값. 기획 확정 필요.
3. **저장 계약** — villageEnvironments 필드 추가를 CSU님께 공유(SaveData_V2 계약).
4. **인게임 검증** — 같은 종류 다중 배치/이동/회전/삭제/리로드 지속 확인.

관련: [[0804_Village_Environment_Items]](이전 아이템 추가 작업), footprint 정사각화(회전 드리프트 수정).
