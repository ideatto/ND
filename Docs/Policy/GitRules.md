# Git 협업 규칙 (누워서 돈벌기 / 6팀)

> 이 문서 하나만 지키면 5명이 충돌 없이 협업할 수 있다.
> 처음이면 **[1. 핵심 3줄]** → **[4. 하루 작업 흐름]** 순서로만 읽어도 된다.

---

## 1. 핵심 3줄 (이것만은 꼭)

1. **작업 시작 전** `dev2`를 최신화(pull)하고, **feature 브랜치**를 새로 만들어 작업한다.
2. **`main`·`dev2`에는 절대 직접 push 금지.** 반드시 PR(합치기 요청)로만 반영한다. PR base는 **`dev2`** 이다.
3. **씬(.unity)·프리팹은** 담당자(Scene Owner) 외에는 동시에 건드리지 않는다.

> **기본 통합 브랜치:** `dev2` (원격 이름 소문자).  
> `dev`는 기본 base가 아니다. 새 작업·PR는 `dev2`를 기준으로 한다.  
> 문서에 `Dev2`/`develop`으로 적힌 경우도 동일하게 `dev2`로 해석한다.

---

## 2. 브랜치 구조

브랜치는 **폴더가 아니라 "작업 버전의 갈래(복사본)"** 다. GitHub Desktop에서 버튼으로 만든다.

```
main (1개)   ← 발표·제출 가능한 최종본 (항상 실행되는 상태만)
  │  (복사해서 만듦 ↓ / 완성되면 합침 ↑)
dev2 (1개)   ← 팀 전체가 매일 모이는 통합 브랜치 (기본 PR base)
  ├─ feature/core      (윤호영)
  ├─ feature/data      (이종현)
  ├─ feature/economy   (정정헌)
  ├─ feature/scene-art (박준서)
  └─ feature/...        (각자 기능마다, 여러 개)
```

| 브랜치 | 개수 | 역할 |
|--------|------|------|
| `main` | 1 | 최종 제출본. PR로만 변경 |
| `dev2` | 1 | 팀 통합 기준. **기본 PR base.** PR로만 변경 |
| `feature/*` | 여러 개 | 각자 작업용. 만들고 → PR → 리뷰 후 처리 |

> **비유**: `main`=제출본, `dev2`=조 공용 초안, `feature`=각자 사본.
> 각자 사본에서 쓰고 → 초안(`dev2`)에 모으고 → 완성되면 제출본(`main`)에 반영.

---

## 3. 브랜치 이름 규칙

사람 이름보다 **기능 이름**을 우선한다.

```
feature/player-move       기능 추가
feature/ui-inventory      기능 추가
fix/enemy-hitbox          버그 수정
asset/stage01-props       에셋 작업
docs/git-rules            문서 작업
```

여러 명이 같은 기능을 나눠 하면 개인 층을 추가한다:
```
feature/ui/jonghyun
feature/ui/junseo
```

---

## 4. 하루 작업 흐름 (매일 반복)

```
① Sync    수업 시작: dev2 최신화       (GitHub Desktop: Fetch → Pull)
② Branch  작업용 feature 브랜치 생성    (Current branch → New branch, base=dev2)
③ Work    Unity 작업 / 테스트
④ Commit  작게, 의미 단위로 기록        (Summary 작성 → Commit)
⑤ Push    원격에 내 브랜치 올리기        (Push origin)
⑥ PR      합치기 요청 / 충돌 확인        (Create Pull Request, base=dev2)
⑦ Merge   dev2에 합친 뒤 팀 전체 pull
```

**황금 습관**: 작업 시작 전 `pull`, 작업 끝나기 전 `push`, merge 후 팀 전체 `pull`.

