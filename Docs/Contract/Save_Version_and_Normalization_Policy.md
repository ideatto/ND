# Save Version and Normalization Policy

## Approved version decision

- product/document label `V2`와 serialized numeric version은 별개다.
- 현재 production numeric version은 `6`이다.
- 승인된 다음 기준선은 `CurrentVersion = 7`이다. 이 문서 작업은 코드 값을 변경하지 않는다.
- version 7은 InvestmentQuest completion collection, Building `buildingId + level`, Building ID validation과 관련 normalization을 포함한다.
- version 6 Multi-Caravan cutover 이후 Building 영속 식별 의미가 바뀌므로 단순 optional-field normalization으로 처리하지 않는다.

## Version 6 load/reset boundary

- v6 테스트 저장을 v7로 자동 migration하지 않는다.
- legacy `displayName`에서 `buildingId`를 자동 추론하거나 fuzzy match하지 않는다.
- unsupported v6 파일을 조용히 새 게임으로 덮어쓰지 않는다.
- 가능한 경우 원본 또는 backup을 보존하고 사용자에게 보이는 명시적 reset 뒤 v7 저장을 만든다.
- 팀 전체 테스트 저장 초기화를 전제로 한다. backup의 Title UI 노출 방식은 미결정이다.

## Normalization order

1. disk file을 변경하지 않고 parse한다.
2. 지원하지 않는 numeric version을 visible reject하고 원본/backup을 보존한다.
3. 누락된 optional child를 만들고 null collection을 empty list로 바꾼다.
4. 계약상 안전한 scalar 기본값만 적용한다. 음수 level/ticks는 명시된 범위에서 0으로 보정할 수 있다.
5. required ID와 full GUID trade ID를 검증한다.
6. Caravan ID, trade/pending composite key, InvestmentQuest completion ID, building ID, unlock ID 중복을 탐지한다.
7. unknown shared-definition ID를 보고하고 복구를 위해 보존하며 관련 Command를 차단한다.
8. validation 후에만 non-serialized lookup/runtime cache를 만든다.

## Normalization에서 금지

- `displayName → buildingId` 의미 변환
- duplicate 중 하나 삭제, 합산, first/last/max 선택
- orphan 삭제 또는 Caravan ID 자동 재발급
- 보상 재지급, unlock 자동 재적용, Quest completion 자동 생성
- gameplay/economy 계산이나 resource 지출
- 기존 save 자동 overwrite

repair write, reset, migration은 각각 사용자에게 보이는 별도 save operation이다. 문서 변경만으로 저장 reset이나 conversion을 실행하지 않는다.

## Freeze와 후속 구현

2026-08-04 구조 동결 후 public SaveData 구조 또는 numeric version 변경은 승인된 blocker-level fix로 제한한다. 구현 순서는 v7 DTO/validator → v6 visible reset handling → Building/Investment commands → runtime/UI migration이다.

