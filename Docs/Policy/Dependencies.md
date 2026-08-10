# 외부 패키지·에셋 기록 (Dependencies)

> 외부 에셋(.unitypackage)은 Git에 올리지 않는다. 대신 **여기에 기록**해서
> 팀원이 새 PC에서도 프로젝트를 똑같이 재현할 수 있게 한다.
> (정책 상세: `Docs/GitIgnore_Asset_Policy.md`)

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

- 에셋이름: HyperCasualFXPackVol.2 v1.2 / .unitypackage
  위치: TeamDrive/Packages/HyperCasualFXPackVol.2_v1.2_20260727
  Import: Assets/_ExternalPackages/HyperCasualFXPackVol.2
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: fantasygui4 v1.2 / .unitypackage
  위치: TeamDrive/Packages/fantasygui4_v1.2_20260727
  Import: Assets/_ExternalPackages/fantasygui4
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: polyperfect v10.0 / .unitypackage
  위치: TeamDrive/Packages/polyperfect_v10.0_20260727
  Import: Assets/_ExternalPackages/polyperfect
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: PixelartLoot v1.1 / .unitypackage
  위치: TeamDrive/Packages/PixelartLoot_v1.1_20260727
  Import: Assets/_ExternalPackages/PixelartLoot
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: MobileFantasyIdleUIKit v1.0 / .unitypackage
  위치: TeamDrive/Packages/MobileFantasyIdleUIKit_v1.0_20260727
  Import: Assets/_ExternalPackages/MobileFantasyIdleUIKit
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: GameVFX-CardEffectsCollection v1.01 / .unitypackage
  위치: TeamDrive/Packages/GameVFX-CardEffectsCollection_v1.01_20260727
  Import: Assets/_ExternalPackages/GameVFX-CardEffectsCollection
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: SmokeAndDust v1.0 / .unitypackage
  위치: TeamDrive/Packages/SmokeAndDust_v1.0_20260727
  Import: Assets/_ExternalPackages/SmokeAndDust
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: Match3GameEffect v1.0 / .unitypackage
  위치: TeamDrive/Packages/Match3GameEffect_v1.0_20260727
  Import: Assets/_ExternalPackages/Match3GameEffect
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: 6000FantasyIcons v1.1 / .unitypackage
  위치: TeamDrive/Packages/6000FantasyIcons_v1.1_20260727
  Import: Assets/_ExternalPackages/6000FantasyIcons
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: CharacterEffects v1.1 / .unitypackage
  위치: TeamDrive/Packages/CharacterEffects_v1.1_20260728
  Import: Assets/_ExternalPackages/CharacterEffects
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: ToonScapesSpring v1.02 / .unitypackage
  위치: TeamDrive/Packages/ToonScapesSpring_v1.02_20260728
  Import: Assets/_ExternalPackages/ToonScapesSpring
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: ClickSFX v1.0 / .unitypackage
  위치: TeamDrive/Packages/ClickSFX_v1.0_20260810
  Import: Assets/_ExternalPackages/ClickSFX
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: CasualGameSounds v1.1 / .unitypackage
  위치: TeamDrive/Packages/CasualGameSounds_v1.1_20260810
  Import: Assets/_ExternalPackages/Casual Game Sounds U6
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱

- 에셋이름: MusicLoopsMiniSet v2.0 / .unitypackage
  위치: TeamDrive/Packages/MusicLoopsMiniSet_v2.0_20260810
  Import: Assets/_ExternalPackages/Music Loops Mini Set
  Git 포함: 아니오
  수정: 금지 (필요 시 VendorOverrides/에셋이름)
  담당: 천성욱
---

## UPM(Package Manager) 패키지

Package Manager로 설치한 것은 `Packages/manifest.json`·`packages-lock.json`에 자동 기록되므로 별도 관리가 쉽다. 버전은 lock 파일로 고정된다.

*최종 수정: 2026-07-06*
