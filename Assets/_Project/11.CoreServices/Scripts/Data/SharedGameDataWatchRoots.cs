/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 공용 게임 데이터 SO 감시 폴더 경로와 Player/Editor drift 정책을 정의한다.
 *
 * Main Features
 * - ProjectData와 Sandbox Legacy watch root 경로 상수를 제공한다.
 * - Player 빌드에서 ProjectData drift만 InGame 진입을 차단하는 정책을 문서화한다.
 *
 * Important Notes
 * - 폴더 스캔은 Unity Editor AssetDatabase에서만 가능하다.
 * - Player 빌드는 SharedGameDataWatchInventory 스냅샷으로 동일 비교를 수행한다.
 */
namespace ND.Framework
{
    /// <summary>
    /// 공용 데이터 SO를 감시하는 루트 종류이다.
    /// </summary>
    public enum SharedGameDataWatchRootKind
    {
        /// <summary>
        /// 프로젝트 공용 SO 경로. Player에서 카탈로그 미등록 시 진입을 차단한다.
        /// </summary>
        ProjectData = 0,

        /// <summary>
        /// Sandbox 레거시 SO 경로. 미등록 시 경고만 남기고 진입을 차단하지 않는다.
        /// </summary>
        SandboxLegacy = 1
    }

    /// <summary>
    /// 공용 데이터 catalog drift 검사에 사용하는 watch root 경로 상수이다.
    /// </summary>
    public static class SharedGameDataWatchRoots
    {
        public const string ProjectDataRoot = "Assets/_Project/02.Data/01_ScriptableObjects";
        public const string SandboxLegacyRoot = "Assets/99.Sandbox/_LJH/02.SO";

        /// <summary>
        /// 경로가 ProjectData watch root 하위인지 판별한다.
        /// </summary>
        public static bool IsUnderProjectDataRoot(string assetPath)
        {
            return IsUnderRoot(assetPath, ProjectDataRoot);
        }

        /// <summary>
        /// 경로가 Sandbox Legacy watch root 하위인지 판별한다.
        /// </summary>
        public static bool IsUnderSandboxLegacyRoot(string assetPath)
        {
            return IsUnderRoot(assetPath, SandboxLegacyRoot);
        }

        /// <summary>
        /// 경로에 해당하는 watch root 종류를 반환한다. 해당 없으면 false.
        /// </summary>
        public static bool TryResolveWatchRootKind(string assetPath, out SharedGameDataWatchRootKind kind)
        {
            if (IsUnderProjectDataRoot(assetPath))
            {
                kind = SharedGameDataWatchRootKind.ProjectData;
                return true;
            }

            if (IsUnderSandboxLegacyRoot(assetPath))
            {
                kind = SharedGameDataWatchRootKind.SandboxLegacy;
                return true;
            }

            kind = default;
            return false;
        }

        private static bool IsUnderRoot(string assetPath, string root)
        {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(root))
            {
                return false;
            }

            var normalizedPath = assetPath.Replace('\\', '/');
            var normalizedRoot = root.Replace('\\', '/');
            return normalizedPath.StartsWith(normalizedRoot + "/")
                || string.Equals(normalizedPath, normalizedRoot, System.StringComparison.Ordinal);
        }
    }
}
