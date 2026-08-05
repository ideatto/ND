# InGame Calendar Popup · 구현 로직 정리

**작성일:** 2026-08-05  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/ui/ingame-visual-and-panel-integration`  
**베이스:** `dev2` (`4b952cf`)  
**문서화 시점 상태:** 워킹 트리 미커밋 변경 기준 (커밋/PR 전)  
**Feature root:** `Assets/_Project/05.UI/10_Calendar/`  
**통합 Prefab:** `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`

관련 선행:

- [`0731_game_calendar_and_seasons_logic.md`](./0731_game_calendar_and_seasons_logic.md) — Framework 달력 권위·`GameCalendarPanel` 표시 계층
- [`Docs/Guide/Framework_Game_Calendar_and_Seasons_API_Guide.md`](../../Guide/Framework_Game_Calendar_and_Seasons_API_Guide.md) — 외부 소비자 API 가이드

---

## 1. 목적

`dev2`에 이미 존재하는 **달력 표시 UI**(`GameCalendarPanel`)를 InGame 공용 캔버스(`MainUICanvas`)에 **팝업 형태로 연결**한다.

플레이어는 InGame 화면 좌상단의 달력 버튼으로 현재 게임 날짜·계절·재난·12개월 하이라이트를 확인할 수 있다.

이번 작업이 하는 것:

1. **팝업 가시성 제어** — `GameCalendarPopupController`가 `CalendarPopupRoot` 활성/비활성만 담당
2. **MainUICanvas 통합** — 팝업 계층, dim 배경, 열기/닫기 버튼 배선
3. **기존 Panel 재사용** — `GameCalendarPanel.prefab`을 Prefab Instance로 삽입 (Presenter/View 로직 변경 없음)
4. **패널 스프라이트 9-slice 보정** — cutout 패널 PNG에 sprite border 적용

이번 작업이 하지 않는 것:

- `GameCalendarService`·저장·이벤트 권위 변경
- 달력 데이터 계산·오프라인 복구 로직 변경
- `GameCalendarPanelView` / `GameCalendarPanelPresenter` 표시 규칙 변경
- 다른 InGame 팝업(창고·건물 등)과의 상호 배타 제어
- Scene(`.unity`) 직접 편집

---

## 2. 변경 파일 요약

| 영역 | 파일 | 역할 |
|------|------|------|
| Script (신규) | `05.UI/10_Calendar/Scripts/GameCalendarPopupController.cs` | Popup Root 표시 상태 전용 제어 |
| Prefab (수정) | `08.Prefabs/UI/Maps/MainUICanvas.prefab` | Calendar 팝업 계층·버튼·Panel Instance 배치 |
| Art (수정) | `09.Art/04_UI/panels/ui_panel_sprite_slim_info_cutout.png` | slim info 패널 원본 |
| Art (수정) | `09.Art/04_UI/panels/ui_panel_sprite_subdued_cutout.png` | subdued 패널 원본 |
| Meta (수정) | 위 PNG + `ui_panel_sprite_light_popup_cutout.png.meta` | 9-slice border·플랫폼 import 설정 |

**통계 (워킹 트리 기준):** 6 tracked + 1 untracked script, Prefab YAML +1880 lines 수준

`10_Calendar`의 Presenter/View/MonthSlot/Editor 테스트·`GameCalendarPanel.prefab`은 **dev2에 이미 포함**되어 있으며, 이번 diff에서는 MainUICanvas 쪽 통합만 추가된다.

---

## 3. 계층 구조 (MainUICanvas)

```text
MainUICanvas
└─ (기존 InGame UI ...)
   ├─ CalenderButton                    ← 좌상단 (80, -80), 80×80
   │    OnClick → GameCalendarPopupController.ToggleCalendar()
   │
   └─ PopupRoot                         ← 전체 화면 stretch, 항상 active
        ├─ GameCalendarPopupController    ← popupRoot = CalendarPopupRoot 참조
        └─ CalendarPopupRoot              ← 초기 m_IsActive: 0 (닫힘)
             ├─ ClickBlocker             ← 전체 화면 dim + 닫기
             │    Image: rgba(0,0,0,0.35)
             │    Button OnClick → CloseCalendar()
             │
             └─ CalendarContent          ← 중앙 760×480
                  ├─ Image + Button      ← 투명 hit area, OnClick → CloseCalendar()
                  └─ GameCalendarPanel   ← Prefab Instance (d9be029d...)
                       GameCalendarPanelPresenter
                       GameCalendarPanelView
                       Content / Header / DisasterRow / Months(12 slots)
