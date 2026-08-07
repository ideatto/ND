# Transport Inventory PlayMode 문제 확인 및 권장 해결 방식

## 목적

2026-08-06 InGame PlayMode 시연 중 확인된 다음 두 문제의 원인과 권장 해결 방식을 기록한다.

1. 적재 인벤토리에서 돌과 통나무의 아이콘이 표시되지 않는 문제
2. 목장 동물 탭에서 잠금 영역 방향으로 스크롤이 더 내려가지 않는 문제

이 문서는 원인 조사 결과와 후속 수정 기준을 정리한 문서다. 조사 당시에는 PlayMode 상태와 프로젝트 파일을 수정하지 않았다.

## 2026-08-07 적용 상태

- InGame `CaravanSettingRuntimeBridge.tradeItemAssets`에 `Logs`, `Stone` 표시 에셋 연결 완료
- 여섯 상품 Apple, Wheat, Cloth, Stover, Logs, Stone의 아이콘 참조 확인 완료
- 동물 패널 Content 높이 계산을 `data.Slots.Count` 기준으로 적용 완료
- 높이 변경 직후 레이아웃과 Canvas를 강제 갱신하여 ScrollRect 입력 경계 캐시도 같은 프레임에 갱신
- `TransportInventoryPopup`을 활성 `MainUICanvas` 아래로 이동하고 시작 상태를 비활성화함
- 좌하단에 `TransportInventoryRewardDebugButton`을 배치함. `MainUICanvas/InfoPanel` 바로 다음 sibling, Anchor/Pivot `(0,0)`, Position `(20,20)`으로 두어 팝업과 알림보다 아래에서 렌더링한다.
- 지급 ID는 실제 콘텐츠 ID인 `Wagon_M`, `Wagon_S`, `Horse`를 사용함
- 목장 행 클릭, Popup 활성화, 마차 2개와 말 2마리 지급을 PlayMode Smoke Test에 포함함
- Scene 계약 테스트 2건 통과, PlayMode Smoke Test 실행 성공

---

## 확인된 잔존 문제 — 파괴 마차 정리

- 내구도 0이 된 마차의 `instanceId`가 `player.wagonInventory`와 `caravan.wagon` 양쪽에 남아 장착 중으로 표시될 수 있다.
- 파괴 처리에서 화물 수량만 0으로 만들고 `caravan.cargo`의 항목 자체를 제거하지 않아, 화면에는 빈 적재함으로 보여도 구성 변경 검증은 이를 잘못된 기존 화물로 판단한다.
- 그 결과 다른 마차로 교체할 때 `CargoCapacityExceeded`가 반환되고 “현재 적재 화물을 수용할 수 없다”는 오해 소지가 있는 안내가 표시된다.
- 기대 계약: 파괴 결과가 정산·저장된 시점에 해당 마차를 소유 인벤토리에서 제거하고 `caravan.wagon` 장착을 해제하며, 수량 0 화물 항목을 제거한다. 재시도·롤백 안정성을 위해 Journey 도중이 아니라 파괴 결과의 영속 반영 지점에서 원자적으로 처리한다.

### 2026-08-07 적용

- 실패 정산 Claim의 저장 snapshot 범위 안에서 `FailedTradeTransportLoss.Apply()`를 실행한다.
- 해당 Caravan의 장착 마차와 동물을 각 소유 인벤토리에서 제거한다.
- runtime `wagon` 참조와 `animals`, `cargo`, 식량, 현재 내구도를 비운 뒤 SaveData에 복사한다.
- 저장 실패 시 기존 Claim snapshot 복구 경로가 소유 인벤토리와 Caravan 구성을 함께 되돌린다.
- 성공 무역에는 적용하지 않으며, 반복 적용에도 다른 소유 개체가 삭제되지 않도록 검증한다.
- 손실 적용 후 `runFatalReason`과 `runWagonDestroyed`를 소비하여 새로 장착한 운송 수단이 구버전 보정에 다시 삭제되지 않게 한다.
- S9 Claim 성공 시 다음 실패 Pending 표시를 보류한다. 실패 Popup 확인 후에만 다음 Caravan S8로 진행하며, 마지막이면 Town 화면을 유지한다.

---

## 1. 돌·통나무 적재 슬롯 아이콘 미표시

### 현상

- 적재 인벤토리에 돌과 통나무가 각각 1개씩 들어 있다.
- 슬롯의 수량은 `1`로 표시되지만 아이콘과 품목을 식별할 시각 정보가 표시되지 않는다.
- 슬롯 영역은 어두운 빈 칸처럼 보인다.

