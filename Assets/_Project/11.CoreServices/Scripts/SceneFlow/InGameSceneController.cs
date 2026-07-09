using UnityEngine;

namespace ND.Framework
{
    public sealed class InGameSceneController : MonoBehaviour
    {
        private void Start()
        {
            RefreshCurrentScreen();
        }

        public void RefreshCurrentScreen()
        {
            var root = FrameworkRoot.Instance;
            if (root == null || root.InGameScreenRouter == null)
            {
                return;
            }

            root.InGameScreenRouter.RefreshFromSaveData(root.CurrentSaveData, true);
        }

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
