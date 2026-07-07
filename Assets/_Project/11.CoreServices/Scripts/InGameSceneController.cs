using UnityEngine;

namespace ND.Framework
{
    public sealed class InGameSceneController : MonoBehaviour
    {
        public void ReturnToTitle()
        {
            FrameworkRoot.Instance.ReturnToTitle();
        }

        public void SetTimeScale(float scale)
        {
            FrameworkRoot.Instance.DebugCommands.SetTimeScale(scale);
        }

        public void CompleteTradeImmediately()
        {
            FrameworkRoot.Instance.DebugCommands.CompleteTradeImmediately();
        }
    }
}
