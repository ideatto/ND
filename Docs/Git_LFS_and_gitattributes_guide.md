# Unity 5인 팀 Git LFS 및 `.gitattributes` 운영 가이드

## 1. 목적

이 문서는 Unity 기반 방치형 무역 경영 시뮬레이션 프로젝트에서 다음 사항을 팀 공통 규칙으로 정하기 위한 가이드입니다.

- Git LFS가 필요한 파일과 일반 Git으로 관리해야 하는 파일을 구분합니다.
- 새 확장자 또는 개별 대용량 파일을 Git LFS에 등록하는 절차를 통일합니다.
- 이미 Git에 커밋된 파일을 LFS로 전환하는 방법을 설명합니다.
- 커밋 전에 LFS 적용 여부를 검증하는 명령을 제공합니다.
- 5인 협업에서 저장소 용량과 LFS 사용량이 불필요하게 증가하는 것을 방지합니다.

---

## 2. 현재 `.gitattributes` 검토 결과

### 종합 판정

현재 파일은 일반적인 Unity 프로젝트 기준으로 **상당히 넓고 잘 구성된 편**입니다.

이미 다음 항목을 포함하고 있습니다.

- Unity YAML 파일: `.unity`, `.prefab`, `.asset`, `.meta`, `.mat`, `.anim`, `.controller` 등
- 코드와 셰이더: `.cs`, `.shader`, `.hlsl`, `.compute`
- 주요 3D 모델: `.fbx`, `.blend`, `.obj`, `.dae` 등
- 주요 이미지: `.png`, `.jpg`, `.psd`, `.tga`, `.exr`, `.hdr` 등
- 주요 오디오와 영상: `.wav`, `.mp3`, `.ogg`, `.mp4`, `.mov` 등
- 압축 파일, 폰트, 네이티브 라이브러리, PDF 등
- `UnityYAMLMerge`를 사용하기 위한 Unity YAML 병합 속성

따라서 파일 전체를 새로 교체할 필요는 없습니다. 다만 최근 Unity 프로젝트에서 자주 사용하는 일부 형식과 Unity 바이너리 `.asset` 예외 처리가 빠져 있습니다.

### 보완이 필요한 핵심 항목

1. 최신 3D 교환 형식
   - `.glb`
   - 필요 시 `.gltf`

2. GPU 압축 텍스처와 대형 원본 이미지
   - `.dds`
   - `.ktx`
   - `.ktx2`
   - `.basis`
   - `.psb`

3. 추가 오디오와 영상 형식
   - `.flac`
   - `.aac`
   - `.m4a`
   - `.webm`

4. Unity 네이티브 및 모바일 플러그인
   - `.dylib`
   - `.aar`
   - `.jar`

5. Unity가 `.asset` 확장자로 저장하지만 실제 내용은 바이너리인 파일
   - `LightingData.asset`
   - TerrainData
   - NavMeshData
   - 일부 베이크 데이터와 플러그인 생성 데이터

현재 저장소에는 `LightingData.asset`에 대해 바이너리 처리 규칙과 LFS 규칙이 함께 존재합니다.

기존 바이너리 처리 규칙:

```gitattributes
LightingData.asset binary
```

권장 LFS 처리 규칙:

```gitattributes
LightingData.asset lfs
```

베이크된 조명 데이터를 저장소에서 관리한다면 `LightingData.asset lfs` 규칙을 최종 기준으로 삼는 편이 일반적으로 안전합니다. 다만 실제 `.gitattributes` 정리는 팀 합의 후 중복 규칙을 제거하는 별도 변경으로 처리합니다.

> 주의: `*.asset lfs`를 추가하면 안 됩니다. 대부분의 Unity `.asset` 파일은 텍스트 YAML이며 Git diff와 병합의 이점을 받을 수 있습니다. 바이너리 `.asset`만 파일명 또는 폴더 경로로 선별해야 합니다.

---

## 3. 현재 파일에 추가할 권장 규칙

현재 `.gitattributes`의 **LFS 관련 규칙 아래쪽**, 특히 일반 `*.asset unity-yaml` 규칙보다 뒤에 추가합니다. 더 구체적인 예외 규칙은 파일 아래쪽에 두는 것이 안전합니다.

### 3.1 기본 권장 추가 항목

