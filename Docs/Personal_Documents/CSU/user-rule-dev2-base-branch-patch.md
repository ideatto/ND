# User Rule Patch — Base Branch `dev2`

**작성일:** 2026-07-11  
**목적:** Cursor User Rules의 Git Collaboration 블록에서 기본 base를 `dev` → `dev2`로 맞춘다.  
**참고:** User Rules는 Cursor 클라우드 저장소라 Agent가 직접 수정할 수 없다. 아래 문구를 User Rules에 수동 반영한다.

---

## 프로젝트에 이미 반영된 것

| 경로 | 내용 |
|------|------|
| [`Docs/Policy/GitRules.md`](../../Policy/GitRules.md) | 팀 통합 브랜치·PR base를 `dev2`로 통일 |
| [`.cursor/rules/git-base-branch-dev2.mdc`](../../../.cursor/rules/git-base-branch-dev2.mdc) | Agent alwaysApply Rule |

---

## User Rules에서 바꿀 문장

Git Collaboration / Pull Request 관련 User Rule에서:

| 기존 | 변경 |
|------|------|
| Use `dev` as the default base branch | Use `dev2` as the default base branch |
| Do not commit or push directly to `main` or `dev` | Do not commit or push directly to `main` or `dev2` |
| Check out `dev` | Check out `dev2` |
| Create a new task branch from the updated `dev` | Create a new task branch from the updated `dev2` |
| Confirm the base branch is `dev` | Confirm the base branch is `dev2` |
| Base: `dev` (Git Work Summary) | Base: `dev2` |

`dev`는 원격에 남아 있어도 **기본 통합·PR base로 쓰지 않는다.**
