# Caravan Overview UI Contract

## 1. 목적

본 문서는 멀티 Caravan 화면에서 기존 S3 마차·동물 구성과 S4 상품 적재 기능을 선형 무역 준비 흐름에서 분리하는 목표 구조를 정의한다.

`Caravan Overview`는 최대 Caravan 슬롯을 요약 표시하고, 각 Caravan의 장비 및 다음 출발 적재 계획으로 진입하는 시작 화면이다. UI는 저장 데이터나 경제 데이터를 직접 변경하지 않으며, Provider가 제공한 ViewData를 표시하고 Framework Command에 안정적인 ID를 전달한다.

---

## 2. 목표 화면 흐름

```text
[Caravan Overview]
  ├─ Caravan Slot
  │   ├─ Wagon Icon     → Caravan Setting (기존 S3 기능)
  │   ├─ Inventory Icon → Load Setting (기존 S4 기능)
  │   └─ Journey State / 요약 정보
  │
  └─ Trade Button
      → 출발 Caravan 선택
      → 선택 Caravan 구성·적재 계획 조회
      → 목적지 선택
      → 경로 선택
      → 용병 고용
      → 출발 요약
      → 출발 Command
      → Traveling
      → Settlement
```

장비와 적재 계획은 Caravan에 귀속된다. 목적지, 경로, 용병은 무역 버튼을 누른 뒤 만들어지는 출발 Draft에 귀속된다.

---

## 3. ViewData 역할

| ViewData | 역할 |
|---|---|
| `CaravanOverviewViewData` | 전체 슬롯과 현재 선택된 `caravanId`를 제공한다. |
| `CaravanBlockViewData` | 슬롯 상태, Journey 상태, 마차·동물·화물 아이콘 요약과 무역 준비 가능 여부를 제공한다. |
| `CaravanSettingViewData` | 기존 S3에 표시할 마차·동물 목록, 현재 선택, 편집 가능 여부를 제공한다. |
| `CaravanLoadSettingViewData` | 기존 S4에 표시할 상품 목록, 출발 예정 적재 계획, 적재량·슬롯·비용 투영값을 제공한다. |
| `TradePrepareViewData` | 선택된 Caravan ID를 유지하며 목적지·경로·용병·출발 요약 정보를 제공한다. |

Overview 블록의 동물·화물 배열은 작은 아이콘이나 수량 배지를 위한 표시 요약이다. S3·S4 상세 패널의 편집 원본으로 사용하지 않는다.

---

## 4. 편집 권한

| Caravan 상태 | 장비 패널 | 적재 패널 | 새 무역 준비 |
|---|---|---|---|
| `Prepare` | 조회·편집 | 조회·편집 | 가능 여부를 Provider가 제공 |
| `Traveling` | 조회 전용 | 조회 전용 | 불가 |
| `Settling` | 조회 전용 | 조회 전용 | 불가 |
| 저장 처리 중 | 조회 가능, 입력 차단 | 조회 가능, 입력 차단 | 불가 |

UI가 `JourneyState`만 보고 편집 가능 여부를 다시 계산하지 않는다. Provider가 `canEdit`, `editBlockedReason`, `canBeginTradePreparation`, `tradePreparationBlockedReason`을 제공한다.

---

## 5. 화면별 UI 계약표

아래 Command 이름은 UI가 필요로 하는 계약 예시이며 실제 타입명과 반환형은 Framework 합의 후 확정한다.

