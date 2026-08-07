# Caravan Overview 현재 형태 재조립 가이드

## 1. 목적과 적용 범위

이 문서는 `MainUICanvas.prefab`과 `InGame.unity`의 현재 변경을 폐기한 뒤에도 Caravan Overview를 현재 형태로 다시 조립하기 위한 기준 문서다. 최신 dev2의 실패 Claim 전손, 손실 Popup과 순차 정산은 `0807_Dev2_InGame_Reassembly_and_Failed_Trade_Loss.md`를 함께 따른다.

포함 기능은 다음과 같다.

- Caravan 이름 영역 클릭으로 Treadmill 열기/닫기
- 이름 영역과 Set 버튼 사이의 별도 Rename 버튼
- Rename 버튼으로 기존 이름 변경 팝업 열기
- Prepare/Traveling/Selling/Settling/Completed 상태 아이콘
- Traveling 상태의 말 스프라이트 애니메이션
- Selling은 느낌표, Settling과 Completed는 체크 표시
- Empty/Locked/Unknown 슬롯에서는 Occupied 전용 버튼과 상태 표시 숨김

UI 오브젝트는 Prefab Mode에서 미리 배치한다. 슬롯처럼 공통적으로 반복되는 데이터 행을 제외하고 조립용 UI를 런타임에 생성하지 않는다.

실패 손실 Popup은 Overview 원본 Prefab의 구성 요소가 아니다. `InGame.unity`의 활성 MainUICanvas instance 아래에 별도 Prefab instance로 배치하고 `MainUICanvas.prefab`에는 Apply하지 않는다.

## 2. 폐기 전에 반드시 보존할 파일

다음은 Prefab/Scene 조립 결과가 아니라 기능 코드 또는 원본 에셋이므로 함께 폐기하면 안 된다.

- `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/CaravanSlotView.cs`
- `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/CaravanOverviewPresenter.cs`
- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/CaravanOverviewRenameBinding.cs`
- `Assets/99.Sandbox/_LJH/01.Script/Runtime/UI/CaravanRenamePopupController.cs`
- `Assets/_Project/09.Art/04_UI/icons/Animations/CaravanSlot/HorseCycle.png`
- `Assets/_Project/09.Art/04_UI/icons/Animations/CaravanSlot/HorseCycle.png.meta`
- `Assets/_Project/09.Art/04_UI/icons/Animations/CaravanSlot/CaravanSlotTravelingHorse.anim`
- `Assets/_Project/09.Art/04_UI/icons/Animations/CaravanSlot/CaravanSlotTravelingHorse.anim.meta`
- `Assets/_Project/09.Art/04_UI/icons/Animations/CaravanSlot/JourneyStateDisplay.controller`
- Rename 아이콘으로 사용할 `Assets/_Project/09.Art/04_UI/icons/pencle.png`와 `.meta`

`HorseCycle.png`, 애니메이션 클립, Rename 아이콘이 아직 untracked라면 Prefab/Scene discard와 별개로 먼저 보존해야 한다.

기존 회전 데모 클립 `CaravanSlotTravelingRotate.anim`은 더 이상 사용하지 않는다. 전체 폴더를 과거 상태로 복원하여 이 클립이 되살아나더라도 Controller의 Traveling 상태에 연결하지 않는다.

## 3. 목표 Hierarchy

각 슬롯은 다음 구조를 기준으로 한다. 기존 네 개 `CaravanSlot` 모두 동일하게 조립한다.

```text
MainUICanvas
└─ InfoPanel
   └─ CaravanPanel
      └─ CaravanScrollView        [CaravanOverviewPresenter]
         └─ Viewport
            └─ Content
               ├─ CaravanSlot 0   [CaravanSlotView]
               │  ├─ CaravanInfo / DisplayName
               │  ├─ RenameButton [Image, Button, LayoutElement]
               │  │  └─ Icon      [Image]
               │  ├─ SettingButton
               │  ├─ CargoButton
               │  ├─ JourneyStateDisplay
               │  │  ├─ Label     [TMP_Text]
               │  │  └─ Icon      [Image, Animator]
               │  └─ LockOverlay
               ├─ CaravanSlot 1
               ├─ CaravanSlot 2
               └─ CaravanSlot 3
