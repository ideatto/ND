/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Framework가 로드한 공용 기준 데이터를 다른 시스템이 조회할 수 있는 계약을 정의한다.
 * - Sandbox ScriptableObject 타입을 외부 API로 직접 노출하지 않고 안정적인 ID 기반 조회를 제공한다.
 *
 * Main Features
 * - 공용 데이터 로드 여부와 데이터 수량 요약을 제공한다.
 * - Town, Market, TradeItem, Wagon, DraftAnimal, Route 정의를 ID로 조회한다.
 *
 * Usage for Team Members
 * - FrameworkEvents.SharedGameDataLoaded 이벤트에서 전달되는 provider를 보관하거나 FrameworkRoot.SharedGameData를 조회한다.
 * - SaveData에는 선택한 ID를 저장하고, 표시와 계산에 필요한 기준값은 이 provider에서 조회한다.
 *
 * Important Notes
 * - 반환되는 정의 객체는 Framework가 만든 읽기 전용 스냅샷으로 취급해야 한다.
 * - Sandbox 데이터 구조가 바뀌어도 이 계약은 가능한 한 유지한다.
 */
using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>
    /// 공용 기준 데이터를 ID 기반으로 조회하는 Framework 계약이다.
    /// </summary>
    public interface ISharedGameDataProvider
    {
        /// <summary>
        /// 공용 데이터가 검증을 통과해 사용 가능한 상태인지 여부이다.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// 데이터 수량과 주요 ID를 포함한 디버그용 요약 문자열이다.
        /// </summary>
        string Summary { get; }

        /// <summary>
        /// 로드된 마을 수이다.
        /// </summary>
        int TownCount { get; }

        /// <summary>
        /// 로드된 시장 수이다.
        /// </summary>
        int MarketCount { get; }

        /// <summary>
        /// 로드된 무역품 수이다.
        /// </summary>
        int TradeItemCount { get; }

        /// <summary>
        /// 로드된 마차 수이다.
        /// </summary>
        int WagonCount { get; }

        /// <summary>
        /// 로드된 견인 동물 수이다.
        /// </summary>
        int DraftAnimalCount { get; }

        /// <summary>
        /// 로드된 무역로 수이다.
        /// </summary>
        int RouteCount { get; }

        IReadOnlyList<string> TownIds { get; }

        IReadOnlyList<string> MarketIds { get; }

        IReadOnlyList<string> TradeItemIds { get; }

        IReadOnlyList<string> WagonIds { get; }

        IReadOnlyList<string> DraftAnimalIds { get; }

        IReadOnlyList<string> RouteIds { get; }

        bool TryGetTown(string id, out SharedTownDefinition town);

        bool TryGetMarket(string id, out SharedMarketDefinition market);

        bool TryGetTradeItem(string id, out SharedTradeItemDefinition tradeItem);

        bool TryGetWagon(string id, out SharedWagonDefinition wagon);

        bool TryGetDraftAnimal(string id, out SharedDraftAnimalDefinition draftAnimal);

        bool TryGetRoute(string id, out SharedRouteDefinition route);
    }
}
