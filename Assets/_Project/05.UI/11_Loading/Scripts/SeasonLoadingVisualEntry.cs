using System;
using UnityEngine;

namespace ND.UI.Loading
{
    [Serializable]
    public sealed class SeasonLoadingVisualEntry
    {
        [SerializeField] private string seasonId;
        [SerializeField] private Sprite backgroundSprite;

        public string SeasonId => seasonId;
        public Sprite BackgroundSprite => backgroundSprite;
    }
}
