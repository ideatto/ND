# Caravan Overview UI Contract

## 1. 목적

본 문서는 멀티 Caravan 화면에서 기존 S3 마차·동물 구성과 S4 상품 적재 기능을 선형 무역 준비 흐름에서 분리하는 목표 구조를 정의한다.

`Caravan Overview`는 최대 Caravan 슬롯을 요약 표시하고, 각 Caravan의 장비 및 다음 출발 적재 계획을 조회·편집하는 시작 화면이다. Overview에서 집중한 Caravan은 편집 대상일 뿐 출발 대상으로 자동 선택되지 않는다. UI는 저장 데이터나 경제 데이터를 직접 변경하지 않으며 Provider가 제공한 ViewData를 표시한다.

---

## 2. 목표 화면 흐름

```text
[Caravan Overview]
  ├─ Caravan Slot
  │   ├─ Wagon Icon     → Caravan Setting (기존 S3 기능)
  │   ├─ Inventory Icon → Load Setting (기존 S4 기능)
  │   └─ Journey State / 요약 정보
  │
  └─ Main HUD Trade Button
      → TradePrepareUI 열기
      → 출발 Caravan 프리셋 선택
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
| `CaravanOverviewViewData` | 전체 슬롯과 설정·적재 패널에서 현재 집중한 `focusedCaravanId`를 제공한다. |
| `CaravanBlockViewData` | 슬롯 상태, Journey 상태와 마차·동물·화물 아이콘 요약을 제공한다. |
| `CaravanSettingViewData` | 기존 S3에 표시할 마차·동물 목록, 현재 선택, 편집 가능 여부를 제공한다. |
| `CaravanLoadSettingViewData` | 기존 S4에 표시할 상품 목록, 출발 예정 적재 계획, 적재량·슬롯·비용 투영값을 제공한다. |
| `TradePrepareCaravanOptionViewData` | TradePrepareUI 안에서 선택할 출발 Caravan과 선택 가능 여부를 제공한다. |
| `TradePrepareViewData` | TradePrepareUI가 이번 출발 대상으로 선택한 `departureCaravanId`와 목적지·경로·용병·출발 요약 정보를 제공한다. |

Overview 블록의 동물·화물 배열은 작은 아이콘이나 수량 배지를 위한 표시 요약이다. S3·S4 상세 패널의 편집 원본으로 사용하지 않는다.

### 3.1 Overview Provider 계약

`ICaravanOverviewViewDataProvider.GetOverview()`는 UI가 `SaveData`나 런타임 `CaravanData`를 직접 조회하지 않도록 완성된 `CaravanOverviewViewData` 스냅샷을 제공한다.

- 반환 ViewData와 모든 배열은 `null`이 아니어야 한다.
- UI가 반환 객체를 변경하더라도 다음 조회 결과가 오염되지 않아야 한다.
- Provider는 슬롯 상태와 설정·적재 패널에서 집중할 ID를 결정한다.
- `TestCaravanOverviewViewDataProvider`는 실제 다중 Caravan 저장 구조가 연결되기 전 UI·SmokeTest에서만 사용하는 임시 구현이다.
- Overview 집중 변경은 UI 표시 상태이며, 생성과 구성 저장만 별도 Framework Command가 담당한다.

---

## 4. 편집 권한

| Caravan 상태 | 장비 패널 | 적재 패널 | 새 무역 준비 |
|---|---|---|---|
| `Prepare` | 조회·편집 | 조회·편집 | 가능 여부를 Provider가 제공 |
| `Traveling` | 조회 전용 | 조회 전용 | 불가 |
| `Settling` | 조회 전용 | 조회 전용 | 불가 |
| 저장 처리 중 | 조회 가능, 입력 차단 | 조회 가능, 입력 차단 | 불가 |

UI가 `JourneyState`만 보고 편집 가능 여부를 다시 계산하지 않는다. Setting/Load Provider가 `canEdit`과 `editBlockedReason`을 제공한다. 출발 선택 가능 여부는 TradePrepare Provider가 `TradePrepareCaravanOptionViewData.canSelect`와 `disabledReason`으로 제공한다.

---

## 5. 화면별 UI 계약표

아래 Command 이름은 UI가 필요로 하는 계약 예시이며 실제 타입명과 반환형은 Framework 합의 후 확정한다.

| 화면 | 입력 ViewData | 사용자 입력 | 요청 계약 예시 | 성공 시 | 실패 시 |
|---|---|---|---|---|---|
| Caravan Overview | `CaravanOverviewViewData` | 슬롯 선택 | `FocusCaravan(caravanId)` UI 상태 갱신 | 선택 강조 및 설정·적재 패널 대상 갱신 | 기존 집중 대상 유지 |
| Caravan Overview | `CaravanOverviewViewData` | 빈 슬롯 선택 | `CreateCaravan(slotIndex)` | 생성된 Caravan에 집중하고 Overview 재조회 | 잠금·생성 실패 사유 표시 |
| Caravan Overview | `CaravanBlockViewData` | 마차 아이콘 클릭 | Caravan Setting ViewData 조회 | Caravan Setting 열기 | 조회 실패 Notice 표시 |
| Caravan Setting | `CaravanSettingViewData` | 마차·동물 구성 저장 | `UpdateCaravanSetting(caravanId, setting)` | 패널 및 Overview 재조회 | 기존 구성 유지, 실패 사유 표시 |
| Caravan Overview | `CaravanBlockViewData` | 인벤토리 아이콘 클릭 | Load ViewData 조회 | Load Setting 열기 | 조회 실패 Notice 표시 |
| Load Setting | `CaravanLoadSettingViewData` | 적재 계획 저장 | `UpdateCaravanLoadSetting(caravanId, plan)` | 패널 및 Overview 재조회 | 기존 계획 유지, 실패 사유 표시 |
| Main HUD | 없음 | 기존 무역 버튼 클릭 | `OpenTradePreparation()` | Caravan 선택 단계의 TradePrepareUI 열기 | 현재 화면 유지, 초기화 실패 표시 |
| TradePrepare Caravan 선택 | `TradePrepareViewData` | 출발 Caravan 선택 | `SelectDepartureCaravan(caravanId)`로 UI Draft 갱신 | 선택 Caravan 요약을 갱신하고 목적지 단계로 이동 | 선택 유지, `disabledReason` 표시 |
| 목적지·경로 | `TradePrepareViewData` | 목적지·경로 선택 | 출발 Draft 갱신 | 용병 화면으로 이동 | 선택 유지, 경로 실패 사유 표시 |
| 용병 고용 | `TradePrepareViewData` | 용병 선택·해제 | 출발 Draft 갱신 | 비용·전투력 ViewData 재조회 | 선택 유지, 재화·고용 실패 표시 |
| 출발 요약 | `TradePrepareViewData` | 출발 버튼 클릭 | `RequestTradeDeparture(tradeDraft)` | 저장 성공 후 Traveling 전환 | 저장·검증 실패 표시, 화면 유지 |
| Traveling | 진행 ViewData | 상세 확인 | 진행 ViewData 조회 | 진행률과 남은 시간 갱신 | 마지막 유효 데이터 유지 |
| Settlement | Settlement ViewData | Claim 클릭 | `ClaimSettlement(caravanId, fullTradeId)` | 저장 성공 후 Overview 복귀 | 중복 Claim·저장 실패 표시 |

---

## 6. 데이터 변경 규칙

- UI는 `caravanId`와 `fullTradeId`를 생성하지 않는다.
- `CaravanOverviewViewData.focusedCaravanId`는 설정·적재 편집 대상이며 출발 Draft로 복사하지 않는다.
- Main HUD의 무역 버튼은 `caravanId` 없이 TradePrepareUI를 연다.
- `TradePrepareViewData.departureCaravanId`는 TradePrepareUI의 출발 Caravan 선택 단계에서만 갱신한다.
- 실제 Caravan 옵션 Provider가 연결되면 출발 Caravan 선택은 필수이며, 연결 전 기존 단일 Caravan 씬은 호환 모드로 동작한다.
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

1. Caravan 기능 제작자가 Overview, Caravan Setting, Load Setting과 TradePrepare Caravan Option Provider를 제공한다.
2. Framework가 `caravanId` 기반 조회·변경 Command를 제공한다.
3. 기존 S3 마차·동물 UI를 `CaravanSettingViewData` 입력 패널로 전환한다.
4. 기존 S4 상품 적재 UI를 `CaravanLoadSettingViewData` 입력 패널로 전환한다.
5. 새 Provider/Command 경로가 검증된 후 `TradePrepareViewData`의 구형 S3·S4 목록 필드를 제거한다.

---

## 8. 아직 외부 제공이 필요한 항목

- 실제 멀티 Caravan 목록과 안정적인 `caravanId`
- Overview의 마지막 집중 Caravan 복구가 필요한지에 대한 UI 정책
- TradePrepareUI에서 사용할 Caravan 옵션과 선택 가능 사유
- Caravan별 장비 및 적재 계획 저장 구조
- 마차·동물·상품 stable content ID와 아이콘 Catalog
- `canEdit` 및 출발 가능 상태 판정
- 자산 중복 배치 차단 규칙
- 출발 시 구매·재고 차감 transaction
- 저장 실패와 rollback 결과를 포함하는 Framework Command 결과