```gitattributes
# Modern 3D formats
*.glb                   lfs

# Texture and large image formats
*.dds                   lfs
*.ktx                   lfs
*.ktx2                  lfs
*.basis                 lfs
*.psb                   lfs

# Additional audio and video formats
*.aac                   lfs
*.flac                  lfs
*.m4a                   lfs
*.webm                  lfs

# Native and mobile plugin binaries
*.dylib                 lfs
*.aar                   lfs
*.jar                   lfs

# Unity binary asset exception
LightingData.asset      lfs
```

### 3.2 사용하는 제작 도구에 따라 추가할 항목

다음 형식은 프로젝트에서 실제로 사용할 때만 등록합니다.

```gitattributes
# Text-based but usually generated 3D data
*.gltf                  lfs

# 2D source art
*.kra                   lfs
*.clip                  lfs
*.ase                   lfs
*.aseprite              lfs
*.xcf                   lfs
*.ai                    lfs
*.afdesign              lfs
*.afphoto               lfs

# Substance 3D source files
*.spp                   lfs
*.sbs                   lfs
*.sbsar                 lfs

# WebAssembly or machine-learning model data, when used
*.wasm                  lfs
*.onnx                  lfs
```

`.gltf`는 JSON 텍스트 형식입니다. 파일이 작고 실제 diff가 유용하다면 일반 Git으로 유지할 수 있습니다. 자동 생성되고 크기가 크며 사람이 직접 병합하지 않는 파일이라면 LFS가 적합합니다.

---

## 4. LFS로 관리하면 안 되는 대표 파일

다음 Unity 파일은 일반적으로 텍스트 YAML이므로 LFS로 보내지 않습니다.

```text
*.unity
*.prefab
*.meta
*.mat
*.anim
*.controller
*.overrideController
*.asset
*.asmdef
*.inputactions
*.shadergraph
```

이 파일들은 Git diff, 브랜치 병합, `UnityYAMLMerge`의 이점을 받을 수 있습니다.

특히 다음 규칙은 금지합니다.

```gitattributes
*.asset lfs
*.unity lfs
*.prefab lfs
*.meta lfs
```

단, `LightingData.asset`, TerrainData, NavMeshData처럼 실제 바이너리인 예외는 개별 파일명이나 경로로 등록합니다.

---

## 5. TerrainData와 NavMeshData 관리 방법

TerrainData와 NavMeshData는 `.asset` 확장자를 사용하므로 확장자만으로는 텍스트 `.asset`과 구분할 수 없습니다.

가장 안전한 방식은 팀 폴더 규칙을 정한 뒤 경로 단위로 LFS를 적용하는 것입니다.

예시 폴더 구조:

```text
Assets/_Project/World/TerrainData/
Assets/_Project/Navigation/NavMeshData/
```

예시 `.gitattributes`:

```gitattributes
Assets/_Project/World/TerrainData/*.asset          lfs
Assets/_Project/Navigation/NavMeshData/*.asset     lfs
```

하위 폴더까지 사용할 경우 실제 프로젝트 경로에 맞춰 규칙을 추가합니다. `.meta` 파일은 LFS에 넣지 않고 일반 Git으로 유지합니다.

---

## 6. 팀원 최초 설정

### GitHub Desktop 사용자

GitHub Desktop에는 Git LFS가 함께 설치되지만, 저장소별 초기 설정과 점검은 명령줄에서 수행하는 것이 안전합니다.

프로젝트 루트에서 다음을 실행합니다.

```bash
git lfs install
git lfs env
git lfs pull
```

- `git lfs install`: 현재 사용자 환경에 Git LFS 필터를 설정합니다.
- `git lfs env`: Git LFS 설치와 저장소 설정을 확인합니다.
- `git lfs pull`: 현재 체크아웃된 커밋에 필요한 LFS 실제 파일을 내려받습니다.

새로 저장소를 클론한 팀원은 Unity를 열기 전에 `git lfs pull` 결과를 확인해야 합니다.

---

## 7. 새 확장자를 LFS에 등록하는 표준 절차

예시: `.glb` 파일을 LFS에 등록합니다.

```bash
git lfs track "*.glb"
git diff -- .gitattributes
git add .gitattributes
git commit -m "chore: track GLB files with Git LFS"
```

그다음 실제 에셋을 추가합니다.

```bash
git add Assets/Art/Models/example.glb
git commit -m "feat: add trade town model"
git push
```

### 현재 저장소의 매크로 형식과 맞추는 방법

현재 `.gitattributes` 상단에는 다음 매크로가 있습니다.

```gitattributes
[attr]lfs filter=lfs diff=lfs merge=lfs -text
```