### 확인 결과

저장 및 적재 데이터 자체는 존재한다.

| itemId | displayName | quantity | unitWeight | icon |
|---|---:|---:|---:|---|
| `Stone` | 돌 | 1 | 5 | `null` |
| `Logs` | 통나무 | 1 | 5 | `null` |

InGame의 Caravan RuntimeBridge에 연결된 `tradeItemAssets`에는 현재 다음 상품만 존재한다.

- Apple
- Wheat
- Cloth
- Stover

`Stone`과 `Logs`에 대응하는 `TradeItemData` 에셋이 목록에 없으므로, 적재 데이터를 UI ViewData로 변환하는 과정에서 아이콘을 찾지 못한다. 수량과 중량은 저장 데이터에서 전달되지만 표시 에셋이 없어서 아이콘만 `null`이 된다.

### 원인

슬롯 렌더링이나 적재 저장 실패가 아니라, `itemId`를 `TradeItemData`로 변환하기 위한 런타임 카탈로그 참조가 불완전한 것이 원인이다.

### 권장 최소 해결 방식

1. 다음 `TradeItemData` 에셋을 사용한다.
   - `Assets/_Project/02.Data/01_ScriptableObjects/TradeItem/TradeItem_Stone.asset`
   - `Assets/_Project/02.Data/01_ScriptableObjects/TradeItem/TradeItem_Logs.asset`
2. InGame에서 사용하는 Caravan RuntimeBridge의 `tradeItemAssets`에 해당 에셋을 직렬화 참조로 추가한다.
3. 기존과 동일하게 `itemId`를 기준으로 에셋을 조회하고, 이름이나 배열 순서로 대체 식별하지 않는다.
4. 에셋 누락 시 조용히 빈 아이콘을 표시하는 대신, `itemId`가 포함된 경고를 한 번 남기는 방식을 권장한다.

상품 구매·재화 차감·적재 저장 로직은 이 문제의 수정 범위가 아니다.

### 수정 후 검증

- 기존 저장에 들어 있는 `Stone`, `Logs`가 재접속 후에도 정상 표시되는지 확인한다.
- 슬롯 수량이 각각 기존 값과 동일한지 확인한다.
- 아이콘 보완 전후에 적재 중량과 구매 금액이 변하지 않는지 확인한다.
- 등록되지 않은 임의의 itemId가 들어오면 예외로 UI 전체가 중단되지 않고 경고가 남는지 확인한다.

---

## 2. 동물 탭 스크롤 하단 이동 제한

### 현상

- 목장 Lv.1의 동물 탭에서 해금된 20칸이 5열 × 4행으로 표시된다.
- 아래쪽 잠금 영역이 존재해야 하지만, 스크롤이 해금 슬롯 영역의 끝보다 아래로 충분히 이동하지 않는다.
- 화면에는 첫 번째 행 일부와 나머지 해금 행만 보이는 위치까지 이동하지만 잠금 영역을 계속 확인할 수 없다.

### 런타임 확인 결과

동물 패널의 주요 값은 다음과 같다.

| 항목 | 확인 값 |
|---|---:|
| 해금 슬롯 | 20 |
| 최대 슬롯 | 100 |
| 열 수 | 5 |
| 셀 크기 | 112 × 112 |
| 셀 간격 | 24 × 18 |
| Viewport 높이 | 302 |
| 20칸 기준 Content 높이 | 538 |
| 100칸 기준 Content 높이 | 2618 |

`TransportInventoryPanelView.Awake()`는 현재 풀에 존재하는 슬롯 수를 사용해 Content 높이를 먼저 계산한다. 초기 풀에 동물 슬롯 20개가 있으면 높이가 538로 설정된다.

이후 `Render()`에서는 ViewData의 전체 슬롯 수를 사용해 다시 높이를 계산하므로 정상적인 최종 높이는 2618이어야 한다. 실제 PlayMode 조사에서도 상태에 따라 Content 높이가 538이었다가 갱신 후 2618로 바뀌는 것을 확인했다.

잠금 가림막은 다음 상태이므로 스크롤 입력을 직접 차단하는 원인은 아니다.

- Image `raycastTarget = false`
- CanvasGroup `blocksRaycasts = false`
- CanvasGroup `interactable = false`
- LayoutElement `ignoreLayout = true`

또한 매 프레임 스크롤 위치를 초기화하는 `Update()`는 없다. 스크롤을 맨 위로 되돌리는 처리는 팝업 최초 표시 또는 탭 선택 시에만 호출된다.

