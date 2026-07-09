using ND.Framework;
using UnityEngine;

public class InGameSceneRouterDebug : MonoBehaviour
{

    private void OnEnable()
    {
        FrameworkEvents.InGameScreenChanged += OnInGameScreenChanged;
    }

    private void OnDisable()
    {
        FrameworkEvents.InGameScreenChanged -= OnInGameScreenChanged;
    }

    private void OnInGameScreenChanged(InGameScreenState state)
    {
        Debug.Log($"[InGame Route Debug] Current Screen State : {state}");
    }
}
