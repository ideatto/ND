# Framework & Integration 담당자 — 축소된 1차 빌드 개인 마일스톤

## 1. 문서 정보

- **업데이트:** 2026-07-14
- **제출:** 2026-07-16 23:59
- **일정 원칙:** 주말·공휴일 작업 없음
- **현재 단계:** UX 라우팅, 저장·복구 회귀, 통합 빌드와 디버깅
- **씬 소유권:** 기본은 기존 SceneOwners. 일시적 소유권 변경은 협의 후만.

---

# 2. 축소된 책임 범위

- Boot → Title → Loading → InGame → Title
- 공용 FrameworkRoot 생명주기
- 새 게임·이어하기 분기
- **제출 UX:** SaveData 없을 때 Continue 비활성, Reset SaveData 확인 창
- Preparation·Traveling·Settlement 화면 라우팅
- 게임 시간과 현실 시간→인게임 시간 변환
- 무역 시작·종료 예정 시간 기록
- 저장·불러오기
- Traveling 및 SettlementPending 복구
- 오프라인 진행
- 정산 Claim과 중복 수령 방지 연결
- 통합 로그, RC 빌드, 제출 백업

## 1차 빌드 제외

- 성장·기부·투자·대출 **상태의 플레이어 경로 적용**(기존 저장 필드·코드는 유지 가능)
- 튜토리얼 상태 UX
- 계절·재난 강제 전환
- 로드 이벤트 강제 발생
- HMAC·암호화
- 신규 고급 저장 아키텍처 확장

이미 구현된 저장 안정성 기능은 유지하되 제출 전 구조 확장은 하지 않는다. 현재 Continue 등이 **백엔드·디버그 검증용**으로 열려 있으면, 제출 UX(비활성·확인 창)로 맞춘다.

---

# 3. 현재 상태

## 검증 완료

- 전체 씬 왕복
- 공용 매니저 중복 방지
- Traveling 저장과 진행률 복구
- 오프라인 진행
- SettlementPending 결과 복구
- Claim과 중복 Claim 방지
- 성공·실패 후 Preparation 라우팅

## 결함·확인 사항

- 제출 UX 미반영 시: SaveData 없을 때 Continue가 새 게임처럼 진입(현재는 백엔드/디버그용일 수 있음)
- 플레이어 UI와 기존 라우터 연결이 아직 최종 통합되지 않음
- 테스트 Harness의 고정 Trade ID 반복 사용
- 제출 빌드에서 디버그 기능 노출 여부 점검 필요

---

# 4. 작업 일정

## 2026-07-14 — Title과 InGame UX 라우팅 연결

- SaveData 유무에 따른 Continue 활성/비활성 확정
- Reset SaveData 확인 창 연결(UI와 분담)
- 저장이 없을 때 Continue가 새 게임을 암묵적으로 생성하지 않도록 처리
- Preparation, Traveling, Settlement 초기 화면 결정 연결
- UI 출발 버튼과 무역 시작 기록 연결
- UI Claim 버튼과 검증된 Claim 경로 연결
- 화면 중복 전환과 버튼 중복 입력 방지 확인
- UI 담당 Scene/Prefab 연결 지원

### 완료 기준

- 새 게임과 이어하기가 구분된다.
- 현재 저장 상태에 맞는 InGame 화면이 열린다.
- 플레이어 버튼이 기존 Framework API를 통해 동작한다.

### 선행 구현 메모

- **UI & Data:** Title·InGame 프리팹과 버튼 이벤트 지점 필요
- **Core Gameplay:** 출발·진행·정산 상태와 결과 API 필요
- **Content & Tools:** 새 게임 기본 데이터가 유효해야 함

## 2026-07-15 — 저장·오프라인·정산 회귀

- UI 경로에서 Traveling 종료·재실행 복구
- 식량 감소와 오프라인 진행 중복 적용 여부 확인
- SettlementPending 종료·재실행 복구
- Claim 후 저장, 정산 재표시 방지
- 성공·실패 후 다음 무역 라우팅 확인
- 동일 정산 중복 이벤트와 중복 재화 지급 방지
- 고정 Trade ID가 실제 플레이 데이터에 영향을 주지 않는지 확인
- Title 복귀 후 Continue 재검증

### 완료 기준

- 종료 위치에 맞는 화면과 데이터가 복구된다.
- 정산이 한 번만 지급된다.
- 화면 상태와 저장 상태가 모순되지 않는다.

### 선행 구현 메모

- **Core Gameplay:** 실패 원인과 정산 결과가 확정되어야 함
- **UI & Data:** 진행·정산 화면의 데이터 바인딩 필요
- **Progression & System:** 재화 증감 API와 기본 정산값 필요

## 2026-07-16 — RC·제출

### 오전

- RC1 생성
- 새 게임, 이어하기, Traveling 복구, Settlement 복구 테스트
- 화면 중복과 저장 유실 테스트
- Framework P0·P1 수정

### 오후

- 최종 RC 생성
- 최소 3명 독립 실행 결과 수집
- 버전명·커밋·빌드 폴더 기록
- 제출본과 백업본 생성
- 23:59까지 제출

### 완료 기준

- 독립 실행 빌드에서 씬 진입 가능
- 이어하기와 정산 복구 가능
- 중복 보상 없음
- 제출 빌드와 소스 커밋 추적 가능

### 선행 구현 메모

- **전 담당:** RC 대상 PR 병합 및 테스트 결과 공유
- **UI & Data:** 최종 Scene/Prefab 확정

---

# 5. 개인 완료 체크리스트

- [ ] SaveData 없을 때 Continue 비활성
- [ ] Reset SaveData 확인 창
- [ ] 새 게임·이어하기 구분
- [ ] Preparation·Traveling·Settlement 라우팅
- [ ] 출발 중복 입력 방지
- [ ] Claim 중복 입력 방지
- [ ] Traveling 복구
- [ ] SettlementPending 복구
- [ ] 오프라인 진행 중복 적용 없음
- [ ] 정산 중복 지급 없음
- [ ] RC와 백업본 생성
