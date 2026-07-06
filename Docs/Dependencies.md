# 외부 패키지·에셋 기록 (Dependencies)

> 외부 에셋(.unitypackage)은 Git에 올리지 않는다. 대신 **여기에 기록**해서
> 팀원이 새 PC에서도 프로젝트를 똑같이 재현할 수 있게 한다.
> (정책 상세: `Docs/GitIgnore_에셋정책.md`)

## 기록 규칙

- 외부 에셋을 새로 쓰면 **반드시 아래 형식으로 추가**한다.
- import 위치는 항상 `Assets/_ExternalPackages/{에셋이름}` 으로 통일한다.
- 원본은 수정하지 않는다. 수정이 필요하면 `Assets/_Project/VendorOverrides/{에셋이름}` 로 복사 후 수정하고 이유를 적는다.

## 기록 형식 (복사해서 사용)

```
- 에셋이름: v버전 / 형식(.unitypackage 또는 UPM)
  위치: TeamDrive/Packages/파일명
  Import: Assets/_ExternalPackages/에셋이름
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: (이름)
```

## 예시

```
- Casual UI Pack: v1.4.0 / .unitypackage
  위치: TeamDrive/Packages/CasualUI_v1.4.0.unitypackage
  Import: Assets/_ExternalPackages/CasualUI
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/CasualUI)
  담당: 박준서
```

---

## 현재 사용 중인 외부 패키지

*(아직 없음 — 추가 시 위 형식으로 기록)*

---

## UPM(Package Manager) 패키지

Package Manager로 설치한 것은 `Packages/manifest.json`·`packages-lock.json`에 자동 기록되므로 별도 관리가 쉽다. 버전은 lock 파일로 고정된다.

*최종 수정: 2026-07-06*
