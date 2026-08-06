using UnityEngine;

namespace ND.UI.Loading
{
    /// <summary>Pure progress mapping shared by the presenter and EditMode tests.</summary>
    public static class LoadingProgress
    {
        public static float MapInGameSceneProgress(float sceneProgress)
        {
            return Mathf.Lerp(0.2f, 0.75f, Sanitize01(sceneProgress));
        }

        public static float MapRequiredAdditiveProgress(float sceneProgress)
        {
            return Mathf.Lerp(0.75f, 0.95f, Sanitize01(sceneProgress));
        }

        /// <summary>기존 호출부를 위한 전체 scene progress mapping이다.</summary>
        public static float MapSceneProgress(float sceneProgress)
        {
            return Mathf.Lerp(0.2f, 1f, Sanitize01(sceneProgress));
        }

        public static float MoveDisplayedProgress(float displayed, float target, float maximumDelta)
        {
            var safeDisplayed = Sanitize01(displayed);
            var safeTarget = Mathf.Max(safeDisplayed, Sanitize01(target));
            var safeDelta = IsFinite(maximumDelta) ? Mathf.Max(0f, maximumDelta) : 0f;
            return Mathf.MoveTowards(safeDisplayed, safeTarget, safeDelta);
        }

        private static float Sanitize01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
