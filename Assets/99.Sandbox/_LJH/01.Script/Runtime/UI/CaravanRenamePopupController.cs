using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Prefab-backed modal for entering a Caravan display name.</summary>
[DisallowMultipleComponent]
public sealed class CaravanRenamePopupController : MonoBehaviour
{
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;

    private string caravanId = string.Empty;
    private Action<string, string> submitted;

    private void Awake()
    {
        cancelButton?.onClick.AddListener(Close);
        confirmButton?.onClick.AddListener(Confirm);
    }

    public void Open(string id, string currentName, Action<string, string> onSubmitted)
    {
        if (input == null || errorText == null)
        {
            Debug.LogError("Caravan rename popup references are not configured.", this);
            return;
        }

        caravanId = id?.Trim() ?? string.Empty;
        submitted = onSubmitted;
        input.interactable = true;
        input.text = currentName ?? string.Empty;
        errorText.text = string.Empty;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (Application.isPlaying)
        {
            input.Select();
            input.ActivateInputField();
        }
    }

    public void ShowError(string message)
    {
        if (errorText != null) errorText.text = message ?? string.Empty;
        if (input != null) input.interactable = true;
    }

    private void Confirm()
    {
        if (submitted == null)
        {
            ShowError("이름 변경 요청을 다시 시작해 주세요.");
            return;
        }

        input.interactable = false;
        submitted.Invoke(caravanId, input.text);
    }

    private void Close()
    {
        caravanId = string.Empty;
        submitted = null;
        gameObject.SetActive(false);
    }
}