따라서 파일을 직접 편집할 때는 다음처럼 짧게 작성해도 됩니다.

```gitattributes
*.glb lfs
```

`git lfs track "*.glb"` 명령은 다음과 같은 전체 형식을 추가할 수 있습니다.

```gitattributes
*.glb filter=lfs diff=lfs merge=lfs -text
```

두 형식은 기능상 동일합니다. 팀 문서 가독성을 위해 기존 파일에서는 `*.glb lfs` 형식을 권장합니다.

---

## 8. 확장자 전체와 개별 파일 중 무엇을 등록할지 판단하는 기준

### 확장자 전체를 등록

다음 조건이 대부분 맞으면 확장자 전체를 등록합니다.

- 파일 형식이 바이너리라서 Git diff가 의미 없습니다.
- 같은 형식의 파일이 계속 추가될 예정입니다.
- 파일 크기가 보통 수 MB 이상입니다.
- 수정할 때마다 Git 저장소 이력이 크게 증가할 수 있습니다.
- 팀원이 직접 텍스트 병합할 가능성이 없습니다.

예시:

```bash
git lfs track "*.psd"
```

### 개별 파일만 등록

다음 조건이면 특정 파일 경로만 등록합니다.

- 같은 확장자의 대부분 파일은 작습니다.
- 특정 파일 하나만 예외적으로 큽니다.
- LFS 저장 공간과 다운로드 사용량을 절약해야 합니다.
- 확장자 전체를 LFS로 보내면 불필요한 파일까지 포함됩니다.

예시:

```bash
git lfs track "Assets/Art/Source/WorldMap_Master.psd"
```

`.gitattributes`에는 다음과 같이 기록됩니다.

```gitattributes
Assets/Art/Source/WorldMap_Master.psd filter=lfs diff=lfs merge=lfs -text
```

Git LFS의 미래 추적 규칙은 파일 크기가 아니라 경로 패턴을 기준으로 작동합니다. `.gitattributes`에서 “50MB가 넘는 파일만 자동 추적” 같은 조건은 작성할 수 없습니다.

---

## 9. 커밋 전 LFS 적용 여부 확인

### 특정 파일 검사

```bash
git check-attr filter diff merge text -- Assets/Art/Models/example.glb
```

정상적인 LFS 파일은 대체로 다음과 비슷하게 표시됩니다.

```text
filter: lfs
diff: lfs
merge: lfs
text: unset
```

### 현재 LFS 파일 목록

```bash
git lfs ls-files
```

### 커밋 예정 LFS 상태

```bash
git lfs status
```

### 일반 Git 상태

```bash
git status
```

### 실제 인덱스에 LFS 포인터가 들어가는지 확인

```bash
git show :Assets/Art/Models/example.glb
```

LFS가 정상 적용되었다면 바이너리 내용 대신 다음 형태의 짧은 포인터가 표시됩니다.

```text
version https://git-lfs.github.com/spec/v1
oid sha256:...
size ...
```

작업 폴더의 원본 파일은 정상적인 실제 에셋이어야 하며, Git 인덱스와 원격 저장소에는 포인터가 기록됩니다.

---

## 10. 50MiB 이상 파일 점검

GitHub는 일반 Git에 50MiB보다 큰 파일을 추가하면 경고하고, 100MiB보다 큰 파일은 일반 Git push를 차단합니다.

Git Bash에서 프로젝트의 50MiB 초과 파일을 검사하는 예시:

```bash
find . -type f -size +50M   -not -path "./.git/*"   -not -path "./Library/*"   -not -path "./Temp/*"   -not -path "./Logs/*"   -print
```

발견된 파일마다 다음을 판단합니다.

1. 생성 결과물인가?
   - 빌드, 캐시, 임시 출력이면 `.gitignore`에 추가합니다.

2. 프로젝트 원본 에셋인가?
   - 저장소에 필요하면 LFS 적용 여부를 확인합니다.

3. 같은 확장자의 대용량 파일이 계속 생길 것인가?
   - 그렇다면 확장자 전체를 등록합니다.

4. 특정 파일만 예외적으로 큰가?
   - 해당 파일 경로만 등록합니다.

---

## 11. 이미 Git에 추적 중인 파일을 LFS로 전환

### 11.1 과거 이력을 바꾸지 않고 현재 버전부터 전환

협업 중인 저장소에서는 이 방법이 상대적으로 안전합니다.

