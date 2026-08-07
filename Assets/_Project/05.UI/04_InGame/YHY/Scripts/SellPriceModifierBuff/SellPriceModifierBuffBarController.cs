using ND.Economy;
using ND.Framework;
using UnityEngine;

namespace ND.UI.InGame.SellPriceModifierBuff
{
    public sealed class SellPriceModifierBuffBarController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SellPriceModifierPolicy sellPriceModifierPolicy;
        [SerializeField] private global::TradeItemData[] tradeItems;

        [Header("Views")]
        [SerializeField] private SellPriceModifierBuffView seasonBuff;
        [SerializeField] private SellPriceModifierBuffView distanceBuff;
        [SerializeField] private SellPriceModifierBuffView luckyMoneyBuff;
        [SerializeField] private SellPriceModifierBuffTooltipController tooltip;

        [Header("Season Sprites")]
        [SerializeField] private Sprite springSprite;
        [SerializeField] private Sprite summerSprite;
        [SerializeField] private Sprite autumnSprite;
        [SerializeField] private Sprite winterSprite;
        [SerializeField] private Sprite distanceSprite;
        [SerializeField] private Sprite luckyMoneySprite;

        private SellPriceModifierBuffView activeView;

        public void ConfigureReferences(
            SellPriceModifierBuffView season,
            SellPriceModifierBuffView distance,
            SellPriceModifierBuffView lucky,
            SellPriceModifierBuffTooltipController sharedTooltip)
        {
            seasonBuff = season;
            distanceBuff = distance;
            luckyMoneyBuff = lucky;
            tooltip = sharedTooltip;
        }

        private void OnEnable()
        {
            FrameworkEvents.SeasonChanged += HandleSeasonChanged;
            RefreshViews();
        }

        private void OnDisable()
        {
            FrameworkEvents.SeasonChanged -= HandleSeasonChanged;
            activeView = null;
            tooltip?.Close();
        }

        public void Open(SellPriceModifierBuffView view, SellPriceModifierBuffViewData data)
        {
            if (view == null || data == null || tooltip == null) return;
            activeView = view;
            tooltip.OpenNormal(data, view.transform as RectTransform);
        }

        public void Toggle(SellPriceModifierBuffView view)
        {
            if (view == null || view != activeView) return;
            tooltip?.ToggleDetail();
        }

        public void Close(SellPriceModifierBuffView view)
        {
            if (view == null || view != activeView) return;
            activeView = null;
            tooltip?.Close();
        }

        private void HandleSeasonChanged(GameCalendarSnapshot previous, GameCalendarSnapshot current)
        {
            activeView = null;
            tooltip?.Close();
            RefreshViews();
        }

        private void RefreshViews()
        {
            string seasonId = GetCurrentSeasonId();
            seasonBuff?.Initialize(this, GetSeasonSprite(seasonId), () =>
                SeasonBuffViewDataBuilder.Build(sellPriceModifierPolicy, GetCurrentSeasonId(), tradeItems));
            distanceBuff?.Initialize(this, distanceSprite, () =>
                DistanceBuffViewDataBuilder.Build(sellPriceModifierPolicy));
            luckyMoneyBuff?.Initialize(this, luckyMoneySprite, () =>
                LuckyMoneyBuffViewDataBuilder.Build(
                    sellPriceModifierPolicy,
                    FrameworkRoot.Instance?.CurrentSaveData,
                    WeatherLuckyMoneyStateReader.IsActive));
        }

        private static string GetCurrentSeasonId()
            => FrameworkRoot.Instance?.CurrentSaveData?.world?.currentSeasonId ?? string.Empty;

        private Sprite GetSeasonSprite(string seasonId)
        {
            if (seasonId == GameCalendarDate.SpringId) return springSprite;
            if (seasonId == GameCalendarDate.SummerId) return summerSprite;
            if (seasonId == GameCalendarDate.AutumnId) return autumnSprite;
            if (seasonId == GameCalendarDate.WinterId) return winterSprite;
            return null;
        }
    }
}
