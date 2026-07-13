// =============================================================================
// TradePrepareDemoData — 무역 준비 데모용 "진짜 SO 데이터 묶음" (Test 씬 전용)
// =============================================================================
// [담당] Core Gameplay (윤호영)  /  [용도] 데모가 쓸 실제 SO 에셋들을 한 곳에 모은 참조 묶음.
//
// [역할] 이종현님의 진짜 SO 타입(TownData/WagonData/DraftAnimalData/TradeItemData)으로 만든
//        더미 에셋들을 배열로 담는다. 데모 드라이버는 이 SO들을 패널 DTO로 변환(어댑터)해 쓴다.
//        · 루트(RouteData)는 TownData.AvailableRoutes로 접근하므로 별도 배열 없음.
//        · 이 자체는 참조 묶음일 뿐 — 값은 각 SO 에셋 안에 있다.
//
// [주의] 여기 담기는 에셋들은 "진짜 타입 · 더미 값"이다. 실제 밸런스 데이터로 교체 예정.
// =============================================================================

using UnityEngine;

/// <summary>데모가 쓸 실제 SO 더미 에셋들의 참조 묶음. [Test 씬 전용]</summary>
[CreateAssetMenu(fileName = "TradePrepareDemoData", menuName = "ND/Demo/TradePrepareDemoData")]
public class TradePrepareDemoData : ScriptableObject
{
    [Header("① 거점 도시들 (루트는 각 TownData.AvailableRoutes)")]
    public TownData[] towns;

    [Header("② 이동수단 (WagonData: None / WagonWithAnimals / Mount)")]
    public WagonData[] transports;

    [Header("② 이동수단 소지 개수 (transports와 같은 순서, 0=미소지 빈 슬롯. 도보는 무시)")]
    public int[] transportOwned;

    [Header("③ 동물 (DraftAnimalData)")]
    public DraftAnimalData[] animals;

    [Header("④ 아이템 (TradeItemData)")]
    public TradeItemData[] items;
}
