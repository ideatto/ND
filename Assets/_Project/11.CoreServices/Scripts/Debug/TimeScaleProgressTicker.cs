using ND.Framework;
using System.Collections;
using UnityEngine;

public class TimeScaleProgressTicker : MonoBehaviour
{
    [SerializeField, Min(0.1f)]
    private float intervalSeconds = 3f;

    private Coroutine repeatCoroutine;


    private void Start()
    {
        repeatCoroutine = StartCoroutine(RepeatRoutine());
       
    }

    private IEnumerator RepeatRoutine()
    {
        while (true)
        {
            // N초 대기
            yield return new WaitForSeconds(intervalSeconds);

            // 반복 실행할 함수
            ExecuteFunction();
        }
    }

    private void ExecuteFunction()
    {
        Debug.Log($"{intervalSeconds}초마다 CheckProgressAndCompletion 함수가 호출되었습니다.");

        FrameworkRoot.Instance.TradeProgressCoordinator.CheckProgressAndCompletion();
    }

    private void OnDisable()
    {
        StopRepeatCoroutine();
    }

    private void StopRepeatCoroutine()
    {
        if (repeatCoroutine == null)
        {
            return;
        }

        StopCoroutine(repeatCoroutine);
        repeatCoroutine = null;
    }
}