| 화면 | 입력 ViewData | 사용자 입력 | 요청 계약 예시 | 성공 시 | 실패 시 |
|---|---|---|---|---|---|
| Caravan Overview | `CaravanOverviewViewData` | 슬롯 선택 | `SelectCaravan(caravanId)` | 선택 강조 및 무역 버튼 대상 갱신 | 기존 선택 유지, Notice 표시 |
| Caravan Overview | `CaravanOverviewViewData` | 빈 슬롯 선택 | `CreateCaravan(slotIndex)` | 생성된 Caravan을 선택하고 Overview 재조회 | 잠금·생성 실패 사유 표시 |
| Caravan Overview | `CaravanBlockViewData` | 마차 아이콘 클릭 | Caravan Setting ViewData 조회 | Caravan Setting 열기 | 조회 실패 Notice 표시 |
| Caravan Setting | `CaravanSettingViewData` | 마차·동물 구성 저장 | `UpdateCaravanSetting(caravanId, setting)` | 패널 및 Overview 재조회 | 기존 구성 유지, 실패 사유 표시 |
| Caravan Overview | `CaravanBlockViewData` | 인벤토리 아이콘 클릭 | Load ViewData 조회 | Load Setting 열기 | 조회 실패 Notice 표시 |
| Load Setting | `CaravanLoadSettingViewData` | 적재 계획 저장 | `UpdateCaravanLoadSetting(caravanId, plan)` | 패널 및 Overview 재조회 | 기존 계획 유지, 실패 사유 표시 |
| Caravan 선택 | `CaravanOverviewViewData` | 무역 대상 확정 | `BeginTradePreparation(caravanId)` | 선택 ID를 유지하고 목적지 화면으로 이동 | 현재 화면 유지, 차단 사유 표시 |
| 목적지·경로 | `TradePrepareViewData` | 목적지·경로 선택 | 출발 Draft 갱신 | 용병 화면으로 이동 | 선택 유지, 경로 실패 사유 표시 |
| 용병 고용 | `TradePrepareViewData` | 용병 선택·해제 | 출발 Draft 갱신 | 비용·전투력 ViewData 재조회 | 선택 유지, 재화·고용 실패 표시 |
| 출발 요약 | `TradePrepareViewData` | 출발 버튼 클릭 | `RequestTradeDeparture(caravanId, tradeDraft)` | 저장 성공 후 Traveling 전환 | 저장·검증 실패 표시, 화면 유지 |
| Traveling | 진행 ViewData | 상세 확인 | 진행 ViewData 조회 | 진행률과 남은 시간 갱신 | 마지막 유효 데이터 유지 |
| Settlement | Settlement ViewData | Claim 클릭 | `ClaimSettlement(caravanId, fullTradeId)` | 저장 성공 후 Overview 복귀 | 중복 Claim·저장 실패 표시 |

---

## 6. 데이터 변경 규칙

- UI는 `caravanId`와 `fullTradeId`를 생성하지 않는다.
- Caravan Setting/Load Setting 패널은 Provider가 준 ViewData를 표시하고 사용자 의도를 Command로 전달한다.
- Load Setting 저장은 다음 출발 계획을 저장하는 동작이다. 그 시점에 플레이어 재화나 상점 재고를 차감하지 않는다.
- 실제 구매·재고 차감·자산 잠금은 출발 Command가 최신 런타임 데이터를 다시 검증한 뒤 처리한다.
- 출발 성공 전에 UI가 Traveling 화면으로 먼저 이동하지 않는다.
- Command 성공 후 UI는 내부 ViewData를 임의 수정하지 않고 Provider에서 최신 값을 다시 조회한다.
- 저장 실패 시 현재 화면과 이전 확정 구성을 유지하고 `SaveResult`에 대응하는 Notice를 표시한다.

---

## 7. 기존 S3·S4 마이그레이션 경계

현재 씬의 S3·S4 패널은 `TradePrepareUIManager`, `TradePrepareUiRuntimeBinding`, 단일 `TradePrepareDraftStore`에 연결되어 있다. 따라서 이번 계약 단계에서는 기존 필드와 바인딩을 즉시 삭제하지 않는다.

후속 연결 순서는 다음과 같다.

1. Caravan 기능 제작자가 Overview, Caravan Setting, Load Setting ViewData Provider를 제공한다.
2. Framework가 `caravanId` 기반 조회·변경 Command를 제공한다.
3. 기존 S3 마차·동물 UI를 `CaravanSettingViewData` 입력 패널로 전환한다.
4. 기존 S4 상품 적재 UI를 `CaravanLoadSettingViewData` 입력 패널로 전환한다.
5. 새 Provider/Command 경로가 검증된 후 `TradePrepareViewData`의 구형 S3·S4 목록 필드를 제거한다.

---

## 8. 아직 외부 제공이 필요한 항목

- 실제 멀티 Caravan 목록과 안정적인 `caravanId`
- 마지막 선택 Caravan 복구 데이터
- Caravan별 장비 및 적재 계획 저장 구조
- 마차·동물·상품 stable content ID와 아이콘 Catalog
- `canEdit` 및 출발 가능 상태 판정
- 자산 중복 배치 차단 규칙
- 출발 시 구매·재고 차감 transaction
- 저장 실패와 rollback 결과를 포함하는 Framework Command 결과