### GitHub Desktop 버튼 흐름 (명령어 없이)
| 시점 | 버튼 | 확인할 것 |
|------|------|-----------|
| 수업 시작 | Fetch origin → Pull origin | Current branch가 `dev2`인지 |
| 작업 시작 | Current branch → New branch | base가 `dev2`인지 |
| 작업 중 | Changes 탭 | `Library/`·`Temp/`가 안 섞였는지 |
| 커밋 | Summary 작성 → Commit | 메시지가 "final" 같은 무의미어가 아닌지 |
| 공유 | Push origin | 원격 브랜치가 생겼는지 |
| PR | Create Pull Request | base: `dev2`, compare: `feature/*` |

---

## 5. 커밋 메시지 규칙

**1 commit = 1 의도.** 스크립트와 관련 `.meta`는 함께 커밋한다.

```
접두어(범위): 설명

예)
feat(player): add dash cooldown
fix(ui): prevent null inventory slot
asset(enemy): update goblin attack animation
docs(deps): record imported audio pack version
```

접두어: `feat`(기능) `fix`(버그) `refactor`(구조개선) `asset`(에셋) `docs`(문서) `chore`(설정)

❌ 나쁜 예: "final", "fix", "찐막", 하루 작업 전체를 한 번에 커밋

---

## 6. PR 전 체크리스트

- [ ] Unity에서 Play 후 핵심 기능이 동작하는가?
- [ ] 의도치 않은 scene/prefab/meta/Library 파일이 안 섞였는가?
- [ ] `dev2`를 최신으로 받고 충돌을 먼저 해결했는가?
- [ ] PR base가 `dev2`인가? (`dev`를 기본 base로 쓰지 않는다)
- [ ] 큰 파일은 LFS 대상이고, 외부 패키지는 `Dependencies.md`에 기록했는가?
- [ ] 리뷰어가 테스트할 방법을 PR 설명에 적었는가?

> PR은 "검사받기"가 아니라 **팀 전체가 변경 내용을 이해하는 과정**이다.

---

## 7. Unity 충돌 예방

C# 스크립트는 잘 합쳐지지만, **씬·프리팹 충돌은 초보자에게 어렵다. 예방이 우선.**

- **Scene Owner**: 씬 편집 담당자를 정한다 → **박준서**. (`SceneOwners.md` 참고)
- **Prefab First**: 공통 오브젝트는 prefab으로 빼서 씬 변경을 줄인다.
- **Rename Freeze**: 남이 작업 중인 파일명·폴더 이동 금지.
- **Small PR**: 씬 변경은 작게 나눠 빨리 합친다.

---

## 8. 금지 사항 (초보자)

- ❌ `main`·`dev2`에 직접 push / "Commit to main/dev2" 버튼
- ❌ 기본 base로 `dev`를 사용해 feature 브랜치·PR를 만드는 것
- ❌ `git reset --hard`, `git clean -fd`, force push
- ❌ 충돌 중 `.meta` 파일 삭제
- ❌ 큰 파일을 혼자 되돌리려고 히스토리 rewrite

> 사고가 나면 혼자 해결하지 말고 **팀장(천성욱)·강사에게** 알린다.
> 이미 push한 기록은 "삭제"보다 **"새 commit으로 되돌리기(revert)"** 가 안전하다.

---

## 9. 팀원 담당

| 이름 | 담당 | 기능 브랜치 |
|------|------|-------------|
| 천성욱 | 팀장·버전관리·문서 | - |
| 윤호영 | 코어 플레이·GitHub·Release | `feature/core` |
| 박준서 | UX·씬(Scene Owner)·사운드·Art | `feature/scene-art` |
| 정정헌 | 밸런스·경제·성장·데이터 값 | `feature/economy` |
| 이종현 | 핵심 루프·UI·데이터 구조·저장 | `feature/data` |

---

*관련 문서: `Docs/Policy/GitIgnore_Asset_Policy.md`, `Docs/Policy/SceneOwners.md`, `Docs/Policy/Dependencies.md`*
*Cursor Rule: `.cursor/rules/git-base-branch-dev2.mdc`*
*최종 수정: 2026-07-11 — 기본 통합 브랜치를 `dev2`로 명시 (`dev` 비기본)*