```bash
git lfs track "*.glb"
git add .gitattributes
git add --renormalize .
git status
git commit -m "chore: migrate current GLB files to Git LFS"
git push
```

이 방법은 현재 커밋 이후의 파일을 LFS 포인터로 바꾸지만, 과거 커밋에 저장된 원본 바이너리는 Git 이력에 남습니다.

### 11.2 과거 이력 전체를 LFS로 전환

저장소가 이미 지나치게 커졌거나 100MiB 초과 파일 때문에 push가 차단될 때 사용합니다.

먼저 분석합니다.

```bash
git lfs migrate info --everything
```

예시 전환:

```bash
git lfs migrate import --everything --include="*.glb,*.psd,*.wav"
```

이 명령은 커밋 SHA를 변경하는 **이력 재작성**입니다.

반드시 다음 조건을 지켜야 합니다.

- 팀 리더 한 명만 수행합니다.
- 원격 저장소와 로컬 저장소를 백업합니다.
- 작업 중인 브랜치와 Pull Request를 먼저 정리합니다.
- 팀원 전원에게 작업 중단 시간을 공유합니다.
- 강제 push 이후 팀원은 새로 clone하는 방식을 권장합니다.
- `main`, `dev` 같은 보호 브랜치의 강제 push 설정을 확인합니다.

단순히 새 확장자를 추가하는 상황에서는 이력 재작성을 사용하지 않습니다.

---

## 12. `.gitignore`와 `.gitattributes`가 겹치는 항목

현재 설정에는 다음처럼 `.gitignore`에서 제외하면서 `.gitattributes`에서는 LFS로 지정한 항목이 일부 있습니다.

- `.unitypackage`
- `.apk`
- `.pdb`

Git에서 무시되는 파일은 기본적으로 커밋되지 않으므로 LFS 규칙이 실제로 적용되지 않습니다. 이는 오류는 아니지만 규칙이 중복된 상태입니다.

### `.unitypackage` 처리 기준

현재 프로젝트는 `_ExternalPackages`와 `.unitypackage`를 저장소에서 제외하는 정책을 사용하고 있으므로 `.unitypackage` LFS 규칙은 사실상 사용되지 않습니다.

향후 `.unitypackage` 자체를 저장소에 보관하기로 결정했다면 다음 두 조치가 모두 필요합니다.

1. `.gitignore`에서 `*.unitypackage` 규칙 제거
2. `.gitattributes`의 `*.unitypackage lfs` 규칙 유지

일반적으로 외부 패키지는 Unity Package Manager, 팀 공유 저장소, 릴리스 첨부 파일 등을 사용하고 프로젝트 Git 이력에는 넣지 않는 편이 낫습니다.

---

## 13. `_ExternalPackages` 정책 주의사항

현재 `.gitignore`는 다음 폴더의 내용을 전부 무시합니다.

```text
Assets/_ExternalPackages/
```

이 방식은 저장소 용량을 줄일 수 있지만 Unity 참조 안정성 측면에서는 보편적인 방식이 아닙니다.

Unity의 Scene, Prefab, Material은 `.meta` 파일의 GUID를 통해 에셋을 참조합니다. 팀원마다 다른 버전의 패키지를 가져오거나 import 경로가 다르면 다음 문제가 발생할 수 있습니다.

- Missing Script
- Missing Prefab
- Material 또는 Texture 참조 손실
- 서로 다른 GUID로 인한 씬 차이
- 팀원 PC에서만 정상 동작하는 상태

이 정책을 유지하려면 최소한 다음을 팀 문서에 고정해야 합니다.

- 패키지 정확한 파일명과 버전
- 다운로드 위치
- import 대상 경로
- import 순서
- 커스텀 수정 금지
- 수정이 필요할 때 복사할 `VendorOverrides` 경로
- 새 팀원이 프로젝트를 열기 전에 수행할 설치 절차

가능하면 UPM 패키지, Git URL 패키지, 임베디드 패키지 또는 라이선스가 허용되는 범위에서의 저장소 추적을 우선 검토합니다.

---

## 14. LFS 저장 공간과 다운로드 사용량 주의

Git LFS 파일은 일부만 수정해도 변경된 파일 전체가 새 LFS 객체로 저장됩니다.

예를 들어 500MB 원본 파일에서 1바이트만 변경하고 다시 push해도 새 버전 500MB가 추가로 저장될 수 있습니다.

따라서 다음 파일을 무조건 LFS로 넣는 것은 피해야 합니다.

