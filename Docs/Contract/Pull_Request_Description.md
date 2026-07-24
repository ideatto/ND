# Pull Request

## Purpose

팀에서 확정한 구조 대출, 일시불 투자 퀘스트, 마차 파괴·수리, 건축 재료, SaveResult 및 복구 정책을 기존 SaveData 정책 문서에 일관되게 반영한다. 이번 범위는 정책 문서 정렬이며 production 코드와 Unity serialized asset은 변경하지 않는다.

## Changes

- Donation과 누적 Investment 목표 모델을 제거하고, 거래 화폐와 명시된 Caravan 무역품을 한 번에 제출하는 일시불 Investment Quest 계약으로 교체했다.
- 구조 대출을 Progression의 `RescueLoanCalculator` 계약에 맞춰 `MinimumTradeCost` 전액 발급, 활성 대출 중 중복 발급 금지, 제한 모드, 재파산 판정으로 정렬했다.
- 정산 Claim에서 대출 상환을 제거하고, 별도 `RepayRescueLoan(amount)` 명령의 부분·전액 상환과 재화·대출 상태 rollback 계약을 추가했다.
- 마차 내구도 0 파괴, 적재 무역품·식량 손실, Trade 실패 snapshot과 거래 화폐 기반 수리 공식을 추가했다.
- 건축물을 stable `buildingId` 기반으로 정의하고 Caravan 물품을 거점 임시 인벤토리로 이전한 뒤에만 업그레이드 재료로 사용하도록 정리했다.
- 목표 저장 API를 `SaveResult Save(SaveData data)` 하나로 확정하고 `TrySave()` 및 `void Save()` wrapper 제안을 과거안으로 표시했다.
- PreCommandSnapshot, LastDurableSnapshot, Disk Backup, 최대 3회 retry, 중요 Command 직렬 queue 및 rollback UI 정책을 정렬했다.
- 복구 테스트 매트릭스를 확정 정책의 정상·거절·중복·저장 실패 시나리오로 갱신했다.

## Check

- 지정된 SaveDataPolicy 문서 간 Donation, Investment, rescue loan, Save API, snapshot·rollback 표현을 교차 검색했다.
- Git diff로 변경 파일이 Markdown 문서에만 한정되는지 확인했다.
- Unity Console 및 Editor compilation/runtime verification: 확인 필요
- 구조 대출 발급·부분/전액 상환·제한 해제·재파산 판정, 정산 자동 상환 제거, 파괴·수리, 건축 업그레이드 runtime 테스트: 확인 필요

## Risk

- Scene: No
- Prefab: No
- Meta: No
- Package: No
- ScriptableObject/data: No
- 기존 동작, Save Version, 저장 데이터 및 public API는 이 문서 브랜치에서 변경하지 않는다.
- 실제 구현 시 SaveData migration, 기존 DisplayName 건축 데이터 호환, event timing, shared reference 복구, 중복 실행 방지와 UI 입력 차단을 검증해야 한다.
- `RescueLoanDefinition.MinimumTradeCost`의 실제 정의 자산/ID, 재파산 판정 호출 시점, 대출 UI validation query 연결은 구현 소유 영역에서 확인해야 한다.

## Related

없음
