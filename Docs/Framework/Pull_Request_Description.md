# Pull Request

## Purpose

Framework의 멀티 Caravan 저장 계약에 최종 P1 정책과 Command/Event 호환성 우선 마이그레이션 방향을 반영한다. 현재 production API와 이벤트는 유지하며, 구현 전 필요한 인벤토리와 담당 영역별 전환 조건을 문서화한다.

## Changes

- Donation decay의 game-time 변환, 시각 역행 처리, 0 하한 정책을 확정했다.
- Save 실패 분류별 retry/rollback 방향과 Dirty/important save queue 병합 규칙을 추가했다.
- 선택 Caravan ID, 반복 이벤트 consumption count, 고정 최소 거래 비용, rescue loan 제한 모드 계약을 추가했다.
- Command/Event 정책을 `Approved`, 즉시 전면 교체를 `Approved Direction - Staged Migration Required`로 명시했다.
- additive contract, compatibility adapter, 담당자별 전환, event timing 전환, legacy 제거의 6단계 계획을 추가했다.
- API/Event 인벤토리 템플릿, 담당 영역 체크리스트, 위험 목록, 후속 브랜치 제안, 17개 테스트 시나리오를 추가했다.

## Check

- 문서 간 정책 표현과 미결정 항목 유지 여부를 검토했다.
- production C# API/Event, Scene, Prefab, package, ProjectSettings가 변경되지 않았는지 Git diff로 확인했다.
- Unity Console 및 Editor compilation/runtime verification: 확인 필요
- 문서에 정의한 runtime/compatibility 테스트: 구현 브랜치에서 확인 필요

## Risk

- Scene: No
- Prefab: No
- Meta: No
- Package: No
- ScriptableObject/data: No
- 기존 동작과 저장 데이터는 이 문서 브랜치에서 변경하지 않는다.
- 실제 전환 시 void Save, event publication timing, rollback, direct mutation, serialized Button callback, 중복 subscriber가 주요 검토 위험이다.
- Donation 수치, retry 간격/횟수, loan 제한 모드의 일부 UX 정책은 미결정 상태다.

## Related

없음
