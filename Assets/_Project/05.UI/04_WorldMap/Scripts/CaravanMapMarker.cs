/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - 활성 무역 진행률에 따라 월드맵 캐러밴 마커 위치를 갱신한다.
 * - Transform 위치는 저장하지 않고 RouteVisual 보간으로 재구성한다.
 *
 * Main Features
 * - SetRoute / SetProgress로 위치·방향을 갱신한다.
 * - 스프라이트는 flipX, 그 외는 forward를 탄젠트에 맞춘다.
 *
 * Important Notes
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
using UnityEngine;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// 활성 무역 캐러밴의 맵 마커이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaravanMapMarker : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool useSpriteFlip = true;
        [SerializeField] private bool alignToTangent = true;

        private RouteVisual activeRoute;

        /// <summary>
        /// 따라갈 RouteVisual을 설정한다. null이면 숨긴다.
        /// </summary>
        public void SetRoute(RouteVisual route)
        {
            activeRoute = route;
            gameObject.SetActive(route != null);
        }

        /// <summary>
        /// progress01(0~1)에 맞춰 월드 위치와 방향을 갱신한다.
        /// </summary>
        public void SetProgress(float progress01)
        {
            if (activeRoute == null)
            {
                return;
            }

            progress01 = Mathf.Clamp01(progress01);
            transform.position = activeRoute.EvaluatePosition(progress01);

            if (!alignToTangent)
            {
                return;
            }

            var tangent = activeRoute.EvaluateTangent(progress01);
            if (tangent.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (useSpriteFlip && spriteRenderer != null)
            {
                spriteRenderer.flipX = tangent.x < 0f;
            }
            else
            {
                // 맵이 XY 평면(orthographic)이라고 가정하고 탄젠트를 forward로 맞춘다.
                transform.right = new Vector3(tangent.x, tangent.y, 0f).normalized;
            }
        }

        /// <summary>
        /// 빌더에서 SpriteRenderer 참조를 주입한다.
        /// </summary>
        public void Configure(SpriteRenderer renderer, bool flipSprite)
        {
            spriteRenderer = renderer;
            useSpriteFlip = flipSprite;
        }
    }
}
