using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ND.UI.CargoSell
{
    /// <summary>Reuses inactive pending-sale rows and grows the pool only when supplied ViewData exceeds it.</summary>
    public sealed class CargoSellPendingSaleListView : MonoBehaviour
    {
        [SerializeField, Min(0)] private int prewarmCount = 3;

        private readonly List<CargoSellPendingSaleRowView> rows = new List<CargoSellPendingSaleRowView>();
        private Transform content;
        private GameObject template;
        private GameObject emptyState;
        private TMP_Text pendingCountText;
        private TMP_Text totalSalePriceText;
        private Action<CargoSellPendingSaleRowViewData> leftClicked;
        private Action<string> rightClicked;

        private void Awake()
        {
            Resolve();
            EnsureRows(prewarmCount);
            Render(Array.Empty<CargoSellPendingSaleRowViewData>());
        }

        public void SetInteractionCallbacks(
            Action<CargoSellPendingSaleRowViewData> onLeftClicked,
            Action<string> onRightClicked)
        {
            leftClicked = onLeftClicked;
            rightClicked = onRightClicked;
        }

        public void Render(IReadOnlyList<CargoSellPendingSaleRowViewData> source)
        {
            Resolve();
            IReadOnlyList<CargoSellPendingSaleRowViewData> valid = (source ??
                Array.Empty<CargoSellPendingSaleRowViewData>())
                .Where(row => row != null && row.Quantity > 0 && !string.IsNullOrWhiteSpace(row.ItemId))
                .ToArray();

            EnsureRows(valid.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                bool active = i < valid.Count;
                rows[i].gameObject.SetActive(active);
                if (active) rows[i].Bind(valid[i], leftClicked, rightClicked);
            }

            if (emptyState != null) emptyState.SetActive(valid.Count == 0);
            int types = valid.Select(row => row.ItemId).Distinct(StringComparer.Ordinal).Count();
            int quantity = valid.Sum(row => Math.Max(0, row.Quantity));
            decimal revenue = valid.Sum(row =>
                (decimal)Math.Max(0L, row.SellUnitPrice) * Math.Max(0, row.Quantity));
            long total = revenue >= long.MaxValue ? long.MaxValue : (long)revenue;

            if (pendingCountText != null)
                pendingCountText.text = "판매 등록 " + types + "종 / 총 " + quantity + "개";
            if (totalSalePriceText != null)
                totalSalePriceText.text = "총 판매 금액  " + total + "G";
        }

        private void EnsureRows(int count)
        {
            while (rows.Count < count)
            {
                GameObject instance = Instantiate(template, content);
                instance.name = "PendingSaleRow_Pooled_" + rows.Count;
                CargoSellPendingSaleRowView view =
                    instance.GetComponent<CargoSellPendingSaleRowView>() ??
                    instance.AddComponent<CargoSellPendingSaleRowView>();
                instance.SetActive(false);
                rows.Add(view);
            }
        }

        private void Resolve()
        {
            if (content != null) return;
            RectTransform viewport = GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(child => child.name == "Viewport");
            content = viewport.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(child => child.name == "Content");
            template = content.Cast<Transform>().Select(child => child.gameObject)
                .First(child => child.name == "PendingSaleRowTemplate");
            template.SetActive(false);
            emptyState = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "EmptyState")?.gameObject;

            Transform popup = transform;
            while (popup.parent != null && popup.name != "CargoSellPopup") popup = popup.parent;
            pendingCountText = popup.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "PendingCountText");
            totalSalePriceText = popup.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "TotalSalePriceText");
        }
    }
}