```

### 3.1 역할 분리

| 컴포넌트 | 책임 | Framework 접점 |
|----------|------|----------------|
| `GameCalendarPopupController` | 팝업 shell 열기/닫기/토글 | 없음 |
| `GameCalendarPanelPresenter` | 달력 이벤트 구독·현재 snapshot 조회 | `FrameworkRoot.GameCalendar`, `FrameworkEvents` |
| `GameCalendarPanelView` | snapshot → TMP·월 슬롯 반영 | 없음 (읽기 전용 표시) |
| `GameCalendarMonthSlotView` | 개별 월 숫자·current highlight | 없음 |

팝업 컨트롤러는 **데이터를 알지 않는다.** Panel은 PopupRoot가 active인 동안 Framework 이벤트를 계속 받을 수 있지만, `CalendarPopupRoot`가 비활성이면 하위 Presenter도 `OnDisable`로 구독을 해제한다.

---

## 4. 사용자 상호작용 흐름

```text
[닫힌 상태]
  CalenderButton 클릭
    → ToggleCalendar()
    → CalendarPopupRoot.SetActive(true)

[열린 상태]
  CalenderButton 클릭
    → ToggleCalendar()
    → CalendarPopupRoot.SetActive(false)

  ClickBlocker(배경 dim) 클릭
    → CloseCalendar()

  CalendarContent 투명 Button 클릭
    → CloseCalendar()
    (패널 바깥 여백 영역을 눌렀을 때 닫기)
```

### 4.1 `GameCalendarPopupController` API

| 메서드 | 조건 | 동작 |
|--------|------|------|
| `OpenCalendar()` | `popupRoot` 연결됨 && 현재 비활성 | `SetActive(true)` |
| `CloseCalendar()` | `popupRoot` 연결됨 && 현재 활성 | `SetActive(false)` |
| `ToggleCalendar()` | `popupRoot` 연결됨 | 활성 상태 반전 |
| `IsOpen` | — | `popupRoot.activeSelf` |

`popupRoot` 미연결 시 `Debug.LogWarning` 후 no-op. 중복 Open/Close 호출도 no-op.

---

## 5. 달력 데이터 표시 흐름 (기존 Panel 재사용)

Popup이 열리면 `GameCalendarPanelPresenter`가 아래 순서로 View를 갱신한다.

```text
OnEnable
  ├─ FrameworkEvents 구독
  │    CalendarInitialized
  │    CalendarRestored
  │    YearChanged / MonthChanged / SeasonChanged / DisasterChanged
  └─ RefreshFromCurrentState()
       ├─ GameCalendar.TryGetCurrent(out snapshot) 성공
       │    → view.Refresh(snapshot)
       └─ 실패 (세션 미시작)
            → view.HideUntilInitialized()  // content 비활성

이벤트 수신
  CalendarInitialized(snapshot)        → Refresh(snapshot)
  CalendarRestored(result)             → Refresh(result.Current)
  Year/Month/Season/Disaster Changed   → Refresh(current)
