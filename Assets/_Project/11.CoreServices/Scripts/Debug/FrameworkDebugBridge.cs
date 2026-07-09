using UnityEngine;

namespace ND.Framework
{
    public sealed class FrameworkDebugBridge : MonoBehaviour
    {
        [SerializeField] private float debugTimeScale = 10f;

        [ContextMenu("Framework/Set Debug Time Scale")]
        public void SetDebugTimeScale()
        {
            FrameworkRoot.Instance.DebugCommands.SetTimeScale(debugTimeScale);
        }

        [ContextMenu("Framework/Reset Time Scale")]
        public void ResetTimeScale()
        {
            FrameworkRoot.Instance.DebugCommands.SetTimeScale(1f);
        }

        [ContextMenu("Framework/Complete Trade Immediately")]
        public void CompleteTradeImmediately()
        {
            FrameworkRoot.Instance.DebugCommands.CompleteTradeImmediately();
        }

        [ContextMenu("Framework/Force Load Completed")]
        public void ForceLoadCompleted()
        {
            FrameworkRoot.Instance.DebugCommands.ForceLoadCompleted();
        }
    }
}
