using UnityEngine;

namespace ND.Framework
{
    public sealed class LoadingSceneController : MonoBehaviour
    {
        [SerializeField] private bool completeLoadingOnStart = true;

        private void Start()
        {
            if (completeLoadingOnStart)
            {
                CompleteLoading();
            }
        }

        public void CompleteLoading()
        {
            FrameworkRoot.Instance.CompleteLoadingAndEnterGame();
        }
    }
}
