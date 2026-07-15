/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - WorldMapTest.unity에 Phase 1+Spline 예시 계층을 생성하고 저장한다.
 * - 이번 작업에서 허용된 테스트 씬만 수정한다.
 * - 테스트 씬 EventSystem은 Input System UI Input Module을 사용한다 (StandaloneInputModule 금지).
 *
 * Usage
 * - Unity 메뉴: ND/World Map/Build WorldMapTest Scene
 */
#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.UI;
using Unity.Mathematics;

namespace ND.UI.WorldMap.Editor
{
    /// <summary>
    /// WorldMapTest 씬에 예시 월드맵 계층을 구성한다.
    /// </summary>
    public static class WorldMapTestSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/05.UI/04_WorldMap/Scene/WorldMapTest.unity";
        private const string ArtFolder = "Assets/_Project/05.UI/04_WorldMap/Art";
        private const string PrefabFolder = "Assets/_Project/05.UI/04_WorldMap/Prefabs";
        private const string WorldMapRootPrefabPath = PrefabFolder + "/WorldMapRoot.prefab";
        private const string WorldMapPanelPrefabPath = PrefabFolder + "/WorldMapPanel.prefab";

        [MenuItem("ND/World Map/Build WorldMapTest Scene")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[WorldMap] Scene not found: {ScenePath}");
                return;
            }

            EnsureArtFolder();
            EnsurePrefabFolder();
            var bgSprite = EnsureColorSpriteAsset($"{ArtFolder}/MapBackground.png", new Color(0.25f, 0.4f, 0.28f, 1f));
            var townSprite = EnsureColorSpriteAsset($"{ArtFolder}/TownMarker.png", Color.white);
            var ringSprite = EnsureColorSpriteAsset($"{ArtFolder}/SelectionRing.png", new Color(1f, 0.85f, 0.2f, 0.85f));
            var caravanSprite = EnsureColorSpriteAsset($"{ArtFolder}/CaravanMarker.png", new Color(0.95f, 0.35f, 0.2f, 1f));
            var lineMaterial = EnsureLineMaterial($"{ArtFolder}/RouteLine.mat");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ClearEditableRoots(scene);

            var cameraGo = new GameObject("Camera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.18f, 0.22f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.tag = "MainCamera";

            var root = BuildWorldMapRoot(
                bgSprite,
                townSprite,
                ringSprite,
                caravanSprite,
                lineMaterial);
            SceneManager.MoveGameObjectToScene(root, scene);

            var rootPrefab = PrefabUtility.SaveAsPrefabAsset(root, WorldMapRootPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[WorldMap] Saved prefab: {WorldMapRootPrefabPath}");

            var uiRoot = new GameObject("WorldMapUi");
            SceneManager.MoveGameObjectToScene(uiRoot, scene);

            EnsureEventSystem(scene);

            var hudCanvas = CreateScreenCanvas(uiRoot.transform, "HudCanvas", sortOrder: 100);
            var openButton = CreateUiButton(hudCanvas.transform, "OpenMapButton", "Open Map", new Vector2(-320f, 240f));

            // Prefab 저장용 임시 패널을 구성한 뒤, 씬에는 Prefab 인스턴스로 배치한다.
            var panelGo = new GameObject("WorldMapPanel");
            SceneManager.MoveGameObjectToScene(panelGo, scene);
            var mapHostGo = new GameObject("MapHost");
            mapHostGo.transform.SetParent(panelGo.transform, false);
            var panel = panelGo.AddComponent<WorldMapPanel>();

            var panelCanvas = CreateScreenCanvas(panelGo.transform, "PanelCanvas", sortOrder: 110);
            var closeButton = CreateUiButton(panelCanvas.transform, "CloseMapButton", "Close Map", new Vector2(320f, 240f));

            var rootInstance = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, mapHostGo.transform);
            rootInstance.name = "WorldMapRoot";

            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("startHidden").boolValue = true;
            panelSo.FindProperty("worldMapRootPrefab").objectReferenceValue = rootPrefab;
            panelSo.FindProperty("mapHost").objectReferenceValue = mapHostGo.transform;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            var panelPrefab = PrefabUtility.SaveAsPrefabAsset(panelGo, WorldMapPanelPrefabPath);
            Object.DestroyImmediate(panelGo);
            Debug.Log($"[WorldMap] Saved prefab: {WorldMapPanelPrefabPath}");

            var panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, uiRoot.transform);
            panelInstance.name = "WorldMapPanel";
            panel = panelInstance.GetComponent<WorldMapPanel>();
            closeButton = panelInstance.transform.Find("PanelCanvas/CloseMapButton")?.GetComponent<Button>();

