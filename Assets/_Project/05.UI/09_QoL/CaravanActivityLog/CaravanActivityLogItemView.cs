/*
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CaravanActivityLogItemView : MonoBehaviour
{
    [SerializeField] private Image caravanIcon;
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private TMP_Text messageText;

    public void Bind(string message, Color caravanColor)
    {
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
        }

        if (caravanIcon != null)
        {
            caravanIcon.color = caravanColor;
        }

        if (bubbleBackground != null)
        {
            var tint = Color.Lerp(Color.white, caravanColor, 0.12f);
            tint.a = 0.96f;
            bubbleBackground.color = tint;
        }
    }
}
*/