```

오브젝트 이름이 조금 다르더라도 `CaravanSlotView`의 직렬화 필드가 아래 계약대로 연결되어야 한다.

## 4. CaravanSlotView 연결

각 슬롯의 `CaravanSlotView` Inspector를 다음처럼 연결한다.

| 필드 | 연결 대상 |
| --- | --- |
| `Slot Index` | 슬롯 순서에 따라 0, 1, 2, 3 |
| `Display Name Text` | 슬롯 이름 TMP |
| `Rename Button` | 새 `RenameButton`의 Button |
| `Setting Button` | 기존 Set 버튼 |
| `Cargo Button` | 기존 Cargo 버튼 |
| `Journey State Display` | 상태 표시 루트 |
| `Journey State Text` | 상태 표시의 fallback TMP |
| `Journey State Icon Image` | 상태 표시의 `Icon` Image |
| `Journey State Icon Animator` | 같은 `Icon`의 Animator |
| `Traveling Animator Parameter` | `IsTraveling` |

버튼 `OnClick()`에는 Persistent Listener를 직접 넣지 않는다. `CaravanSlotView`가 코드에서 listener를 등록하고 Presenter 이벤트로 전달한다.

## 5. 슬롯 가로 배치와 Rename 버튼

현재 형태의 기준값은 다음과 같다.

- Display Name 영역 `LayoutElement.Min Width`: 173
- Display Name 영역 `LayoutElement.Preferred Width`: 173
- Display Name TMP: Auto Size 켜기
- Display Name TMP Font Size 범위: 14~20
- Display Name TMP Overflow: Ellipsis
- Display Name TMP 좌우 Margin: 6
- Rename 버튼 `LayoutElement.Min Width`: 72
- Rename 버튼 `LayoutElement.Preferred Width`: 72
- Rename 버튼 높이: 44
- Rename 버튼 Image 색상: `RGB(212, 170, 93)`에 가까운 황갈색
- Rename 버튼에는 비활성 `Label` TMP 자식을 유지하고 문자열은 비워 둔다. 현재 Prefab 계약 테스트가 이 구조를 검사한다.
- Rename 버튼 자식 `Icon` Image에 Rename 스프라이트 지정
- Rename Icon의 `Raycast Target`: Off

Rename 버튼은 Display Name과 Setting 버튼 사이에 둔다. 버튼 자체와 아이콘은 Prefab에 존재해야 하며 런타임에 생성하지 않는다.

동작 계약은 다음과 같다.

- Display Name 클릭: `TreadmillRequested(caravanId, displayName)`
- Rename 버튼 클릭: `RenameRequested(caravanId)`
- 이름 영역을 길게 눌러 Rename하는 기존 방식은 사용하지 않는다.

## 6. Rename 팝업 연결

`CaravanScrollView` 또는 현재 Overview 조립 루트에서 다음 연결을 확인한다.

| 컴포넌트 | 필드 | 연결 대상 |
| --- | --- | --- |
| `CaravanOverviewRenameBinding` | `Presenter` | 같은 Overview의 `CaravanOverviewPresenter` |
| `CaravanOverviewRenameBinding` | `Popup` | 기존 `CaravanRenamePopupController` |

`CaravanOverviewPresenter.slotViews`에는 네 슬롯을 인덱스 순서대로 연결한다. `RenameRequested`는 Presenter를 거쳐 Rename Binding이 팝업을 연다.

## 7. Journey 상태 아이콘 연결

각 슬롯에 동일한 스프라이트를 배정한다.

| 상태 | CaravanSlotView 필드 | 배정 |
| --- | --- | --- |
| Prepare | `Prepare State Icon` | `HorseCycle_0` |
| Traveling | `Traveling State Icon` | `HorseCycle_0` |
| Selling | `Selling State Icon` | 느낌표 아이콘 |
| Settling | `Settling State Icon` | 기존 Completed 체크 아이콘 |
| Completed | `Completed State Icon` | 기존 체크 아이콘 유지 |

아이콘이 정상 배정되면 코드가 `Journey State Text`를 숨긴다. 스프라이트가 비어 있을 때만 텍스트가 fallback으로 표시된다.

## 8. HorseCycle 임포트와 애니메이션

`HorseCycle.png` Texture Import Settings:

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Multiple`
- Pixels Per Unit: 100
- Mesh Type: `Full Rect`
- Generate Physics Shape: Off
- Filter Mode: Bilinear
- Compression: None
- Mip Maps: Off
- Alpha Is Transparency: On

현재 슬라이스는 4열 × 3행, 총 12개이며 각 셀의 기준 크기는 404 × 323이다. 덜컹임을 줄이려면 모든 프레임을 같은 사각형 크기와 같은 Pivot으로 유지한다.

