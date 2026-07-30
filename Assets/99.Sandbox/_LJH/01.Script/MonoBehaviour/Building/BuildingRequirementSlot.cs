using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BuildingRequirementSlot : MonoBehaviour
{
    [Header("UI_References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text iconPlaceholderText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;

    [Header("Amount Color")]
    [SerializeField] private Color normalColor = Color.black;
    [SerializeField] private Color insufficientColor = new Color32(190, 55, 45, 255);

    private void Awake()
    {
        if(iconPlaceholderText == null)
        {
            Transform placeholderTransform = transform.Find("Icon/IconPlaceholderText");
            iconPlaceholderText = placeholderTransform != null
                ? placeholderTransform.GetComponent<TMP_Text>()
                : null;
        }

        if(amountText != null)
        {
            amountText.richText = true;
        }
    }

    public void Bind(string displayName, Sprite icon, long ownedAmount, long requiredAmount, bool isSatisfied)
    {
        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.color = Color.white;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if(iconPlaceholderText != null)
        {
            iconPlaceholderText.gameObject.SetActive(false);
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrWhiteSpace(displayName) ? "-" : displayName;
        }

        if (amountText != null)
        {
            amountText.color = normalColor;
            amountText.text = FormatAmount(ownedAmount, requiredAmount, isSatisfied);
        }
    }

    private string FormatAmount(long ownedAmount, long requireAmount, bool isSatisfied)
    {
        if (isSatisfied)
        {
            return $"{ownedAmount:N0} " + $"/ {requireAmount:N0}";
        }

        string colorCode = ColorUtility.ToHtmlStringRGB(insufficientColor);

        return $"<color=#{colorCode}>" + $"{ownedAmount:N0}" + $"</color> / " + $"{requireAmount:N0}";
    }
}
