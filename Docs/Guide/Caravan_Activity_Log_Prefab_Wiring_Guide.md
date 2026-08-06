# Caravan Activity Log 상세 패널 연결 가이드

이 문서는 디자인 팀이 수정한 최신 Prefab을 유지하면서 활동 로그 항목 클릭과 상세 패널 전환 기능을 연결하는 방법을 설명한다.

## 주의사항

- `ND/UI/Create Caravan Activity Log Prefabs` 메뉴를 실행하지 않는다.
- 기존 `CaravanActivityLogItem.prefab`과 `CaravanActivityLogPanel.prefab`을 코드 작업본으로 덮어쓰지 않는다.
- 디자인 팀의 최신 Prefab에서 아래 컴포넌트와 Inspector 참조만 연결한다.

## 1. 로그 항목 연결

대상: `CaravanActivityLogItem.prefab`

1. 로그 한 줄의 루트 오브젝트에 `CaravanActivityLogItemView`가 있는지 확인한다.
2. 클릭 영역으로 사용할 자식 오브젝트에 `Image`와 `Button`을 추가한다.
3. `Button.targetGraphic`에 같은 오브젝트의 `Image`를 연결한다.
4. `CaravanActivityLogItemView` Inspector를 다음과 같이 연결한다.
   - `Caravan Icon`: 캐러밴 아이콘 `Image`
   - `Bubble Background`: 말풍선 배경 `Image`
   - `Message Text`: 로그 문구 `TMP_Text`
   - `Click Button`: 2번에서 준비한 `Button`

`Button`의 On Click 목록은 비워 둔다. 런타임에 `CaravanActivityLogItemView`가 리스너를 연결한다.

## 2. 상세 패널 오브젝트 구성

상세 패널은 별도 Prefab을 만들거나 `CaravanActivityLogPanel.prefab`의 자식으로 직접 구성할 수 있다.

1. 상세 화면 루트에 `CaravanActivityLogDetailPanel`을 추가한다.
2. 상세 내용을 나중에 배치할 빈 자식 `RectTransform`을 만든다.
3. 목록으로 돌아갈 `Button`을 만든다.
4. `CaravanActivityLogDetailPanel` Inspector를 연결한다.
   - `Content Root`: 2번의 빈 `RectTransform`
   - `Back Button`: 3번의 `Button`
5. 상세 화면 루트는 초기 상태를 비활성화한다.

현재 `Content Root`에는 확정된 상세 내용이 없으므로 UI를 추가하지 않는다.

## 3. 목록 패널 연결

대상: `CaravanActivityLogPanel.prefab`

`CaravanActivityLogPanel` Inspector의 기존 참조는 유지하고 다음 항목을 연결한다.

- `List Viewport`: 목록을 표시하는 Viewport 오브젝트
- `List Scrollbar`: 목록 세로 Scrollbar 오브젝트
- `Detail Panel`: 2단계에서 구성한 `CaravanActivityLogDetailPanel`

동작 흐름은 다음과 같다.

1. `CaravanActivityLogItem(Clone)`의 `Click Button` 클릭
2. 해당 항목이 선택 로그 데이터를 이벤트로 전달
3. `List Viewport`와 `List Scrollbar` 비활성화
4. 상세 패널 활성화
5. `Back Button` 클릭 시 상세 패널 비활성화 후 목록 복귀

## 4. 수동 검증

1. InGame 씬을 Play Mode로 실행한다.
2. 생성된 `CaravanActivityLogItem(Clone)`의 클릭 영역을 누른다.
3. 목록이 숨겨지고 상세 패널이 열리는지 확인한다.
4. `Back Button`을 눌러 목록으로 돌아오는지 확인한다.
5. Scrollbar가 자동 숨김 상태인 경우에도 상세 화면 전환 후 남아 있지 않은지 확인한다.