```

### 5.1 `GameCalendarPanelView.Refresh` 반영 규칙

| UI 요소 | snapshot 필드 | 표시 |
|---------|---------------|------|
| `yearMonthText` | `Year`, `Month` | `"{Year}년 {Month}월"` |
| `seasonText` | `Season` | 봄/여름/가을/겨울 (한글) |
| `disasterRow` | `ActiveDisasterId` | 비어 있으면 숨김 |
| `disasterText` | `ActiveDisasterId` | `flood`→홍수, `drought`→가뭄, 그 외 raw |
| `monthSlots[1..12]` | `Month` | 해당 월만 `currentHighlight` 활성 |

달력 미초기화 시 `content` GameObject를 숨겨 빈 패널이 보이지 않게 한다. Framework 세션이 시작되면 `CalendarInitialized` 또는 `OnEnable` 시점 `TryGetCurrent`로 자동 표시된다.

### 5.2 Framework 권위 (변경 없음, 참조용)

- 진입점: `FrameworkRoot.Instance.GameCalendar`
- 1게임 일 = UTC 120초, 1월 = 30일, epoch = 1년 3월 1일
- snapshot은 값 복사본; View는 보유·변경하지 않음

상세: [`0731_game_calendar_and_seasons_logic.md`](./0731_game_calendar_and_seasons_logic.md)

---

## 6. Prefab Instance 커스터마이징

`GameCalendarPanel.prefab`을 `CalendarContent` 자식으로 Prefab Instance 배치 후, MainUICanvas에서 아래 override를 적용한다.

- 루트 이름: `GameCalendarPanel`
- 다수 Text/Image의 `m_RaycastTarget: 0` — 팝업 dim/닫기 클릭이 패널 텍스트에 가로막히지 않도록
- 월 슬롯·헤더 RectTransform 미세 조정 (Generator 기본 760×300 대비 InGame popup 760×480 레이아웃)

Panel 내부 Presenter/View 직렬화 참조는 원본 Prefab을 그대로 따른다.

---

## 7. 패널 스프라이트(9-slice) 변경

cutout 패널 PNG 3종의 `.meta`에 **sprite border**를 추가해 UI Image `Type: Sliced`에서 모서리가 늘어나지 않도록 한다.

| 파일 | spriteBorder (L, B, R, T) |
|------|---------------------------|
| `ui_panel_sprite_slim_info_cutout.png` | 97, 49, 101, 59 |
| `ui_panel_sprite_subdued_cutout.png` | (동일 계열 border 적용) |
| `ui_panel_sprite_light_popup_cutout.png.meta` | import/platform 설정 정렬 |

PNG 바이너리도 일부 갱신되어 있으나, 이번 작업의 의도는 **InGame 팝업·정보 패널의 9-slice 렌더링 품질 확보**이다. Calendar Popup shell 자체는 Unity 기본 white sprite + dim overlay를 사용한다.

---

## 8. 설계 의도

### 8.1 Popup Controller를 분리한 이유

- **표시(Presenter/View)** 와 **shell 가시성(PopupController)** 을 분리해 Panel Prefab을 다른 Scene/Canvas에서도 재사용 가능
- Unity Button `OnClick`은 단순 public 메서드만 필요 → PopupController는 `SetActive` 래퍼로 유지
- 달력 권위·이벤트는 Framework/Presenter에만 두고, Popup은 Framework를 참조하지 않음

### 8.2 `CalendarPopupRoot` 초기 비활성

- InGame 진입 시 팝업이 떠 있지 않도록 기본 닫힘
- Panel Presenter는 PopupRoot 자식이므로, 닫힌 동안 `OnDisable`로 이벤트 구독 해제 → 불필요한 Refresh 방지

### 8.3 닫기 경로 3종

- 배경 dim (`ClickBlocker`)
- 패널 주변 투명 영역 (`CalendarContent` Button)
- 토글 버튼 재클릭 (`CalenderButton`)

모두 동일 `GameCalendarPopupController` 인스턴스(`PopupRoot`에 부착)를 대상으로 한다.

---

## 9. 미구현·후속 검토

| 항목 | 상태 |
|------|------|
| 다른 팝업과 동시 열림 방지 (modal stack) | 미구현 |
| ESC / Back 입력으로 닫기 | 미구현 |
| Popup 열림 시 InGame 입력 차단 정책 | dim만 적용, 전역 input lock 없음 |
| `CalenderButton` 오타 수정 | Prefab 이름 그대로 유지 |
| Edit Mode / Play Mode 자동 테스트 (PopupController) | 미추가 (Panel 테스트는 dev2에 존재) |

---

## 10. 검증 체크리스트

- [ ] Unity 컴파일 오류 없음
- [ ] InGame Scene에서 `MainUICanvas` 로드
- [ ] 좌상단 달력 버튼 → 팝업 열림
- [ ] 배경 dim / 패넼 외부 클릭 / 버튼 재클릭 → 팝업 닫힘
- [ ] Framework 세션 시작 후 년월·계절·현재 월 highlight 표시
- [ ] 재난 월(flood/drought)에서 disaster row 표시
- [ ] 달력 미초기화 상태에서 content 숨김
- [ ] `ND/UI/Calendar` Edit Mode 테스트 (`GameCalendarPanelTests`) 통과

---

## 11. 관련 파일 경로

```text
Assets/_Project/05.UI/10_Calendar/
  Scripts/GameCalendarPopupController.cs      ← 이번 추가
  Scripts/GameCalendarPanelPresenter.cs
  Scripts/GameCalendarPanelView.cs
  Scripts/GameCalendarMonthSlotView.cs
  Prefabs/GameCalendarPanel.prefab
  Editor/GameCalendarPanelTests.cs

Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab

Assets/_Project/11.CoreServices/Scripts/Time/GameCalendarService.cs
Assets/_Project/11.CoreServices/Scripts/Events/FrameworkEvents.cs
```

---

## 12. 한 줄 요약

```text
CalenderButton → PopupController가 CalendarPopupRoot를 켠다
  → GameCalendarPanelPresenter가 Framework snapshot/이벤트로 View를 갱신
  → dim/외부 클릭/토글이 PopupController.CloseCalendar()로 shell을 끈다
```

달력 **무엇을 보여줄지**는 Framework + Panel, **어떻게 띄울지**는 MainUICanvas + PopupController가 담당한다.
