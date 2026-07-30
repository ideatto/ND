# Caravan Activity Log 및 전투 연출 작업 기록

- 작업 기준일: 2026-07-29
- 기준 브랜치: `featuer/combatactionpanel/jjh`
- 기준 커밋: `d5b9de4`

## Purpose

- 캐러반별 무역 진행 상황을 시간순 말풍선 로그로 표시한다.
- 무역 출발, 산적 조우, 전투 승패, 목적지 도착을 SaveData 기반으로 복원 가능하게 기록한다.
- 산적 전투 시작·승리·패배 상황에 서로 다른 이미지와 VFX를 연결할 수 있게 한다.
- 산적에게 화물이나 용병을 잃었지만 무역이 계속되는 비치명적 패배도 승리로 오인하지 않게 한다.

## Scope

### 변경

- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeRouteEventProcessor.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`
- `Assets/_Project/11.CoreServices/Editor/TradeRouteEventProcessorTests.cs`
- `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/CaravanCombatSequencePanel.cs`
- `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/CaravanCombatSequencePanel.cs.meta`
- `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/Prefabs/CaravanCombatSequencePanel.prefab`
- `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/Prefabs/CaravanCombatSequencePanel.prefab.meta`
- `Assets/_Project/11.CoreServices/Editor/CaravanActivityLogPrefabBuilder.cs`

### 연동 확인

- `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeStartService.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/CaravanActivityLog.cs`
- `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/CaravanActivityLogPanel.cs`
- `Assets/_Project/05.UI/09_QoL/CaravanActivityLog/CaravanActivityLogItemView.cs`

### 제외

- Scene 배치
- 실제 이미지 및 VFX 에셋 제작
- Package 설정
- SaveData version 증가
- 기존 저장 데이터의 별도 마이그레이션

## Ownership

- Framework 및 무역 진행 생산자:
  - `TradeRouteEventProcessor`
  - `TradeProgressCoordinator`
  - `SaveData`
  - 현재 원천 소유자: Framework & Integration
- 캐러반 활동 로그 및 전투 표시 소비자:
  - `CaravanActivityLog`
  - `CaravanActivityLogPanel`
  - `CaravanCombatSequencePanel`
  - 현재 유지보수자: 확인 필요
- 판단 근거:
  - 파일 경로, Technical Ownership 주석 및 `dev2` Framework 통합 커밋을 기준으로 구분했다.

## Changes

### 활동 로그 저장

- `SaveData.caravanActivityLogs`에 캐러반 활동 로그를 시간순으로 보관한다.
- 각 항목은 다음 identity를 가진다.
  - `sequence`
  - `occurredUtcTicks`
  - `caravanId`
  - `tradeId`
  - `routeId`
  - `townId`
  - `routeEventId`
  - `eventType`
- 기본 최대 보관 수는 100개이다.
- 기존 저장 데이터에서 목록이 null이면 `JsonSaveService.NormalizeData`가 빈 목록으로 보정한다.

### 산적 승패 전달 수정

- `JourneyRunner.ResolveBanditRaid`가 반환하는 `passedSafely`를 실제 전투 승패 원천으로 사용한다.
- `RouteEventOccurrence`에 nullable `CombatVictory` 결과를 추가했다.
- `IsFatal`은 캐러반의 무역 지속 가능 여부로 유지하고 전투 승패와 분리했다.
- 활동 로그 변환 규칙:
  - 산적 조우: `CombatEncounter`, JSON `eventType: 1`
  - 안전 통과/승리: `CombatVictory`, JSON `eventType: 2`
  - 산적전 패배: `CombatDefeat`, JSON `eventType: 3`
- 비치명적 패배도 `eventType: 3`으로 기록된다.
- 자동 Route Event와 강제 Route Event 양쪽에 같은 판정 규칙을 적용했다.

### 로그 패널

- 최신 로그가 아래에 추가된다.
- 사용자 조작이 없으면 최신 항목이 보이도록 하단으로 이동한다.
- 스크롤바를 조작한 뒤 5초 동안 자동 이동을 유예한다.
- 배경 및 로그 항목은 Raycast를 차단하지 않고 스크롤바만 입력을 받는다.
- 캐러반별 색상 아이콘으로 항목을 구분한다.

### 전투 연출 패널

- 산적 조우 로그와 같은 `caravanId + tradeId + routeEventId`의 승패 로그를 한 연출로 연결한다.
- 코루틴 재생 순서:
  1. 산적 조우 화면
  2. 전환 페이드
  3. 승리 또는 패배 화면
  4. 결과별 모션
  5. 종료 페이드
- 조우·승리·패배별 Sprite 슬롯을 제공한다.
- 조우·승리·패배별 선택형 VFX Prefab 슬롯을 추가했다.
  - `encounterVfxPrefab`
  - `victoryVfxPrefab`
  - `defeatVfxPrefab`
- 단계 전환 시 이전 VFX를 비활성화하고 제거한 뒤 새 VFX를 생성한다.
- 패널 비활성화 또는 연출 종료 시 활성 VFX를 정리한다.
- VFX 부모는 `vfxRoot`를 사용하며, 미지정 시 `cardRoot`를 사용한다.
- Particle System 또는 Animator가 활성화 시 자동 재생되는 Prefab을 연결하는 방식이다.

### Prefab Builder

- `ND/UI/Create Caravan Activity Log Prefabs` 메뉴에서 다음 Prefab을 생성한다.
  - `CaravanActivityLogItem.prefab`
  - `CaravanActivityLogPanel.prefab`
  - `CaravanCombatSequencePanel.prefab`
- 전투 패널에는 `StageVfxRoot`가 생성되고 `vfxRoot`에 연결된다.

## Check

- `dotnet build Assembly-CSharp.csproj --no-restore`
  - 산적 승패 전달 수정 후 오류 0
  - 기존 경고 41
- 전투 VFX 파일 이식 후 기존 생성 csproj 빌드
  - 오류 0
  - 기존 경고 14
  - 신규 `CaravanCombatSequencePanel.cs`는 Unity Asset Refresh 전 생성 csproj에 아직 포함되지 않음
- `git diff --check`
  - 통과
- `TradeRouteEventProcessorTests`에 추가한 시나리오:
  - 안전 통과가 승리로 전달됨
  - 비치명적 산적전 패배가 패배로 전달됨
  - 강제 산적 이벤트가 실제 결과를 전달함
- Unity Test Runner
  - 수동 실행 필요
- Unity Play Mode 전투 이미지 및 VFX 연출
  - 실제 에셋 할당 후 수동 확인 필요

## Risk

- Scene 변경: No
- Prefab 변경: Yes
  - 전투 연출 Prefab과 상황별 VFX 참조 슬롯이 추가됐다.
- Meta 변경: Yes
  - 신규 전투 패널 Script 및 Prefab GUID를 유지하기 위한 Meta가 추가됐다.
- Package 변경: No
- SaveData 구조 변경: Yes
  - 캐러반 활동 로그 목록을 저장한다.
- 직렬화 필드 변경: Yes
  - 활동 로그 DTO와 전투 패널 Sprite/VFX 참조 필드가 추가됐다.
- Enum 직렬화 변경: No
  - 기존 enum 숫자는 변경하지 않고 별도의 활동 로그 enum을 사용한다.
- Public API 변경: Yes
  - `RouteEventOccurrence.CombatVictory`와 전투 패널 VFX 접근자가 추가됐다.
- 이벤트 연결 변경: Yes
  - 실제 산적 판정 결과가 승리·패배 로그를 결정한다.
- 다중 객체 또는 ID 연결 변경: Yes
  - `caravanId`, `tradeId`, `routeEventId` 조합으로 조우와 결과를 연결한다.
- 기존 데이터 마이그레이션 필요: No
  - 누락된 로그 목록은 빈 목록으로 정규화된다.
- 원천 소유자 리뷰 필요: Yes
  - Framework의 Route Event 결과 계약에 nullable `CombatVictory`가 추가됐다.

## Remaining

- Unity에서 `Assets > Refresh` 후 신규 스크립트 컴파일 확인
- Unity Test Runner에서 Route Event 관련 테스트 실행
- 조우·승리·패배 Sprite 및 VFX Prefab 할당
- Play Mode에서 자동/강제 산적 이벤트의 승패 연출 확인
- Framework 원천 소유자의 `RouteEventOccurrence.CombatVictory` API 리뷰
