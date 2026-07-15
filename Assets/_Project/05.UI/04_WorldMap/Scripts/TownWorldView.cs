/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - 월드맵 상의 마을 표시와 클릭 입력을 담당한다.
 * - 무역 상태나 SaveData를 직접 변경하지 않는다.
 *
 * Main Features
 * - townId 바인딩, unlock/selected 시각 상태, 클릭 이벤트 전달.
 *
 * Important Notes
 * - TownClicked는 WorldMapPresenter가 중계하며, 무역 준비 UI 연결은 후속에서 구독한다.
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
using System;
using UnityEngine;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// 월드맵 상의 단일 마을 시각/입력 뷰이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TownWorldView : MonoBehaviour
    {
        [Tooltip("Shared TownData.TownId와 일치해야 하는 마을 식별자입니다.")]
        [SerializeField] private string townId;

        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private Color unlockedColor = Color.white;
        [SerializeField] private Color lockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.92f, 0.4f, 1f);
        [SerializeField] private Transform selectionRing;

        private bool isUnlocked = true;
        private bool isSelected;

        /// <summary>
        /// Shared / Save와 매칭하는 town ID이다.
        /// </summary>
        public string TownId => townId;

        /// <summary>
        /// 플레이어가 이 마을을 클릭했을 때 발생한다. 인자는 townId이다.
        /// </summary>
        /// <remarks>
        /// 무역 출발이나 Save 쓰기는 수행하지 않는다. Presenter 또는 상위 오케스트레이터가 구독한다.
        /// </remarks>
        public event Action<string> TownClicked;

        /// <summary>
        /// Inspector 또는 빌더에서 town ID와 아이콘을 설정한다.
        /// </summary>
        public void Configure(string id, SpriteRenderer renderer, Transform ring = null)
        {
            townId = id ?? string.Empty;
            iconRenderer = renderer;
            if (ring != null)
            {
                selectionRing = ring;
            }

            ApplyVisualState();
        }

        /// <summary>
        /// unlock / selected 표시 상태를 갱신한다.
        /// </summary>
        public void SetPresentationState(bool unlocked, bool selected)
        {
            isUnlocked = unlocked;
            isSelected = selected;
            ApplyVisualState();
        }

        private void OnMouseUpAsButton()
        {
            if (string.IsNullOrEmpty(townId))
            {
                return;
            }

            TownClicked?.Invoke(townId);
        }

        private void ApplyVisualState()
        {
            if (selectionRing != null)
            {
                selectionRing.gameObject.SetActive(isSelected);
            }

            if (iconRenderer == null)
            {
                return;
            }

            if (isSelected)
            {
                iconRenderer.color = selectedColor;
            }
            else
            {
                iconRenderer.color = isUnlocked ? unlockedColor : lockedColor;
            }
        }
    }
}
