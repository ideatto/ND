using System;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CaravanActivityLogItemView : MonoBehaviour
{
    [SerializeField] private Image caravanIcon;
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button clickButton;

    private CaravanActivityLogEntrySaveData boundEntry;
    private bool clickListenerRegistered;

    public event Action<CaravanActivityLogEntrySaveData> Clicked;

    public void Bind(
        string message,
        Color caravanColor,
        CaravanActivityLogEntrySaveData entry)
    {
        boundEntry = entry;
        EnsureClickListener();

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

    private void Awake()
    {
        EnsureClickListener();
    }

    private void HandleClicked()
    {
        if (boundEntry != null)
        {
            Clicked?.Invoke(boundEntry);
        }
    }

    private void EnsureClickListener()
    {
        if (clickButton == null || clickListenerRegistered)
        {
            return;
        }

        clickButton.onClick.AddListener(HandleClicked);
        clickListenerRegistered = true;
    }

    private void OnDestroy()
    {
        if (clickListenerRegistered && clickButton != null)
        {
            clickButton.onClick.RemoveListener(HandleClicked);
        }
        boundEntry = null;
        Clicked = null;
    }
}
