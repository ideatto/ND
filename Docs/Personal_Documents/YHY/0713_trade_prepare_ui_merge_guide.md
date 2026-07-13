# 무역 준비 UI (Test 씬 데모) — 병합 가이드

- **작성**: 윤호영 (Core Gameplay) / 2026-07-13
- **브랜치**: `feature/UI/Core/BuildPrepareDisplay-YHY`
- **목적**: 무역 준비 플로우(도시·루트 → 상단 슬롯 → 상단 구성(웨건+동물) → 적재 → 요약) UI 패널 세트 + Test 씬 데모

---

## 1. 이 브랜치에 들어있는 것

### 화면 플로우 (Test 씬에서 Play로 확인 가능)

```
① 도시+루트   도시 클릭 → 아래로 루트 아코디언 펼침 / 도시 0.5초 롱프레스 → 도시 정보 팝업
② 상단 슬롯   내 상단 슬롯 4칸(새 게임=전부 Empty). 슬롯 선택 → Edit 버튼 노출 + 하단 Next 활성
               · Edit → ③ (구성/편집 — 저장된 슬롯이면 웨건+동물 그대로 복원)
               · Next → 빈 슬롯이면 ③(구성), 저장된 슬롯이면 ④(적재)로 직행
③ 상단 구성   왼쪽=웨건(Edit로 넣기/Remove로 빼기, 이미지+동물슬롯), 오른쪽=동물 인벤토리(그리드)
               · 동물 칸 클릭=바로 1마리 탑승(잔량 차감), 웨건 슬롯 클릭=1마리 하차
               · 동물 칸 마우스오버=정보 툴팁 / 우상단 Save 체크=이 구성을 슬롯에 저장
               · 웨건 팝업에 도보(None)·마차(Wagon)·자동차(Mount) — 도보는 항상, 나머진 소지분만
④ 적재        아이템 수량 적재(웨건 칸 수만큼 종류 제한)
⑤ 요약        고른 값 집계 + Depart(데모 로그)
```

### 신규 파일

| 경로 | 내용 |
|---|---|
| `01.Core/05_Caravan/YHY/Panels/` (11개) | 재사용 UI 패널. **씬·SO 타입에 안 묶임**(중립 DTO+이벤트만) |
| ├ `TownRoutePanel` | ① 도시+루트 아코디언 (+롱프레스) |
| ├ `TownInfoPopup` | 도시 정보 팝업 (이미지+설명 / 기여도+특산품 아이콘그리드) |
| ├ `CaravanSlotPanel` | ② 상단 슬롯 (선택 시 Edit 노출) |
| ├ `AnimalInventoryPanel` | ③ 상단 구성 (웨건 Edit/Remove + 동물 편성 + 요구량 검증) |
| ├ `WagonSelectPopup` | 웨건 선택 팝업 (인벤토리) |
| ├ `ItemLoadPanel` / `QuantityRow` | ④ 적재 (수량 행 공용) |
| ├ `AnimalTooltip` / `AnimalTooltipTrigger` | 마우스오버 정보 툴팁 (동물·특산품 공용) |
| ├ `LongPressTrigger` | 탭/롱프레스 구분 |
| └ `TransportSelectPanel` | ⚠️ 패널 자체는 미사용. `TransportType`/`TransportEntry` **타입 홀더**로만 쓰임 (삭제 금지) |
| `01.Core/05_Caravan/YHY/Demo/` | Test 씬 전용 데모 드라이버 + 더미 데이터 |
| ├ `TradePrepareFlowDemo.cs` | 플로우 드라이버 (화면 전환·슬롯 저장/복원·SO→DTO 어댑터). **실제 빌드엔 안 씀** |
| ├ `TradePrepareDemoData.cs` + `.asset` | 더미 에셋 참조 묶음 SO |
| └ `Data/*.asset` (18개) | **진짜 SO 타입**(TownData·RouteData·WagonData·DraftAnimalData·TradeItemData)으로 만든 더미 인스턴스 |
| `01.Core/05_Caravan/YHY/PrepareDisplayData.cs` | 준비 화면 계산값 DTO (UI 바인딩용) |
| `01.Core/05_Caravan/YHY/TradePreparePanel.cs` | 계산값 표시+Framework 출발 API 연결 패널 (요약 화면의 실제 버전 후보) |
| `07.Scenes/04_InGame/Test.unity` | 데모 씬 (UI_TradePrepare + EventSystem) |
| `08.Prefabs/UI_TradePrepare.prefab` | **준비 플로우 전체 프리팹** — 패널·팝업·툴팁·데모드라이버 포함. 씬에 놓고 Play만 하면 동작(씬에 EventSystem 필요, 대부분 이미 있음) |

