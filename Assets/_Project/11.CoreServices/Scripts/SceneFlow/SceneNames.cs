/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Framework scene flow에서 사용하는 Unity scene 이름 상수를 정의한다.
 * - 문자열 오타로 인한 scene load 실패를 줄인다.
 *
 * Main Features
 * - Boot, Title, Loading, InGame scene 이름을 제공한다.
 *
 * Usage for Team Members
 * - SceneFlowService 또는 scene controller에서 scene 이름이 필요할 때 이 상수를 사용한다.
 *
 * Main Public APIs
 * - Boot, Title, Loading, InGame: Unity Build Settings에 등록된 scene 이름과 일치해야 하는 상수.
 *
 * Important Notes
 * - 상수 값을 변경할 때는 Unity Build Settings와 실제 scene asset 이름을 함께 확인해야 한다.
 */
namespace ND.Framework
{
    /// <summary>
    /// CoreServices scene flow에서 사용하는 scene 이름 상수 모음이다.
    /// </summary>
    public static class SceneNames
    {
        /// <summary>
        /// framework boot scene 이름이다.
        /// </summary>
        public const string Boot = "Boot";

        /// <summary>
        /// title scene 이름이다.
        /// </summary>
        public const string Title = "Title";

        /// <summary>
        /// loading scene 이름이다.
        /// </summary>
        public const string Loading = "Loading";

        /// <summary>
        /// in-game scene 이름이다.
        /// </summary>
        public const string InGame = "InGame";
    }
}
