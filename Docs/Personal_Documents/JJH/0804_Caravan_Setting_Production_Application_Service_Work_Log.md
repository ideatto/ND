# Caravan Setting Production Application Service 2A 작업 기록

> 이 문서는 개인 구현·검증 기록이다. 팀 공용 구조, API 사용법과 후속 Scene 이전 조건의 기준 문서는 [Caravan Setting Production Service Contract](../../Contract/Caravan_Setting_Production_Service_Contract.md)이다.

## Purpose

- production Scene이 `TestCaravanSettingService`에 직접 의존하는 구조를 제거하기 전에 Framework 소유 application boundary를 만든다.
- SaveData 조회, SharedGameData 시장 조회와 저장 transaction을 명시적으로 주입한다.
- 저장 스키마에 아직 없는 미배치 wagon/animal inventory를 임시 fixture로 대체하지 않는다.

## Scope

### 변경

- `Assets/_Project/11.CoreServices/Scripts/CaravanSetup/CaravanSettingApplicationService.cs`
- `Assets/_Project/11.CoreServices/Scripts/CaravanSetup/CaravanSettingRuntimeBridge.cs`
- `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs`
- `Assets/_Project/11.CoreServices/Editor/CaravanSettingApplicationServiceTests.cs`
- 위 신규 파일의 `.meta`

### 제외

- Scene, Prefab, ScriptableObject, Package
- SaveData schema/version과 migration
- 기존 `TestCaravanSettingService`와 기존 Scene 직렬화 참조
- 미배치 wagon/animal 보유 inventory 설계

## Ownership

- `FrameworkRoot.cs`: Framework & Integration 소유. 최근 이력은 `csu1222` 중심이다.
- 기존 UI Provider/Command 계약과 `CaravanOverviewEditBinding`: 최근 이력은 `ljh-ccc` 중심이다.
- `TestCaravanSettingService`: 최근 이력은 `junghen001-oss`, `ljh-ccc`이다.
- FrameworkRoot 공개 API와 향후 Scene 교체는 관련 소유자 리뷰가 필요하다.

## Changes

- `CaravanSettingApplicationService`가 기존 Provider/Command 6개 계약을 구현한다.
- `Func<SaveData>`, `ISaveService`, `Func<ISharedGameDataProvider>`와 Unity content asset resolver를 명시적으로 주입한다.
- Caravan 조회, 시장 catalog, 저장 cargo 정규화와 출발 선택 옵션은 1단계에서 분리한 책임별 서비스에 위임한다.
- setting/cargo command는 모든 검증 후 SaveData를 변경하고 한 번 저장한다.
- 저장 실패 또는 예외 시 command 이전 SaveData JSON snapshot으로 원복한다.
- `FrameworkRoot`가 production application service를 생성하고 `CaravanSetting`으로 공개한다.
- `CaravanSettingRuntimeBridge`는 Scene이 요구하는 MonoBehaviour 계약과 `TradeItemData` asset ID 매핑만 담당한다.
- 미배치 보유 inventory가 없으므로 setting command는 현재 Caravan에 저장된 wagon과 animal 인스턴스 집합 내부의 변경만 허용한다.
- wagon/animal definition ID가 SaveData에 없으므로 view의 content definition ID는 비워 둔다. 이를 추측이나 이름 매칭으로 채우지 않는다.

## Check

- `git diff --check`
  - 통과
- `CaravanSettingApplicationServiceTests`
  - 조회, 성공 저장, 저장 실패 원복, authoritative market cargo 저장 시나리오 4종 추가
  - Unity `6000.5.2f1` EditMode 4/4 통과
- Unity batch compile 시도
  - 프로젝트 요구 버전: `6000.5.2f1`
  - 설치 버전: `6000.3.14f1`
  - 두 차례 모두 `Application.AssetDatabase Initial Refresh Start`에서 로그 갱신 없이 정체되어 프로세스를 종료함
  - compile 성공으로 간주하지 않음
- Unity 생성 csproj import 기반 `dotnet build --no-restore`
  - 격리 worktree의 `Library/PackageCache`가 완성되지 않아 Unity package 참조와 `Assembly-CSharp` 참조를 해석하지 못함
  - 변경 runtime 파일 이름으로 필터링한 출력에는 개별 C# 진단이 없었으나 전체 build 실패이므로 compile 통과로 간주하지 않음
  - 검증용 임시 csproj는 제거함
- 관련 EditMode 회귀
  - 책임별 서비스, 기존 facade integration, Scene 계약, Missing Script, 신규 application service
  - 36/36 통과
  - C# compile 오류 0
- 실제 UI smoke
  - `-nographics` 실행은 URP NullGfxDevice native crash로 결과 미생성
  - graphics batch 재실행은 테스트 자체를 실행했으나 0/1 실패
  - 실패 원인: 현재 영속 save가 `Traveling` 상태라 Setting/Cargo action을 제공하는 Prepare Caravan이 없음
  - 연결 누락, Missing Script 또는 compile 실패가 아니며 사용자 save를 초기화하지 않음
  - 기존 WorldMap overlay 경고와 MissingRoute 경고가 출력됨
  - 사용자 영속 save와 분리하기 위해 PlayMode 진입 후 runtime SaveData만 JSON snapshot으로 백업한다.
  - 메모리에 `Prepare` Caravan fixture를 적용하고 테스트 종료 전에 원래 runtime snapshot을 복원한다.
  - SaveService의 Save/Reset API는 호출하지 않는다.
  - graphics batch 재실행: 1/1 통과
  - 로그에서 fixture 적용 전후 `Traveling → Preparation → Traveling` 복원을 확인했다.

## Risk

- Scene 변경: No
- Prefab 변경: No
- Meta 변경: Yes
  - production service, runtime bridge, tests와 문서의 `.meta`가 추가된다.
- Package 변경: No
- SaveData 구조 변경: No
- 직렬화 필드 변경: Yes
  - 신규 runtime bridge에 `TradeItemData[] tradeItemAssets`가 있으나 아직 Scene에 추가하지 않았다.
- Enum 직렬화 변경: No
- Public API 변경: Yes
  - `FrameworkRoot.CaravanSetting` 접근점이 추가된다.
- 이벤트 연결 변경: No
- 다중 객체 또는 ID 연결 변경: Yes
  - 모든 조회와 command는 명시적 `caravanId`를 사용한다.
- 기존 데이터 마이그레이션 필요: No
- 원천 소유자 리뷰 필요: Yes
  - FrameworkRoot API, wagon/animal definition ID 및 보유 inventory 계약 검토가 필요하다.

## Remaining

- production definition ID와 미배치 보유 inventory 계약 확정
- wagon/animal definition ID와 미배치 보유 inventory 계약 확정
- 계약 확정 후 runtime bridge에 production content asset을 연결
- InGame Scene 참조 교체와 Scene/PlayMode 회귀 검증
- 검증 후 `TestCaravanSettingService` 이동 또는 제거
