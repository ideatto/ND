# Caravan Setting Service 책임 분리 작업 기록

## Purpose

- `TestCaravanSettingService`가 Caravan 편성, 화물, 저장 데이터 조회, 시장 카탈로그, 선택 옵션과 UI 명령을 모두 담당해 변경 영향 범위가 지나치게 넓었다.
- 기존 Scene과 UI 계약을 유지하면서 계산·조회 책임을 독립 서비스로 분리한다.
- 이번 변경은 `TestCaravanSettingService` 제거가 아니라, 제거 가능한 얇은 facade로 축소하는 1단계 작업이다.
- C# 단위 테스트뿐 아니라 실제 `InGame` UI 연결까지 검증한다.

## Scope

### 변경

- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Temporary/TestCaravanSettingService.cs`
- `Assets/_Project/11.CoreServices/Scripts/CaravanSetup/`
  - 책임별 서비스 6종과 `.meta`
- `Assets/_Project/11.CoreServices/Editor/`
  - 서비스 단위 테스트 6종
  - facade integration 테스트
  - Scene 계약 및 Missing Script 테스트
  - PlayMode UI smoke 테스트
- 위 신규 테스트의 `.meta`

### 확인만 수행

- `InGame`, `InGame_Test`, `Boot`, `Title` Scene
- Caravan Setting/Cargo UI의 Provider 및 Command 연결
- `FrameworkRoot.Instance.CurrentSaveData`와 SaveData 조회 흐름
- `QuestGenerationSettings.asset`의 스크립트 GUID

### 제외

- Scene, Prefab, ScriptableObject 및 Package 변경
- SaveData 구조와 마이그레이션
- 기존 UI Provider/Command public 계약 교체
- production `CaravanSettingService` 도입과 기존 facade 삭제
- 기존 사용자 변경인 `GameCalendarPanel.prefab`

## Ownership

- `TestCaravanSettingService` 최근 이력: `junghen001-oss`, `ljh-ccc`
- `InGame.unity` 최근 이력: `ideatto`, `junghen001-oss` 등
- 후속 production facade 전환은 Scene과 Framework 구성 방식에 영향을 주므로 관련 소유자 리뷰가 필요하다.

## Changes

### 의존 구조

```text
Scene / UI Binding
        │
        │ Provider · Command 계약
        ▼
TestCaravanSettingService
(현재 facade / composition root)
        │
        ├── CaravanCompositionDraftService
        ├── CaravanCargoDraftService
        ├── CaravanMarketCatalogService
        ├── CaravanSaveQueryService
        ├── CaravanSavedCargoService
        └── CaravanSelectionOptionService
                    │
                    ▼
      SaveData · CaravanSaveData · Inventory
