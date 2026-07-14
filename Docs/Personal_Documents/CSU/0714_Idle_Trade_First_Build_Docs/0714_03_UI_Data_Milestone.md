# UI & Data 담당자 — 축소된 1차 빌드 개인 마일스톤

## 1. 문서 정보

- **업데이트:** 2026-07-14
- **제출:** 2026-07-16 23:59
- **일정 원칙:** 주말·공휴일 작업 없음
- **현재 단계:** 플레이어용 핵심 UX 구현과 데이터 바인딩

---

# 2. 축소된 책임 범위

- Title 새 게임·이어하기 UX
- SaveData 없을 때 Continue 비활성
- Reset SaveData 확인 창
- Caravan 준비 화면
- 도시·무역로·상품·식량·마차·견인 동물 선택
- 용병 고용 UI **단순 틀**(제출 루프 포함, 실기능·연결은 2차)
- 슬롯·적재량·적정 적재량·최대 적재량 표시
- 과적 상태와 출발 불가 이유 표시
- 출발 버튼
- Traveling 진행 화면
- Settlement 성공·실패·수익·비용·순이익 표시
- 실패 원인 표시
- Claim 버튼과 중복 입력 방지
- Preparation·Traveling·Settlement 화면 전환 표현
- 목표 해상도 대응과 필수 텍스트 검수

## 1차 빌드 제외

- 성장 화면
- 기부·투자·대출 UI
- 튜토리얼·마스코트
- 계절·재난·로드 이벤트 상세 UI
- 용병 **전투** UI 및 고용 **실기능·데이터 연결**
- 이전 구성 자동 불러오기 고급 UX

씬 수정은 **기존 SceneOwners**를 기본으로 한다. 일시적 소유권 변경은 협의 후에만 한다.

---

# 3. 현재 상태

- 백엔드 무역 루프는 디버그 도구로 검증됨
- 플레이어가 조작할 Preparation UX는 최종 통합 전
- 용병 고용 UI 틀은 제출 루프에 포함, 실기능은 2차
- Traveling과 Settlement 데이터 어댑터는 있으나 실제 최종 화면 연결 점검 필요
- Title Continue/Reset은 제출 UX(비활성·확인 창)로 맞출 것. 현재 일부는 백엔드/디버그용일 수 있음
- 실패 원인 `WagonBroken` 표시 문제를 Core 결과와 함께 수정해야 함

---

# 4. 작업 일정

## 2026-07-14 — Preparation과 Title 완성

- Title의 New Game·Continue 버튼 상태와 피드백 구현
- SaveData가 없을 때 Continue 비활성화
- Reset SaveData 확인 창
- 도시·무역로 선택 UI
- 상품·식량 입력 UI
- 마차·견인 동물 선택 UI
- 용병 고용 UI 단순 틀(버튼·화면 전환만, 실기능 stub 가능)
- 현재 슬롯, 적재량, 적정 적재량, 최대 적재량 표시
- 과적 상태와 예상 영향 표시
- 출발 불가 사유 표시
- 출발 버튼을 Core 검증·Framework 출발 경로에 연결

### 완료 기준

- 디버그 Context Menu 없이 정상 Caravan을 구성하고 출발한다.
- 잘못된 구성은 버튼 상태 또는 메시지로 이유를 알 수 있다.
- UI가 적재·가격 계산을 자체 구현하지 않는다.

### 선행 구현 메모

- **Core Gameplay:** View Data와 출발 검증 결과 코드
- **Framework:** New Game·Continue 및 출발 이벤트 연결
- **Content & Tools:** 표시명·아이콘·유효한 기본 데이터
- **Progression & System:** 기본 구매 가격과 비용 데이터

## 2026-07-15 — Traveling·Settlement와 전체 UX 회귀

- Traveling 진행률과 남은 시간 표시
- 현재 식량 또는 식량 부족 상태 표시
- Settlement에서 Success·Failed 구분
- 판매 수익, 총비용, 순이익 표시
- 실패 원인 표시
- Claim 버튼과 중복 입력 방지
- Claim 후 Preparation 화면 복귀
- Traveling·SettlementPending 재실행 후 올바른 화면 복구
- 성공·실패 전체 동선 UX 테스트

### 완료 기준

- 플레이어가 화면만 보고 현재 상태와 다음 행동을 이해한다.
- 정산 표시값이 실제 재화 변화와 일치한다.
- 실패 원인이 Core 결과와 일치한다.

### 선행 구현 메모

- **Core Gameplay:** 진행률, 식량 상태, 실패 원인, 정산 결과
- **Framework:** 화면 라우팅, 저장 복구, Claim 결과
- **Progression & System:** 정산 항목과 재화 반영
- **Content & Tools:** 정산·오류 문구와 아이콘

## 2026-07-16 — 레이아웃·입력·RC 수정

- 목표 해상도에서 핵심 패널 확인
- 긴 텍스트, 버튼 겹침, 스크롤, 비활성 상태 확인
- 더블 클릭과 화면 중복 전환 확인
- Console Error와 Missing Reference 제거
- RC1에서 발견된 P0·P1 UI 결함만 수정
- 최종 Scene/Prefab 버전 확정

### 완료 기준

- 필수 버튼 미작동 없음
- 핵심 정보 잘림 없음
- 상태 변경 후 이전 화면이 겹치지 않음
- 최종 빌드에서 개발자 조작 없이 전체 루프 가능

### 선행 구현 메모

- **Framework:** RC 빌드와 최종 씬 라우팅
- **전 담당:** 최종 데이터·API 동결

---

# 5. 개인 완료 체크리스트

- [ ] SaveData 없을 때 Continue 비활성
- [ ] Reset SaveData 확인 창
- [ ] Preparation에서 전체 구성 가능
- [ ] 용병 고용 UI 틀 포함
- [ ] 슬롯·적재·과적 정보 표시
- [ ] 출발 불가 이유 표시
- [ ] 출발 버튼 정상
- [ ] Traveling 진행 표시
- [ ] 성공·실패 정산 표시
- [ ] 수익·비용·순이익 표시
- [ ] 실패 원인 표시
- [ ] Claim 후 Preparation 복귀
- [ ] 핵심 화면 해상도 대응
- [ ] Missing Reference 없음
