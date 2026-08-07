# 판매 가격 보정 Buff Tooltip UI

작성일: 2026-08-07 / 담당: 윤호영 / 브랜치: `feature/ui/sell-price-modifier-buff-tooltips`

## 배경

인게임 판매 화면에서 플레이어가 **현재 적용 중인 판매 가격 보정**을 바로 보고 싶었다.

대상 보정은 세 가지:

| Buff | 의미 | 데이터 출처 |
|---|---|---|
| Season | 계절 카테고리/개별 무역품 판매 보정 | `SellPriceModifierPolicy` + `TradeItemData` |
| Distance | 거리 구간별 판매 보정 | `SellPriceModifierPolicy.DistanceRules` |
| Lucky | 행운(낙뢰) 판매 보정 + 적용 중 캐러밴 | Policy + SaveData + `WeatherLuckyMoneyStateReader` |

기존 경제/날씨 계산 코드는 건드리지 않고, **읽기 전용 UI 레이어**로 Tooltip을 올리는 것이 목표다.

## 목표 동작

- Hover → **Normal Tooltip** (요약)
- Click → **Detail Tooltip** (상세, 같은 SharedTooltip에서 토글)
- Pointer Exit → Tooltip 닫힘
- 동시에 열리는 Tooltip은 **하나**(SharedTooltip)
- 배치 정책: **오른쪽 우선**, 공간 부족 시 왼쪽
- Tooltip이 **Root Canvas 밖으로 나가지 않도록** clamp

## 구현

### Runtime (`Assets/_Project/05.UI/04_InGame/YHY/Scripts/SellPriceModifierBuff/`)

- `SellPriceModifierBuffBarController`
  - Season / Distance / Lucky 뷰 + SharedTooltip 소유
  - Policy·TradeItem·시즌 스프라이트 참조
  - `FrameworkEvents.SeasonChanged` 구독 → 시즌 변경 시 Tooltip 닫고 뷰 갱신
  - Open / Toggle / Close로 단일 active view 관리
- `SellPriceModifierBuffView`
  - 아이콘별 Pointer Enter / Click / Exit
  - Enter 시 ViewData 빌드 후 Bar에 Open 요청
- `SellPriceModifierBuffViewData` + Builder
  - `SeasonBuffViewDataBuilder` — 현재 시즌 enabled·non-zero 카테고리 / 개별 아이템 행
  - `DistanceBuffViewDataBuilder` — 거리 구간 규칙 목록
  - `LuckyMoneyBuffViewDataBuilder` — 행운 설명 + 활성 캐러밴 이름(중복 제거, displayName 없으면 id)
  - `SellPriceModifierValueFormatter` — Percent/Add/Multiply 표기
- `SellPriceModifierBuffTooltipController`
  - Closed / Normal / Detail 상태
  - 위치: 앵커 옆 배치 → world corners로 Bounds 초과량 계산 → X/Y 보정
  - Bounds 해석 순서:
    1. Explicit `bounds` 필드
    2. 없으면 `Canvas.rootCanvas` RectTransform
    3. Canvas도 없으면 parent RectTransform fallback
  - Prefab Mode(부모 Canvas 없음)에서도 exception 없이 안전 fallback

### Editor / Prefab

- `SellPriceModifierBuffPrefabBuilder`
  - 메뉴: `Tools/ND/Sell Price Modifier Buff/Rebuild Prefab`
  - Prefab만 재생성, Scene 미수정
- Prefab: `Assets/_Project/08.Prefabs/UI/SellPriceModifierBuff/SellPriceModifierBuffBar.prefab`
  - SeasonBuff / DistanceBuff / LuckyMoneyBuff + SharedTooltip
  - `bounds = null` 유지 → Runtime에서 Root Canvas 자동 탐색

### Tests

- `SellPriceModifierBuffBuilderTests` (Edit Mode) — **6/6 PASS**
  - 시즌 카테고리 필터
  - Lucky percent 포맷
  - 활성 캐러밴 unique/id fallback
  - Distance rule 순서·enabled 필터 등

## Tooltip Bounds 수정 (재검증 핵심)

기존 문제:

```text
bounds == null
→ SharedTooltip parent(작은 BuffBar)를 bounds로 사용
→ 화면 모서리에서 잘못된 clamp
```

수정:

```text
Explicit bounds → 없으면 Root Canvas → 없으면 parent fallback
초기 좌/우 배치 → Tooltip/Bounds world corners 비교 → X/Y 보정
```

Cursor 재검증 결과 (2026-08-07):

- 1920×1080 중앙/좌/우/상/하/네 모서리 Normal·Detail: **전부 Canvas 내부**
- 1600×900 네 모서리: PASS
- Normal ↔ Detail 크기 변경 후 재 clamp: PASS
- Buff 전환 / Exit→Re-enter: PASS
- Prefab Mode·Missing Canvas: exception 없음
- Protected files (`InGame.unity`, `MainUICanvas`, Policy asset, Economy, SaveData): dirty 없음
- 판정: **CONDITIONAL PASS** (Bounds는 완료, 실마우스 flicker·Scene wiring은 미검증)

## 보호한 것 / 의도적으로 안 한 것

건드리지 않음:

- `InGame.unity`
- `MainUICanvas.prefab`
- `SellPriceModifierPolicy_Default.asset`
- Economy production / SaveData / WeatherLucky lifecycle / TradeProgressCoordinator

이유: Scene·공용 Prefab·경제 데이터는 권한/머지 충돌 위험이 커서, Buff Prefab·UI 스크립트만으로 기능을 닫았다.

## 남은 것 (후속)

1. **InGame Scene wiring**
   - MarketTradePanel(또는 판매 UI) 아래에 BuffBar Prefab 배치
   - Policy / TradeItems / 시즌·거리·행운 스프라이트 연결
2. **실제 판매가 ↔ Tooltip 내용 일치 검증**
   - 표시 문구가 최종 판매 계산과 같은지 런타임 확인
3. **실마우스 flicker 확인**
   - SharedTooltip Image의 `raycastTarget = true` 상태
   - 아이콘 경계에서 Enter/Exit 반복이 있으면 raycast 끄기 검토
4. **커밋 / PR**
   - 아직 untracked 상태. base는 `dev2`로 PR 예정

## 파일 목록

```text
Assets/_Project/05.UI/04_InGame/YHY/Scripts/SellPriceModifierBuff/
  SellPriceModifierBuffBarController.cs
  SellPriceModifierBuffView.cs
  SellPriceModifierBuffViewData.cs
  SellPriceModifierBuffTooltipController.cs

Assets/_Project/05.UI/04_InGame/YHY/Editor/
  SellPriceModifierBuffPrefabBuilder.cs
  SellPriceModifierBuffBuilderTests.cs

Assets/_Project/08.Prefabs/UI/SellPriceModifierBuff/
  SellPriceModifierBuffBar.prefab
```

## 한 줄 요약

판매 보정(시즌/거리/행운)을 Hover·Click Tooltip으로 보여주는 BuffBar UI를 추가했고, Tooltip이 Root Canvas 밖으로 나가지 않도록 bounds clamp를 고쳤다. Scene 연동과 실판매가 일치는 다음 단계.
