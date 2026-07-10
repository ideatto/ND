/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Sandbox ScriptableObject 더미 데이터를 Framework 공용 데이터 로더에 전달하는 catalog asset 타입을 정의한다.
 * - 실제 Sandbox asset은 이동하거나 수정하지 않고, Framework feature root의 catalog가 읽기 전용 참조만 보유한다.
 *
 * Main Features
 * - Town, Market, TradeItem, Wagon, DraftAnimal, Route asset 참조 배열을 제공한다.
 * - Resources 경로 상수로 runtime 로더가 catalog asset을 찾을 수 있게 한다.
 *
 * Important Notes
 * - 이 타입은 Sandbox 데이터를 1차 빌드 M0 seed data로 사용하는 임시 연결점이다.
 * - public API에는 Sandbox 원본 타입을 직접 노출하지 않고 SharedGameDataService가 스냅샷으로 변환한다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Framework 공용 데이터 초기화에 사용할 Sandbox ScriptableObject 참조 목록이다.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "ND/Framework/Sandbox Shared Game Data Catalog")]
    public sealed class SandboxSharedGameDataCatalog : ScriptableObject
    {
        public const string ResourceName = "SandboxSharedGameDataCatalog";

        [SerializeField] private global::TownData[] towns;
        [SerializeField] private global::MarketData[] markets;
        [SerializeField] private global::TradeItemData[] tradeItems;
        [SerializeField] private global::WagonData[] wagons;
        [SerializeField] private global::DraftAnimalData[] draftAnimals;
        [SerializeField] private global::RouteData[] routes;

        public global::TownData[] Towns => towns != null ? (global::TownData[])towns.Clone() : new global::TownData[0];

        public global::MarketData[] Markets => markets != null ? (global::MarketData[])markets.Clone() : new global::MarketData[0];

        public global::TradeItemData[] TradeItems => tradeItems != null ? (global::TradeItemData[])tradeItems.Clone() : new global::TradeItemData[0];

        public global::WagonData[] Wagons => wagons != null ? (global::WagonData[])wagons.Clone() : new global::WagonData[0];

        public global::DraftAnimalData[] DraftAnimals => draftAnimals != null ? (global::DraftAnimalData[])draftAnimals.Clone() : new global::DraftAnimalData[0];

        public global::RouteData[] Routes => routes != null ? (global::RouteData[])routes.Clone() : new global::RouteData[0];
    }
}