### 기존 파일 수정 (본인 소유 2개만)

- `CaravanCalculator.cs` — `BuildPrepareDisplay()` 추가 (계산값 한 방 묶음)
- `JourneyRunTest.cs` — `[ContextMenu] 준비 표시 확인` 로그 추가

---

## 2. 병합 안전성 (충돌 리스크)

- **다른 팀원 파일 수정 없음** — 이종현님 SO 타입(TownData 등)은 **읽기만** 함 (데모 어댑터가 DTO로 변환)
- **InGame/Title 씬 안 건드림** — 데모는 별도 Test 씬
- asmdef 없음(전부 Assembly-CSharp) → 참조 문제 없음
- 전부 새 파일 + 본인 파일 2개 수정 → **충돌 가능성 사실상 0**

## 3. ✅ 커밋 제외 처리 — 이미 적용돼 있음 (그냥 add 해도 안전)

개인 도구/부산물이 커밋에 섞이지 않게 **로컬에 이미 설정 완료**:

| 파일 | 처리 방식 | 상태 |
|---|---|---|
| `Assets/Screenshots/` (+.meta) | `.gitignore` 등록 | ✅ status에 안 뜸 |
| `Packages/manifest.json` / `packages-lock.json` (unity-mcp 개인 도구 포함) | `git skip-worktree` — 로컬 수정을 git이 무시 | ✅ status에 안 뜸 |
| `LiberationSans SDF - Fallback.asset` (+.meta) 폰트 캐시 | `git skip-worktree` | ✅ status에 안 뜸 |

→ 이제 `git add .` 해도 위 파일들은 절대 안 들어감. `git status`에 보이는 것만 커밋 대상.

> **🚨 절대 파일을 삭제하지 말 것!**
> `Packages/manifest.json`을 지우면 유니티가 기본값으로 재생성하면서
> ugui(TMP)·URP 등 팀 패키지가 전부 날아가고 에러 수백 개가 남. (2026-07-13 실제 발생)
> 복구: `git restore Packages/manifest.json Packages/packages-lock.json` 후 유니티 재시작
> (unity-mcp 쓰는 중이면 manifest에 그 한 줄만 다시 추가)

**skip-worktree 참고**: 나중에 팀이 manifest를 바꿔서 pull이 충돌나면
`git update-index --no-skip-worktree Packages/manifest.json` 으로 풀고 pull 후 다시 걸면 됨.

## 4. 테스트 방법

1. `Assets/_Project/07.Scenes/04_InGame/Test.unity` 열기
2. ▶ Play → 도시 클릭부터 플로우 진행 (마우스로 전부 조작 가능)
3. 확인 포인트: 아코디언 펼침 / 슬롯 Edit·Next 분기 / 웨건 Edit·Remove / 동물 클릭 탑승·잔량 차감 / Save 체크 후 ②에서 슬롯 저장 확인 / 저장 슬롯 Edit 복원 / 툴팁 / 요약 값

## 5. 알려진 제약 (의도된 것)

- **라벨이 영어** — 프로젝트에 한글 TMP 폰트가 없어 데모는 영어로. 실제 UI 씬 폰트 확정되면 한글 전환
- **더미 데이터** — Demo/Data의 SO는 진짜 타입이지만 값은 임시. 밸런스 값으로 교체하면 그대로 반영
- **소지 개수** — `TradePrepareDemoData.asset`의 `transportOwned`가 [도보, A=1, B=0, Cart=0]. 미소지 웨건은 팝업에 안 나옴(의도)
- **애니메이션 없음** — 와이어프레임의 커짐/슬라이드 연출은 미구현(구조 우선)
- **Demo 폴더는 빌드 제외 대상** — 실제 InGame 통합 시 패널(Panels/)만 프리팹으로 가져가고 드라이버는 참고용

## 6. 병합 후 다음 단계 (제안)

1. ~~Panels를 프리팹으로 추출~~ → **완료: `08.Prefabs/UI_TradePrepare.prefab`** → InGame 씬(이종현님) 배치만 남음
2. 데모 어댑터(SO→DTO)를 실제 게임 데이터 소스로 교체
3. 요약 화면을 `TradePreparePanel`(CaravanCalculator·Validator·Framework 출발 API 연결)로 교체
4. 한글 폰트 적용 + 연출(애니메이션)
