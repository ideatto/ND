using UnityEngine;

namespace ND.Framework
{
    public sealed class TitleSceneController : MonoBehaviour
    {
        public bool HasSaveData => FrameworkRoot.Instance.SaveService.HasSaveData();

        public void StartNewGame()
        {
            FrameworkRoot.Instance.StartNewGame();
        }

        public void ContinueGame()
        {
            FrameworkRoot.Instance.ContinueGame();
        }

        public void ResetSaveData()
        {
            FrameworkRoot.Instance.SaveService.ResetSaveData();
        }

        public void ExitGame()
        {
            FrameworkLog.Info("Exit requested.");
            Application.Quit();
        }
    }
}
