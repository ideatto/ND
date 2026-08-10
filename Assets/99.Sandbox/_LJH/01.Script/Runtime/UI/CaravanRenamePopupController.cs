using System;
using System.Collections;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Prefab-backed modal for entering a Caravan display name.</summary>
[DisallowMultipleComponent]
public sealed class CaravanRenamePopupController : MonoBehaviour
{
    [Tooltip("Prefab-backed backdrop button. Clicking outside the panel closes this popup.")]
    [SerializeField] private Button backdropButton;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;

    private string caravanId = string.Empty;
    private Action<string, string> submitted;
    private bool ownsImeLimit;
    private IMECompositionMode previousImeMode;
    private Coroutine imeLimitRoutine;

    private void Awake()
    {
        backdropButton?.onClick.AddListener(Close);
        cancelButton?.onClick.AddListener(Close);
        confirmButton?.onClick.AddListener(Confirm);
        if (input != null)
        {
            input.characterLimit = CaravanRenameService.MaxLength;
            input.onValueChanged.AddListener(HandleInputChanged);
            input.onSelect.AddListener(HandleInputSelected);
            input.onDeselect.AddListener(HandleInputDeselected);
        }
    }

    private void OnDisable()
    {
        CancelImeLimitRoutine();
        RestoreImeMode();
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
        HandleInputChanged(input.text);
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

    private void HandleInputChanged(string value)
    {
        ApplyImeLimit();
        if (confirmButton == null) return;

        string normalized = value?.Trim() ?? string.Empty;
        confirmButton.interactable = normalized.Length > 0
            && normalized.Length <= CaravanRenameService.MaxLength;
    }

    private void HandleInputSelected(string _)
    {
        CancelImeLimitRoutine();
        imeLimitRoutine = StartCoroutine(ApplyImeLimitAfterActivation());
    }

    private void HandleInputDeselected(string _)
    {
        CancelImeLimitRoutine();
        RestoreImeMode();
    }

    private void Close()
    {
        CancelImeLimitRoutine();
        RestoreImeMode();
        caravanId = string.Empty;
        submitted = null;
        gameObject.SetActive(false);
    }

    private void RestoreImeMode()
    {
        if (!ownsImeLimit) return;

        Input.imeCompositionMode = previousImeMode;
        ownsImeLimit = false;
    }

    private IEnumerator ApplyImeLimitAfterActivation()
    {
        yield return null;
        imeLimitRoutine = null;
        ApplyImeLimit();
    }

    private void CancelImeLimitRoutine()
    {
        if (imeLimitRoutine == null) return;

        StopCoroutine(imeLimitRoutine);
        imeLimitRoutine = null;
    }

    private void ApplyImeLimit()
    {
        bool reachedLimit = input != null
            && input.isFocused
            && input.text.Length >= CaravanRenameService.MaxLength;

        if (!reachedLimit)
        {
            RestoreImeMode();
            return;
        }

        if (ownsImeLimit) return;

        previousImeMode = Input.imeCompositionMode;
        Input.imeCompositionMode = IMECompositionMode.Off;
        ownsImeLimit = true;
    }
}
