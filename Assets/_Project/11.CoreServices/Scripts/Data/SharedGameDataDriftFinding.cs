/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - catalog에 등록되지 않은 watch SO 한 건의 검사 결과를 표현한다.
 */
namespace ND.Framework
{
    /// <summary>
    /// SharedGameData catalog drift 검사에서 발견한 미등록 에셋 정보이다.
    /// </summary>
    public sealed class SharedGameDataDriftFinding
    {
        public string AssetGuid;
        public string AssetPath;
        public string TypeName;
        public string DataId;
        public SharedGameDataWatchRootKind WatchRootKind;

        /// <summary>
        /// Player 빌드에서 이 항목이 InGame 진입을 차단해야 하면 true이다.
        /// </summary>
        public bool BlocksPlayerBuild => WatchRootKind == SharedGameDataWatchRootKind.ProjectData;

        /// <summary>
        /// 로그에 사용할 요약 문자열을 만든다.
        /// </summary>
        public string ToLogMessage()
        {
            return
                $"Shared game data asset is not registered in catalog: {AssetPath} " +
                $"({TypeName}, id={DataId}, root={WatchRootKind})";
        }
    }
}
