/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - 논리 routeId에 대응하는 시각 경로를 직선 또는 Spline으로 표현한다.
 * - LineRenderer로 경로를 그리고 캐러밴 위치 보간에 EvaluatePosition을 제공한다.
 *
 * Main Features
 * - Straight: start/end Transform Lerp.
 * - Spline: SplineContainer 샘플링. 누락 시 직선 폴백.
 * - 기하가 바뀔 때만 LineRenderer를 재빌드한다.
 *
 * Important Notes
 * - 시각 경로 길이는 게임플레이 거리/시간에 영향을 주지 않는다.
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
using UnityEngine;
using UnityEngine.Splines;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// 월드맵 상의 단일 무역로 시각 표현이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RouteVisual : MonoBehaviour
    {
        [Tooltip("Shared RouteData.RouteId와 일치해야 하는 루트 식별자입니다.")]
        [SerializeField] private string routeId;

        [SerializeField] private RoutePathType pathType = RoutePathType.Straight;

        [Header("Straight Path")]
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;

        [Header("Spline Path")]
        [SerializeField] private SplineContainer splineContainer;

        [Header("Rendering")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] [Min(2)] private int lineResolution = 24;
        [SerializeField] private Color defaultColor = new Color(0.55f, 0.45f, 0.3f, 1f);
        [SerializeField] private Color activeColor = new Color(0.95f, 0.75f, 0.2f, 1f);
        [SerializeField] private float lineWidth = 0.08f;

        private bool splineFallbackLogged;
        private bool lineBuilt;

        /// <summary>
        /// Shared / Save와 매칭하는 route ID이다.
        /// </summary>
        public string RouteId => routeId;

        /// <summary>
        /// Inspector 또는 빌더에서 route ID와 경로 타입을 설정한다.
        /// </summary>
        public void Configure(
            string id,
            RoutePathType type,
            Transform start,
            Transform end,
            SplineContainer spline,
            LineRenderer renderer)
        {
            routeId = id ?? string.Empty;
            pathType = type;
            startPoint = start;
            endPoint = end;
            splineContainer = spline;
            lineRenderer = renderer;
            lineBuilt = false;
            splineFallbackLogged = false;
            RebuildLineIfNeeded(force: true);
        }

        private void Awake()
        {
            EnsureLineRenderer();
            RebuildLineIfNeeded(force: true);
        }

        private void OnValidate()
        {
            lineResolution = Mathf.Max(2, lineResolution);
            EnsureLineRenderer();
            RebuildLineIfNeeded(force: true);
        }

        /// <summary>
        /// 진행률(0~1)에 해당하는 월드 좌표를 반환한다.
        /// </summary>
        public Vector3 EvaluatePosition(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (TryEvaluateSpline(progress, out var splinePosition))
            {
                return splinePosition;
            }

            if (startPoint == null || endPoint == null)
            {
                return transform.position;
            }

            return Vector3.Lerp(startPoint.position, endPoint.position, progress);
        }

        /// <summary>
        /// 진행률 근처의 대략적인 진행 방향(탄젠트)을 반환한다.
        /// </summary>
        public Vector3 EvaluateTangent(float progress)
        {
            progress = Mathf.Clamp01(progress);
            const float delta = 0.01f;

            if (pathType == RoutePathType.Spline && splineContainer != null && splineContainer.Spline != null)
            {
                // SplineContainer.EvaluateTangent는 이미 월드 방향 벡터를 반환한다.
                var world = (Vector3)splineContainer.EvaluateTangent(progress);
                if (world.sqrMagnitude > 0.0001f)
                {
                    return world.normalized;
                }
            }

            var from = EvaluatePosition(Mathf.Max(0f, progress - delta));
            var to = EvaluatePosition(Mathf.Min(1f, progress + delta));
            var dir = to - from;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.right;
        }

        /// <summary>
        /// active 여부에 따라 LineRenderer 색을 갱신한다.
        /// </summary>
        public void SetActiveVisual(bool isActive)
        {
            EnsureLineRenderer();
            if (lineRenderer == null)
            {
                return;
            }

            var color = isActive ? activeColor : defaultColor;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }

        /// <summary>
        /// 경로 기하가 바뀌었을 때 LineRenderer를 재샘플링한다.
        /// </summary>
        public void RebuildLineIfNeeded(bool force = false)
        {
            EnsureLineRenderer();
            if (lineRenderer == null)
            {
                return;
            }

            if (!force && lineBuilt)
            {
                return;
            }

            var resolution = Mathf.Max(2, lineResolution);
            lineRenderer.positionCount = resolution;
            lineRenderer.widthMultiplier = lineWidth;
            lineRenderer.useWorldSpace = true;

            for (var i = 0; i < resolution; i++)
            {
                var t = i / (float)(resolution - 1);
                lineRenderer.SetPosition(i, EvaluatePosition(t));
            }

            lineBuilt = true;
        }

        private bool TryEvaluateSpline(float progress, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (pathType != RoutePathType.Spline)
            {
                return false;
            }

            if (splineContainer == null || splineContainer.Spline == null)
            {
                if (!splineFallbackLogged)
                {
                    Debug.LogWarning(
                        $"[WorldMap] RouteVisual '{name}' routeId='{routeId}' spline is missing. Falling back to straight path.",
                        this);
                    splineFallbackLogged = true;
                }

                return false;
            }

            // SplineContainer.EvaluatePosition은 이미 월드 좌표를 반환한다.
            worldPosition = (Vector3)splineContainer.EvaluatePosition(progress);
            return true;
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer == null)
            {
                return;
            }

            if (lineRenderer.sharedMaterial == null)
            {
                lineRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
        }
    }
}