```

- 신규 서비스는 `TestCaravanSettingService`를 참조하지 않는다.
- facade가 각 서비스에 필요한 데이터와 의존성을 전달한다.
- `FrameworkRoot.Instance` 접근은 facade에만 남겨 하위 서비스의 전역 상태 의존을 막는다.

### 책임 분리

| 서비스 | 책임 |
| --- | --- |
| `CaravanCompositionDraftService` | 인원·운송수단 편성 초안 생성과 적용 |
| `CaravanCargoDraftService` | 화물 편집 초안 생성과 commit 입력 구성 |
| `CaravanMarketCatalogService` | 화물 UI용 시장 카탈로그와 기본 재고 구성 |
| `CaravanSaveQueryService` | `caravanId` 기준 저장 Caravan 조회 |
| `CaravanSavedCargoService` | 저장 화물을 UI 편집 상태로 변환 |
| `CaravanSelectionOptionService` | 인원·운송수단 선택 옵션과 선택 상태 구성 |

### facade 역할

- `TestCaravanSettingService`는 기존 Scene 직렬화 참조와 Provider/Command 계약을 유지한다.
- 내부 계산과 데이터 가공은 책임별 서비스에 위임한다.
- 테스트에서는 `saveDataOverrideForTests`, 실제 실행에서는 `FrameworkRoot.Instance.CurrentSaveData`를 사용한다.
- 기존 UI를 유지한 채 하위 로직을 독립적으로 검증할 수 있게 되었다.

### Scene 연결

- 원본 ND의 `InGame.unity`에는 현재 facade와 Setting/Cargo Provider·Command 참조가 정상 연결되어 있었다.
- 기존 serialized field와 interface 계약을 유지했기 때문에 Scene을 다시 저장하지 않았다.
- Scene에 기록된 스크립트 GUID와 실제 `.meta` GUID가 일치함을 확인했다.

## Architectural Direction

### 현재 단계

- 책임별 로직은 분리됐지만 production Scene은 아직 `TestCaravanSettingService`를 composition root로 사용한다.
- 따라서 이번 변경은 **책임 분리 1단계**이며 최종 구조는 아니다.
- 각 하위 서비스가 `FrameworkRoot.Instance`를 직접 참조하게 만들면 입력과 의존성이 숨겨지고 테스트가 전역 상태에 종속되므로 피한다.

### 다음 단계

- Core/Framework 계층에 production `CaravanSettingService` 또는 `CaravanSettingApplicationService`를 둔다.
- 기존 UI Provider/Command 계약을 production facade로 이전한다.
- 현재 SaveData provider, 저장/transaction, catalog/inventory 의존성을 명시적으로 주입한다.
- Framework의 composition root에서 production facade를 구성하고 UI Binding에 전달한다.
- Scene 참조 교체와 회귀 테스트가 끝나면 `TestCaravanSettingService`를 Test 전용으로 이동하거나 삭제한다.

### 현 구조를 장기간 유지할 경우의 위험

- production Scene이 Test 이름의 타입에 계속 묶여 역할과 소유권을 혼동시킨다.
- 신규 orchestration이 임시 facade에 다시 쌓이면 거대 서비스가 재발한다.
- 최종 제거 시 C#과 Scene 직렬화 참조를 함께 변경해야 해 전환 비용이 커진다.

## Check

- 원본 저장소: `C:\unity\ND`
- 작업 브랜치: `feature/refactoring/connection/ofdata/jjh`
- 기준 HEAD: `71f1a153f80c5b8559e87ea5b56f2ab53e4b2025`
- Unity C# 컴파일
  - Assembly-CSharp 및 Assembly-CSharp-Editor 재빌드 성공
  - 컴파일 오류 0
- 서비스 및 facade integration EditMode 테스트: 28/28 통과
- Scene 계약 및 Missing Script EditMode 테스트: 4/4 통과
- 실제 UI 연결 PlayMode smoke 테스트: 1/1 통과
- 전체 신규 검증: 33/33 통과
- `QuestGenerationSettings.asset`
  - ND-review에서는 잘못된 스크립트 GUID를 실제 `.meta` GUID로 교정해 검증했다.
  - 원본 ND는 이미 올바른 GUID를 사용하고 있어 원본 asset은 변경하지 않았다.
- Scene/Prefab/SO: 이번 작업으로 인한 Git 변경 없음
- Console
  - Missing Script 오류 없음
  - Quest settings fallback 경고 없음
  - 기존 경고 1건: `[WorldMap] Overlay labels could not be bound because the render presenter is unavailable.`

## Risk

- Scene 변경: No
- Prefab 변경: No
  - `GameCalendarPanel.prefab`은 기존 사용자 변경이며 이번 범위에서 제외한다.
- Meta 변경: Yes
  - 신규 서비스 폴더·스크립트와 테스트의 `.meta`가 추가된다.
- Package 변경: No
- SaveData 구조 변경: No
- 직렬화 필드 변경: No
- Enum 직렬화 변경: No
- Public API 변경: No
- 이벤트 연결 변경: No
- 다중 객체 또는 ID 연결 변경: Yes
  - 편집 대상과 command 전달을 `caravanId` 기준으로 검증한다.
- 기존 데이터 마이그레이션 필요: No
- 원천 소유자 리뷰 필요: Yes
  - production facade 위치, Framework 구성과 Scene 교체 시점의 합의가 필요하다.

## PR Scope Checklist

- 포함
  - `TestCaravanSettingService.cs`
  - `CaravanSetup` 신규 서비스 6종과 `.meta`
  - 신규 테스트 10종과 `.meta`
  - 이 작업 기록 문서
- 제외
  - `GameCalendarPanel.prefab`
  - Scene, Prefab, ScriptableObject, Package 파일
- PR에는 “Test 서비스 제거 완료”가 아니라 “책임 분리 1단계 및 Scene 회귀 검증 완료”로 기록한다.

## Remaining

- production facade의 위치와 의존성 계약 합의
- Framework composition root 구성과 InGame Scene 참조 교체
- 교체 후 Scene 및 PlayMode 회귀 검증
- `TestCaravanSettingService`의 Test 전용 이동 또는 삭제
- 관련 코드 및 Scene 소유자 리뷰
- WorldMap overlay 기존 경고 별도 확인