`CaravanSlotTravelingHorse.anim`에는 `Icon : Image.Sprite` 트랙만 둔다. `Icon : Rotation`, Position, Scale 트랙이 생겼다면 제거한다. 현재 권장 애니메이션 프레임은 `HorseCycle_0`~`HorseCycle_3` 반복이며 Loop Time을 켠다.

Animator Controller 조립:

1. `JourneyStateDisplay.controller`에 Bool Parameter `IsTraveling`을 둔다.
2. 기본 상태는 Sprite를 건드리지 않는 `Idle`로 둔다.
3. `TravelingHorse` 상태 Motion에 `CaravanSlotTravelingHorse.anim`을 연결한다.
4. `Idle -> TravelingHorse`: `IsTraveling == true`, Has Exit Time Off.
5. `TravelingHorse -> Idle`: `IsTraveling == false`, Has Exit Time Off.
6. 각 슬롯의 상태 `Icon` GameObject에 Animator를 미리 부착하고 Controller를 연결한다.

`CaravanSlotView`는 Traveling일 때만 Animator를 활성화하고 정적 상태에서는 비활성화한다. 따라서 Selling/Settling/Completed 아이콘이 Animator의 빈 Sprite에 덮이지 않는다.

실패 S9 Claim 저장 성공 뒤 해당 Caravan은 Prepare로 돌아오고 기존 장착 Wagon/Animal은 비워진다. Overview Slot 자체를 삭제하거나 잠금 슬롯으로 바꾸지 않는다. Setting을 다시 열면 남아 있는 예비 운송 자산으로 새 구성을 저장할 수 있어야 한다.

현재 에셋 계약 확인값:

- `HorseCycle.png.meta`: `HorseCycle_0`~`HorseCycle_11`, 총 12 Sprite
- `CaravanSlotTravelingHorse.anim`: `m_Sprite` 트랙 1개, 키 4개, Loop Time On
- `JourneyStateDisplay.controller`: `IsTraveling` Bool, `Idle`, `TravelingHorse` 상태와 양방향 전이
- Animator는 상태 Icon과 같은 GameObject에 부착하므로 Animation Clip의 binding path는 빈 경로가 정상이다.

## 9. Treadmill 연결

Prefab 에셋끼리 Scene 인스턴스를 직접 참조할 수 없으므로 이 연결은 `InGame.unity`에서 수행한다.

1. `InGame.unity`를 연다.
2. Scene의 `MainUICanvas` 인스턴스 아래 `CaravanScrollView`에서 `CaravanOverviewPresenter`를 찾는다.
3. Scene에 존재하는 `TreadmillPanel` 인스턴스를 찾는다.
4. `CaravanOverviewPresenter.Treadmill Panel`에 해당 `TreadmillPanel` 컴포넌트를 연결한다.
5. Scene override로 저장하며 이를 `MainUICanvas.prefab`에 Apply하지 않는다.

동작은 이름 영역 클릭 시 `TreadmillPanel.Toggle(caravanId, displayName)`이다. 같은 Caravan을 다시 클릭하면 닫히고 다른 Caravan을 클릭하면 해당 Lane으로 전환한다.

## 10. 재조립 검증

- [ ] 네 슬롯의 `slotIndex`가 0~3으로 중복 없이 연결됨
- [ ] 네 슬롯 모두 `renameButton`이 None이 아님
- [ ] Rename 버튼이 이름과 Set 버튼 사이에 있음
- [ ] Rename 버튼에 텍스트가 보이지 않고 아이콘이 표시됨
- [ ] 이름 클릭은 Treadmill만 열고 Rename 팝업을 열지 않음
- [ ] Rename 버튼은 Rename 팝업만 열고 Treadmill을 열지 않음
- [ ] Prepare에서 말 0번 이미지가 정지 상태로 표시됨
- [ ] Traveling에서 말 애니메이션이 반복됨
- [ ] Selling에서 텍스트 대신 느낌표가 표시됨
- [ ] Settling과 Completed에서 체크가 표시됨
- [ ] Traveling이 아닌 상태에서 Animator가 정적 아이콘을 지우지 않음
- [ ] Empty/Locked/Unknown 슬롯에서 Rename/Set/Cargo/상태 UI가 숨겨짐
- [ ] `CaravanOverviewPresenter.treadmillPanel`이 Scene에서 연결됨
- [ ] Console에 Missing Script, Missing Reference, Animator Parameter 오류가 없음
