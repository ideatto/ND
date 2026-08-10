using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.InGame.BaseCamp
{
    [DisallowMultipleComponent]
    public sealed class BaseCampOverviewPopupController : MonoBehaviour
    {
        [Serializable]
        private sealed class BuildingLevelRow
        {
            public string buildingDisplayName = string.Empty;
            public TMP_Text label;
        }

        [SerializeField] private TMP_Text baseCampLevelText;
        [SerializeField] private BuildingLevelRow[] buildingRows = Array.Empty<BuildingLevelRow>();
        [SerializeField] private TMP_Text unlockGuideText;
        [SerializeField] private TMP_Text caravanSlotProgressText;
        [SerializeField] private TMP_Text nextUnlockText;
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            backdropButton?.onClick.AddListener(Close);
            closeButton?.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            backdropButton?.onClick.RemoveListener(Close);
            closeButton?.onClick.RemoveListener(Close);
        }

        public bool Open()
        {
            ND.Framework.SaveData saveData = ND.Framework.FrameworkRoot.Instance?.CurrentSaveData;
            if (saveData?.player?.villageBuildings == null)
                return false;

            if (!ND.Framework.BaseCampBuildingLevelPolicy.TryGetUniqueLevel(
                    saveData.player.villageBuildings,
                    ND.Framework.BaseCampBuildingLevelPolicy.BaseCampDisplayName,
                    out int baseCampLevel))
                return false;

            if (baseCampLevelText != null)
                baseCampLevelText.text = $"Lv.{baseCampLevel}";

            for (int i = 0; i < buildingRows.Length; i++)
            {
                BuildingLevelRow row = buildingRows[i];
                if (row == null || row.label == null)
                    continue;

                if (!ND.Framework.BaseCampBuildingLevelPolicy.TryGetUniqueLevel(
                        saveData.player.villageBuildings,
                        row.buildingDisplayName,
                        out int buildingLevel))
                    return false;

                row.label.text = $"Lv.{buildingLevel}";
            }

            if (unlockGuideText != null)
            {
                unlockGuideText.text = baseCampLevel
                        < ND.Framework.BaseCampProgressionPolicy.EndingBuildingUnlockLevel
                    ? "다음 레벨을 해금하려면 베이스 캠프를 증축하세요."
                    : "베이스 캠프의 모든 해금 조건을 달성했습니다.";
            }

            ApplyProgressionSummary(baseCampLevel);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            return true;
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Displays BaseCamp-owned unlock progress without inspecting Caravan occupancy.
        /// The same policy used by creation and Overview state calculation owns these values.
        /// </summary>
        private void ApplyProgressionSummary(int baseCampLevel)
        {
            int unlockedSlots = Mathf.Min(
                Mathf.Max(baseCampLevel, 0),
                ND.Framework.BaseCampProgressionPolicy.MaxCaravanSlotCount);

            if (caravanSlotProgressText != null)
            {
                caravanSlotProgressText.text =
                    $"현재 캐러밴 슬롯: {unlockedSlots} / {ND.Framework.BaseCampProgressionPolicy.MaxCaravanSlotCount}";
            }

            if (nextUnlockText == null)
                return;

            if (baseCampLevel < ND.Framework.BaseCampProgressionPolicy.MaxCaravanSlotCount)
            {
                nextUnlockText.text = "다음 레벨: 캐러밴 슬롯 해금";
            }
            else if (baseCampLevel < ND.Framework.BaseCampProgressionPolicy.EndingBuildingUnlockLevel)
            {
                // Lv.5 unlocks the single-level EndingItem registered in the building catalog.
                nextUnlockText.text = "다음 레벨: 엔딩 건물 해금";
            }
            else
            {
                nextUnlockText.text = "모든 캐러밴 슬롯 및 건물 해금 완료";
            }
        }
    }
}
