using ND.Framework;
using UnityEngine;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// 프로덕션 계절 상태를 월드맵 배경 스프라이트에 반영한다.
    /// </summary>
    public sealed class WorldMapSeasonBackgroundBinder : MonoBehaviour
    {
        [Tooltip("계절 배경을 표시할 월드맵 SpriteRenderer입니다. 비어 있으면 같은 GameObject에서 찾습니다.")]
        [SerializeField] private SpriteRenderer targetRenderer;

        [Tooltip("봄에 표시할 월드맵 배경 스프라이트입니다.")]
        [SerializeField] private Sprite springSprite;

        [Tooltip("여름에 표시할 월드맵 배경 스프라이트입니다.")]
        [SerializeField] private Sprite summerSprite;

        [Tooltip("가을에 표시할 월드맵 배경 스프라이트입니다.")]
        [SerializeField] private Sprite autumnSprite;

        [Tooltip("겨울에 표시할 월드맵 배경 스프라이트입니다.")]
        [SerializeField] private Sprite winterSprite;

        private bool hasWarnedMissingRenderer;
        private string warnedMissingSpriteSeasonId;

        /// <summary>
        /// 활성화될 때 정적 계절 변경 이벤트를 구독하고 저장된 현재 계절을 즉시 반영한다.
        /// 비활성 상태에서 계절이 바뀌었거나 저장을 복원한 경우에도 최신 배경으로 동기화된다.
        /// </summary>
        private void OnEnable()
        {
            FrameworkEvents.SeasonChanged += HandleSeasonChanged;
            ApplySeason(FrameworkRoot.Instance?.CurrentSaveData?.world?.currentSeasonId);
        }

        /// <summary>
        /// 반복 활성화 및 파괴 이후 중복 알림을 방지하기 위해 정적 이벤트 구독을 해제한다.
        /// </summary>
        private void OnDisable()
        {
            FrameworkEvents.SeasonChanged -= HandleSeasonChanged;
        }

        /// <summary>
        /// 프로덕션 달력 전환이 완료되어 이벤트가 발생하면 전달된 현재 스냅샷의 계절을 반영한다.
        /// </summary>
        private void HandleSeasonChanged(GameCalendarSnapshot previous, GameCalendarSnapshot current)
        {
            ApplySeason(current.SeasonId);
        }

        private void ApplySeason(string seasonId)
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (targetRenderer == null)
            {
                if (!hasWarnedMissingRenderer)
                {
                    Debug.LogWarning(
                        $"[{nameof(WorldMapSeasonBackgroundBinder)}] A SpriteRenderer is required on '{name}'.",
                        this);
                    hasWarnedMissingRenderer = true;
                }

                return;
            }

            Sprite resolvedSprite = ResolveSprite(seasonId);
            if (resolvedSprite == null)
            {
                if (IsKnownSeason(seasonId) && warnedMissingSpriteSeasonId != seasonId)
                {
                    Debug.LogWarning(
                        $"[{nameof(WorldMapSeasonBackgroundBinder)}] No sprite is assigned for season '{seasonId}' on '{name}'. The current sprite will be preserved.",
                        this);
                    warnedMissingSpriteSeasonId = seasonId;
                }

                return;
            }

            warnedMissingSpriteSeasonId = null;
            if (targetRenderer.sprite != resolvedSprite)
            {
                targetRenderer.sprite = resolvedSprite;
            }
        }

        private Sprite ResolveSprite(string seasonId)
        {
            if (seasonId == GameCalendarDate.SpringId) return springSprite;
            if (seasonId == GameCalendarDate.SummerId) return summerSprite;
            if (seasonId == GameCalendarDate.AutumnId) return autumnSprite;
            if (seasonId == GameCalendarDate.WinterId) return winterSprite;
            return null;
        }

        private static bool IsKnownSeason(string seasonId)
        {
            return seasonId == GameCalendarDate.SpringId
                || seasonId == GameCalendarDate.SummerId
                || seasonId == GameCalendarDate.AutumnId
                || seasonId == GameCalendarDate.WinterId;
        }
    }
}
