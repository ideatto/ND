using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the Caravan S4 shop-slot footer readable after the shared cargo UI
/// applies its default slot palette.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class CaravanShopSlotVisual : MonoBehaviour
{
    [SerializeField] private Color footerColor = new Color32(88, 82, 80, 245);
    [SerializeField] private Color footerTextColor = Color.white;

    private void OnEnable()
    {
        Apply();
    }

    private void Start()
    {
        // CargoLoadingPanelController applies its common palette during Awake.
        // Reapply this S4-specific footer once all Awake calls have completed.
        Apply();
    }

    private void Apply()
    {
        Transform footer = FindDirectChild("ItemNameBackground");
        if (footer == null)
            return;

        Image footerImage = footer.GetComponent<Image>();
        if (footerImage != null)
            footerImage.color = footerColor;

        TMP_Text nameText = footer.GetComponentInChildren<TMP_Text>(true);
        if (nameText != null)
            nameText.color = footerTextColor;

        TMP_Text priceText = FindDirectChild("Text (TMP)")?.GetComponent<TMP_Text>();
        if (priceText != null)
        {
            priceText.color = footerTextColor;
            priceText.transform.SetAsLastSibling();
        }
    }

    private Transform FindDirectChild(string childName)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
