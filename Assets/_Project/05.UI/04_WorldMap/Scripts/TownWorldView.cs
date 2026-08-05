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

        [Tooltip("이 마을의 데이터(TownData). 아이콘 스프라이트를 TownData.icon에서 읽어 표시합니다. 미니맵/월드맵 마을 아이콘의 원본.")]
        [SerializeField] private TownData townData;

        [Tooltip("아이콘 목표 월드 크기(유닛). 스프라이트 원본 픽셀 크기가 제각각이어도 이 크기로 자동 정규화합니다. 0 이하면 정규화 안 함.")]
        [SerializeField] private float iconWorldSize = 1.5f;

        [Tooltip("트레드밀 도착 연출에서 이 마을로 스폰할 건물 프리팹(예: Building_HarborVillage). 비우면 도착 연출 없이 지형만.")]
        [SerializeField] private GameObject treadmillBuildingPrefab;

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

        /// <summary>트레드밀 도착 연출에서 스폰할 이 마을의 건물 프리팹(없으면 null).</summary>
        public GameObject TreadmillBuildingPrefab => treadmillBuildingPrefab;

        /// <summary>
        /// 플레이어가 이 마을을 클릭했을 때 발생한다. 인자는 townId이다.
        /// </summary>
        /// <remarks>
        /// 무역 출발이나 Save 쓰기는 수행하지 않는다. Presenter 또는 상위 오케스트레이터가 구독한다.
        /// </remarks>
        public event Action<string> TownClicked;

        private void Awake()
        {
            ApplyIconFromData();   // 씬 배치 마을: TownData.icon을 SpriteRenderer에 반영(흰 네모 방지).
        }

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

            ApplyIconFromData();
            ApplyVisualState();
        }

        /// <summary>
        /// TownData의 아이콘 스프라이트를 SpriteRenderer에 반영하고 크기를 정규화한다.
        /// TownData나 icon이 없으면(미등록) 기존 스프라이트를 유지한다(덮어써서 비우지 않음).
        /// </summary>
        private void ApplyIconFromData()
        {
            if (iconRenderer != null && townData != null && townData.Icon != null)
            {
                iconRenderer.sprite = townData.Icon;
                NormalizeIconSize();
            }
        }

        /// <summary>
        /// 스프라이트 원본 픽셀 크기가 종류마다 달라도 iconWorldSize(월드 유닛)로 보이도록 스케일을 맞춘다.
        /// 이미 목표 크기면 배율 1이라 변화 없음(멱등). 코드에서 처리하므로 씬·프리팹에 크기 override가 안 생긴다.
        /// </summary>
        private void NormalizeIconSize()
        {
            if (iconRenderer == null || iconRenderer.sprite == null || iconWorldSize <= 0f) return;
            float current = iconRenderer.bounds.size.x;   // 현재 월드 가로 크기
            if (current <= 0.0001f) return;
            Transform t = iconRenderer.transform;
            t.localScale *= iconWorldSize / current;      // 목표 크기로 정규화
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
