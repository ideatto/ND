# .gitignore & 에셋 관리 정책 설명서

> 프로젝트: **누워서 돈벌기** (6팀 / Unity 6000.5.2f1)
> 목적: 용량이 크거나 재생성 가능한 파일이 GitHub에 올라가지 않도록 하는 규칙과, 외부 에셋을 팀이 공유하는 방식을 설명한다.

---

## 1. 왜 필요한가

Git 저장소는 **"프로젝트를 재현하는 데 필요한 정보"만** 담아야 한다.
아래 파일들은 저장소에 넣으면 안 된다.

| 분류 | 예시 | 이유 |
|------|------|------|
| Unity 재생성물 | `Library/`, `Temp/`, `obj/`, `Logs/` | 각 PC에서 Unity가 자동 생성. 커밋하면 저장소가 폭발적으로 커짐 |
| 빌드 결과물 | `Build/`, `*.apk`, `*.app` | 소스로 언제든 다시 빌드 가능 |
| 대용량 외부 에셋 | Asset Store 에셋, `*.unitypackage` | 용량이 크고 수정하지 않는 원본. 저장소가 무거워짐 |
| 개인 설정 | `UserSettings/`, `*.csproj`, `.vs/` | 사람마다 달라 충돌만 유발 |

이 파일들이 실수로 올라가는 것을 `.gitignore`가 자동으로 막는다.

---

## 2. 이번에 추가한 규칙 (핵심)

`.gitignore` 맨 아래에 다음이 추가되었다.

```gitignore
# 팀 규칙: 외부 에셋 보관소
/[Aa]ssets/_ExternalPackages/*
!/[Aa]ssets/_ExternalPackages/.gitkeep
```

### 동작 방식
- **1번째 줄** `.../_ExternalPackages/*`
  → `Assets/_ExternalPackages` **폴더 안의 모든 파일과 하위 폴더(.meta 포함)를 무시**한다.
- **2번째 줄** `!.../.gitkeep`
  → 단, `.gitkeep` **한 개만 예외로 추적**한다.
  → 덕분에 **폴더 구조 자체는 clone 시 유지**되지만, 안에 import한 무거운 에셋은 올라가지 않는다.

> `*` (별표 1개)는 **바로 아래 단계만** 대상으로 하므로, `.gitkeep` 예외 처리가 정상 작동한다.
> (만약 `**` 를 썼다면 하위 전체가 강제 무시되어 `.gitkeep` 예외가 안 먹는다.)

### 검증 결과 (실제 확인함)
| 대상 | 결과 |
|------|------|
| `_ExternalPackages/CasualUI/hero.prefab` | ✅ 무시됨 |
| `_ExternalPackages/.gitkeep` | ✅ 추적됨 (폴더 유지) |
| `_Project/VendorOverrides/*.cs` | ✅ 추적됨 (일부러 커밋) |

---

## 3. `.unitypackage` 는 이미 막혀 있다

`.gitignore` 80번째 줄에 아래가 이미 있다.

```gitignore
*.unitypackage
*.unitypackage.meta
```

- 커스텀 패키지 파일(`.unitypackage`) 자체는 **커밋도 LFS도 되지 않는다(= 저장소 밖).**
- `.gitattributes` 에는 `*.unitypackage filter=lfs` 로 되어 있지만, **파일이 ignore + LFS 양쪽에 걸리면 ignore가 이긴다.** 결과적으로 저장소에 안 들어가므로 팀 정책과 일치한다.
- 예외적으로 꼭 넣어야 하는 패키지가 있다면 `git add -f <파일>` 로만 강제한다(팀장 승인 후).

---

## 4. 외부 에셋 실제 작업 흐름

무거운 외부 에셋(Asset Store 등)은 **Git이 아니라 TeamDrive로 공유**한다.

```
① Package Owner 가 .unitypackage 를 TeamDrive에 업로드
   파일명 규칙:  AssetName_vMajor.Minor.Patch_YYYYMMDD.unitypackage
        ↓
② Docs/Dependencies.md 에 이름·버전·위치·import 경로·담당자 기록
        ↓
③ 팀원 각자: TeamDrive에서 같은 .unitypackage 다운로드
        ↓
④ Unity > Assets > Import Package > Custom Package
   → import 위치는 반드시  Assets/_ExternalPackages/{AssetName}
        ↓
⑤ .gitignore 가 자동으로 무시 → Git Changes 에 안 올라옴 (정상)
```

**핵심**: 모든 팀원이 **정확히 같은 버전**의 `.unitypackage`를 import하므로 GUID가 동일하게 유지된다 → 저장소에 에셋을 안 넣어도 참조가 깨지지 않는다.

---

## 5. 예외: `_Project/VendorOverrides/`

- 외부 에셋을 **수정해야 할 때는 원본을 고치지 않는다.**
- 원본을 `Assets/_Project/VendorOverrides/{AssetName}` 로 **복사한 뒤 수정**한다.
- 이 폴더는 **일부러 Git으로 추적**한다(우리가 책임지는 변경분이므로).
- 수정 이유는 `Docs/Dependencies.md`에 기록한다.

---

## 6. 대용량 파일 기준 (커밋 전 확인)

| 크기 | 처리 |
|------|------|
| ~50 MiB 미만 | 일반 Git / LFS 로 커밋 가능 |
| 50 MiB 이상 | **팀 확인 대상.** 정말 필요한지 검토 |
| 100 MiB 이상 | **일반 Git 금지.** LFS 또는 외부 보관 |
| 200 MiB 이상 / 수정 안 할 원본 | **Custom Package(외부 보관) 우선** |

- 이미지/모델/오디오/영상 등 바이너리는 `.gitattributes`에 의해 **LFS로 추적**된다.
- 단, LFS도 용량·대역폭 예산이 있다. **반복 수정하는 대형 원본은 저장소 밖에 두는 게 안전하다.**

---

## 7. 실수했을 때

- **큰 파일을 실수로 커밋함** → 혼자 `reset --hard`/`force push`로 해결하지 말 것. **팀장/강사에게 히스토리 정리 요청.**
- **`.gitignore`는 이미 커밋된 파일을 자동 삭제하지 않는다.** 이미 올라간 파일은 아래로 추적 해제:
  ```bash
  git rm -r --cached Assets/_ExternalPackages/문제폴더
  git commit -m "chore: remove external package from tracking"
  ```
- **새 파일이 이상하게 많이 잡힘** → 먼저 `.gitignore` 위치(저장소 루트)와 규칙을 확인.

---

## 8. 빠른 체크리스트 (커밋 전 30초)

- [ ] `Library/`, `Temp/`, `Build/` 가 Changes에 섞이지 않았나?
- [ ] `_ExternalPackages/` 내부 파일이 Changes에 안 보이나?
- [ ] 50 MiB 넘는 파일을 커밋하려는 건 아닌가?
- [ ] 외부 에셋을 수정했다면 `VendorOverrides`에 복사본으로 했나?
- [ ] 새 외부 패키지를 썼다면 `Docs/Dependencies.md`에 기록했나?

---

*최종 수정: 2026-07-06 · 관련 파일: `.gitignore`, `.gitattributes`, `Docs/Dependencies.md`(예정)*
