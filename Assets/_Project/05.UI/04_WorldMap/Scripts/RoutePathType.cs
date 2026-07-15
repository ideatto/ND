/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - RouteVisual이 사용하는 경로 표현 방식을 구분한다.
 */
namespace ND.UI.WorldMap
{
    /// <summary>
    /// 월드맵 루트의 시각 경로 타입이다.
    /// </summary>
    public enum RoutePathType
    {
        /// <summary>
        /// 시작점과 끝점을 직선으로 보간한다.
        /// </summary>
        Straight = 0,

        /// <summary>
        /// Unity SplineContainer를 따라 보간한다. 스플라인이 없으면 직선으로 폴백한다.
        /// </summary>
        Spline = 1
    }
}
