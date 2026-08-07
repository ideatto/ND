using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ND.UI.InGame.SellPriceModifierBuff
{
    public sealed class SellPriceModifierBuffView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image icon;

        private SellPriceModifierBuffBarController owner;
        private Func<SellPriceModifierBuffViewData> buildViewData;

        public void ConfigureReferences(Image iconImage)
        {
            icon = iconImage;
        }

        public void Initialize(
            SellPriceModifierBuffBarController barController,
            Sprite sprite,
            Func<SellPriceModifierBuffViewData> builder)
        {
            owner = barController;
            buildViewData = builder;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.Open(this, buildViewData?.Invoke());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                owner?.Toggle(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.Close(this);
        }

        private void OnDisable()
        {
            owner?.Close(this);
        }
    }
}
