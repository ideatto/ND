# PlayerMainManager — 플레이어 데이터 메인 매니저 (진행 중)

- **작성**: 윤호영 (Core Gameplay) / 2026-07-15
- **브랜치**: `feature/Core/PlayerMainManager`
- **목적**: 각 시스템이 SaveData를 직접 참조하던 것을, 이 매니저를 창구로 거치게 바꿔 **진실을 한 곳으로** 모은다.

---

## 1. 배경 (왜 만드나)

- 지금까지 메인 매니저가 없어 여러 시스템이 **SaveData(Framework 소유)를 직접 참조**하고 있었다.
- 앞으로는 **PlayerMainManager**를 만들어 이쪽을 창구로 삼고, 다른 코드가 SaveData 대신 매니저를 참조하도록 점진 전환한다.
- 최종적으로 매니저가 "진실"이 되고, SaveData는 **저장 포맷** 역할로 남는다.

## 1.5 매니저 구조 (B: 메인이 총괄 창구) — 확정

- **원칙**: 데이터의 관리 주체는 **항상 도메인 매니저**. SaveData는 저장 포맷일 뿐 관리 주체가 아니다.
- **Core(윤호영)가 소유하는 플레이어측 매니저 2개**:
  - `PlayerMainManager` — 자산(소지금·거점 인벤토리·마차 화물)
  - `VillageBuildingRegistry` — 마을·건물 (= 마을 매니저)
  - (상단 구성=이종현님 무역준비 / 무역 진행=천성욱님 소유 → Core는 안 만듦)
- **B 구조**: `PlayerMainManager`가 **메인 창구**가 되고, 마을 매니저를 **참조로 노출**한다.
  - 다른 코드는 `PlayerMainManager.Instance.Village.AddOrUpgrade(i)` 처럼 **한 문**으로 접근.
  - **소유·로직은 마을 매니저에 그대로.** 메인은 소유하지 않음(god-object 회피).
  - 마을 매니저는 마을 씬에 속해 씬과 함께 생겼다 사라지므로, 메인은 **들고 있지 않고 호출 시점에 조회**(`Village => VillageBuildingRegistry.Instance`, 없으면 null).
- **검증**: `m.Village == VillageBuildingRegistry.Instance`(동일 참조, 복제 없음), 메인 창구로 상점 Lv.1→Lv.3 업그레이드 확인.

## 2. 소유·경계 원칙

| 데이터 | 진실 위치 | 이유 |
|---|---|---|
| 소지금(Gold) | **SaveData** `player.tradingCurrency` 참조 | 이미 SaveData에 있음. 매니저는 감싸기만(진실 1개) |
| 마차 화물 | **SaveData** `caravan.cargo` 참조 | 위와 동일. 무역/정산 시스템이 평소 채움 |
| 거점 인벤토리 | **매니저가 직접 소유** | SaveData에 아직 없음 → 처음부터 매니저가 진실. 저장은 추후 SaveData 편입 |

- **경계**: SaveData·TradeItemData 등 공용 데이터 정의는 **이종현님(UI & Data)** 소유. Core는 이 타입들을 **소비만** 하고 정의를 바꾸지 않는다.
- 매니저가 참조하는 건 파일이 아니라 `FrameworkRoot.CurrentSaveData`(런타임 메모리 객체). 소지금·마차는 그 객체를 감싼다.

## 3. 아이템 모델 통일 (안 A)

- 거점 창고 아이템과 마차 화물은 **서로 오가는 같은 물건** → 표현을 통일.
- 둘 다 **`ND.Framework.CargoEntrySaveData`**(= `TradeItemSaveData item` + `int quantity`)를 그대로 사용.
- `TradeItemSaveData`: `itemId` + `itemName` + `weight` + `basePrice` + `maxCount` (저장용 최소 DTO).
- **SO(`TradeItemData`) → DTO(`TradeItemSaveData`) 변환은 데이터 소유자(이종현님) 매퍼 몫.** Core는 DTO만 받는다.
- 효과: 창고 ↔ 마차 이동이 타입 변환 없이 `RemoveItem` → `AddCargo` 두 줄로 끝남.

## 4. 현재 공개 API

```
// 소지금 (SaveData 참조)
long Gold { get; }
void AddGold(long amount)
bool SpendGold(long amount)        // 부족하면 false
event Action<long> OnGoldChanged

// 거점 인벤토리 (매니저 소유)
IReadOnlyList<CargoEntrySaveData> HomeInventory { get; }
void AddItem(TradeItemSaveData item, int count)   // 같은 id면 합산
bool RemoveItem(string itemId, int count)          // 부족하면 false
int  GetItemCount(string itemId)
event Action OnHomeInventoryChanged

// 마차 화물 (SaveData.caravan.cargo 참조)
IReadOnlyList<CargoEntrySaveData> CaravanCargo { get; }
void AddCargo(TradeItemSaveData item, int count)
bool RemoveCargo(string itemId, int count)
int  GetCargoCount(string itemId)
event Action OnCaravanCargoChanged

// 도메인 매니저 창구 (B: 메인이 총괄)
VillageBuildingRegistry Village { get; }   // 마을 매니저 참조(씬 미로드면 null)
```

- 싱글톤(`Instance`) + `DontDestroyOnLoad`.
- `SaveData` 타입은 **`ND.Framework.SaveData` 풀네임** 사용 (이종현님 전역 `SaveData`와 이름 충돌 → alias·using 불가).
- `SanitizeHomeInventory()`: Awake에서 직렬화로 딸려온 무효 항목(빈 id·수량 0) 제거.

## 5. 파일

- `Assets/_Project/01.Core/08_Player/YHY/PlayerMainManager.cs` — 매니저 본체
- `Assets/_Project/01.Core/08_Player/YHY/PlayerMainManagerDebugHUD.cs` — **임시** 확인용 OnGUI HUD (정식 UI 붙으면 삭제)
- 테스트 씬: `07.Scenes/04_InGame/InGameMainManagerTest.unity` (`PlayerMainManager_DEBUG` 오브젝트에 매니저+HUD)

## 6. 검증 (Play, 리플렉션/HUD)

- 소지금: 1000 → +500 = 1500, -300 = 1200, -99999 = **False**(부족 거부) 유지
- 거점 인벤토리: apple 7+3=10 → -4=6 → -999 **False** 유지
- 마차/이동: 창고 5 + 마차 2 → 창고→마차 3개 이동 → 창고 2 / 마차 5

## 7. 다음 (TODO)

- [ ] **소지금 실연결**: 재화 라인 HUD(이종현님 `CurrencyHudPresenter`)를 `PlayerMainManager.Gold`에 바인딩 (지금 "0 G"로 나옴)
- [ ] **마을 건물 데이터**: `VillageBuildingRegistry`를 매니저로 연결
- [ ] **거점 인벤토리 저장**: SaveData에 필드 추가 협의 (Framework/이종현님)
- [ ] **SO→DTO 매퍼 확인**: `TradeItemData` → `TradeItemSaveData` 변환기 존재 여부 (이종현님)
- [ ] 무역·정산 등 다른 코드가 SaveData 대신 매니저를 참조하도록 점진 전환

## 8. 팀 확인 포인트

- 거점 인벤토리를 마차 화물과 **같은 `CargoEntrySaveData`로 통일**함(안 A) — 이종현님 데이터 모델과 맞물림. 별도 창고 모델이 필요하면 알려주세요.
- 마차 화물은 평소 무역 준비/정산 시스템이 채움. 매니저의 `AddCargo`/`RemoveCargo`는 전환기용 창구이며, 최종 소유권은 협의 필요.