            var controls = uiRoot.AddComponent<WorldMapPanelControls>();
            var controlsSo = new SerializedObject(controls);
            controlsSo.FindProperty("panel").objectReferenceValue = panel;
            controlsSo.FindProperty("openMapButton").objectReferenceValue = openButton;
            controlsSo.FindProperty("closeMapButton").objectReferenceValue = closeButton;
            controlsSo.ApplyModifiedPropertiesWithoutUndo();

            // 기존 WorldMapTestEntry는 Prefab 패널 방식으로 대체한다.
            var legacyEntry = GameObject.Find("WorldMapTestEntry");
            if (legacyEntry != null)
            {
                Object.DestroyImmediate(legacyEntry);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldMap] Built example hierarchy in {ScenePath}");
        }

        private static GameObject BuildWorldMapRoot(
            Sprite bgSprite,
            Sprite townSprite,
            Sprite ringSprite,
            Sprite caravanSprite,
            Material lineMaterial)
        {
            var root = new GameObject("WorldMapRoot");

            var background = CreateChild(root.transform, "Background");
            var bgVisual = CreateChild(background.transform, "WorldMapBackground");
            var bgRenderer = bgVisual.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = bgSprite;
            bgRenderer.sortingOrder = -20;
            bgVisual.transform.localScale = new Vector3(18f, 12f, 1f);

            var routeLayer = CreateChild(root.transform, "RouteLayer");
            var townLayer = CreateChild(root.transform, "TownLayer");
            var caravanLayer = CreateChild(root.transform, "CaravanLayer");
            var uiLayer = CreateChild(root.transform, "WorldUiLayer");

            var townBase = CreateTown(townLayer.transform, "Town_BaseCamp", "basecamp", new Vector3(-4.5f, -1.5f, 0f), townSprite, ringSprite);
            var townDummy = CreateTown(townLayer.transform, "Town_Dummy", "dummytown", new Vector3(4.5f, 1.5f, 0f), townSprite, ringSprite);
            var townWaypoint = CreateTown(townLayer.transform, "Town_Waypoint", "demo_waypoint", new Vector3(0f, 3.2f, 0f), townSprite, ringSprite);

            var straightRoute = CreateStraightRoute(
                routeLayer.transform,
                "Route_DummyStraight",
                "dummyroute",
                townBase.transform,
                townDummy.transform,
                lineMaterial);

            var splineRoute = CreateSplineRoute(
                routeLayer.transform,
                "Route_DemoSpline",
                "demo_spline_route",
                townBase.transform.position,
                townWaypoint.transform.position,
                townDummy.transform.position,
                lineMaterial);

            var markerGo = CreateChild(caravanLayer.transform, "ActiveCaravanMarker");
            var markerRenderer = markerGo.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = caravanSprite;
            markerRenderer.sortingOrder = 20;
            markerGo.transform.localScale = Vector3.one * 0.55f;
            var marker = markerGo.AddComponent<CaravanMapMarker>();
            marker.Configure(markerRenderer, flipSprite: true);
            markerGo.SetActive(false);

            var canvasGo = CreateChild(uiLayer.transform, "WorldUiCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var progressGo = CreateUiText(canvasGo.transform, "ProgressPercentLabel", new Vector2(20f, -20f), "Progress --");
            var riskGo = CreateUiText(canvasGo.transform, "RiskLabel", new Vector2(20f, -60f), "Risk --");

            var presenter = root.AddComponent<WorldMapPresenter>();
            presenter.Configure(
                new[] { townBase, townDummy, townWaypoint },
                new[] { straightRoute, splineRoute },
                marker,
                progressGo.GetComponent<TextMeshProUGUI>(),
                riskGo.GetComponent<TextMeshProUGUI>());

            return root;
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/05.UI/04_WorldMap", "Prefabs");
            }
        }

        private static void EnsureArtFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/05.UI/04_WorldMap/Art"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/05.UI/04_WorldMap", "Art");
            }
        }

        private static Sprite EnsureColorSpriteAsset(string assetPath, Color color)
        {
            var absolute = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var pixels = new Color[256];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(absolute, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 16f;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Material EnsureLineMaterial(string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static void ClearEditableRoots(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null)
                {
                    continue;
                }

                if (go.name == "WorldMapRoot"
                    || go.name == "WorldMapPanel"
                    || go.name == "WorldMapUi"
                    || go.name == "EventSystem"
                    || go.name == "Main Camera"
                    || go.name == "Camera"
                    || go.name == "WorldMapTestEntry")
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 프로젝트 Active Input Handling(Input System)에 맞춰 UI EventSystem을 구성한다.
        /// StandaloneInputModule은 구 Input API를 호출하므로 사용하지 않는다.
        /// </summary>
        private static void EnsureEventSystem(Scene scene)
        {
            var existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                // 재베이크 전 잔존 또는 수동 배치된 구 모듈을 Input System용으로 교체한다.
                var legacy = existing.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    Object.DestroyImmediate(legacy);
                }

                if (existing.GetComponent<InputSystemUIInputModule>() == null)
                {
                    existing.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                return;
            }

            var eventSystemGo = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystemGo, scene);
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas CreateScreenCanvas(Transform parent, string name, int sortOrder)
        {
            var canvasGo = new GameObject(name);
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Button CreateUiButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent, false);

            var rect = buttonGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160f, 40f);
            rect.anchoredPosition = anchoredPosition;

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.15f, 0.2f, 0.28f, 0.92f);

            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22f;
            tmp.color = Color.white;

            return button;
        }

        private static TownWorldView CreateTown(
            Transform parent,
            string name,
            string townId,
            Vector3 position,
            Sprite townSprite,
            Sprite ringSprite)
        {
            var go = CreateChild(parent, name);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = townSprite;
            renderer.sortingOrder = 10;
            go.transform.localScale = Vector3.one * 0.7f;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.6f;

            var ring = CreateChild(go.transform, "SelectionRing");
            var ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = ringSprite;
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
            Transform start,
            Transform end,
            Material lineMaterial)
        {
            var go = CreateChild(parent, name);
            var startPoint = CreateChild(go.transform, "StartPoint");
            startPoint.transform.position = start.position;
            var endPoint = CreateChild(go.transform, "EndPoint");
            endPoint.transform.position = end.position;

            var line = go.AddComponent<LineRenderer>();
            ConfigureLine(line, lineMaterial);

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
            Vector3 c,
            Material lineMaterial)
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
            ConfigureLine(line, lineMaterial);

            var visual = go.AddComponent<RouteVisual>();
            visual.Configure(routeId, RoutePathType.Spline, startPoint.transform, endPoint.transform, container, line);
            return visual;
        }

        private static void ConfigureLine(LineRenderer line, Material lineMaterial)
        {
            line.widthMultiplier = 0.08f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.sharedMaterial = lineMaterial;
            line.startColor = new Color(0.55f, 0.45f, 0.3f, 1f);
            line.endColor = line.startColor;
        }

        private static GameObject CreateUiText(Transform parent, string name, Vector2 anchoredPos, string text)
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
            return go;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
#endif
