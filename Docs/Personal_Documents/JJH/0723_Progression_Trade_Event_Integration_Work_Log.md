# Progression 무역 이벤트 통합 및 검증 작업 로그

- 작성일: 2026-07-23
- 담당: Progression & System / Economy
- 상태: 런타임 코드 연결 완료, Unity 플레이 검증 및 실제 콘텐츠 연결 대기
- 기준 문서:
  - `00_Team_Rules_and_Milestone_Second_Build.md`
  - `05_Progression_System_Milestone_Second_Build.md`
  - `Earn_Money_While_Lying_Down_Game_Design.md`

## 1. 문서 분리 원칙

기존 `0723_Progression_Unresolved_Design_Decisions.md`에 정리한 미결 항목과 기존 분류는 삭제하거나 이 문서로 합치지 않는다.

기존 문서는 향후 기획 협의와 이전 임시 구현 추적에 사용하도록 그대로 보존한다. 이 문서는 2026-07-23에 실제 검증하고 프로젝트에 반영한 작업만 별도로 기록한다.

## 2. 오늘 확정하여 적용한 이벤트 규칙

### 2.1 거리 기반 이벤트 판정

- 이벤트 판정 횟수는 `floor(이동 거리 ÷ 이벤트 간격)`을 사용한다.
- 한 번의 무역에서 이벤트가 여러 번 발생할 수 있다.
- 최소·최대 발생 횟수는 별도로 제한하지 않는다.
- 이벤트 발생 확률과 이벤트 간격은 현재 `TradeEventPreviewCalculator`가 소유한다.
- 현재 임시 기본값은 이벤트 간격 100km, 판정 1회당 발생 확률 20%이다.
- 동일한 Trade ID와 판정 인덱스는 온라인·오프라인·저장 복원 후에도 같은 결과를 재현한다.
- 이미 처리한 판정 인덱스를 저장하여 같은 이벤트가 중복 실행되지 않게 한다.

### 2.2 산적 이벤트와 용병

- Caravan은 용병을 한 명만 선택해 사용한다.
- 무사 통과 확률은 다음 계산식을 사용한다.

```text
기본 안전 보정 확률 + (용병 전투력 ÷ 산적 전투력 × 50)
```

- 최종 확률은 0~100으로 제한한다.
- 용병이 없으면 Caravan의 기본 안전 보정 확률만 사용한다.
- 용병과 산적의 전투력은 Data에서 공급한다.
- 전투 실패 시 선택한 용병은 즉시 Caravan에서 제거된다.
- 제거된 용병의 instanceId는 이번 무역 결과에 누적한다.

### 2.3 상품과 여물 약탈

- 일반 상품과 여물은 서로 다른 약탈률을 사용한다.
- 각 그룹의 현재 잔량에 약탈률을 적용한 뒤 한 번 올림한다.
- 상품은 남은 상품 전체에서 무작위로 선택해 차감한다.
- 상품 손실에는 한 무역 전체의 누적 `lossLimitRate` 상한을 적용한다.
- 여물 약탈은 상품 손실 상한과 별개로 처리한다.
- `lossLimitRate=0`은 유효한 완전 손실 보호 값으로 저장·복원한다.
- 음수, 1 초과, NaN, 무한대 값만 안전 기본값 1로 복구한다.

## 3. 폐기하거나 정리한 이전 구현

### 3.1 이전 산적 처리 제거

다음 이전 임시 규칙을 제거했다.

- 용병 수만큼 전투를 자동 방어하는 `ResolveRaid`
- 산적 실패 시 고정 상품 수량과 마차 내구도를 함께 감소시키는 처리
- `raidDurabilityDamage`, `raidCargoDamage`, `limitRaidDurability` 기반 처리

`JourneyRunTest`는 실제 `ResolveBanditRaid` 호출 방식으로 변경했다.

### 3.2 내구도 손실 API 유지

`ApplyDurabilityLoss`는 향후 전투 이벤트 실패에 사용할 공식 마일스톤 API이므로 제거하지 않았다.

- 폐기된 산적 전용 토글만 제거했다.
- 이벤트성 내구도 손실은 `lossLimitRate × 최대 내구도` 누적 상한을 따른다.
- 기존 마차 파괴와 출발 차단 로직은 변경하지 않았다.

### 3.3 ForceRouteEvent pending hook 제거

기존 `ForceRouteEvent`는 실제 결과를 적용하지 않고 pending ID만 남겼다.

현재는 다음 순서로 변경했다.

1. Traveling Trade와 Route를 검증한다.
2. Route에서 정확한 eventId를 찾는다.
3. 구현된 Combat 이벤트만 실제 산적 처리 경로로 실행한다.
4. Caravan 변경 결과를 SaveData에 반영하고 즉시 저장한다.
5. 적용과 저장이 성공한 후 `RouteEventForced` 알림을 발행한다.

사용되지 않게 된 `TryConsumeForcedRouteEvent`와 pending 필드는 제거했다.

## 4. 저장 및 정산 연결

다음 결과를 Caravan run 상태와 저장 데이터에 연결했다.

- 처리한 거리 이벤트 판정 수
- 실제 완료된 이벤트 수
- 실제 처리된 Combat 수
- 상품 손실 수량
- 여물 손실 수량
- 소멸한 용병 instanceId 목록
- 마차 파괴 여부
- 파괴된 마차 instanceId
- Caravan 기본 안전 보정 확률

