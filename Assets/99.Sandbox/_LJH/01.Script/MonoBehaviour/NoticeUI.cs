using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 출발 실패 메시지를 일정 시간 동안 표시한 뒤 투명하게 닫는 UI입니다.
/// </summary>
public sealed class NoticeUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float fadeDuration = 3f;

    private Coroutine fadeCoroutine;

    /// <summary>
    /// 새로운 메시지를 표시합니다. 이미 표시 중이면 기존 타이머를 다시 시작합니다.
    /// </summary>
    public void Show(string message)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        fadeCoroutine = StartCoroutine(FadeAndClose());
    }

    private void OnDisable()
    {
        // An external screen close also stops Unity coroutines, so discard the stale handle.
        fadeCoroutine = null;
    }

    private IEnumerator FadeAndClose()
    {
        float elapsed = 0f;

        // unscaledDeltaTime을 사용해 게임 배속이나 일시정지와 무관하게 동작시킵니다.
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (canvasGroup != null)
            {
                canvasGroup.alpha =
                    1f - Mathf.Clamp01(elapsed / fadeDuration);
            }

            yield return null;
        }

        // Clear the handle before disabling because Unity stops this coroutine with the object.
        fadeCoroutine = null;
        gameObject.SetActive(false);
    }
}
