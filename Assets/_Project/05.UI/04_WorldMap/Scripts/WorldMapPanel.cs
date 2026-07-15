/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - InGame UI 패널로 월드맵 Prefab을 표시·숨김한다.
 * - 열기/닫기 버튼(또는 외부 Show/Hide 호출)으로 패널 GameObject 활성 상태를 제어한다.
 *
 * Main Features
 * - WorldMapRoot Prefab 인스턴스 보유/생성.
 * - Show/Hide로 패널 전체를 활성·비활성한다.
 * - startHidden이면 Start에서 한 번 Hide한다(비활성 Prefab의 첫 Show와 충돌하지 않음).
 * - TownClicked / RouteClicked를 패널 밖으로 중계한다.
 *
 * Important Notes
 * - InGameScreenState(Traveling/Preparation)에 따라 자동 Show/Hide하지 않는다.
 * - 열기 버튼은 패널이 비활성일 때도 동작하도록 패널 밖에 둔다. WorldMapPanelControls 참고.
 * - 무역 출발·정산·Save 쓰기를 수행하지 않는다.
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
using System;
using UnityEngine;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// InGame에 배치하는 월드맵 패널이다. 버튼 또는 API로 show/hide한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMapPanel : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("베이크된 WorldMapRoot Prefab입니다.")]
        [SerializeField] private GameObject worldMapRootPrefab;

        [Tooltip("비어 있으면 이 Transform 아래에 Prefab을 생성한다.")]
        [SerializeField] private Transform mapHost;

        [Header("Display")]
        [Tooltip("Awake 시 패널을 비활성으로 시작한다. InGame에서는 보통 true이다.")]
        [SerializeField] private bool startHidden = true;

        private GameObject mapInstance;
        private WorldMapPresenter presenter;
        private bool isVisible;
        private bool openRequested;

        /// <summary>
        /// 패널이 중계하는 Presenter이다. Prefab 인스턴스 생성 후에만 유효하다.
        /// </summary>
        public WorldMapPresenter Presenter => presenter;

        /// <summary>
        /// 현재 패널 표시 여부이다.
        /// </summary>
        public bool IsVisible => isVisible && gameObject.activeInHierarchy;

        /// <summary>
        /// 맵 마을 클릭을 중계한다.
        /// </summary>
        public event Action<string> TownClicked;

        /// <summary>
        /// 맵 루트 선택을 중계한다.
        /// </summary>
        public event Action<string> RouteClicked;

        private void Awake()
        {
            EnsureMapInstance();
        }

        // startHidden은 Start에서 적용한다. 비활성 상태로 배치된 Prefab이
        // Show()로 처음 활성화될 때 Awake의 Hide와 충돌하지 않도록 한다.
        private void Start()
        {
            if (startHidden && !openRequested)
            {
                Hide();
            }
        }

        private void OnEnable()
        {
            BindPresenterEvents(true);
            if (isVisible)
            {
                presenter?.RefreshAll();
            }
        }

        private void OnDisable()
        {
            BindPresenterEvents(false);
        }

        /// <summary>
        /// 월드맵 패널을 활성화하고 Presenter를 갱신한다.
        /// </summary>
        /// <remarks>
        /// 맵 열기 버튼 또는 외부 UI에서 호출한다.
        /// 패널 GameObject가 비활성이면 먼저 활성화한다.
        /// </remarks>
        public void Show()
        {
            openRequested = true;
            isVisible = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureMapInstance();
            if (mapInstance != null && !mapInstance.activeSelf)
            {
                mapInstance.SetActive(true);
            }

            presenter?.RefreshAll();
        }

        /// <summary>
        /// 월드맵 패널을 비활성화한다.
        /// </summary>
        /// <remarks>
        /// 맵 닫기 버튼 또는 외부 UI에서 호출한다.
        /// 패널 GameObject를 끄므로 열기 버튼은 패널 밖에 배치해야 한다.
        /// </remarks>
        public void Hide()
        {
            openRequested = false;
            isVisible = false;

            if (mapInstance != null && mapInstance.activeSelf)
            {
                mapInstance.SetActive(false);
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Prefab 참조를 설정한다. Editor/베이크 도구에서 사용한다.
        /// </summary>
        public void Configure(GameObject rootPrefab, Transform host, bool hiddenOnStart)
        {
            worldMapRootPrefab = rootPrefab;
            mapHost = host != null ? host : transform;
            startHidden = hiddenOnStart;
        }

        private void EnsureMapInstance()
        {
            if (mapInstance != null)
            {
                return;
            }

            if (mapHost == null)
            {
                mapHost = transform;
            }

            // 이미 자식으로 배치된 WorldMapRoot가 있으면 Prefab 생성 없이 사용한다.
            var existing = mapHost.Find("WorldMapRoot");
            if (existing != null)
            {
                mapInstance = existing.gameObject;
                presenter = mapInstance.GetComponent<WorldMapPresenter>();
                if (presenter == null)
                {
                    presenter = mapInstance.GetComponentInChildren<WorldMapPresenter>(true);
                }

                return;
            }

            if (worldMapRootPrefab == null)
            {
                Debug.LogWarning("[WorldMap] WorldMapPanel has no WorldMapRoot prefab assigned.", this);
                return;
            }

            mapInstance = Instantiate(worldMapRootPrefab, mapHost, false);
            mapInstance.name = "WorldMapRoot";
            presenter = mapInstance.GetComponent<WorldMapPresenter>();
            if (presenter == null)
            {
                presenter = mapInstance.GetComponentInChildren<WorldMapPresenter>(true);
            }

            BindPresenterEvents(true);
        }

        private void BindPresenterEvents(bool subscribe)
        {
            if (presenter == null)
            {
                return;
            }

            presenter.TownClicked -= HandleTownClicked;
            presenter.RouteClicked -= HandleRouteClicked;
            if (subscribe)
            {
                presenter.TownClicked += HandleTownClicked;
                presenter.RouteClicked += HandleRouteClicked;
            }
        }

        private void HandleTownClicked(string townId)
        {
            TownClicked?.Invoke(townId);
        }

        private void HandleRouteClicked(string routeId)
        {
            RouteClicked?.Invoke(routeId);
        }
    }
}