정산 전달 경로는 다음과 같다.

```text
CaravanData
→ JourneyResultData
→ PendingSettlementSaveData
→ PendingSettlementSaveDataMapper 저장·복원
→ SettlementViewData
```

기존 저장 데이터에 새 필드가 없으면 숫자는 0, bool은 false, 문자열과 목록은 빈 값으로 복원한다. 기존 Pending 결과 버전은 변경하지 않았다.

## 5. 이벤트 종류별 현재 처리 상태

| 이벤트 종류 | 현재 상태 | 처리 원칙 |
|---|---|---|
| Combat | 실제 처리 연결 완료 | 산적 판정, 상품·여물 약탈, 용병 소멸, 저장·정산 반영 |
| Lucky | 효과 공식 미확정 | 판정 인덱스만 소비하고 완료 이벤트로 집계하지 않음 |
| Weather | 효과 공식 미확정 | 판정 인덱스만 소비하고 완료 이벤트로 집계하지 않음 |

Lucky와 Weather에 임의 보상·손실·가격 계산을 추가하지 않았다. 해당 기획은 기존 미결 문서에서 계속 관리한다.

## 6. 기존 구현과의 결합 검증 결과

### 6.1 유지한 기존 구현

- 내구도 0일 때 `runWagonDestroyed=true`
- `WagonBroken` 실패 처리
- 파괴 시 적재 상품과 남은 여물 전손
- 파괴 마차 수리 불가 판정
- 내구도 0 또는 파괴 상태인 마차의 출발 차단
- 상품 손실 기반 부분 성공 판정
- Caravan과 진행 상태의 저장·복원

### 6.2 중복이 아니므로 유지한 필드

- `runEventsOccurred`: 실제 결과까지 처리된 모든 Route 이벤트 수
- `runBattlesFought`: 실제 처리된 Combat 이벤트 수

현재는 Combat만 구현되어 값이 같을 수 있지만, Lucky와 Weather가 구현되면 서로 다른 통계가 된다.

## 7. 주요 변경 파일

### Core

- `Assets/_Project/01.Core/04_TradeLoop/YHY/JourneyRunner.cs`
- `Assets/_Project/01.Core/04_TradeLoop/YHY/JourneyResultData.cs`
- `Assets/_Project/01.Core/05_Caravan/YHY/CaravanData.cs`
- `Assets/_Project/01.Core/05_Caravan/YHY/JourneyRunTest.cs`

### Economy 및 검증

- `Assets/_Project/03.Economy/06_Integration/TradePreparationEconomyPreview.cs`
- `Assets/_Project/03.Economy/06_Integration/Editor/TradeEventPreviewCalculatorTests.cs`

### Framework 저장·진행

- `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataView.cs`
- `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataService.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/CaravanSaveDataMapper.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/PendingSettlementSaveData.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/PendingSettlementSaveDataMapper.cs`

### Framework UI 및 Debug

- `Assets/_Project/11.CoreServices/Scripts/UI/Settlement/SettlementViewData.cs`
- `Assets/_Project/11.CoreServices/Scripts/UI/Settlement/SettlementUiDataAdapter.cs`
- `Assets/_Project/11.CoreServices/Scripts/Debug/FrameworkDebugCommands.cs`
- `Assets/_Project/11.CoreServices/Scripts/Debug/TradeStartDebugHarness.cs`
- `Assets/_Project/11.CoreServices/Scripts/Events/FrameworkEvents.cs`

## 8. 검증 결과

- `Assembly-CSharp.csproj` 전체 런타임 빌드 성공
- 컴파일 오류 0개
- 기존 경고 33개
- 주요 변경 파일 `git diff --check` 통과
- 동일 Trade ID·판정 인덱스의 결정적 재현 검증 추가
- 분할 진행과 일괄 진행의 결과 동일성 검증 추가
- Caravan 저장 왕복 및 중복 이벤트 방지 검증 추가
- 반복 약탈의 상품 손실 상한 검증 추가
- `lossLimitRate=0` 저장 왕복 검증 추가
- 미지원 이벤트가 완료 결과로 잘못 기록되지 않는 검증 추가
- 강제 Combat 이벤트가 거리 판정 수를 변경하지 않는 검증 추가

Editor 전용 전체 테스트 프로젝트는 기존 NUnit 대상 프레임워크 불일치가 있으므로 별도 환경 정리가 필요하다.

## 9. 다음 작업

1. 상품·여물 손실, 용병 소멸, 이벤트 횟수, 마차 파괴 결과를 한 시나리오로 묶은 통합 저장 왕복 테스트
2. 실제 Settlement 패널에서 새 ViewData 필드 표시 여부 확인
3. Data 팀의 실제 Combat RouteEventData가 추가된 뒤 플레이 모드 검증
4. Lucky와 Weather의 확정 기획 이후 실제 결과 처리 구현
5. Unity 실행 환경에서 다음 흐름 검증

```text
출발
→ Combat 강제 발생
→ 약탈 및 용병 소멸
→ 정산
→ 저장 및 재접속
→ 동일 결과 표시
```

## 10. 보존 문서

다음 문서는 이번 작업 로그와 별개로 유지한다.

- `0723_Progression_Unresolved_Design_Decisions.md`

해당 문서의 미결 내용, 현재 임의 구현 방식, 임시 값, 추가 기획 필요 항목은 삭제하지 않았으며 향후 팀 합의 시 계속 사용한다.
