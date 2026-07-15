/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - WorldMapTest 씬에서 Play 시 예시 월드맵 계층을 런타임으로 구성한다.
 * - Editor 메뉴 빌더와 동일한 ID/계층 계약을 사용한다.
 *
 * Important Notes
 * - Framework Shared ID: basecamp, dummytown, dummyroute
 * - demo_spline_route / demo_waypoint는 시각 데모용이며 Shared에 없을 수 있다.
 */
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// WorldMapTest 씬 진입 시 예시 맵을 조립한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class WorldMapTestEntry : MonoBehaviour
    {
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private bool built;

        private void Awake()
        {
            if (buildOnAwake && !built)
            {
                BuildExample();
            }
        }

        [ContextMenu("Build Example World Map")]
        public void BuildExample()
        {
            if (built && transform.Find("WorldMapRoot") != null)
            {
                return;
            }

            // 기존 루트가 있으면 제거 후 재구성한다.
            var existing = transform.Find("WorldMapRoot");
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            var root = new GameObject("WorldMapRoot");
            root.transform.SetParent(transform, false);

            EnsureCamera();

            var background = CreateChild(root.transform, "Background");
            var bgVisual = CreateChild(background.transform, "WorldMapBackground");
            var bgRenderer = bgVisual.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = CreateRuntimeSprite(new Color(0.25f, 0.4f, 0.28f, 1f));
            bgRenderer.sortingOrder = -20;
            bgVisual.transform.localScale = new Vector3(18f, 12f, 1f);

            var routeLayer = CreateChild(root.transform, "RouteLayer");
            var townLayer = CreateChild(root.transform, "TownLayer");
            var caravanLayer = CreateChild(root.transform, "CaravanLayer");
            var uiLayer = CreateChild(root.transform, "WorldUiLayer");

            var townBase = CreateTown(townLayer.transform, "Town_BaseCamp", "basecamp", new Vector3(-4.5f, -1.5f, 0f));
            var townDummy = CreateTown(townLayer.transform, "Town_Dummy", "dummytown", new Vector3(4.5f, 1.5f, 0f));
            var townWaypoint = CreateTown(townLayer.transform, "Town_Waypoint", "demo_waypoint", new Vector3(0f, 3.2f, 0f));

            var straight = CreateStraightRoute(
                routeLayer.transform,
                "Route_DummyStraight",
                "dummyroute",
                townBase.transform.position,
                townDummy.transform.position);

            var spline = CreateSplineRoute(
                routeLayer.transform,
                "Route_DemoSpline",
                "demo_spline_route",
                townBase.transform.position,
                townWaypoint.transform.position,
                townDummy.transform.position);

            var markerGo = CreateChild(caravanLayer.transform, "ActiveCaravanMarker");
            var markerRenderer = markerGo.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = CreateRuntimeSprite(new Color(0.95f, 0.35f, 0.2f, 1f));
            markerRenderer.sortingOrder = 20;
            markerGo.transform.localScale = Vector3.one * 0.55f;
            var marker = markerGo.AddComponent<CaravanMapMarker>();
            marker.Configure(markerRenderer, true);
            markerGo.SetActive(false);

            var canvasGo = CreateChild(uiLayer.transform, "WorldUiCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var progress = CreateUiText(canvasGo.transform, "ProgressPercentLabel", new Vector2(20f, -20f), "Progress --");
            var risk = CreateUiText(canvasGo.transform, "RiskLabel", new Vector2(20f, -60f), "Risk --");

            var presenter = root.AddComponent<WorldMapPresenter>();
            presenter.Configure(
                new[] { townBase, townDummy, townWaypoint },
                new[] { straight, spline },
                marker,
                progress,
                risk);

            built = true;
        }

        private static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }

            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.18f, 0.22f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static TownWorldView CreateTown(Transform parent, string name, string townId, Vector3 position)
        {
            var go = CreateChild(parent, name);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateRuntimeSprite(Color.white);
            renderer.sortingOrder = 10;
            go.transform.localScale = Vector3.one * 0.7f;
            go.AddComponent<CircleCollider2D>().radius = 0.6f;

            var ring = CreateChild(go.transform, "SelectionRing");
            var ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = CreateRuntimeSprite(new Color(1f, 0.85f, 0.2f, 0.35f));
            ringRenderer.sortingOrder = 9;
            ring.transform.localScale = Vector3.one * 1.6f;
            ring.SetActive(false);

            var view = go.AddComponent<TownWorldView>();
            view.Configure(townId, renderer, ring.transform);
            return view;
        }

        private static RouteVisual CreateStraightRoute(
            Transform parent,
            string name,
            string routeId,
            Vector3 start,
            Vector3 end)
        {
            var go = CreateChild(parent, name);
            var startPoint = CreateChild(go.transform, "StartPoint");
            startPoint.transform.position = start;
            var endPoint = CreateChild(go.transform, "EndPoint");
            endPoint.transform.position = end;
            var line = go.AddComponent<LineRenderer>();
            ConfigureLine(line);
            var visual = go.AddComponent<RouteVisual>();
            visual.Configure(routeId, RoutePathType.Straight, startPoint.transform, endPoint.transform, null, line);
            return visual;
        }

        private static RouteVisual CreateSplineRoute(
            Transform parent,
            string name,
            string routeId,
            Vector3 a,
            Vector3 b,
            Vector3 c)
        {
            var go = CreateChild(parent, name);
            var container = go.AddComponent<SplineContainer>();
            var spline = container.Spline;
            spline.Clear();
            spline.Add(new BezierKnot(new float3(a.x, a.y, a.z)));
            spline.Add(new BezierKnot(new float3(b.x, b.y + 0.5f, b.z)));
            spline.Add(new BezierKnot(new float3(c.x, c.y, c.z)));
            spline.SetTangentMode(0, TangentMode.AutoSmooth);
            spline.SetTangentMode(1, TangentMode.AutoSmooth);
            spline.SetTangentMode(2, TangentMode.AutoSmooth);

            var startPoint = CreateChild(go.transform, "StartPoint");
            startPoint.transform.position = a;
            var endPoint = CreateChild(go.transform, "EndPoint");
            endPoint.transform.position = c;
            var line = go.AddComponent<LineRenderer>();
            ConfigureLine(line);
            var visual = go.AddComponent<RouteVisual>();
            visual.Configure(routeId, RoutePathType.Spline, startPoint.transform, endPoint.transform, container, line);
            return visual;
        }

        private static void ConfigureLine(LineRenderer line)
        {
            line.widthMultiplier = 0.08f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.sharedMaterial = new Material(shader);
            }

            line.startColor = new Color(0.55f, 0.45f, 0.3f, 1f);
            line.endColor = line.startColor;
        }

        private static TextMeshProUGUI CreateUiText(Transform parent, string name, Vector2 anchoredPos, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 28f;
            tmp.color = Color.white;
            var rect = tmp.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(480f, 40f);
            return tmp;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite CreateRuntimeSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color[64];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            return Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
        }
    }
}
