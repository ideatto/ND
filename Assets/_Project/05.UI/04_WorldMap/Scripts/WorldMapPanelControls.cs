/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - 월드맵 열기/닫기 버튼을 WorldMapPanel.Show/Hide에 연결한다.
 * - 패널이 비활성일 때도 열기 버튼이 동작하도록 패널 밖(항상 활성)에 둔다.
 *
 * Main Features
 * - Open 버튼 → WorldMapPanel.Show
 * - Close 버튼 → WorldMapPanel.Hide
 *
 * Important Notes
 * - InGame HUD의 맵 열기 버튼과 패널 내부 닫기 버튼을 이 컴포넌트에 할당한다.
 * - 무역 상태나 SaveData를 변경하지 않는다.
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// 맵 열기/닫기 UI 버튼을 패널 Show/Hide에 바인딩한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMapPanelControls : MonoBehaviour
    {
        [Tooltip("제어할 월드맵 패널입니다.")]
        [SerializeField] private WorldMapPanel panel;

        [Tooltip("맵을 여는 버튼입니다. 패널 밖(항상 활성)에 배치한다.")]
        [SerializeField] private Button openMapButton;

        [Tooltip("맵을 닫는 버튼입니다. 패널 안팎 어디에 두어도 된다.")]
        [SerializeField] private Button closeMapButton;

        private void OnEnable()
        {
            if (openMapButton != null)
            {
                openMapButton.onClick.AddListener(HandleOpenClicked);
            }

            if (closeMapButton != null)
            {
                closeMapButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void OnDisable()
        {
            if (openMapButton != null)
            {
                openMapButton.onClick.RemoveListener(HandleOpenClicked);
            }

            if (closeMapButton != null)
            {
                closeMapButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        /// <summary>
        /// Editor/베이크 도구가 참조를 주입할 때 사용한다.
        /// </summary>
        public void Configure(WorldMapPanel mapPanel, Button openButton, Button closeButton)
        {
            panel = mapPanel;
            openMapButton = openButton;
            closeMapButton = closeButton;
        }

        private void HandleOpenClicked()
        {
            if (panel == null)
            {
                Debug.LogWarning("[WorldMap] WorldMapPanelControls has no panel assigned.", this);
                return;
            }

            panel.Show();
        }

        private void HandleCloseClicked()
        {
            if (panel == null)
            {
                Debug.LogWarning("[WorldMap] WorldMapPanelControls has no panel assigned.", this);
                return;
            }

            panel.Hide();
        }
    }
}
