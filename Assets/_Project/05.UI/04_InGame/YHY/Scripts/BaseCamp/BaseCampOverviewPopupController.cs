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
        [SerializeField] private TMP_Text levelLimitText;
        [SerializeField] private TMP_Text unlockGuideText;
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

            if (levelLimitText != null)
                levelLimitText.text = $"건물 레벨 상한: Lv.{baseCampLevel}";
            if (unlockGuideText != null)
                unlockGuideText.text = "다음 레벨을 해금하려면 베이스 캠프를 증축하세요.";

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            return true;
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
