# Handoff User Rule Handoff

**작성일:** 2026-07-11  
**브랜치:** `fix/framework/caravan-ingame-food-sync`  
**Feature 루트:** 미확인 (문서·Rule 설정 작업)  
**관련 문서:** `Docs/Personal_Documents/CSU/handoff-user-rule-setup.md`

---

## 1. 작업 목표

- 목표: `핸드오프 템플릿 작성` 트리거 시 현재 Agent 세션 작업을 CSU 핸드오프 문서로 자동 저장하는 User Rule 추가
- 완료 기준: User Rules 복사용 블록 제공, 핸드오프 워크플로 검증 문서 생성, 재개 요청문 제공
- 제외 범위: `.cursor/rules/` Project Rule 생성, git commit, 팀 공유용 Rule

---

## 2. 현재 상태

| 구분 | 내용 |
|------|------|
| 완료 | Context Usage 100% 동작 설명, 핸드오프 요청문·재개 요청문 템플릿 제공, User Rule 플랜 수립, `handoff-user-rule-setup.md` 생성, `handoff-user-rule-handoff.md` 생성(본 문서) |
| 진행 중 | User Rules UI에 Rule 블록 붙여넣기 (사용자 수동 작업) |
| 미완료 | User Rules 적용 후 새 채팅에서 `핸드오프 템플릿 작성` 트리거 실사용 검증 |
| 차단됨 | User Rules는 Cursor 클라우드 동기화 저장소라 Agent가 직접 User Rules 파일을 수정할 수 없음 |

---

## 3. 핵심 결정 사항

- User Rules만 사용: Project Rules(`.cursor/rules/`) 대신 개인 전역 User Rules 선택
- 트리거 문구: `핸드오프 템플릿 작성` (정확 일치)
- 저장 경로: `Docs/Personal_Documents/CSU/{task-slug}-handoff.md`
- 워크스페이스 가드: `Docs/Personal_Documents/CSU/` 없으면 파일 생성 중단
- git diff 우선: 대화 내용과 diff 불일치 시 diff 기준으로 변경 파일 기록

---

## 4. 변경 파일

### 생성
- `Docs/Personal_Documents/CSU/handoff-user-rule-setup.md` — User Rules 복사용 블록 및 설정 가이드
- `Docs/Personal_Documents/CSU/handoff-user-rule-handoff.md` — 본 핸드오프 문서 (워크플로 검증용)

### 수정
- 없음

### 삭제
- 없음

### 승인된 외부 수정
- 없음

### Scene / Prefab / Meta / Package / 데이터 변경 여부
- Scene 변경: No
- Prefab 변경: No
- Meta 변경: No
- Package 변경: No
- ScriptableObject 또는 데이터 변경: No

---

## 5. 구현된 동작

- 핸드오프 User Rule 초안을 `handoff-user-rule-setup.md`에 정리
- Rule 트리거·워크플로·문서 구조·재개 요청문 출력 규칙 정의
- 현재 세션 맥락과 git 상태를 반영한 검증용 핸드오프 문서 생성

---

## 6. 검증 결과

- 코드 검토: 완료 (Rule 초안·문서 구조 검토)
- Unity 컴파일: 해당 없음
- 테스트한 항목: CSU 폴더 존재 확인, git status/diff 확인, 핸드오프 문서 생성, 재개 요청문 작성
- 테스트하지 못한 항목: User Rules UI 붙여넣기 후 새 채팅에서 `핸드오프 템플릿 작성` 트리거 실행

---

## 7. 알려진 리스크

- User Rules는 클라우드 관리라 Agent가 직접 등록 불가 — 사용자가 setup 문서에서 복사·붙여넣기 필요
- User Rules가 모든 프로젝트에 주입되므로, CSU 폴더 가드 없는 다른 워크스페이스에서는 트리거 시 안내만 출력됨
- 대화가 짧으면 핸드오프가 빈약할 수 있음 — git diff로 보강

---

## 8. 하지 말 것 (Do Not)

- `.cursor/rules/` Project Rule을 이번 작업 범위에서 생성하지 말 것
- setup 문서·handoff 문서를 git commit하지 말 것 (사용자가 요청하기 전까지)
- plan 파일(`handoff_user_rule_1c86be00.plan.md`) 수정하지 말 것

---

## 9. 다음 세션 읽기 순서

1. `@Docs/Personal_Documents/CSU/handoff-user-rule-handoff.md`
2. `@Docs/Personal_Documents/CSU/handoff-user-rule-setup.md`

---

## 10. 다음 단계 (단일 작업)

> `handoff-user-rule-setup.md`의 Rule 블록을 Cursor User Rules에 붙여넣은 뒤, 새 Agent 채팅에서 `핸드오프 템플릿 작성`을 입력해 핸드오프 문서가 CSU 폴더에 생성되는지 확인한다.

**완료 조건:** 새 채팅 트리거 실행 시 `{task-slug}-handoff.md` 생성 및 재개 요청문 채팅 출력

**검증 방법:** User Rules 저장 → 새 Agent 채팅 → `핸드오프 템플릿 작성` → 파일·재개 요청문 확인
