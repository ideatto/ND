# Rescue Loan UI

## 목적

`RescueLoanPanel.prefab`은 무역 자금이 최소 무역 비용보다 부족할 때 고정 원금 구조 대출을 발급하고, 활성 대출을 상환하는 인게임 UI입니다.

현재 기본 정책은 다음과 같습니다.

- 최소 무역 비용: Presenter에서 `1,000` 공급
- 발급 원금: 부족액이 아닌 고정 원금 `1,000`
- 발급 조건: 무역 자금이 `1,000` 미만이고 활성 구조 대출이 없을 때
- 상환 가능 시점: 발급 직후부터 가능
- 상환 제한: 상환 후 보유 자금이 최소 무역 비용보다 작아지면 거부
- 전액 상환: 남은 원금과 구조 대출 제한 상태를 해제

## 파일 위치

```text
Assets/_Project/05.UI/12_RescueLoan/
├─ Prefabs/RescueLoanPanel.prefab
├─ Scripts/
│  ├─ RescueLoanPanelPresenter.cs
│  ├─ RescueLoanPanelSnapshot.cs
│  ├─ RescueLoanPanelView.cs
│  └─ RescueLoanPanelVisibility.cs
└─ Editor/
   ├─ RescueLoanPanelPrefabGenerator.cs
   ├─ RescueLoanPanelTests.cs
   └─ RescueLoanPanelPlayModeTests.cs
```

## InGame 씬 배치 위치

프리팹은 `InGame.unity`의 `MainUICanvas` 바로 아래에 별도 Prefab Instance로 배치합니다.

```text
InGame
└─ MainUICanvas
   ├─ 기존 UI 오브젝트
   └─ RescueLoanPanel  ← RescueLoanPanel.prefab 인스턴스
```

다음 위치에는 넣지 않습니다.

- `NavigationButtonGroup` 내부
- 기존 메뉴 버튼의 자식
- `InGameCanvas` 아래의 별도 복제 오브젝트

구조 대출 버튼은 프리팹 내부에서 독립적으로 배치됩니다. 따라서 기존 `거점 / 무역 / 인벤토리 / 설정 / 퀘스트` 버튼의 폭이나 위치를 밀어내지 않습니다.

## 권장 설치 방법

Unity 메뉴에서 다음 항목을 실행합니다.

```text
ND > UI > Install Rescue Loan Panel InGame
```

이 명령은 다음 작업을 수행합니다.

1. `RescueLoanPanel.prefab`을 다시 생성합니다.
2. `InGame.unity`를 엽니다.
3. 기존 구조 대출 패널 인스턴스를 제거해 중복을 방지합니다.
4. `MainUICanvas` 아래에 프리팹 인스턴스 하나를 배치합니다.
5. 루트 RectTransform을 Canvas 전체에 맞춥니다.
6. 씬과 에셋을 저장합니다.

수동으로 배치할 경우에도 반드시 Prefab Instance 상태를 유지하고, 루트 RectTransform을 전체 Stretch로 설정해야 합니다.

## 프리팹 구성

```text
RescueLoanPanel
├─ LoanLauncherButton
│  └─ Label
└─ LoanModal
   ├─ Title
   ├─ Status
   ├─ TradeMoney
   ├─ MinimumTradeCost
   ├─ OriginalPrincipal
   ├─ RemainingPrincipal
   ├─ Restriction
   ├─ RepaymentRow
   ├─ ActionButton
   ├─ Message
   └─ CloseButton
```

### LoanLauncherButton

- 발급 가능 상태에서는 `구조 대출`로 표시됩니다.
- 활성 대출이 있으면 `대출 상환`으로 변경됩니다.
- 정상 자금 상태이고 활성 대출도 없으면 숨겨집니다.
- 기존 NavigationButtonGroup에는 참여하지 않는 독립 버튼입니다.

### LoanModal