### 원인

초기 슬롯 풀 개수와 ViewData 전체 슬롯 개수가 서로 다른 시점에 Content 높이 계산 기준으로 사용된다. 그 결과 최초 표시 및 레이아웃 계산 순서에 따라 ScrollRect가 20칸 높이를 스크롤 가능한 범위로 인식할 수 있다.

즉, 비활성 슬롯이나 잠금 가림막이 입력을 막는 문제가 아니라 Content 높이의 초기화 기준과 적용 시점이 일관되지 않은 문제다.

### 권장 최소 해결 방식

1. `Awake()`에서 현재 슬롯 풀 개수를 기준으로 최종 Content 높이를 확정하지 않는다.
2. `Render()`에서 전달받은 `data.Slots.Count`를 스크롤 전체 높이의 단일 기준으로 사용한다.
3. 슬롯 부족분 생성, 슬롯 활성화·비활성화, 잠금 가림막 배치가 끝난 후 Content 높이를 계산한다.
4. 높이 변경 이후 Unity 레이아웃을 반영한 다음 스크롤 위치를 초기화한다.
5. 일반 `Refresh()`에서는 사용자가 보고 있던 스크롤 위치를 유지하고, 팝업 최초 Open 또는 탭 전환 시에만 맨 위로 초기화한다.

슬롯 100개를 모두 GameObject로 미리 생성할 필요는 없다. 현재 합의대로 해금된 슬롯이 부족할 때만 추가 생성하고 재사용하되, 스크롤 전체 높이와 잠금 가림막은 최대 슬롯 수를 기준으로 표현하면 된다.

### 수정 시 주의점

- 잠금 가림막을 GridLayoutGroup의 일반 슬롯으로 포함하지 않는다.
- 가림막이 Pointer/Drag 입력을 소비하지 않도록 현재 Raycast 설정을 유지한다.
- `ContentSizeFitter`와 수동 높이 계산을 동시에 사용하지 않는다.
- 탭 전환이나 일반 갱신 때 슬롯을 제거하고 다시 생성하지 않는다.
- 스크롤 이벤트에서 ViewData 전체를 다시 빌드하지 않는다.

### 수정 후 검증

#### 목장 Lv.1

- 동물 20칸이 5열 × 4행으로 표시된다.
- 20칸 이후의 잠금 영역까지 스크롤할 수 있다.
- 잠금 영역 위에서 마우스 휠과 드래그가 모두 동작한다.
- 최하단에서 과도하게 빈 영역이 노출되지 않는다.

#### 목장 레벨 상승

- Lv.2 이상에서 부족한 슬롯만 추가 생성된다.
- 기존 슬롯 GameObject는 재사용된다.
- 해금 영역과 잠금 가림막의 경계가 새 레벨에 맞게 이동한다.
- 탭을 반복 전환해도 슬롯 수가 누적 증가하지 않는다.

#### 갱신

- 동물 추가·삭제 이벤트로 일반 Refresh가 발생해도 현재 스크롤 위치가 불필요하게 맨 위로 이동하지 않는다.
- 팝업을 새로 열거나 탭을 새로 선택했을 때는 계획대로 맨 위에서 시작한다.

---

## 권장 처리 순서

1. 기능 스크립트 복원과 컴파일을 프리팹·Scene 조립보다 먼저 완료한다.
2. RuntimeBridge의 `tradeItemAssets`에 위 경로의 Stone과 Logs 표시 에셋을 연결한다.
3. Popup을 활성 `MainUICanvas` 아래에 배치하고 Popup 자신의 시작 상태를 비활성화한다.
4. 개발 씬 좌하단에 테스트 지급 버튼을 한 번 배치한다.
5. 기존 저장 데이터로 적재 슬롯의 아이콘·수량·중량을 검증한다.
6. 동물 패널 Content 높이 계산 기준을 `data.Slots.Count`로 통일한다.
7. 높이 변경 직후 `LayoutRebuilder.ForceRebuildLayoutImmediate`와 `Canvas.ForceUpdateCanvases`를 호출한다.
8. 자동 테스트에서 동물 Content 높이가 2600보다 크고 정규화 위치 0으로 하단 이동 가능한지 검사한다.
9. 최초 Open, 탭 전환, 일반 Refresh 각각의 스크롤 위치 정책을 검증한다.
10. Scene 계약 테스트와 목장 클릭·지급 버튼 PlayMode Smoke Test를 실행한다.
