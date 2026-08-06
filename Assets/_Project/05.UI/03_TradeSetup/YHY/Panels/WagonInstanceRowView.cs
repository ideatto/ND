using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WagonInstanceRowView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    public void Bind(int number, TransportSelectPanel.TransportEntry wagon,
        Action<TransportSelectPanel.TransportEntry> onSelect)
    {
        if (label != null)
            label.text = $"└ {number}. {wagon.name}  내구도 " +
                         $"{Math.Max(0, wagon.currentDurability)} / {Math.Max(0, wagon.maxDurability)}";

        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.interactable = wagon.canSelect;
        button.onClick.AddListener(() => onSelect?.Invoke(wagon));
    }
}