- 자주 자동 재생성되는 대형 파일
- 빌드 결과물
- 캐시
- 매번 전체가 바뀌는 임시 데이터
- 외부에서 다시 다운로드할 수 있는 설치 파일
- 사용하지 않는 원본 제작 파일

GitHub 계정 또는 조직의 LFS 저장 공간과 다운로드 사용량을 팀장이 정기적으로 확인해야 합니다.

---

## 15. 문제 해결

### 파일이 짧은 텍스트 포인터로만 보임

```bash
git lfs install
git lfs pull
```

그래도 복원되지 않으면 다음을 확인합니다.

```bash
git lfs env
git lfs ls-files
git status
```

### `should have been pointers` 오류

`.gitattributes`에 LFS 규칙을 추가했지만 기존 추적 파일이 아직 일반 Git blob으로 남아 있을 가능성이 큽니다.

```bash
git add --renormalize .
git status
git commit -m "chore: normalize Git LFS tracked files"
```

과거 이력까지 문제가 있으면 팀 리더가 `git lfs migrate` 사용을 검토합니다.

### 100MiB 초과 파일로 push 거부

파일을 최신 커밋에서 삭제했더라도 과거 커밋에 남아 있으면 push가 거부될 수 있습니다.

```bash
git lfs migrate info --everything
```

필요하면 팀 단위로 이력 재작업을 수행합니다.

### LFS 파일 다운로드 실패

```bash
git lfs fetch --all
git lfs pull
```

원격 LFS 할당량, 권한, 네트워크 상태도 확인합니다.

---

## 16. 팀 공통 운영 규칙

1. `.gitattributes` 변경은 가능하면 에셋 추가와 분리하여 먼저 커밋합니다.
2. 새 대용량 확장자를 추가하기 전 팀원에게 공유합니다.
3. 50MiB 이상 파일은 커밋 전에 LFS 적용 여부를 확인합니다.
4. `.unity`, `.prefab`, `.meta`, 일반 `.asset`은 LFS에 등록하지 않습니다.
5. TerrainData, NavMeshData 등 바이너리 `.asset`은 전용 폴더에 배치합니다.
6. LFS 규칙을 제거하거나 이력을 재작성하는 작업은 팀 리더 승인 없이 수행하지 않습니다.
7. 새 팀원은 clone 후 Unity 실행 전에 `git lfs install`과 `git lfs pull`을 실행합니다.
8. 빌드 결과물과 재생성 가능한 캐시는 LFS가 아니라 `.gitignore`로 제외합니다.
9. 대형 원본 파일은 불필요한 중간 버전을 반복 커밋하지 않습니다.
10. 커밋 전 `git lfs status`와 `git status`를 함께 확인합니다.

---

## 17. 권장 커밋 예시

`.gitattributes` 보완:

```bash
git add .gitattributes
git commit -m "chore: expand Unity Git LFS tracking rules"
```

`.gitignore` 보완:

```bash
git add .gitignore
git commit -m "chore: update Unity gitignore rules"
```

기존 파일의 현재 버전 LFS 전환:

```bash
git add --renormalize .
git commit -m "chore: normalize assets tracked by Git LFS"
```

---

## 18. 최종 체크리스트

- [ ] 모든 팀원이 `git lfs install`을 실행했다.
- [ ] `.gitattributes`가 저장소 루트에 있다.
- [ ] `.gitattributes` 자체가 Git에 커밋되어 있다.
- [ ] `.glb`, `.psb`, 추가 오디오·영상·플러그인 형식을 검토했다.
- [ ] `LightingData.asset`을 사용할 경우 LFS 규칙을 적용했다.
- [ ] TerrainData와 NavMeshData 전용 폴더 규칙을 정했다.
- [ ] `*.asset lfs` 같은 광범위한 규칙이 없다.
- [ ] 50MiB 이상 파일을 검사했다.
- [ ] `git lfs status`에서 예상 파일만 표시된다.
- [ ] 새 clone 환경에서 `git lfs pull` 후 Unity 프로젝트가 정상적으로 열린다.

---

## 참고 기준

- GitHub 공식 Unity `.gitignore` 템플릿
- GitHub Docs: Git Large File Storage 설정 및 대용량 파일 제한
- Git LFS 공식 `git lfs migrate` 문서
- Unity Manual: Version Control 및 UnityYAMLMerge
- Unity Learn: Unity 프로젝트의 Git LFS 설정
