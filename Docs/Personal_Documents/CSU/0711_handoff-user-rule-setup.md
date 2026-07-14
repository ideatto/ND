# Handoff User Rule 설정 가이드

**목적:** Agent 대화창에서 `핸드오프 템플릿 작성` 입력 시, 현재 세션 작업을 `Docs/Personal_Documents/CSU/` 아래 핸드오프 Markdown으로 저장합니다.

**적용 위치:** Cursor **User Rules** (개인 전역 설정)

---

## 1. User Rules 추가 방법

1. Cursor **Settings** 열기 (`Ctrl + ,` 또는 메뉴)
2. **Rules, Skills, Subagents** → **User Rules** 이동
3. 기존 규칙 **아래**에 [2. 복사용 Rule 블록](#2-복사용-rule-블록) 전체를 붙여넣기
4. 저장 후 **새 Agent 채팅**에서 트리거 테스트

> User Rules는 Cursor 계정에 동기화됩니다. 로컬 파일만으로는 자동 적용되지 않습니다.

---

## 2. 복사용 Rule 블록

아래 코드 블록 **전체**를 User Rules에 붙여넣습니다. (앞뒤 ` ```markdown ` / ` ``` ` 줄은 제외)

```markdown
---
description: "핸드오프 템플릿 작성" 트리거 시 현재 Agent 세션 작업을 CSU 핸드오프 문서로 저장
alwaysApply: true
---

# Handoff Template Rule

## Trigger

사용자 메시지가 정확히 `핸드오프 템플릿 작성`이면, 다른 작업을 하지 말고 핸드오프 문서 작성만 수행한다.

## Preconditions

- 워크스페이스에 `Docs/Personal_Documents/CSU/`가 존재해야 한다.
- 없으면 파일을 생성하지 말고, ND 프로젝트 여부를 확인하라고 안내한다.

## Workflow

1. 현재 Agent 대화의 목표, 완료/진행/미완료, 결정 사항, 리스크를 정리한다.
2. `git status`와 `git diff`로 실제 변경 파일을 확인한다. 대화와 diff가 다르면 diff를 우선한다.
3. 추측은 `미확인`으로 표기한다.
4. Document Structure 섹션 형식으로 Markdown을 작성한다.
5. `Docs/Personal_Documents/CSU/{task-slug}-handoff.md`에 저장한다.
   - task-slug: 작업명 kebab-case 영문
   - 동일 파일명 존재 시 `-2` 접미사 또는 `YYYY-MM-DD-` 접두사 사용
6. 저장 후 채팅에 생성 경로와 재개 요청문을 출력한다.

## Document Structure

핸드오프 문서는 다음 섹션을 포함한다.

- 제목: `# {작업명} Handoff`
- 메타: 작성일, 브랜치, Feature 루트, 관련 문서
- `## 1. 작업 목표` (목표, 완료 기준, 제외 범위)
- `## 2. 현재 상태` (완료 / 진행 중 / 미완료 / 차단됨 표)
- `## 3. 핵심 결정 사항`
- `## 4. 변경 파일` (생성·수정·삭제, 승인된 외부 수정, Scene/Prefab/Meta/Package/데이터 Yes/No)
- `## 5. 구현된 동작`
- `## 6. 검증 결과`
- `## 7. 알려진 리스크`
- `## 8. 하지 말 것 (Do Not)`
- `## 9. 다음 세션 읽기 순서` (`@Docs/Personal_Documents/CSU/{이번-handoff}.md` 포함)
- `## 10. 다음 단계 (단일 작업)` (작업 1문장, 완료 조건, 검증 방법)

## Resume Prompt Output

문서 저장 후 채팅에 재개 요청문을 출력한다. 포함 항목:

- `이전 Agent 세션에서 이어서 작업합니다.`
- `@Docs/Personal_Documents/CSU/{filename}`
- 브랜치, Feature 루트, 다음 작업(단일), 금지 사항 요약
- `핸드오프를 읽고 5줄 요약 + 실행 계획을 보여준 뒤, 확인 후 코드 수정을 시작하세요.`
```

상세 문서 구조 예시는 [handoff-user-rule-handoff.md](handoff-user-rule-handoff.md)를 참고합니다.

---

## 3. 트리거 테스트

1. ND 프로젝트에서 Agent 대화를 짧게 진행
2. `핸드오프 템플릿 작성` 입력
3. 확인:
   - `Docs/Personal_Documents/CSU/{task}-handoff.md` 생성
   - 완료/미완료 구분
   - git diff와 변경 파일 일치
   - 재개 요청문 채팅 출력
4. 새 Agent 채팅에서 재개 요청문 붙여넣기 → 5줄 요약·계획 확인

---

## 4. 관련 문서

- 핸드오프 요청문·재개 요청문 템플릿: 이전 Agent 대화 참고
- CSU 설계 문서 예시: `Core-services-M1-sync.md`
- 팀 handoff 예시: `Docs/Personal_Documents/JJH/0710_progression_m1_core_ui_integration_handoff.md`