- 발급 전에는 고정 원금과 현재 부족액을 표시합니다.
- 발급 후에는 최초 원금, 남은 원금 및 상환 입력란을 표시합니다.
- 기존 공용 `PanelOpener`를 통해 열고 닫습니다.
- 닫혀 있을 때는 GameObject가 비활성화됩니다.

## 코드 역할

### RescueLoanPanelPresenter

- `FrameworkRoot.RescueLoan` CommandService와 View를 연결합니다.
- `loanId = rescue_loan`, `minimumTradeCost = 1000` 정의를 공급합니다.
- SaveData에서 자금과 대출 상태를 읽어 Snapshot을 생성합니다.
- 발급 및 상환 요청을 CommandService로 전달합니다.
- 저장 데이터 로드, 자금 변경, 대출 발급·상환 이벤트에서 UI를 갱신합니다.
- 상환 후 최소 보유금액 조건 위반을 사용자용 한국어 메시지로 변환합니다.

### RescueLoanPanelView

- Snapshot에 따라 버튼 표시 여부와 문구를 결정합니다.
- 발급 모드와 상환 모드를 전환합니다.
- 상환 입력값을 양의 정수로 검증합니다.
- 발급 및 상환 요청 이벤트를 Presenter에 전달합니다.

### RescueLoanPanelVisibility

- 런처 버튼과 닫기 버튼을 기존 `PanelOpener`에 연결합니다.
- 모달 GameObject 활성 상태를 열림·닫힘 상태로 사용합니다.
- 런타임에서 Transform을 검색하거나 다른 부모로 이동하지 않습니다.

### RescueLoanCalculator / RescueLoanCommandService

- Calculator는 발급·상환 가능 여부와 금액을 순수 계산합니다.
- CommandService는 계산 결과를 SaveData에 반영하고 저장합니다.
- 발급·상환 성공 후 `TradingCurrencyChanged`를 발행해 기존 자금 HUD를 갱신합니다.

## 런타임 요구사항

`InGame.unity`에는 다음 항목이 정상적으로 존재해야 합니다.

- `FrameworkRoot`
- `MainUICanvas`
- `GraphicRaycaster`
- `EventSystem`
- 유효한 `CurrentSaveData.player`
- 유효한 `CurrentSaveData.rescueLoan`

프리팹의 직렬화 참조를 임의로 해제하면 버튼은 보이더라도 클릭, 모달 표시 또는 발급·상환이 동작하지 않을 수 있습니다.

## 검증

### EditMode

```text
ND.UI.RescueLoanEditor.RescueLoanPanelTests
```

프리팹 컴포넌트, 표시 상태, 발급·상환 화면, 입력 검증 및 오류 문구를 확인합니다.

### InGame PlayMode

```text
ND.UI.RescueLoanEditor.RescueLoanPanelPlayModeTests
```

다음 내용을 실제 `InGame.unity`에서 확인합니다.

- 프리팹 인스턴스가 한 개만 존재하는지
- `MainUICanvas` 아래에 배치됐는지
- 기존 메뉴 버튼 수가 변경되지 않았는지
- 자금 부족 시 구조 대출 버튼이 자동으로 표시되는지
- EventSystem Raycast가 버튼에 도달하는지
- 실제 버튼 클릭으로 모달이 열리는지
- 모달 CanvasGroup이 표시 및 입력 가능한 상태인지

## 변경 시 주의사항

- `MainUICanvas.prefab`에 구제 대출 오브젝트를 직접 저장하지 않습니다.
- 런타임 `SetParent`로 버튼을 NavigationButtonGroup에 이동하지 않습니다.
- 프리팹 생성기를 실행한 뒤 `InGame.unity`에 중복 인스턴스가 없는지 확인합니다.
- Scene, Prefab 및 `.meta` 파일이 의도한 범위만 변경됐는지 확인합니다.
- `MainUICanvas.prefab`이 대규모 재직렬화되었다면 변경을 커밋하기 전에 원인을 확인합니다.
- `ND-review`나 테스트 결과 파일을 배포 대상으로 사용하지 않습니다.
