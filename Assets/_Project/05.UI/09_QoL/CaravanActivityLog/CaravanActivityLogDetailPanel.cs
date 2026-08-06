using System;
using ND.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class CaravanActivityLogDetailPanel : MonoBehaviour
{
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button backButton;

    public event Action CloseRequested;
    public CaravanActivityLogEntrySaveData CurrentEntry { get; private set; }

    private bool backListenerRegistered;

    private void Awake()
    {
        EnsureBackListener();
    }

    private void OnDestroy()
    {
        if (backListenerRegistered && backButton != null)
        {
            backButton.onClick.RemoveListener(HandleBackClicked);
        }
    }

    public void Open(CaravanActivityLogEntrySaveData entry)
    {
        if (entry == null)
        {
            return;
        }

        CurrentEntry = entry;
        EnsureBackListener();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        CurrentEntry = null;
        gameObject.SetActive(false);
    }

    private void HandleBackClicked()
    {
        CloseRequested?.Invoke();
    }

    private void EnsureBackListener()
    {
        if (backButton == null || backListenerRegistered)
        {
            return;
        }

        backButton.onClick.AddListener(HandleBackClicked);
        backListenerRegistered = true;
    }
}
